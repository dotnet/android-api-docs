# XML documentation importer

`importer.cs` is a conservative file-based C# app that fills exact `To be added`
placeholders inside `<Docs>` from declared members on official Android developer
reference pages and official Java 21 API pages.

Run it with the .NET 10 SDK or newer:

```powershell
dotnet run tools\importer.cs -- --self-test
dotnet run tools\importer.cs -- --path docs\xml\Android.Animation\ArgbEvaluator.xml --member Evaluate --report artifacts\argb-import
dotnet run tools\importer.cs -- --path docs\xml\Android.Animation --namespace Android.Animation --max-changes 10 --cache C:\temp\android-doc-cache
dotnet run tools\importer.cs -- --path docs\xml --api-since 37 --max-changes 10 --report artifacts\api-37-import
dotnet run tools\importer.cs -- --path docs\xml\Android.Animation\ArgbEvaluator.xml --member Evaluate --apply --max-changes 4
dotnet run tools\importer.cs -- --path docs\xml\Android.Animation --namespace Android.Animation --offline --cache C:\temp\android-doc-cache
```

Dry-run is the default. An unscoped scan is rejected, and `--apply` requires a
path or namespace write scope. Generated `docs/xml/index.xml` and non-API
`docs/xml/_filter.xml` and `docs/xml/FrameworksIndex` files are always excluded.
The default limit is 25 placeholder elements. Use `--api-since` to restrict
owners to declarations introduced in an exact Android API level. Selection
recognizes both `ApiSince` and `SupportedOSPlatform` metadata. Members without
their own availability metadata inherit the containing type's API level.

The importer uses the managed type registration, exact JNI names and descriptors,
and `JniField` owner metadata for projected constants. It also recognizes scalar
`JniTypeSignature` type metadata and `JniConstructorSignature` descriptors.
Narrow source-verified mappings cover Java.Interop runtime members whose upstream
implementation embeds an exact JNI member identifier and whose identity is also
confirmed by sibling generated binding metadata.
Array-rank `JniTypeSignature` metadata is not mapped to its element type because
Java API pages do not declare array wrapper types. It skips members with missing
registrations, unknown type descriptors, overload mismatches, ambiguous matches,
inherited-only detail, missing documentation channels, or source text that contains
only a Java type, nullability marker, cross-reference heading, or standalone
deprecation boilerplate. Android page license/trademark footers and update timestamps
are filtered, and literal Unicode escapes are decoded before XML escaping. HTML tags
are removed with quoted attributes intact, and empty table description cells remain
empty rather than shifting Java types into prose. Java `deprecation-block` containers
are excluded before selecting exact `block` documentation. Android return tables are
selected only by an exact `Returns` heading cell, not by prose containing that word.
Remarks placeholder replacements retain every usable official source fragment in
order, rendering prose as `<para>` and Java code examples as
`<code lang="text/java">`. Stale links for the same source member are replaced
and current links are placed before existing attribution. Enum field prose,
source links, and attribution are emitted in `<summary>` because their
`<remarks>` are not published by ECMA2Yaml. A deprecated enum summary retains
both its caution and subsequent semantic value prose. The importer never creates
generic prose or falls back to AOSP. Existing non-placeholder documentation is
retained, except that an exact prior importer-generated caution-only enum summary
can be completed from the same authoritative source. A four-member allow-list
can correct the historic `ConcurrentHashMap` and `ConcurrentSkipListMap`
conditional Boolean return error only when the exact Java source URL, the full
old importer markup, and a `System.Boolean` managed return all match. Repair
eligibility is evaluated against the untouched summary and requires exactly
three importer-owned
paragraphs: plain deprecation prose, the exact field source reference, and exact
Android attribution. Additional nodes or markup preserve the summary verbatim.
The known Android `SetOperatorPlmnIds` PLMN ordering defect is corrected only
when the exact Android source URL, managed member ID, parameter name, and full
importer-owned original parameter text all match; all other parameter
documentation is preserved.
Four channel-specific corrections cover the known `Android.Ranging.Ble.CS`
source typos in the security-level-one enum summary, builder Bluetooth-address
parameter, and parcel-write summary and remarks. They require the exact managed
member, canonical source URL, full original plain text, and complete source-proven
importer paragraph/reference/attribution structure. Authored or mixed markup,
changed source text, and duplicate channels are preserved. The same narrow
allow-list corrects newly imported source channels so later refreshes cannot
reintroduce these defects; no general grammar or punctuation normalization occurs.
Two exact EAP channel guards additionally require the complete official source
contract, canonical URL and reference label, registered JNI descriptor, managed
return and parameter types, and (for repairs) the complete original importer-owned
Docs. `EapAkaInfo.Builder.SetReauthId` skips the source parameter that wrongly
describes a re-authentication ID as the client's EAP identity; an exact prior
import is withdrawn to its placeholder rather than replaced with inferred prose.
`EapSessionConfig.Builder.SetEapMsChapV2Config` corrects only the full known
`faciliate` return text. Both guards operate on first-fill imports as well as
strict prior-owned repairs. Changed source contracts, authored or mixed markup,
CDATA, comments, processing instructions, attributes, references, attribution,
and managed metadata prevent repairs and are preserved with a reported skip.
Java-signature remarks repairs likewise require only an unmodified Java signature,
the exact canonical Android source reference, and the exact Android attribution;
authored nodes are preserved. Summary repair selection is XML-aware, including
CDATA containing literal closing-tag text.
The Java 21 `ZoneRules.getTransition(LocalDateTime)` example's `rule`/`rules`
receiver typo can be repaired only for the exact managed member, source URL,
original code block, and complete importer-owned source remarks. Authored
content, changed code or whitespace, and mismatched source structure are
preserved.
The exact Java 21 `ZoneOffsetTransitionRule.of(...)` `time` parameter channel
is skipped when its complete source text unconditionally uses the before-offset
time base: `UTC` and `STANDARD` instead use the selected `TimeDefinition`.
An earlier importer-owned copy of that exact parameter can be withdrawn to its
placeholder only with the exact managed member, canonical URL, full source text,
plain original parameter markup, and complete importer-owned source remarks.
Authored or mixed content, CDATA, comments, processing instructions, mismatched
source/parameter metadata, and all other parameter channels are preserved.
Repair-only mapping or source failures are reported against the `summary` target.
Existing self-closing `<remarks />` elements are expanded in place rather than
duplicated. Importer-owned remarks refreshes and copied-description repairs use
parser-corresponding element spans and skip layouts that cannot be located
unambiguously. Copied-description repairs additionally require an actual,
importer-owned source-reference element and only replace direct-text summaries
or remarks paragraphs; mixed-content paragraphs are reported and preserved.
Java explanatory lead-ins immediately preceding a code block are retained with
their trailing colon only when they use a source-proven code-introduction form;
ordinary incomplete prose remains excluded.
Android CDDL introductions ending in `CBOR with the following CDDL:` are likewise
retained only immediately before a non-empty code block, preserving certificate
extension metadata that introduces the schema. This is checked in parsed block
order for both nested and sibling paragraph/code elements. Empty code or
intervening paragraphs cannot make a later code block eligible. A standalone
introduction or unrelated incomplete Android prose remains excluded.
Source-proven Java remarks with an
exact Android attribution can receive missing ordered source fragments without
altering that attribution. A plain source paragraph may own one exact
whitespace-normalized source fragment or one exact consecutive sequence of
such fragments; the established `Added in <version>.` annotation is retained
only as non-source metadata after all source prose. Source and retained
paragraphs must contain only text and whitespace: comments, CDATA, processing
instructions, and child markup cause a reported no-edit skip. Legacy Android
attribution paragraphs are treated as
importer metadata only when their complete parsed markup and normalized text
match a known generated form; lookalikes with authored content are preserved
and reported.
Source-reference reconciliation likewise uses parsed importer-owned paragraphs
and element spans: stale references are replaced and duplicates removed only
when their XML elements are safely located. Literal markup in comments, CDATA,
or processing instructions, and authored references or attribution, are never
modified; unsafe source-reference locations are reported as skips.

The `KeyStoreException.RetryPolicy` value channel is skipped when its exact
managed member ID, official `getRetryPolicy()` URL, and complete source return
text match the known incorrect flag-combination wording. Retry policies are
mutually exclusive codes, not flags. The guard does not affect other channels,
members, URLs, corrected source text, or existing authored documentation.

Existing non-placeholder remarks are retained unless their full structure proves
they were generated by this importer: non-empty plain `<para>` or Java `<code>`
elements must be an ordered subset beginning with the exact first visible source
element, followed only by the exact mapped source-reference paragraph and Android
attribution (when applicable). Any extra nodes, markup, source mismatch, or
out-of-order content preserves the remarks verbatim. Eligible remarks are rebuilt
from the complete ordered visible source paragraphs and code blocks, rather than
having text appended. Nested Android code-container markup for one physical
sample is coalesced, while separate repeated visible blocks remain in order.

Official pages are cached by URL hash. Network requests use a clear user agent,
bounded concurrency, a size limit, and deterministic retry/backoff. `--offline`
only reads the cache. `--report path` writes a deterministic JSON report and an
adjacent text report.

On apply, each changed file is reparsed before and after an atomic write while
retaining its original newline convention and UTF-8 BOM state. Report entries
remain `would_apply` until the corresponding atomic write succeeds; partial
failures retain the completed-file count and source counters. Run `git diff
--check` after a batch.

Limitations:

- Java module routing is intentionally limited to `java.base`, `java.sql`,
  `java.xml`, and `java.net.http`.
- Documentation is imported as XML-escaped plain text; source HTML formatting
  is not reproduced.
- Only existing placeholders are replaced. Exception text is filled only when
  an existing managed `cref` has one unambiguous source exception match.
- Source-page layout changes cause conservative skips rather than guessed text.
