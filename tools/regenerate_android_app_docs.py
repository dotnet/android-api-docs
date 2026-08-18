#!/usr/bin/env python3
"""Regenerate changed Android.App XML documentation from Android's API reference.

The old extraction copied a member's main description into parameter and return
nodes when it could not resolve a Javadoc tag.  This tool deliberately keeps
those channels separate: it maps each managed member through its JNI
registration, extracts only the matching Android developer reference section,
and emits parameter and return text only from their corresponding reference
tables.

Android's public developer reference is the primary source.  The AOSP fallback
is intentionally restricted to a public member whose developer reference page
exists but has no matching detail section.  This keeps hidden annotations and
implementation-only Javadoc out of the generated public documentation.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import html
from html.parser import HTMLParser
import re
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable
from urllib.error import HTTPError, URLError
from urllib.parse import unquote
from urllib.request import Request, urlopen
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
DOCS_ROOT = ROOT / "docs" / "xml" / "Android.App"
ANDROID_REFERENCE = "https://developer.android.com/reference/"
AOSP_SOURCE = (
    "https://android.googlesource.com/platform/frameworks/base/"
    "+/refs/heads/main/core/java/"
)
USER_AGENT = "dotnet-android-api-docs-regenerator/1.0"

ATTRIBUTION = (
    "Portions of this page are modifications based on work created and shared by the "
    '<format type="text/html"><a href="https://developers.google.com/terms/site-policies" '
    'title="Android Open Source Project">Android Open Source Project</a></format> and used '
    "according to terms described in the "
    '<format type="text/html"><a href="https://creativecommons.org/licenses/by/2.5/" '
    'title="Creative Commons 2.5 Attribution License">Creative Commons 2.5 Attribution License.'
    "</a></format>"
)

RAW_DOC_TOKENS = (
    "@hide",
    "@FlaggedApi",
    "@IntDef",
    "{@link",
    "{@code",
    "{@literal",
    "@param",
    "@return",
    'ToolPath="',
)
RAW_DOC_PATTERNS = (
    ("raw Javadoc inline tag", re.compile(r"\{@")),
    ("raw Java annotation", re.compile(r"(?<![\w.])@\w+")),
    (
        "raw Java declaration",
        re.compile(
            r"\b(?:public|private|protected)\s+(?:static\s+)?(?:final\s+)?"
            r"(?:class|interface)\b",
            re.IGNORECASE,
        ),
    ),
)
FORBIDDEN_GENERIC_FALLBACKS = (
    "See the Android reference documentation for this platform member.",
    "Provides the managed binding projection of the corresponding Android API.",
)


@dataclass(frozen=True)
class SourceSection:
    anchor: str
    title: str
    html: str


@dataclass
class ExtractedDocs:
    summary: str
    paragraphs: list[str]
    parameters: dict[str, str]
    result: str
    throws: list[str]
    source_url: str
    source_label: str


class HtmlText(HTMLParser):
    """Convert a small HTML fragment to readable plain text without dependencies."""

    _BLOCK_TAGS = {
        "br",
        "p",
        "div",
        "li",
        "tr",
        "table",
        "ul",
        "ol",
        "dl",
        "dt",
        "dd",
        "h1",
        "h2",
        "h3",
        "h4",
    }

    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.parts: list[str] = []
        self.ignored_depth = 0

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag in {"script", "style", "svg"}:
            self.ignored_depth += 1
        if tag in self._BLOCK_TAGS:
            self.parts.append("\n")

    def handle_endtag(self, tag: str) -> None:
        if tag in {"script", "style", "svg"} and self.ignored_depth:
            self.ignored_depth -= 1
        if tag in self._BLOCK_TAGS:
            self.parts.append("\n")

    def handle_data(self, data: str) -> None:
        if not self.ignored_depth:
            self.parts.append(data)

    def text(self) -> str:
        return normalize_text("".join(self.parts))


def normalize_text(text: str) -> str:
    text = html.unescape(text).replace("\xa0", " ")
    text = re.sub(r"<!--.*?-->", " ", text, flags=re.DOTALL)
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def html_text(fragment: str) -> str:
    parser = HtmlText()
    parser.feed(fragment)
    parser.close()
    return parser.text()


def clean_platform_text(text: str) -> str:
    """Remove Javadoc syntax and hidden annotations without rewriting semantics."""

    text = normalize_text(text)
    text = re.sub(
        r"\{@(?:link|linkplain|code|literal|value)\s+([^}]+)\}",
        r"\1",
        text,
    )
    text = re.sub(r"\{@\w+\s+([^}]+)\}", r"\1", text)
    text = re.sub(r"\{@\w+\}", "", text)
    text = re.sub(
        r"@(?:hide|FlaggedApi|IntDef|SystemApi|TestApi|RequiresPermission|Nullable|NonNull)"
        r"(?:\s*\([^)]*\))?",
        "",
        text,
    )
    text = re.sub(r"@(?:param|return|throws|exception)\b", "", text)
    text = re.sub(r"@\w+(?:\s*\([^)]*\))?", "", text)
    text = re.sub(r"(?<=\w)#(?=[A-Za-z_])", ".", text)
    text = re.sub(r"(?<!\w)#(?=[A-Za-z_])", "", text)
    text = re.sub(r"\s+([,:;])", r"\1", text)
    text = re.sub(r"\bnull\s+\.", "null.", text)
    return normalize_text(text)


def remove_non_prose_blocks(fragment: str) -> str:
    """Discard source-code and table blocks before extracting prose."""

    fragment = re.sub(
        r"<table\b[^>]*>.*?</table>",
        " ",
        fragment,
        flags=re.IGNORECASE | re.DOTALL,
    )
    return re.sub(
        r"<(?:devsite-code|pre)\b[^>]*>.*?</(?:devsite-code|pre)>",
        " ",
        fragment,
        flags=re.IGNORECASE | re.DOTALL,
    )


def escape_xml(text: str) -> str:
    return html.escape(clean_platform_text(text), quote=False)


def first_sentence(text: str) -> str:
    text = clean_platform_text(text)
    if not text:
        return ""
    sentence = re.search(r"^(.+?[.!?])(?:\s|$)", text)
    if sentence:
        return sentence.group(1)
    return text[:300].rstrip()


def request(url: str, cache: Path | None) -> str:
    """Read an official source URL, retaining a local cache only when requested."""

    cache_file: Path | None = None
    if cache:
        cache.mkdir(parents=True, exist_ok=True)
        cache_file = cache / hashlib.sha256(url.encode("utf-8")).hexdigest()
        if cache_file.exists():
            return cache_file.read_text(encoding="utf-8")

    req = Request(url, headers={"User-Agent": USER_AGENT})
    try:
        with urlopen(req, timeout=90) as response:
            data = response.read().decode("utf-8")
    except HTTPError:
        raise
    except URLError as error:
        raise RuntimeError(f"Could not fetch {url}: {error}") from error

    if cache_file:
        cache_file.write_text(data, encoding="utf-8", newline="\n")
    return data


def parse_attributes(tag: str) -> dict[str, str]:
    return {
        match.group(1): html.unescape(match.group(2))
        for match in re.finditer(r'([:\w-]+)\s*=\s*"([^"]*)"', tag)
    }


class ReferencePage:
    def __init__(self, java_path: str, cache: Path | None) -> None:
        self.java_path = java_path
        self.url = f"{ANDROID_REFERENCE}{java_path.replace('$', '.')}"
        self.html = request(self.url, cache)
        self.sections = self._sections()
        self.overview = self._overview()

    def _sections(self) -> list[SourceSection]:
        headings: list[tuple[int, str, str]] = []
        for match in re.finditer(r"<h3\b[^>]*>", self.html, flags=re.IGNORECASE):
            attrs = parse_attributes(match.group(0))
            if "api-name" not in attrs.get("class", "").split() or "id" not in attrs:
                continue
            close = self.html.find("</h3>", match.end())
            if close < 0:
                continue
            headings.append(
                (match.start(), attrs["id"], clean_platform_text(html_text(self.html[match.end() : close])))
            )

        section_headings = [
            match.start()
            for match in re.finditer(
                r'<h2\b[^>]*\bclass="[^"]*\bapi-section\b[^"]*"[^>]*>',
                self.html,
                flags=re.IGNORECASE,
            )
        ]
        sections: list[SourceSection] = []
        for index, (start, anchor, title) in enumerate(headings):
            next_heading = headings[index + 1][0] if index + 1 < len(headings) else len(self.html)
            next_section = next(
                (position for position in section_headings if position > start),
                len(self.html),
            )
            end = min(next_heading, next_section)
            sections.append(SourceSection(anchor, title, self.html[start:end]))
        return sections

    def _overview(self) -> list[str]:
        content_start = self.html.find('id="jd-content"')
        if content_start < 0:
            return []
        summary = re.search(
            r'<h2\b[^>]*\bid="summary"[^>]*>',
            self.html[content_start:],
            flags=re.IGNORECASE,
        )
        if not summary:
            return []
        content = self.html[content_start : content_start + summary.start()]
        # Class signatures and inheritance tables precede the final horizontal rule.
        final_rule = content.rfind("<hr")
        if final_rule >= 0:
            content = content[final_rule:]
        paragraphs: list[str] = []
        for match in re.finditer(r"<p\b[^>]*>(.*?)</p>", content, flags=re.IGNORECASE | re.DOTALL):
            raw = remove_non_prose_blocks(match.group(1))
            if "api-signature" in raw or "See also:" in raw:
                continue
            text = clean_platform_text(html_text(raw))
            if text and text not in paragraphs:
                paragraphs.append(text)
        return paragraphs

    def source_url(self, anchor: str | None = None) -> str:
        return self.url if not anchor else f"{self.url}#{anchor}"


def parse_jni_arguments(descriptor: str) -> list[str]:
    if not descriptor or not descriptor.startswith("("):
        return []
    arguments: list[str] = []
    index = 1
    while index < len(descriptor) and descriptor[index] != ")":
        start = index
        while descriptor[index] == "[":
            index += 1
        if descriptor[index] == "L":
            end = descriptor.find(";", index)
            if end < 0:
                return []
            index = end + 1
        else:
            index += 1
        arguments.append(descriptor[start:index])
    return arguments


def split_top_level(values: str) -> list[str]:
    if not values.strip():
        return []
    result: list[str] = []
    start = 0
    depth = 0
    for index, char in enumerate(values):
        if char in "<([":
            depth += 1
        elif char in ">)]":
            depth = max(0, depth - 1)
        elif char == "," and depth == 0:
            result.append(values[start:index].strip())
            start = index + 1
    result.append(values[start:].strip())
    return result


PRIMITIVE_DESCRIPTORS = {
    "boolean": "Z",
    "byte": "B",
    "char": "C",
    "double": "D",
    "float": "F",
    "int": "I",
    "long": "J",
    "short": "S",
    "void": "V",
}
JAVA_LANG = {
    "Boolean",
    "Byte",
    "CharSequence",
    "Character",
    "Class",
    "ClassLoader",
    "Double",
    "Enum",
    "Exception",
    "Float",
    "Integer",
    "Iterable",
    "Long",
    "Object",
    "Runnable",
    "Short",
    "String",
    "Throwable",
}


def java_type_descriptor(java_type: str, current_path: str) -> str | None:
    value = html.unescape(java_type).replace("%20", " ").strip()
    if not value:
        return None
    value = re.sub(r"@\w+(?:\([^)]*\))?\s*", "", value)
    value = value.replace("? extends ", "").replace("? super ", "").replace("?", "")
    value = re.sub(r"<.*>", "", value).strip()
    dimensions = 0
    if value.endswith("..."):
        value = value[:-3].strip()
        dimensions += 1
    while value.endswith("[]"):
        value = value[:-2].strip()
        dimensions += 1
    if value in PRIMITIVE_DESCRIPTORS:
        result = PRIMITIVE_DESCRIPTORS[value]
    else:
        if "." not in value:
            if value in JAVA_LANG:
                value = f"java.lang.{value}"
            else:
                package = current_path.rsplit("/", 1)[0].replace("/", ".")
                value = f"{package}.{value}"
        parts = value.split(".")
        class_start = next((i for i, part in enumerate(parts) if part and part[0].isupper()), len(parts))
        if class_start == len(parts):
            return None
        package = "/".join(parts[:class_start])
        class_name = "$".join(parts[class_start:])
        result = f"L{package}/{class_name};"
    return "[" * dimensions + result


def anchor_arguments(anchor: str, current_path: str) -> list[str | None]:
    start = anchor.find("(")
    if start < 0 or not anchor.endswith(")"):
        return []
    return [java_type_descriptor(item, current_path) for item in split_top_level(anchor[start + 1 : -1])]


def get_register(element: ET.Element) -> tuple[str, str] | None:
    for attribute_name in element.findall("./Attributes/Attribute/AttributeName"):
        value = attribute_name.text or ""
        match = re.search(r'Register\("([^"]+)"\s*,\s*"([^"]*)"', value)
        if match:
            return match.group(1), match.group(2)
    return None


def get_type_registration(root: ET.Element) -> str | None:
    for attribute_name in root.findall("./Attributes/Attribute/AttributeName"):
        value = attribute_name.text or ""
        match = re.search(r'Register\("(android/app/[^"]+)"', value)
        if match:
            return match.group(1)
    return None


def get_jni_field(member: ET.Element) -> tuple[str, str] | None:
    for attribute_name in member.findall("./Attributes/Attribute/AttributeName"):
        value = attribute_name.text or ""
        match = re.search(r'JniField="([^"]+)\.([^".]+)"', value)
        if match:
            return match.group(1), match.group(2)
    return None


def pascal_constant(name: str) -> str:
    """Map managed constant names such as CategoryAlarm to CATEGORY_ALARM."""

    words = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", name)
    words = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", words)
    return words.upper()


def pascal_camel(name: str) -> str:
    return name[:1].lower() + name[1:] if name else name


def field_anchor_matches(member: ET.Element, anchor: str) -> bool:
    jni_field = get_jni_field(member)
    registered = get_register(member)
    candidates = {
        member.attrib["MemberName"],
        pascal_constant(member.attrib["MemberName"]),
    }
    if jni_field:
        candidates.add(jni_field[1])
    if registered and "(" not in registered[0]:
        candidates.add(registered[0])
    return anchor in candidates


def candidate_section(
    page: ReferencePage,
    member: ET.Element,
    java_path: str,
) -> SourceSection | None:
    member_type = member.findtext("MemberType", default="")
    register = get_register(member)
    if member_type == "Field":
        jni_field = get_jni_field(member)
        field_names = {
            member.attrib["MemberName"],
            pascal_constant(member.attrib["MemberName"]),
        }
        if jni_field:
            field_names.add(jni_field[1])
        if register and "(" not in register[0]:
            field_names.add(register[0])
        return next((item for item in page.sections if item.anchor in field_names), None)
    if member_type == "Property" and not register:
        name = member.attrib["MemberName"]
        property_names = {
            pascal_camel(name),
            f"get{name}",
            f"is{name}",
            pascal_constant(name),
        }
        return next((item for item in page.sections if item.anchor in property_names), None)
    if not register:
        return None

    java_name, descriptor = register
    if java_name == ".ctor":
        java_name = java_path.rsplit("$", 1)[-1].rsplit("/", 1)[-1]
    expected = parse_jni_arguments(descriptor)
    candidates = [
        item
        for item in page.sections
        if item.title == java_name or item.anchor.split("(", 1)[0] == java_name
    ]
    if not candidates:
        return None

    def score(item: SourceSection) -> tuple[int, int, int]:
        actual = anchor_arguments(item.anchor, java_path)
        exact = int(bool(expected) and actual == expected)
        count = int(len(actual) == len(expected))
        known = sum(
            1
            for actual_type, expected_type in zip(actual, expected)
            if actual_type is not None and actual_type == expected_type
        )
        return exact, known, count

    candidates.sort(key=score, reverse=True)
    best = candidates[0]
    actual = anchor_arguments(best.anchor, java_path)
    if expected and len(actual) != len(expected):
        return None
    if expected and all(item is not None for item in actual) and actual != expected:
        return None
    return best


def extract_table_rows(section_html: str, heading: str) -> list[list[str]]:
    rows: list[list[str]] = []
    for table in re.findall(r"<table\b[^>]*>(.*?)</table>", section_html, flags=re.IGNORECASE | re.DOTALL):
        if heading.lower() not in html_text(table).lower():
            continue
        for row in re.findall(r"<tr\b[^>]*>(.*?)</tr>", table, flags=re.IGNORECASE | re.DOTALL):
            cells = re.findall(r"<t[dh]\b[^>]*>(.*?)</t[dh]>", row, flags=re.IGNORECASE | re.DOTALL)
            values = [clean_platform_text(html_text(cell)) for cell in cells]
            if values and heading.lower() not in " ".join(values).lower():
                rows.append(values)
    return rows


def extract_parameter_rows(section_html: str, parameter_names: set[str]) -> dict[str, str]:
    parameters: dict[str, str] = {}
    # Public pages occasionally close a malformed parameter table before its last
    # row.  Scanning rows in the exact member section preserves those parameters
    # without taking rows from a neighboring member.
    for row in re.findall(r"<tr\b[^>]*>(.*?)</tr>", section_html, flags=re.IGNORECASE | re.DOTALL):
        cells = re.findall(r"<td\b[^>]*>(.*?)</td>", row, flags=re.IGNORECASE | re.DOTALL)
        if len(cells) < 2:
            continue
        name = clean_platform_text(html_text(cells[0]))
        if name not in parameter_names:
            continue
        description = clean_platform_text(html_text(cells[1]))
        if description:
            parameters[name] = description
    return parameters


def section_details(
    page: ReferencePage,
    section: SourceSection,
    member: ET.Element,
    java_path: str,
) -> ExtractedDocs:
    parameter_names = {item.attrib["Name"] for item in member.findall("./Parameters/Parameter")}
    parameters = extract_parameter_rows(section.html, parameter_names)

    return_rows = extract_table_rows(section.html, "Returns")
    result = ""
    if return_rows:
        result = " ".join(return_rows[0][1:] if len(return_rows[0]) > 1 else return_rows[0])
    throws: list[str] = []
    for row in extract_table_rows(section.html, "Throws"):
        throws.append(" ".join(row))

    without_tables = remove_non_prose_blocks(section.html)
    without_tables = re.sub(
        r"<tr\b[^>]*>.*?</tr>",
        " ",
        without_tables,
        flags=re.IGNORECASE | re.DOTALL,
    )
    without_tables = re.sub(
        r'<div\b[^>]*\bclass="[^"]*\bapi-level\b[^"]*"[^>]*>.*?</div>',
        " ",
        without_tables,
        flags=re.IGNORECASE | re.DOTALL,
    )
    paragraphs: list[str] = []
    for match in re.finditer(r"<p\b[^>]*>(.*?)</p>", without_tables, flags=re.IGNORECASE | re.DOTALL):
        text = clean_platform_text(html_text(match.group(1)))
        if text and text not in paragraphs:
            paragraphs.append(text)
    if not paragraphs:
        text = clean_platform_text(html_text(without_tables))
        text = re.sub(rf"^{re.escape(section.title)}\s*", "", text)
        if text:
            paragraphs.append(text)

    summary = first_sentence(paragraphs[0]) if paragraphs else ""
    java_type = java_path.replace("/", ".").replace("$", ".")
    label = f"{java_type}.{section.title}"
    return ExtractedDocs(
        summary=summary,
        paragraphs=paragraphs,
        parameters=parameters,
        result=clean_platform_text(result),
        throws=[clean_platform_text(item) for item in throws if clean_platform_text(item)],
        source_url=page.source_url(section.anchor),
        source_label=label,
    )


def aosp_fallback(
    java_path: str,
    register: tuple[str, str],
    cache: Path | None,
) -> ExtractedDocs | None:
    """Use public AOSP Javadoc only if the developer page has a real member gap."""

    name, descriptor = register
    if name == ".ctor" or not descriptor:
        return None
    source_url = f"{AOSP_SOURCE}{java_path}.java?format=TEXT"
    try:
        encoded = request(source_url, cache)
        source = base64.b64decode(encoded).decode("utf-8")
    except (HTTPError, RuntimeError, ValueError, UnicodeDecodeError):
        return None

    # Restrict this fallback to public methods, skip all hidden/flagged source.
    pattern = re.compile(
        r"/\*\*(?P<doc>.*?)\*/\s*"
        r"(?P<annotations>(?:@\w+(?:\([^)]*\))?\s*)*)"
        r"public\s+(?:static\s+)?[\w<>, ?\[\].@]+?\s+"
        + re.escape(name)
        + r"\s*\(",
        re.DOTALL,
    )
    for match in pattern.finditer(source):
        doc = match.group("doc")
        annotations = match.group("annotations")
        if any(token in doc or token in annotations for token in ("@hide", "@FlaggedApi", "@IntDef")):
            continue
        prose = re.split(r"^\s*@(?:param|return|throws|exception)\b", doc, maxsplit=1, flags=re.MULTILINE)[0]
        prose = clean_platform_text(re.sub(r"^\s*\*\s?", "", prose, flags=re.MULTILINE))
        if not prose:
            continue
        parameters = {
            item.group(1): clean_platform_text(item.group(2))
            for item in re.finditer(
                r"^\s*\*\s*@param\s+(\w+)\s+(.*?)(?=^\s*\*\s*@|\Z)",
                doc,
                re.MULTILINE | re.DOTALL,
            )
        }
        result_match = re.search(
            r"^\s*\*\s*@return\s+(.*?)(?=^\s*\*\s*@|\Z)",
            doc,
            re.MULTILINE | re.DOTALL,
        )
        return ExtractedDocs(
            summary=first_sentence(prose),
            paragraphs=[prose],
            parameters=parameters,
            result=clean_platform_text(result_match.group(1)) if result_match else "",
            throws=[],
            source_url=source_url,
            source_label=f"{java_path.replace('/', '.')}.{name}",
        )
    return None


def binding_infrastructure_docs(root: ET.Element, member: ET.Element) -> ExtractedDocs | None:
    name = member.attrib["MemberName"]
    if name in {"JniPeerMembers", "ThresholdClass", "ThresholdType"}:
        return ExtractedDocs(
            summary=f"Provides JNI binding infrastructure for {root.attrib['FullName'].replace('+', '.')}.",
            paragraphs=[
                "This member is used by the .NET for Android binding runtime and does not represent an Android application contract."
            ],
            parameters={},
            result="JNI peer metadata used by the managed binding runtime.",
            throws=[],
            source_url="",
            source_label="",
        )
    signature = member.find("MemberSignature[@Language='C#']")
    if name == ".ctor" and signature is not None and "IntPtr javaReference" in signature.attrib.get("Value", ""):
        return ExtractedDocs(
            summary="Initializes a managed representation of an existing Java Native Interface object.",
            paragraphs=[
                "This constructor is called by the .NET for Android binding runtime when it creates a managed peer for an existing Java object."
            ],
            parameters={
                "javaReference": "The Java Native Interface object reference.",
                "transfer": "The ownership transfer to apply to the Java Native Interface object reference.",
            },
            result="",
            throws=[],
            source_url="",
            source_label="",
        )
    return None


def is_media_route_throw_override(java_path: str, section: SourceSection | None) -> bool:
    return (
        java_path == "android/app/MediaRouteActionProvider"
        and section is not None
        and section.anchor == "onCreateActionView()"
    )


def media_route_throw_docs() -> ExtractedDocs:
    source_url = (
        f"{AOSP_SOURCE}android/app/MediaRouteActionProvider.java"
        "#109"
    )
    return ExtractedDocs(
        summary="Throws because this deprecated overload is not supported by MediaRouteActionProvider.",
        paragraphs=[
            "This overload throws UnsupportedOperationException. Use onCreateActionView(MenuItem) instead."
        ],
        parameters={},
        result="",
        throws=["UnsupportedOperationException: This overload is not supported."],
        source_url=source_url,
        source_label="android.app.MediaRouteActionProvider.onCreateActionView",
    )


def is_wallpaper_colors_parcel_constructor(root: ET.Element, member: ET.Element) -> bool:
    signature = member.find("MemberSignature[@Language='C#']")
    return (
        root.attrib.get("FullName") == "Android.App.WallpaperColors"
        and member.attrib.get("MemberName") == ".ctor"
        and signature is not None
        and "Android.OS.Parcel" in signature.attrib.get("Value", "")
    )


def wallpaper_colors_parcel_docs() -> ExtractedDocs:
    return ExtractedDocs(
        summary="Initializes a WallpaperColors instance from serialized Parcel data.",
        paragraphs=[
            "Reads the serialized wallpaper color state from the supplied Parcel."
        ],
        parameters={"parcel": "The Parcel containing serialized WallpaperColors data."},
        result="",
        throws=[],
        source_url=f"{AOSP_SOURCE}android/app/WallpaperColors.java#118",
        source_label="android.app.WallpaperColors.WallpaperColors(Parcel)",
    )


TYPE_SUMMARY_OVERRIDES = {
    "Android.App.AlertDialog+Builder": "Builds AlertDialog instances.",
    "Android.App.Application+IActivityLifecycleCallbacks": (
        "Receives callbacks for activity lifecycle state changes in an Application."
    ),
    "Android.App.AutomaticZenRule+Builder": "Builds AutomaticZenRule instances.",
    "Android.App.BreadCrumbClickFlags": "Defines flags for FragmentBreadCrumbs click handling.",
    "Android.App.MediaRouteButton": "Displays and selects available media routes.",
    "Android.App.PolicyPrioritySendersType": "Defines priority sender categories for notification policy.",
    "Android.App.RequiredContentUriPermission": (
        "Defines required URI permissions for a component caller."
    ),
    "Android.App.RunningAppProcessInfoImportanceType": (
        "Defines importance values for running application processes."
    ),
}


def apply_contract_override(
    root: ET.Element, member: ET.Element, extracted: ExtractedDocs
) -> ExtractedDocs:
    """Fill public-reference omissions with narrow, per-member contracts.

    These entries cover public pages whose return table only states nullability
    or is absent.  They are deliberately limited to the review-sensitive APIs;
    no method description is copied into a return node.
    """

    type_name = root.attrib["FullName"]
    member_name = member.attrib["MemberName"]
    signature = (
        member.find("MemberSignature[@Language='C#']").attrib.get("Value", "")
        if member.find("MemberSignature[@Language='C#']") is not None
        else ""
    )
    result = extracted.result

    return_type = member.findtext("./ReturnValue/ReturnType", default="")

    if (
        type_name == "Android.App.Activity"
        and member_name == "StartActivityForResult"
        and "Type activityType, int requestCode" in signature
    ):
        extracted.summary = "Starts an activity of the specified managed type and requests a result."
        extracted.paragraphs = [
            "This managed convenience overload creates an Intent for activityType and starts it for a result."
        ]
        extracted.parameters = {
            "activityType": "The managed Activity type to start.",
            "requestCode": (
                "If non-negative, the code returned to OnActivityResult when the activity exits; "
                "if negative, no result is returned."
            ),
        }
        extracted.source_url = (
            f"{ANDROID_REFERENCE}android/app/Activity"
            "#startActivityForResult(android.content.Intent,%20int)"
        )
        extracted.source_label = "android.app.Activity.startActivityForResult"
    elif type_name == "Android.App.Instrumentation" and member_name == "StartActivitySync":
        result = "The Activity that was started and has begun running."
    elif type_name == "Android.App.LoaderManager" and member_name == "GetLoader":
        result = "The Loader associated with the requested ID, or null if no loader exists."
    elif type_name == "Android.App.DownloadManager" and member_name == "Enqueue":
        result = "The unique download ID assigned to the enqueued request."
    elif type_name == "Android.App.Fragment" and member_name == "Instantiate":
        result = "A newly instantiated Fragment."
    elif (
        type_name == "Android.App.FragmentTransaction"
        and return_type == "Android.App.FragmentTransaction"
    ):
        result = (
            "The same FragmentTransaction instance, allowing additional transaction operations "
            "to be chained."
        )
    elif (
        type_name == "Android.App.FragmentTransaction"
        and member_name in {"Commit", "CommitAllowingStateLoss"}
        and return_type == "System.Int32"
    ):
        result = (
            "The identifier of the committed transaction's back-stack entry when the transaction "
            "was added to the back stack; otherwise, a negative value."
        )
    elif type_name == "Android.App.WallpaperManager" and member_name == "GetInstance":
        result = "The WallpaperManager associated with the supplied Context."
    elif type_name == "Android.App.PendingIntent" and member_name.startswith("Get"):
        result = (
            "The PendingIntent for the requested operation, or null when FLAG_NO_CREATE "
            "is set and no matching PendingIntent exists."
        )
    elif type_name == "Android.App.RemoteInput":
        if member_name == "GetChoicesFormatted":
            result = "The choices available for input, or null when no choices were supplied."
        elif member_name == "GetDataResultsFromIntent":
            result = (
                "A map from MIME type to result URI for the specified remote-input key, "
                "or null when no matching data result exists."
            )
        elif member_name == "GetResultsFromIntent":
            result = "The Bundle containing remote-input text results, or null when no results exist."
    elif type_name == "Android.App.RecoverableSecurityException" and member_name == "UserMessage":
        extracted.summary = "Gets the short user-facing message that describes the recovery action."
        extracted.paragraphs = [
            "The message describes the security issue for end users and can be shown in a notification or dialog."
        ]
    elif type_name == "Android.App.RecoverableSecurityException" and member_name == "UserAction":
        result = "The RemoteAction that starts recovery from the security failure."
    elif type_name == "Android.App.RecoverableSecurityException" and member_name == "UserMessageFormatted":
        result = "The short user-facing message that describes the security issue."
    elif member_name == "Build" and (
        type_name.startswith("Android.App.Notification+") or type_name == "Android.App.Notification"
    ):
        return_type = member.findtext("./ReturnValue/ReturnType", default="Android.App.Notification")
        result = f"The {return_type.replace('+', '.')} built by this API."
    elif (
        type_name.startswith("Android.App.Notification+")
        and type_name.endswith("+Builder")
        and return_type == type_name
    ):
        result = f"This {type_name.replace('+', '.')} instance, allowing calls to be chained."
    elif result == "This value cannot be null." and member_name == "Create":
        if return_type:
            result = f"The {return_type.replace('+', '.')} created by this API."
    elif result == "This value cannot be null." and (
        return_type == type_name
    ):
        result = f"This {type_name.replace('+', '.')} instance, allowing calls to be chained."
    elif (
        not result
        and return_type == type_name
        and " static " not in signature
    ):
        result = f"This {type_name.replace('+', '.')} instance, allowing calls to be chained."
    elif (
        result == "This value cannot be null."
        and member_name.startswith(("Get", "Find", "Acquire", "New"))
    ):
        if return_type:
            result = f"The {return_type.replace('+', '.')} returned by this API."

    # String convenience properties are generated from their CharSequence
    # counterparts; retain their user-facing contract instead of a generic
    # managed-value description.
    if type_name == "Android.App.RecoverableSecurityException" and member_name == "UserMessage":
        result = ""
    extracted.result = result
    return extracted


def docs_opening(old_docs: str) -> str:
    match = re.match(r"<Docs\b[^>]*>", old_docs)
    return match.group(0) if match else "<Docs>"


def retained_since(old_docs: str) -> list[str]:
    return re.findall(r"<since\b[^>]*/>", old_docs)


def retained_summary(old_docs: str) -> str:
    """Keep an existing non-placeholder summary when no source replacement exists."""
    match = re.search(r"<summary\b[^>]*>(.*?)</summary>", old_docs, flags=re.DOTALL)
    if not match:
        return ""
    summary = match.group(1).strip()
    try:
        text = normalize_text("".join(ET.fromstring(f"<summary>{summary}</summary>").itertext()))
    except ET.ParseError:
        return ""
    return summary if text and text != "To be added." else ""


def reference_para(url: str, label: str) -> str:
    if not url:
        return ""
    return (
        '<para><format type="text/html"><a href="'
        + html.escape(url, quote=True)
        + '" title="Reference documentation">Android reference for <code>'
        + escape_xml(label)
        + "</code>.</a></format></para>"
    )


def render_docs(
    old_docs: str,
    indent: str,
    member: ET.Element | None,
    extracted: ExtractedDocs,
    root: ET.Element,
) -> str:
    child_indent = indent + "  "
    lines = [f"{indent}{docs_opening(old_docs)}"]
    if member is not None:
        parameter_names = [item.attrib["Name"] for item in member.findall("./Parameters/Parameter")]
        for name in parameter_names:
            description = extracted.parameters.get(name)
            if description:
                lines.append(f'{child_indent}<param name="{name}">{escape_xml(description)}</param>')

    summary = extracted.summary
    if not summary and member is None:
        summary = TYPE_SUMMARY_OVERRIDES.get(root.attrib["FullName"], "")
    summary_xml = escape_xml(summary) if summary else retained_summary(old_docs)
    lines.append(f"{child_indent}<summary>{summary_xml}</summary>")

    if member is not None:
        member_type = member.findtext("MemberType", default="")
        return_type = member.findtext("./ReturnValue/ReturnType", default="")
        if (
            return_type
            and return_type != "System.Void"
            and member_type != "Field"
            and extracted.source_url
        ):
            value = extracted.result
            is_media_route_throw = (
                get_type_registration(root) == "android/app/MediaRouteActionProvider"
                and member.attrib["MemberName"] == "OnCreateActionView"
            )
            if not value and is_media_route_throw:
                value = ""
            if value:
                element = "value" if member_type == "Property" else "returns"
                lines.append(f"{child_indent}<{element}>{escape_xml(value)}</{element}>")

    paragraphs = list(extracted.paragraphs)
    for item in extracted.throws:
        if item:
            paragraphs.append(f"Throws: {item}")
    lines.append(f"{child_indent}<remarks>")
    for paragraph in paragraphs:
        if paragraph:
            lines.append(f"{child_indent}  <para>{escape_xml(paragraph)}</para>")
    source = reference_para(extracted.source_url, extracted.source_label)
    if source:
        lines.append(f"{child_indent}  {source}")
    lines.append(f"{child_indent}  <para>{ATTRIBUTION}</para>")
    lines.append(f"{child_indent}</remarks>")
    for since in retained_since(old_docs):
        lines.append(f"{child_indent}{since}")
    lines.append(f"{indent}</Docs>")
    return "\n".join(lines)


def docs_blocks(text: str) -> list[re.Match[str]]:
    return list(re.finditer(r"<Docs\b[^>]*>.*?</Docs>", text, flags=re.DOTALL))


def original_docs_blocks(path: Path) -> list[str]:
    """Read pre-repair Docs blocks solely to recover verified source URLs.

    Existing prose is never reused.  This supports interface constants whose
    owner is outside android.app (for example ComponentCallbacks2 constants
    projected through Activity.InterfaceConsts).
    """

    relative = path.relative_to(ROOT).as_posix()
    try:
        source = subprocess.check_output(
            ["git", "show", f"HEAD:{relative}"], cwd=ROOT, text=True, encoding="utf-8"
        )
    except subprocess.CalledProcessError:
        return []
    return [block.group(0) for block in docs_blocks(source)]


def linked_source_section(
    old_docs: str,
    member: ET.Element,
    cache: Path | None,
    pages: dict[str, ReferencePage | None],
) -> tuple[ReferencePage, SourceSection, str] | None:
    """Resolve a verified field link without reusing old prose."""

    member_type = member.findtext("MemberType", default="")
    if member_type != "Field":
        return None
    for url in re.findall(r'<a\s+href="([^"]+)"', old_docs, flags=re.IGNORECASE):
        url = html.unescape(url)
        if not url.startswith(ANDROID_REFERENCE) or "#" not in url:
            continue
        source_url, anchor = url.split("#", 1)
        java_path = source_url[len(ANDROID_REFERENCE) :]
        if not field_anchor_matches(member, anchor):
            continue
        if java_path not in pages:
            try:
                pages[java_path] = ReferencePage(java_path, cache)
            except HTTPError as error:
                if error.code != 404:
                    raise
                pages[java_path] = None
        page = pages[java_path]
        if page is None:
            continue
        section = next((item for item in page.sections if item.anchor == anchor), None)
        if section:
            return page, section, java_path
    return None


def managed_string_alias_section(
    page: ReferencePage,
    member: ET.Element,
    peers: list[ET.Element],
    java_path: str,
) -> SourceSection | None:
    """Map a managed string convenience overload to its JNI CharSequence peer."""

    if member.findtext("MemberType", default="") != "Method" or get_register(member):
        return None
    parameter_types = [item.attrib.get("Type", "") for item in member.findall("./Parameters/Parameter")]
    char_sequence_types = {
        "System.String": "Java.Lang.ICharSequence",
        "System.String[]": "Java.Lang.ICharSequence[]",
    }
    if not any(item in char_sequence_types for item in parameter_types):
        return None

    for peer in peers:
        if peer is member or peer.attrib.get("MemberName") != member.attrib.get("MemberName"):
            continue
        if not get_register(peer):
            continue
        if peer.findtext("MemberType", default="") != "Method":
            continue
        if peer.findtext("./ReturnValue/ReturnType", default="") != member.findtext(
            "./ReturnValue/ReturnType", default=""
        ):
            continue
        peer_types = [item.attrib.get("Type", "") for item in peer.findall("./Parameters/Parameter")]
        if len(peer_types) != len(parameter_types):
            continue
        if not all(
            peer_type == char_sequence_types.get(parameter_type, parameter_type)
            for parameter_type, peer_type in zip(parameter_types, peer_types)
        ):
            continue
        section = candidate_section(page, peer, java_path)
        if section:
            return section
    return None


def line_indent(text: str, offset: int) -> str:
    start = text.rfind("\n", 0, offset) + 1
    return re.match(r"[ \t]*", text[start:offset]).group(0)


def inferred_java_path(root: ET.Element) -> str | None:
    registered = get_type_registration(root)
    if registered:
        return registered

    full_name = root.attrib.get("FullName", "")
    short_name = root.attrib.get("Name", "")
    if (
        not full_name.startswith("Android.App.")
        or short_name.endswith(("Attribute", "EventArgs", "InterfaceConsts"))
        or root.findtext("Base/BaseTypeName") == "System.Enum"
    ):
        return None
    return "android/app/" + full_name[len("Android.App.") :].replace("+", "$").replace(".", "$")


def enum_java_path(root: ET.Element) -> str | None:
    parents = {
        item[0]
        for member in root.findall("./Members/Member")
        if (item := get_jni_field(member)) is not None and item[0].startswith("android/app/")
    }
    return next(iter(parents)) if len(parents) == 1 else None


def generate_file(path: Path, cache: Path | None, pages: dict[str, ReferencePage | None]) -> dict[str, int]:
    raw_bytes = path.read_bytes()
    text = re.sub(r"\r+\n", "\n", raw_bytes.decode("utf-8")).replace("\r", "\n")
    root = ET.fromstring(text)
    blocks = docs_blocks(text)
    original_blocks = original_docs_blocks(path)
    members = root.findall("./Members/Member")
    expected_blocks = 1 + len(members)
    if len(blocks) != expected_blocks:
        raise ValueError(
            f"{path}: expected {expected_blocks} Docs blocks (type + members), found {len(blocks)}"
        )
    if original_blocks and len(original_blocks) != expected_blocks:
        raise ValueError(
            f"{path}: HEAD has {len(original_blocks)} Docs blocks; expected {expected_blocks}"
        )

    type_java_path = inferred_java_path(root)
    java_path = type_java_path or enum_java_path(root)

    def page_for(source_path: str | None) -> ReferencePage | None:
        if not source_path:
            return None
        if source_path not in pages:
            try:
                pages[source_path] = ReferencePage(source_path, cache)
            except HTTPError as error:
                if error.code != 404:
                    raise
                pages[source_path] = None
        return pages[source_path]

    page = page_for(java_path)
    replacements: list[tuple[int, int, str]] = []
    counts = {"source": 0, "aosp": 0, "managed": 0}

    type_old = blocks[0].group(0)
    type_indent = line_indent(text, blocks[0].start())
    if page and type_java_path:
        overview = page.overview
        type_docs = ExtractedDocs(
            summary=first_sentence(overview[0]) if overview else "",
            paragraphs=overview,
            parameters={},
            result="",
            throws=[],
            source_url=page.source_url(),
            source_label=java_path.replace("/", ".").replace("$", "."),
        )
        counts["source"] += 1
    else:
        type_docs = ExtractedDocs("", [], {}, "", [], "", "")
        counts["managed"] += 1
    replacements.append(
        (
            blocks[0].start() - len(type_indent),
            blocks[0].end(),
            render_docs(type_old, type_indent, None, type_docs, root),
        )
    )

    for index, member in enumerate(members, start=1):
        old_docs = blocks[index].group(0)
        indent = line_indent(text, blocks[index].start())
        source_path = java_path
        jni_field = get_jni_field(member)
        if jni_field and jni_field[0].startswith("android/app/"):
            source_path = jni_field[0]
        source_page = page_for(source_path)
        section = candidate_section(source_page, member, source_path) if source_page and source_path else None
        if section is None and source_page and source_path:
            section = managed_string_alias_section(source_page, member, members, source_path)
        if section is None and original_blocks:
            linked = linked_source_section(original_blocks[index], member, cache, pages)
            if linked:
                source_page, section, source_path = linked
        if is_media_route_throw_override(source_path or "", section):
            extracted = media_route_throw_docs()
            counts["aosp"] += 1
        elif is_wallpaper_colors_parcel_constructor(root, member):
            extracted = wallpaper_colors_parcel_docs()
            counts["aosp"] += 1
        elif section and source_page and source_path:
            extracted = section_details(source_page, section, member, source_path)
            counts["source"] += 1
        else:
            binding = binding_infrastructure_docs(root, member)
            if binding:
                extracted = binding
                counts["managed"] += 1
            else:
                fallback = (
                    aosp_fallback(source_path, get_register(member), cache)
                    if source_page and source_path and get_register(member)
                    else None
                )
                if fallback:
                    extracted = fallback
                    counts["aosp"] += 1
                else:
                    extracted = ExtractedDocs("", [], {}, "", [], "", "")
                    counts["managed"] += 1
        extracted = apply_contract_override(root, member, extracted)
        replacements.append(
            (
                blocks[index].start() - len(indent),
                blocks[index].end(),
                render_docs(old_docs, indent, member, extracted, root),
            )
        )

    for start, end, replacement in reversed(replacements):
        text = text[:start] + replacement + text[end:]
    text = text.rstrip("\n") + "\n"
    path.write_bytes(text.replace("\n", "\r\n").encode("utf-8"))
    return counts


def changed_paths() -> list[Path]:
    base = subprocess.check_output(
        ["git", "merge-base", "origin/main", "HEAD"], cwd=ROOT, text=True
    ).strip()
    output = subprocess.check_output(
        ["git", "diff", "--name-only", base, "HEAD", "--", "docs/xml/Android.App"],
        cwd=ROOT,
        text=True,
    )
    return [
        ROOT / line
        for line in output.splitlines()
        if line.endswith(".xml") and not line.endswith("ns-Android.App.xml")
    ]


def docs_text(path: Path) -> Iterable[str]:
    text = path.read_text(encoding="utf-8")
    for block in docs_blocks(text):
        yield block.group(0)


def validate(paths: list[Path], cache: Path | None) -> int:
    errors: list[str] = []
    links: set[str] = set()
    for path in paths:
        raw = path.read_bytes()
        remaining = raw.replace(b"\r\n", b"")
        if b"\r\n" not in raw or b"\r" in remaining or b"\n" in remaining:
            errors.append(f"{path}: must use CRLF line endings")
        try:
            root = ET.fromstring(raw.decode("utf-8"))
        except ET.ParseError as error:
            errors.append(f"{path}: invalid XML: {error}")
            continue
        for block in docs_text(path):
            for token in RAW_DOC_TOKENS:
                if token in block:
                    errors.append(f"{path}: raw documentation token {token!r}")
            for label, pattern in RAW_DOC_PATTERNS:
                if pattern.search(block):
                    errors.append(f"{path}: {label}")
            for fallback in FORBIDDEN_GENERIC_FALLBACKS:
                if fallback in block:
                    errors.append(f"{path}: generic fallback text remains")
            if "To be added." in block:
                errors.append(f"{path}: placeholder remains")
        for member in root.findall("./Members/Member"):
            docs = member.find("Docs")
            if docs is None:
                continue
            summary = normalize_text("".join(docs.findtext("summary", default="").split()))
            for tag in ("param", "returns", "value"):
                for child in docs.findall(tag):
                    value = normalize_text("".join(child.itertext()))
                    if len(value) > 40 and value == summary:
                        errors.append(
                            f"{path}: {member.attrib['MemberName']} duplicates summary in <{tag}>"
                        )
        links.update(
            html.unescape(value)
            for value in re.findall(
                r'<a\s+href="([^"]+)"',
                raw.decode("utf-8"),
                flags=re.IGNORECASE,
            )
            if value.startswith((ANDROID_REFERENCE, AOSP_SOURCE))
        )

    pages: dict[str, str] = {}
    for link in sorted(links):
        page_url, separator, anchor = link.partition("#")
        try:
            source = pages.get(page_url)
            if source is None:
                source = request(page_url, cache)
                pages[page_url] = source
        except (HTTPError, RuntimeError) as error:
            errors.append(f"{link}: source link could not be fetched: {error}")
            continue
        if separator:
            ids = {
                html.unescape(item)
                for item in re.findall(r'\bid="([^"]+)"', source, flags=re.IGNORECASE)
            }
            if anchor not in ids and unquote(anchor) not in ids:
                errors.append(f"{link}: source anchor was not found")

    index_changed = subprocess.check_output(
        ["git", "diff", "--name-only", "origin/main...HEAD", "--", "docs/xml/index.xml"],
        cwd=ROOT,
        text=True,
    ).strip()
    if index_changed:
        errors.append("docs/xml/index.xml must not be changed")

    if errors:
        print("\n".join(f"ERROR: {error}" for error in errors), file=sys.stderr)
        return 1
    print(
        f"Validated {len(paths)} XML files, {len(links)} Android reference links, "
        "CRLF, XML, source anchors, placeholders, and extraction corruption markers."
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--cache",
        type=Path,
        help="Optional project-local cache for official reference pages.",
    )
    parser.add_argument(
        "--validate",
        action="store_true",
        help="Validate changed Android.App XML without rewriting it.",
    )
    parser.add_argument(
        "paths",
        nargs="*",
        type=Path,
        help="Optional XML files. Defaults to XML files changed from origin/main.",
    )
    args = parser.parse_args()
    paths = [path if path.is_absolute() else ROOT / path for path in args.paths] or changed_paths()
    paths = [path for path in paths if path.name != "ns-Android.App.xml"]
    if args.validate:
        return validate(paths, args.cache)

    pages: dict[str, ReferencePage | None] = {}
    totals = {"source": 0, "aosp": 0, "managed": 0}
    for number, path in enumerate(paths, start=1):
        counts = generate_file(path, args.cache, pages)
        for key, value in counts.items():
            totals[key] += value
        print(f"[{number}/{len(paths)}] {path.relative_to(ROOT)}")
        # Avoid concentrating hundreds of requests into a single instant.
        if number % 20 == 0:
            time.sleep(0.25)
    print(
        "Regenerated "
        f"{len(paths)} files from {totals['source']} exact public-reference members, "
        f"{totals['aosp']} public AOSP gaps, and {totals['managed']} managed-only projections."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
