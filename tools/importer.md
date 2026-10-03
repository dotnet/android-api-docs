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
canonical Android URL/label, and the complete exact unsafe non-code paragraph.
First-fill recognition is independent of unrelated summaries, surrounding prose,
Binder-thread guidance, and source parameter, return, or exception channels.
Only that paragraph is replaced, removing only
`May return null if clients can not bind to the service.`; every other paragraph
and channel remains unchanged and in source order. Known prior-owned remarks
are repaired only with the full original plain-text paragraphs, exact summary
and return, canonical reference, and recognized unchanged attribution. Authored
parameters are not changed, including their existing links and markup.
The six `PollingLoopType` summaries are held back when their full official text
describes a `POLLING_LOOP_TYPE` key in a Bundle passed to
`HostApduService.processPollingFrames(List)`. That callback receives polling
frames, not the internal Bundle. The exclusion requires the exact managed enum
field/value, canonical source URL/label, and complete original prose. Only the
presence of that exact non-code paragraph enables first-fill exclusion, even
with safe introductory or concluding context; corrected, removed, changed,
code-only, or malformed source does not enable it. Only the
exact prior importer-generated summary with its canonical reference and
attribution can be withdrawn to `To be added.`; no substitute description is
invented. Changed source, metadata, authored or mixed XML, CDATA, comments,
processing instructions, attributes, duplicate channels, and altered references
or attribution are preserved and reported. Registered first-fill, prior-owned
repair, negative-case, and byte-identical repeat tests exercise the complete
importer pipeline for all eight NFC members.
The same unsafe-summary marker is honored by generic enum-summary refreshes,
including list-gap completion, source-text refresh, truncated-summary rebuilding,
and reference/attribution reconciliation. A safe prior importer-owned summary
is retained byte-for-byte when contextual source still contains the complete
unsafe polling paragraph; the excluded summary is reported as
`source_channel_ambiguous` without consuming the change budget. Registered
regressions first produce safe summaries through the actual importer, then
exercise all six contextual polling refreshes and persisted zero-write repeats.
This downstream gate does not broaden NFC first-fill recognition or the strict
original-output repair predicate. Corrected, removed, changed, or code-only source
remains eligible under those unchanged rules.
Copied-description label repairs likewise check the unsafe marker for each
candidate's own target before any summary replacement or remarks removal.
Excluded legacy summaries are reported as `source_channel_ambiguous` without
consuming the budget or changing their source references, attribution or bytes.
Registered regressions exercise all six polling fields with explicitly
synthesized legacy-label inputs, not claimed historical importer output.
The options-taking `MediaBrowser.subscribe` and callback-taking `unsubscribe`
overloads omit options matching and callback identity in two reference
paragraphs. Only those exact paragraphs are excluded, with an explicit
`source_channel_ambiguous` report; the remaining reference prose is retained.
First-fill filtering requires the exact registered managed/JNI owner, name and
full descriptor, canonical Android URL and source label, and the complete known
unsafe plain paragraph. It is independent of summary and surrounding safe
context changes, retaining every other source paragraph in order and every other
channel unchanged. Corrected, removed, and code-only versions remain eligible.
Strict prior-owned repair recognition still requires the complete unchanged
source summary and paragraph sequence. The full unfiltered paragraph sequence
is retained independently of the first-fill exclusion marker; reordered or
duplicated unsafe paragraphs cannot authorize withdrawal merely because they
filter to the expected safe sequence. A prior imported copy
can lose only its unsafe paragraph when its complete plain summary, ordered
remarks, source reference, and attribution match known importer output.
Authored or mixed nodes, duplicate channels, altered sources, and mismatched
metadata are preserved. Removal leaves every retained metadata byte unchanged;
corrected future source prose remains eligible.
The known Android `SetOperatorPlmnIds` PLMN ordering defect is corrected only
when the exact Android source URL, managed member ID, parameter name, and full
importer-owned original parameter text all match; all other parameter
documentation is preserved.
Android.App.Admin repairs normalize the exact two ResetPasswordFlags
`resetPasswordWithToken` field descriptions from the source's scalar `byte`
notation to the registered `byte[]` signature before field rendering. The
restriction repair requires the exact managed member, source URL, rendered
parameter channel, and prior importer-owned output before restoring an unsafe
application-restriction sentinel to its placeholder. Exact member/URL/source
typo repairs apply only to proven importer-owned markup; authored and mixed
documentation remains unchanged, while a corrected importer-owned block stays
eligible for future refreshes.
For `DeviceAdminService.onBind`, the exact canonical source/member/plain-paragraph
allow-list removes only the contradicted nullable-binder sentence from new remarks.
The same write-time correction covers attribution-only enrichment, overlapping
remarks placeholders, and incomplete importer-owned refreshes. Strict recognition
of prior importer-owned XML still uses the unmodified official source; API
metadata, other documentation channels, and Binder-thread guidance are preserved.
`Android.Media.TV.Ads.TvAdService.OnBind` has a separate complete-contract guard:
the [pinned Android 16 implementation](https://android.googlesource.com/platform/frameworks/base/+/refs/tags/android-16.0.0_r1/media/java/android/media/tv/ad/TvAdService.java)
always returns its newly created binder,
so only the exact contradicted nullable-binder sentence is removed. Nullable
managed annotations and the independent Binder-thread paragraph remain unchanged.
`TvAdServiceInfo.WriteToParcel` separately corrects the exact `in to` source typo
in summary and remarks. Both require the complete original official declaration,
canonical URL and label, declaring Java owner, JNI descriptor, managed signature,
return type, and ordered parameter names/types; neither infers replacement prose
from implementation code. Corrections cover first fills, attribution-only
enrichment, overlapping placeholders, legacy Java-signature repair, and incomplete
importer-owned refreshes.
Prior-owned repair additionally requires complete matching original Docs and
changes only the exact text spans, preserving reference/attribution and raw
encoding/newlines. The two-channel parcel repair is atomic within its budget.
The unfiltered suite uses exact official declaration fixtures and actual registered
production paths, including persisted zero-write repeats, authored/provenance
negatives, and future corrected or removed source controls. There is no broad
nullable-binder or grammar normalization.
The two TV AD HTML fixtures are base64-encoded to retain the original source
bytes, including trailing whitespace, without introducing whitespace-check
exceptions; the suite decodes them before parsing the unchanged declarations.
Historical pre-TV-guard production APPLY outputs were not retained. Separate
JSON fixtures therefore identify **fresh**, timestamped production outputs of
the unchanged importer at immutable commit
`855613e7c309d2a1f829f70f5a32e2bcd6da1d4b`, not reconstructed history.
Both registered members were isolated from retained original XML without
changing their Docs, type/member/JNI metadata or DocIds, then imported offline
from the original official cache with bounded path, namespace and exact-member
scopes. The old producer applied two OnBind channels and four parcel channels;
both actual persisted repeats applied zero changes. The fixtures retain complete
raw input/output XML and prior Docs as base64, SHA-256, BOM/newline state, source
cache bindings, immutable producer blob/hash and original native execution
receipts. Regression repair seeds load those independently emitted complete
Docs, check agreement with the strict predicate, then verify exact repaired
complete blocks and every unchanged output byte. They do not use the predicate's
expected-Docs helper to manufacture legacy samples.
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
Two additional allow-list channels correct the same `in to` spelling in the
exact `Android.App.Blob.BlobHandle.WriteToParcel` summary and remarks. They use
the same full-text, member, source URL, and importer-ownership requirements;
other parcel implementations and authored documentation are unchanged.
Registered production-path tests cover bounded first-fill, strict prior-owned
repair, provenance and JNI mismatches, authored markup, future corrected source,
and persisted zero-write repeats.
The same exact-text allow-list corrects `Tile.writeToParcel`'s `in to` typo.
For QuickSettings, the exact inherited `TileService.onBind` intent parameter is
withheld because its claim that extras are invisible contradicts the framework
binder extras. The nullable-binder prose and return channel remain eligible:
the implementation can return null after a remote-service failure. Only the
exact `Tile.STATE_ACTIVE` paragraph's incorrect default-state sentence is
excluded, retaining its active-state description and all other source paragraphs.
New tiles actually initialize to `STATE_INACTIVE`. These guards require the
exact managed member, canonical Android source URL, and original channel or
paragraph; unrelated summary edits and additional safe paragraphs cannot disable
them. Corrected official source remains eligible, and existing authored content
is not withdrawn.
Exact EAP channel guards additionally require the complete official source
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
The registered `EapSessionConfig.EapAkaConfig.EapAkaOption` getter's exact
non-null return guarantee is withheld only from its published value channel.
The supported two-argument builder passes null options through the constructor
to the getter; pinned implementation excerpts verify this path without serving
as replacement prose or a Java runtime test. First-fill suppression depends on
the full defective return contract, registered getter descriptor, canonical
Android URL, and source kind, not unrelated summary or safe paragraph wording.
Withdrawal requires the complete original plain importer-owned Docs, reference,
and attribution and consumes one change. Future corrected source, including
removal of the false guarantee, remains eligible; no API metadata is changed.
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
Ten exact `Android.Ranging.Raw` channels are withheld: all three update-rate
enum summaries, the BLE RSSI setter parameter
description naming the different BLE CS class, and the summary/remarks of three
parcel writers containing `in to`. These exclusions require the exact managed
member, canonical Android URL, Android source kind, and complete original text.
Frequent and Normal advertise WiFi PD intervals contradicted by the pinned
Android 17 implementation and omit the NAN RTT periodic-ranging enable condition.
Infrequent has a missing WiFi RTT condition. Implementation is verification
evidence only, never replacement documentation.
The enum summary remains withheld while its exact original non-code paragraph
is present anywhere in the source body, independent of summary edits or added
safe paragraphs, because enum summaries publish that body. Parcel remarks are
likewise withheld while their exact bad non-code paragraph remains anywhere in
the body; only the exact bad parcel summary is also withheld. A safe corrected
parcel summary remains eligible even when the bad remarks paragraph persists.
Setter summary/returns and parcel flags remain eligible.
No replacement prose is invented, no generic grammar correction occurs, and
existing authored or imported channels remain unchanged, except for strict
withdrawal of the exact original importer-owned Frequent/Normal summaries.
Withdrawal requires the exact JNI field owner/name, enum type/value/signature,
complete original source paragraph, canonical reference label/URL, and entire
plain summary/reference/attribution structure; it restores the placeholder in
one operation. Authored or mixed content, CDATA, comments, processing
instructions, attributes, duplicate channels, and altered metadata or
provenance preserve the entire original Docs.
Logical exclusions apply before every supported Raw writer, including nested
paragraph placeholders, enum completion, augmented-placeholder cleanup,
metadata-only enrichment, Java-signature and copied-description repairs,
and importer-owned refreshes. Original source paragraphs remain available for
strict ownership checks but cannot be rendered into withheld channels.
Official source with
the bad paragraph corrected or removed becomes eligible without changing the exclusion.
Changed paragraph text, code-only copies, and different member/source provenance
are unaffected. The exclusions do not reorder source paragraphs.
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

`Android.Media.Quality` has source-bound first-fill exclusions for eight
`WriteToParcel(Parcel, int)` summary/remarks pairs containing the exact
`Flatten this object in to a Parcel.` typo, the two `GetAvailable*Profiles`
returns describing a single nullable profile despite a collection declaration,
and `ParameterCapability.ParameterType` value prose advertising combinations
of mutually exclusive type codes. Each exclusion requires the original full
official declaration (including return type), canonical URL/source label,
managed DocId/C# signature/return/ordered parameters, JNI registration, and
complete affected source channel. Safe direct placeholder channels remain
eligible. These guarded owners take a first-fill-only path: nested placeholders,
existing documentation, metadata-only enrichment, repairs and refreshes are
preserved, not normalized. Corrected official channels remain eligible.
The registered ordinary suite uses complete native contiguous source fragments
with byte lengths/SHA-256 and original managed-owner fixtures, exercises real
bounded offline production applies and zero-write repeats, and checks changed
provenance/declarations/signatures and authored/nested preservation. These are
fresh fixtures, not historical producer-output claims.

The OOB `OobInitiatorRangingConfigSecurityLevel.Secure` enum source is withheld
when its complete official description calls the provisioned-STS/security-level-four
mode "Basic security level". The exclusion requires its canonical Android URL
and source label, the exact managed enum field/type/value, and registered
`SECURITY_LEVEL_SECURE` JNI field. Its summary and logical remarks remain
unchanged and are reported as ambiguous; no replacement security contract is
inferred. The sibling Basic field, future corrected or removed defective source
prose, and all existing authored documentation remain unaffected.

The `ProtoOutputStream.makeToken(int, boolean, int, int, int)` remarks channel
is skipped only for its exact managed identity, registered JNI owner/signature,
managed return type, canonical Android URL, and complete original source paragraph
anywhere in the source remarks. Unrelated summary edits or added safe paragraphs
before or after that paragraph cannot re-enable its import.
That paragraph mixes capacities of 512 and 524,288 with the maximum encoded
values of 9-bit and 19-bit fields. Wrapped depth checks and negative object IDs
do not make those stated maxima representable. The safe summary remains eligible;
correcting or removing the unsafe paragraph restores remarks eligibility.
Other source text and existing authored documentation are preserved. No
implementation-derived replacement prose is imported.
The logical remarks exclusion also covers nested paragraph placeholders (including
those in summaries), metadata-only enrichment, placeholder cleanup, Java-signature
repair, and importer-owned refresh. These layouts retain their complete original
bytes; excluded operations are reported against `remarks`, not `summary`.

The `IkeProtocolErrorType.NoAdditionalSas` enum summary is skipped when its exact
managed field, canonical `IkeProtocolException.ERROR_TYPE_NO_ADDITIONAL_SAS`
Android URL, and complete source prose match `No additional SAa are acceptable`.
The undefined `SAa` term is not guessed or silently corrected. This exclusion
is checked in the production `JniField` mapping before its early return;
other constants, changed source prose, and authored documentation are preserved.

The projected `ConversationActivity.Anniversary` summary and augmented remarks
are withheld when its
complete official `ConversationStatus.ACTIVITY_ANNIVERSARY` sentence says
`and anniversary`. This first-fill-only guard requires the exact managed enum
field and return type, registered JNI field and `JniField` owner, canonical
Android URL, source kind, and complete original summary and paragraph.
No replacement wording is guessed and existing non-placeholder documentation,
including the legacy constant's prose, is preserved. Enum enrichment also
preserves the still-placeholder summary and its existing metadata rather than
wrapping it in a nested placeholder. Corrected official source remains eligible.
Registered production tests cover direct-text and empty-paragraph augmented
remarks with canonical reference and attribution, one-change safe sibling fills,
raw source and binding nonmatches, future corrected or removed source, authored
markup preservation, and byte-identical zero-write repeats.

The string overload of the Controls `RangeTemplate` constructor withholds the
exact official remarks containing strict endpoint inequalities rather than
guessing an inclusive-endpoint correction. This exclusion requires the exact
managed overload, registered JNI owner and descriptor, Android source kind,
canonical source-member URL, and the complete known-bad non-code paragraph.
Harmless summary changes, safe paragraphs before or after it, and independent
formatting guidance cannot bypass the exclusion while that paragraph persists.
Corrected or removed bad prose and code-only lookalikes do not trigger it.
Only the remarks channel is withheld; safe parameters and summaries remain
eligible, and existing authored or prior importer-owned documentation is preserved.
The whole-remarks exclusion also gates selection and execution of the earlier
signature-only and augmented-placeholder metadata repair writers. An empty
filtered paragraph list is not evidence of missing source for these writers:
deliberately withheld remarks retain their signature, reference and attribution
byte-for-byte, including on persisted one-operation repeats. Corrected official
source remains eligible for the existing independently validated repairs.

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
- The exact stale `ResponderConfig.Builder.set80211mcSupported(boolean)` summary
  and remarks are reported and skipped because they incorrectly rule out the
  separately configured IEEE 802.11az protocol when IEEE 802.11mc support is false.
  This guard requires the complete original prose, canonical source URL, source
  identity and managed member; valid parameter and return channels remain eligible.
- The exact `ResponderConfig.Builder.setChannelWidth(int)` summary, remarks and
  parameter text are likewise skipped when they describe encoded `ScanResult`
  channel-width constants as numeric MHz values. The full original parameter
  description must also match; the valid builder return channel remains eligible.
- Exact stale remarks for `ResponderConfig.Builder.setMacAddress(MacAddress)`
  exclude valid USD-only identification, while `PasnConfig.Builder.setWifiSsid`
  and `PasnConfig.getWifiSsid` overlook PMK-authenticated PASN without a password
  or SSID. Only these three remarks channels are withheld when the managed
  member, canonical URL, source identity, complete summary and ordered original
  paragraphs match. Safe summaries, parameters and returns remain eligible,
  as does changed official prose. A bounded repair replaces the complete stale
  importer-owned prose with a placeholder only when the entire remarks structure,
  reference and attribution match; the reference and attribution are retained.
  Authored or mixed XML, CDATA, comments, processing instructions, duplicate
  channels and mismatched provenance are reported and preserved.
