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
are filtered, and literal Unicode escapes are decoded before XML escaping. Source
prose is not imported for the exact `SaProposal.PSEUDORANDOM_FUNCTION_SHA2_512` field
when its complete Android description incorrectly names HMAC-SHA2-384. That channel
is reported as ambiguous instead of inventing replacement prose; existing authored
or imported documentation is preserved.
Three further exact IKE channels are withheld rather than importing contradicted
contracts: the IPv6 requested-prefix getter's `-1` sentinel, the IKE-SA DH list
advertising `DH_GROUP_NONE`, and the Child-SA encryption list advertising 3DES.
The exact MOBIKE paragraph that confuses target SDK with device OS is excluded
while retaining its other official paragraphs. These member/source/full-text
guards do not affect valid sibling SA choices or shared constant descriptions.
Prior importer-owned copies can be withdrawn only when the complete original
source-reference/attribution structure and plain original channel match;
authored or mixed content, comments, CDATA, processing instructions, duplicate
channels, mismatched binding metadata, and changed source text are preserved.
Future corrected official source remains eligible. HTML tags
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
Repair of the known `Control.StatefulBuilder.setControlTemplate` paragraph boundary
is restricted to its exact Android member URL and verified lead-in/description,
preserving the source's blank line as two paragraphs without inventing punctuation.
The corresponding summary and remarks are regenerated together only when the
managed member ID, full old plain-text summary/remarks, exact source reference,
and Android attribution all match the prior importer output. Authored additions,
mixed content, source changes, and unsafe XML locations are reported and preserved.
The two-channel repair requires at least two remaining changes in the batch.
The final `ControlsProviderService.onBind` and `onUnbind` overrides expose inherited
`Service` Javadoc that contradicts their implementations. Exact member-ID/source-URL
guards remove only the verified nullable-binder and default-false remarks sentences,
retaining the other verbatim reference sentences. The inherited `onUnbind` caller-choice
return is reported as `source_channel_ambiguous`, not replaced with implementation-derived
prose. Known prior importer output can be regenerated only with its complete original
remarks, plain summary/return channels, canonical source reference, and attribution
intact; its unsafe return is restored to `To be added.`. Authored or mixed content,
duplicate channels, altered metadata, and changed sources are preserved and reported.
Repairs for one member are atomic and require room for every affected channel.
Implementation verification is not an AOSP prose fallback.
The final `HostApduService.onBind` and `HostNfcFService.onBind` overrides have
the same inherited nullable-binder defect. NFC-specific guards require the
exact managed sealed signature, JNI descriptor, parameter/return metadata,
canonical Android URL/label, and complete source paragraphs and table channels.
They remove only `May return null if clients can not bind to the service.`;
the separate Binder-thread guidance remains verbatim. Known prior-owned remarks
are repaired only with the full original plain-text paragraphs, exact summary
and return, canonical reference, and recognized unchanged attribution. Authored
parameters are not changed, including their existing links and markup.
The six `PollingLoopType` summaries are held back when their full official text
describes a `POLLING_LOOP_TYPE` key in a Bundle passed to
`HostApduService.processPollingFrames(List)`. That callback receives polling
frames, not the internal Bundle. The exclusion requires the exact managed enum
field/value, canonical source URL/label, and complete original prose. Only the
exact prior importer-generated summary with its canonical reference and
attribution can be withdrawn to `To be added.`; no substitute description is
invented. Changed source, metadata, authored or mixed XML, CDATA, comments,
processing instructions, attributes, duplicate channels, and altered references
or attribution are preserved and reported. Registered first-fill, prior-owned
repair, negative-case, and byte-identical repeat tests exercise the complete
importer pipeline for all eight NFC members.
The known Android `SetOperatorPlmnIds` PLMN ordering defect is corrected only
when the exact Android source URL, managed member ID, parameter name, and full
importer-owned original parameter text all match; all other parameter
documentation is preserved.
The DreamService focus callback's stale `View.onWindowFocusChangedNotLocked(boolean)`
source label is corrected only on its exact official member URL when the source
hyperlink targets `View#onWindowFocusChanged(boolean)`. An existing imported
paragraph is repaired only for the exact managed callback, complete old plain-text
paragraph, matching source reference, and unchanged recognized attribution.
Other prose, mixed content, and metadata are preserved.
Four channel-specific corrections cover the known `Android.Ranging.Ble.CS`
source typos in the security-level-one enum summary, builder Bluetooth-address
parameter, and parcel-write summary and remarks. They require the exact managed
member, canonical source URL, full original plain text, and complete source-proven
importer paragraph/reference/attribution structure. Authored or mixed markup,
changed source text, and duplicate channels are preserved. The same narrow
allow-list corrects newly imported source channels so later refreshes cannot
reintroduce these defects; no general grammar or punctuation normalization occurs.
The same exact allow-list covers the RSSI builder's Bluetooth-address parameter
and RSSI parcel-write summary and remarks. The RSSI update-rate setter's malformed
default paragraph (an unresolved `ERROR(...)` label linked to the site root) is
excluded only for its exact managed member, declaring type, full JNI registration,
canonical Android member URL, and complete original paragraph text.
Its safe lead, parameter, and return documentation remain eligible, and the
excluded paragraph is reported as `source_channel_ambiguous`. Changed or corrected
official source is preserved; no default-value prose is inferred from implementation.
Unrelated summary edits or added safe paragraphs do not re-enable the malformed
paragraph; all other source paragraphs retain their original order.
The site-root link describes the observed source, not a required exclusion
predicate: the exact unresolved label remains unsafe even if its hyperlink
changes. Canonical member-page/JNI provenance is checked independently of that
inner hyperlink; no source-label correction is inferred from either link.
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
The declared `Gesture`, `GesturePoint`, and `GestureStroke` `clone()` remarks
retain three exact official colon-ended introductions only before their exact,
adjacent nonempty clone expressions. This preserves the source's general-intent,
non-absolute-requirement, and typical-equality qualifications in source order.
Other URLs, changed introductions or expressions, empty code, and intervening
blocks cannot enable these introductions. An earlier importer-owned copy that
omitted them can be refreshed only with the exact managed/JNI identity, mapped
canonical member URL, complete original source paragraphs and code, and complete
original plain remarks/reference/attribution structure. Authored additions,
mixed markup, CDATA, comments, processing instructions, duplicate remarks, and
changed source or binding provenance are preserved. The repair retains the
original reference and attribution element bytes. The exact prior complete
clone remarks with normalized attribution can restore the known original
nonbreaking spaces only when the entire source, mapped identity, plain importer
reference/attribution, original mixed-code summary, and plain return also match;
only that attribution element changes. Registered first-fill and
prior-copy regression tests exercise one-operation limits and byte-identical
zero-write repeats.
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

The `IkeProtocolErrorType.NoAdditionalSas` enum summary is skipped when its exact
managed field, canonical `IkeProtocolException.ERROR_TYPE_NO_ADDITIONAL_SAS`
Android URL, and complete source prose match `No additional SAa are acceptable`.
The undefined `SAa` term is not guessed or silently corrected. This exclusion
is checked in the production `JniField` mapping before its early return;
other constants, changed source prose, and authored documentation are preserved.

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
