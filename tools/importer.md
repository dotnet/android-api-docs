# XML documentation importer

`importer.cs` is a conservative file-based C# app that fills exact `To be added`
placeholders inside `<Docs>` from declared members on official Android developer
reference pages and official Java 21 API pages.

Run it with the .NET 10 SDK or newer:

```powershell
dotnet run tools\importer.cs -- --self-test
dotnet run tools\importer.cs -- --self-test-preservation
dotnet run tools\importer.cs -- --self-test-telecom
dotnet run tools\importer.cs -- --self-test-provider
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

## Shared .NET binding members

Use `--managed-only` for repeated .NET binding members that have no Android or
Java reference declaration. This is a separate, explicit mode. It does not
download pages or change the normal Android/Java import rules.

```powershell
dotnet run tools\importer.cs -- --managed-only --self-test
dotnet run tools\importer.cs -- --managed-only --path docs\xml\Javax.Crypto.Spec --namespace Javax.Crypto.Spec --max-changes 10 --report artifacts\managed-preview
```

Review both reports before repeating a scoped command with `--apply`. Keep
each batch bounded. Parse changed XML and run `git diff --check` after applying.

The shared text describes the generated `JniPeerMembers`, `ThresholdClass`,
`ThresholdType`, explicit `IJavaPeerable.UnregisterFromRuntime`, and protected
`IntPtr`/`JniHandleOwnership` constructor members. Its sources are the official
[generated binding](https://github.com/dotnet/java-interop/blob/92b5d471f1e0f6164fc332b1f661b472f5cf3aad/tests/generator-Tests/expected.xaji/Constructors/Xamarin.Test.SomeObject.cs),
[interface contract](https://github.com/dotnet/java-interop/blob/92b5d471f1e0f6164fc332b1f661b472f5cf3aad/src/Java.Interop/Documentation/Java.Interop/IJavaPeerable.xml),
and [runtime implementation](https://github.com/dotnet/android/blob/3c25fed857508e40c364dfa1427c98fef4a0e5c0/src/Mono.Android/Java.Lang/Object.cs).
Reports record the pinned source for each replacement. The text is written for
these .NET members; it is not presented as Android or Java reference text.

Each rule checks the registered containing type, assembly, exact C# signature,
DocId, parameter and return types, and applicable hidden-member or interface
metadata. A JNI-registered member is not eligible. Only unique direct
plain-text placeholders are replaced. Existing prose, empty channels, nested
placeholders, markup, comments, CDATA, processing instructions, duplicate
channels, and channel attributes are preserved. Unsupported declarations and
channels are reported, not guessed. The managed self-tests exercise actual
dry runs, bounded applies, encoding and newline preservation, changed metadata,
protected content, and byte-identical repeat runs without network access.

## Android and Java reference import

### Repeated Parcelable and Java enum members

Use `--repeated-only` for `Creator`, `WriteToParcel`, `DescribeContents`,
Java enum `Values`/`ValueOf`, and inherited Parcelable constants. It fills
placeholders without changing existing documentation. It cannot be combined
with `--managed-only` and excludes builders, constructors, collection
`Values` properties, and text `ValueOf` factories.

```powershell
dotnet run tools\importer.cs -- --repeated-only --self-test
dotnet run tools\importer.cs -- --repeated-only --path docs\xml\Android.Graphics\Bitmap+Config.xml --member Values --max-changes 10 --cache C:\temp\android-doc-cache --report C:\temp\enum-preview
```

Review both reports before each scoped `--apply` run. Keep each apply at ten
changes or fewer. Use the same cache with `--offline` for repeat runs.

Rules check the assembly, type and member DocIds, JNI registration, managed
signature, return type, and ordered parameters. Missing or ambiguous source
declarations are reported, not filled.

`Creator` requires a public static final `Parcelable.Creator<T>` or
`ClassLoaderCreator<T>` source field for the registered Parcelable class
and the official creator contract. Managed nullable annotations do not
establish native null behavior.

Enums require the managed `Java.Lang.Enum` base, an official enum declaration,
and matching static method signatures. Missing method documentation can use
[JLS 8.9.3](https://docs.oracle.com/javase/specs/jls/se21/html/jls-8.html#jls-8.9.3).
The `ValueOf` name-matching and unknown/null-name rules also require the
Java 21 `Enum.valueOf` contract.

Parcelable methods must implement the matching interface member. Their class
documentation supplies flags, serialization details, and return bits. Only a
missing destination description can use `Parcelable.writeToParcel`'s contract;
its nullability sentence and flags description are not copied.

`InterfaceConsts` requires the generated field and registered Parcelable
sibling to match. The sibling's source must identify the inherited field
without a class-declared shadow; Parcelable must declare the constant.
The projection follows the pinned
[InterfaceConsts generator](https://github.com/dotnet/java-interop/blob/92b5d471f1e0f6164fc332b1f661b472f5cf3aad/tools/generator/SourceWriters/InterfaceConstsClass.cs).

Only unique direct plain-text placeholders change. Source links and attribution
are added only inside newly filled remarks. Offline self-tests cover source
checks, protected text, change limits, BOM/newlines, and zero-write repeat runs.

### Exact reference matching

The three `AppFunctionMetadata` access-level fields with malformed `ERROR(...)`
links have a narrow reference repair. It requires the complete original lead
paragraph, exact Java field declaration and canonical page, and the page's
independent `PROPERTY_ACCESS_LEVEL` and `PROPERTY_VALUE_ACCESS_LEVEL_SELF`
contracts. These establish the XML property link; the system value's literal
permission identifies the malformed permission reference. The recovered text
is used only for the exact registered managed fields and the matching projected
`AppFunctionMetadataAccessLevel.System` enum value. Changed declarations, text,
binding metadata, code-only copies, and missing corroboration do not enable the
repair. Existing authored documentation is preserved. Registered tests exercise
bounded offline applies and byte-identical persisted repeats.

Seven exact `SdkSandboxManager` source declarations contain the broken
`ERROR(/SurfacePackage)` link label. The importer can recover only that label,
including its occurrence in the request's receiver parameter, when the complete
native HTML fragment matches its pinned SHA-256 and the managed/JNI binding
matches. The Android 16 implementation imports
`android.view.SurfaceControlViewHost.SurfacePackage`; no behavior is inferred
from the broken site-root link. The fixtures in
`tools/importer-fixtures/sdksandbox-source-fragments.txt` retain the complete
native fragments as base64 with their hashes, from page SHA-256
`d4773b93a9924e9a23be63232207d9345d123cbaed3240c395f037843d5910a5`.
Changed declarations or provenance do not enable recovery, and future corrected
reference text follows ordinary parsing. Registered offline tests cover bounded
imports, parameter/prose separation, authored content, and persisted repeats.

The exact `EmbeddedBiometricPrompt.AuthenticationCallback.onAuthenticationError`
and `onAuthenticationHelp` declarations contain broken links to Android's
`BiometricConstants.Errors`, `BiometricFaceConstants.FaceAcquired`, and
`BiometricFingerprintConstants.FingerprintAcquired` annotations. The importer
retains those source names as plain text only for the two complete native
fragments in `tools/importer-fixtures/biometric-callback-source-fragments.txt`
and their exact managed/JNI bindings. The annotation declarations are verified
against the pinned Android 17 implementation; no equivalence to the complete
managed enum is inferred. Changed source or binding metadata does not enable
the recovery. Existing authored channels remain protected.

Twenty-four exact `SecurityLog` `TAG_*` field sections leave bare `<li>` items
open inside one paragraph, so the last item absorbs the rest of the section and
the field has no usable prose. The importer closes only that last item, at the
paragraph end or before a nested paragraph, when the complete native HTML
fragment matches its pinned SHA-256 and the `SecurityLogTags` enum field
binding, JNI field, and constant value match. Other `SecurityLog` sections,
`SecurityLog` constants, and other pages follow ordinary parsing.
`TAG_APP_PROCESS_START` (no sentence boundary before a nested sentence),
`TAG_REMOTE_LOCK` (trailing comma before the list separator), and
`TAG_OS_STARTUP` (nested list) stay withheld. The fixtures in
`tools/importer-fixtures/securitylog-source-fragments.txt` retain recovered,
withheld, and already parsed fragments as base64 with their hashes, from page
SHA-256 `4d645108a71f16aa4bd9a646b1d66d9ec072ef52a8f679ca9cff85e1c0c807b8`.
Offline tests create their own placeholders, check bounded applies, preserve
authored text and all bytes outside replaced summaries, and require a
byte-identical zero-write repeat.

Two exact `Android.Views` declarations have source-bound markup recovery.
`SurfaceControl.JankData.getDesiredPresentTimeNanos()` has a broken
`Transaction.setDesiredPresentTime` link label. The correction names
`Transaction.setDesiredPresentTimeNanos(long)`, verified by the
[pinned Android 17 implementation](https://android.googlesource.com/platform/frameworks/base/+/94b4c163b7dfe5ce3607f7bb8456f9573f7de57d/core/java/android/view/SurfaceControl.java#5835)
and the independently registered managed target.
`WindowManager.LayoutParams.screenBrightnessUnit` uses an opening `<ul>`
where its list should close. Only that final tag is corrected, retaining the
unit description, default, and ordered public unit choices.
The complete native fragments and their page/fragment SHA-256 values are
retained in `views-source-fragments.txt`. Recovery requires those exact bytes,
canonical source provenance, and the complete managed/JNI property identity,
including unique signatures, registrations, assemblies, and return metadata.
Changed fragments or bindings do not enable recovery; corrected future source
uses ordinary parsing. Recovered sources fill only direct plain placeholders,
with reference and attribution added only inside a newly filled remarks channel.
Authored or mixed channels, nested placeholders, and existing metadata are
preserved. The registered production tests cover bounded dry runs and applies,
attached binding and source negatives, LF/CRLF and UTF-8 BOM states, protected
bytes, and byte-identical persisted repeats. No missing value prose is inferred.

The importer uses the managed type registration, exact JNI names and descriptors,
and `JniField` owner metadata for projected constants. It also recognizes scalar
`JniTypeSignature` type metadata and `JniConstructorSignature` descriptors.
Narrow source-verified mappings cover Java.Interop runtime members whose upstream
implementation embeds an exact JNI member identifier and whose identity is also
confirmed by sibling generated binding metadata.
The exact `SslCertificate.DName` constructor maps its JNI-only enclosing instance
separately from the explicit Java `String` parameter. This exception requires the
verified managed declaration, ordered parameters, JNI registration, and canonical
Android constructor declaration; it does not apply to other inner constructors or
infer text for the enclosing instance.
Eight TextClassifiers string properties map to their registered `Formatted`
getters, following the pinned
[string-property generator](https://github.com/dotnet/java-interop/blob/87c5d3ea09de94c40424c1d3eb2bc2bd9a1449ff/tools/generator/SourceWriters/BoundPropertyStringVariant.cs).
Twelve Biometrics string properties map in the same way. Twelve unregistered
Biometrics `System.String` method overloads map to the registered
`Java.Lang.ICharSequence` sibling that the pinned
[string-overload generator](https://github.com/dotnet/java-interop/blob/87c5d3ea09de94c40424c1d3eb2bc2bd9a1449ff/tools/generator/SourceWriters/BoundMethodStringOverload.cs)
calls. At runtime, the overload must have exactly one sibling with the same
name, return type and parameter names, where only each `System.String`
parameter changes to `Java.Lang.ICharSequence`, and that sibling must carry the
listed JNI registration.
Five legacy `TextClassifier` fields use the official `TextClassifier` interface
page rather than the managed compatibility class's internal Java registration.
Focused self-tests compare every mapping with its canonical sibling registration
and reject unlisted member identities. Runtime checks require the exact owner,
assembly, managed signature, return type, and field or sibling getter registration;
negative tests exercise changed metadata. These mappings do not supply missing prose
or infer return documentation from a signature.
Array-rank `JniTypeSignature` metadata is not mapped to its element type because
Java API pages do not declare array wrapper types. It skips members with missing
registrations, unknown type descriptors, overload mismatches, ambiguous matches,
inherited-only detail, missing documentation channels, or source text that contains
only a Java type, nullability marker, cross-reference heading, or standalone
deprecation boilerplate. Android page license/trademark footers and update timestamps
are filtered, and literal Unicode escapes are decoded before XML escaping.

The exact `DevicePresenceEvent` constructor maps the Java parameter `event` to
the managed parameter `e`. The mapping requires the matching managed owner and
assembly, constructor and JNI signatures, ordered parameters, and canonical
Android source. Duplicate or conflicting managed metadata prevents the mapping.
It reads only the Parameters table; duplicate, malformed, reordered or
conflicting parameter rows are reported as ambiguous rather than selected.
Missing parameter rows are reported as missing. Other members do not use this
mapping.

Source prose is not imported for the exact `SaProposal.PSEUDORANDOM_FUNCTION_SHA2_512` field
when its complete Android description incorrectly names HMAC-SHA2-384. That channel
is reported as ambiguous instead of inventing replacement prose; existing authored
or imported documentation is preserved.

The `InferenceInput.Params.Builder` type summary can use the official paragraph
after its deprecation notice only when the managed type, registration, Android
declaration, source identity, and both source paragraphs match exactly. Withheld
or ambiguous source channels do not enable this selection. The XML type fixture
comes from the original placeholder snapshot; the HTML fixture retains the
relevant Android reference fragment from page SHA-256
`5118f5c33eee5d78e0ffa5265b9b14e525fcd254d6a7f0aab3941071da40e5e4`.
Offline production-path tests cover altered identities and source, authored
content, bounded writes, and byte-identical repeat runs.

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
For a structurally verified Android list with omitted closing tags, the importer
retains its nonempty list's immediately preceding colon-ended introduction as a
separate paragraph, followed by ordered list-item and trailing prose paragraphs.
Ordinary colon-ended fragments, empty lists, nested lists, and malformed list
markup retain the usual conservative filtering. A parser-layout refresh is allowed
only when the complete existing remarks exactly match a prior importer rendering
of the same source, including its source reference and attribution; authored or
mixed remarks are preserved. Prior-ownership comparisons normalize prose but
compare Java code text ordinally after XML newline normalization, so authored
code whitespace and comment boundaries cannot authorize a refresh.
The exact Android `BiometricPrompt.Builder.setAllowedAuthenticators(int)` remarks
refresh is withheld when the canonical source contains both the
`BIOMETRIC_WEAK` non-crypto and `BIOMETRIC_STRONG` crypto defaults but the parsed
list loses the former. Detection uses the table- and signature-free non-code
source paragraphs (including the retained legacy list representation), so
unrelated summary or safety-condition wording cannot bypass it. A mismatched
managed/JNI binding or canonical source identity is rejected before any writer.
The existing importer-owned remarks remain untouched and the report records
`source_channel_ambiguous`; a future source paragraph that preserves both
defaults remains eligible for an existing remarks placeholder.
The exact `BiometricManager.Strings.getPromptMessage()` and `getSettingName()`
return descriptions repeat the button-label text despite describing different
getter purposes. The [pinned framework declarations](https://android.googlesource.com/platform/frameworks/base/+/94b4c163b7dfe5ce3607f7bb8456f9573f7de57d/core/java/android/hardware/biometrics/BiometricManager.java)
and [service implementation](https://android.googlesource.com/platform/frameworks/base/+/94b4c163b7dfe5ce3607f7bb8456f9573f7de57d/services/core/java/com/android/server/biometrics/AuthService.java)
confirm distinct getter paths. The importer withholds only the exact copied
`value` text for the verified string aliases and registered `Formatted`
properties, reports `source_channel_ambiguous`, and does not infer a replacement.
If the copied value has mismatched binding or source provenance, the entire
mapping is withheld rather than writing any channels.
Other channels, the button-label getter, existing documentation, and future
corrected source remain eligible or preserved.
Repair of the known `Control.StatefulBuilder.setControlTemplate` paragraph boundary
is restricted to its exact Android member URL and verified lead-in/description,
preserving the source's blank line as two paragraphs without inventing punctuation.
The corresponding summary and remarks are regenerated together only when the
managed member ID, full old plain-text summary/remarks, exact source reference,
and Android attribution all match the prior importer output. Authored additions,
mixed content, source changes, and unsafe XML locations are reported and preserved.
The two-channel repair requires at least two remaining changes in the batch.
The exact no-listener `ImageDecoder.decodeDrawable(Source)` member has a separate
source-bound recovery for the official malformed `</p>.<br>` boundary that leaves
its worker-thread requirement outside the parsed paragraph stack. Recovery requires
the complete ordered create/default-settings/worker-thread contract, exact managed
DocId, static signature, `Drawable` return, `Source` parameter, JNI descriptor,
and canonical Android URL and label before any writer runs. Table, signature, and
code-only lookalikes are not recovered; authored or mixed existing remarks are
preserved, and a future correctly wrapped source paragraph remains ordinarily
eligible. A malformed-boundary source that retains only the create/default-settings
fragments is reported as ambiguous rather than falling back to an incomplete
remarks import.
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
The two final `CallRedirectionService` lifecycle overrides have separate,
channel-specific guards. For the exact registered `OnBind` declaration and
canonical Android source, only the complete known nullable-binder paragraph
loses `May return null if clients can not bind to the service.`; its remaining
sentences and Binder-thread guidance remain in order. For `OnUnbind`, only the
exact inherited caller-choice return is withheld as `source_channel_ambiguous`.
Its existing default-does-nothing/returns-false remarks are not rewritten.
The API 37.2 implementation at
`src/android/telecom/CallRedirectionService.java:330-338` always returns a new
binder from `onBind` and false from `onUnbind`. Verification uses the official
`source-37.2_r01.zip` archive, SHA-256
`9d637c3af6654a2f1b696b3d19c54bb77e38df2b0106116934703f314b3d4e84`,
not implementation-derived replacement prose.
First-fill guards require the exact managed/JNI declaration, canonical source
URL/label, native final declaration, and complete affected non-code paragraph
or return. Unrelated safe source context does not re-enable the defect.
Prior-copy repair additionally requires the entire original source contract
and complete plain importer-owned Docs, including untouched parameter/return
placeholders, summary, ordered remarks, reference and attribution. It removes
only the exact original paragraph's nullable sentence or restores the unsafe
return to `To be added.`, consuming one change and retaining every other byte.
Authored or mixed content, duplicate channels, comments, CDATA, processing
instructions, altered binding/source metadata and changed source are preserved;
no generic refresh can bypass a rejected prior-copy repair.
Corrected official source remains eligible for first-fill imports.
`telecom-redirection-fresh-legacy-output.json` records fresh timestamped
compatibility samples from the unchanged importer at commit
`dfab3d3dce6f3c28f14b9f13af5a413e20b00e17`, not historical APPLY outputs.
Its portable-PDB source checksum was verified before generation. Both the
untouched baseline members and independently placeholder-seeded first-fill
inputs ran through that real producer, using bounded offline preview/apply
commands and zero-write persisted repeats. Raw input/output bytes and hashes,
source-page/fragment hashes, producer hashes and execution receipts are retained;
the strict repair helper does not manufacture these samples.
The focused `--self-test-telecom` suite is also included in `--self-test`.
It verifies both prior-copy sample forms, bounded first fills and repairs,
authored/mixed and changed-binding/source preservation, LF/CRLF and UTF-8 BOM
combinations, corrected/contextual source controls, and byte-identical persisted
repeats through the production pipeline. The native HTML fixture is base64
encoded to preserve the original declaration bytes.
The seven final `SliceProvider` data-operation overrides also expose copied
`ContentProvider` contracts despite ignoring their operation arguments:
`delete`, `getType`, `insert`, the three `query` overloads, and `update`.
Their exact source channels are reported as `source_channel_ambiguous`.
Immutable SHA-256 fingerprints cover the registered JNI owner/name/descriptor,
managed signature and parameter/return bindings, canonical source URL and label,
and complete ordered parsed source contract, including code and parameter text.
The raw declared-source fixture makes these fingerprints reproducible.
Fingerprint serialization uses explicit source-generated JSON contracts so the
same ordered, escaped UTF-8 hash inputs work when reflection serialization is
disabled. The registered suite checks literal JSON bytes and all seven pinned
binding/source hashes without enabling reflection.
No implementation-derived replacement prose is imported. Corrected or removed
official guidance remains eligible. A prior copy is withdrawn one channel at
a time only when the entire Docs block matches the original plain importer
output or its already-withdrawn placeholders. Source reference and attribution
bytes are retained when remarks are withdrawn. Authored or mixed XML, comments,
CDATA, processing instructions, duplicate channels, altered metadata, changed
source, references or attribution preserve the existing documentation.
Every Java code body must also match ordinally after XML newline normalization
before any channel can be withdrawn; prose whitespace normalization cannot prove
code ownership. Registered max-one production negatives load both independent
Query samples, alter a line-comment boundary, and require zero changed files,
zero applied changes, and byte-identical persisted repeats.
The registered prior-copy tests load `slices-provider-legacy-output.json`, produced
by a fresh offline run of the unchanged importer from commit
`f4ce3c93a25be92426e1caa8f65294508c78bd4a` against isolated original binding
inputs. It retains the exact emitted XML bytes as base64, including attribution
whitespace, and records the producer, cache, output hashes and bounded commands.
These are fresh legacy-producer compatibility samples, not historical
production copies. Each complete emitted Docs block is independently compared
with the repair expectation before exercising the existing withdrawal and
preservation controls; the expectation helper does not seed the samples.
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

Four exact Android.Media declarations use the same guarded channel path.
`MediaPlayer.ProvideKeyResponse` withholds only the sentence that promises a null
result for streaming or release requests. The pinned
[MediaDrm JNI implementation](https://android.googlesource.com/platform/frameworks/base/+/94b4c163b7dfe5ce3607f7bb8456f9573f7de57d/media/jni/android_media_MediaDrm.cpp)
creates a Java byte array even for an empty successful key-set result. The
importer keeps the safe preceding sentences and does not infer a replacement
return contract. `MediaSession2.Builder.SetExtras` withholds its exact
null-accepting remarks; the
[pinned implementation](https://android.googlesource.com/platform/frameworks/base/+/99b01a65cc4c104933788b3143285ab6bae65827/media/java/android/media/MediaSession2.java)
rejects null. Safe summary, parameter and return channels remain independent.
Reports identify these withheld contracts as `source_channel_ambiguous`.
The two `MediaFormat.QpOffsetRect.FlattenToString` summaries correct only the
complete known `Returns a a string` text under exact source and binding identity
checks. They fill summary placeholders only; they do not refresh existing prose
or import the source's unresolved remarks.

These rules require attached managed owners, type/member assembly and DocId,
managed signature, ordered parameter types/names, JNI registration, native
declaration, canonical source URL/label/kind, and the specified source text.
All parsed summary, parameter, return and exception channels must match.
The exclusions may retain additional plain source paragraphs; typo correction
and prior-owned withdrawal require the complete original paragraph sequence.
Prior-owned withdrawal requires complete original source and complete prior
Docs ownership; it changes only the unsafe paragraph. Authored or mixed
siblings, references, attribution, comments, CDATA and processing instructions
are protected. SetExtras restores that one paragraph to a placeholder rather
than supplying inferred prose.
`tools/importer-fixtures/media-source-contracts.json` retains complete native
fragments, hashes, raw inputs/outputs and **fresh** independent legacy producer
receipts from unchanged importer commit
`91582c12b1439fec3b77a33e5829d078c204da41`; these are not historical outputs.
Offline production tests cover bounded first fills and prior-owned repairs,
BOM/newline combinations, exact outside-span bytes, source and attached-owner
negative controls, future corrected sources, and persisted zero-write repeats.
Whitespace or entity variations within legacy prose, reference or attribution
paragraphs do not establish ownership and cannot enable withdrawal.

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
For `SystemDataSyncFlags.CallMetadata`, it also removes the stray `}` after the
method reference in the exact Android `FLAG_CALL_METADATA` paragraph. The
managed field, canonical source URL, and complete original paragraph must all
match; changed or corrected source remains eligible without a general punctuation
repair. Only an exact prior importer-owned paragraph can be repaired.
The exact `BluetoothLeDeviceFilter.Builder.SetRenameFromName` source mistakenly
describes byte data and an advertised name. The
[pinned implementation](https://android.googlesource.com/platform/frameworks/base/+/94b4c163b7dfe5ce3607f7bb8456f9573f7de57d/core/java/android/companion/BluetoothLeDeviceFilter.java#143)
instead selects characters from the device's display name. Exact source-text
corrections cover its prefix, suffix, summary and remarks; the byte-based
overload, changed source text, and authored content are not rewritten.
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
The registered `ChangeLogTokenRequest.Builder.addDataOriginFilter(DataOrigin)`
parameter is withheld when its complete official contract describes a list of
package names for the single `DataOrigin` argument. The implementation adds that
one origin to a set; it is not used as replacement prose. This exclusion reuses
the exact registered-channel safeguards: managed identity/signature/types, JNI,
canonical source URL/label, and the complete original source must all match.
An exact prior importer-owned parameter can be withdrawn only with its complete
summary/return placeholders and metadata-only remarks, source reference, and
attribution intact. Authored or mixed content, including whitespace-only CDATA
at the Docs root, remarks container, or source-reference paragraph, and changed
source contracts are preserved. Corrected or removed source
prose remains eligible under the ordinary channel rules.

The parameterless `MediaRouteActionProvider.onCreateActionView()` return is
withheld when its complete official source contract promises a new non-null
view. The [pinned current platform implementation](https://android.googlesource.com/platform/frameworks/base/+/1cdfff555f4a21f71ccc978290e2e212e2f8b168/core/java/android/app/MediaRouteActionProvider.java#107)
always throws `UnsupportedOperationException`; it is evidence, not replacement
prose. The current API 37.2 implementation in the official
`source-37.2_r01.zip` archive has the same file SHA-256,
`09f116e12a5b2450538f36c531982b51ba25b87ae53937275a1ff21084699115`,
and the same throwing body at lines 107-111. The archive SHA-256 is
`9d637c3af6654a2f1b696b3d19c54bb77e38df2b0106116934703f314b3d4e84`.
The guard reuses the exact registered-channel rules and requires the
managed DocId, signature, return type, JNI descriptor, canonical source URL and
label, and full original source contract. The `MenuItem` overload is separate
and remains eligible. Authored content is preserved, and changed future source
remains eligible. The base64 native reference fixture has SHA-256
`6463d3a74b5b3e8e2695ece6dd1143a01d2e3ef52361adf3843572a7af80ea90`,
from page SHA-256
`02995ca27dcaa3906d9b31331306aac8b331f17e1945a0081a475294297ab792`.
Registered offline production tests cover the exclusion, changed binding and
source controls, authored content, a one-operation sibling fill, exact outside-span
byte preservation, and persisted zero-write repeats.

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

Eight exact Provider column-interface pairs have a separate declared-source
mapping: `MediaStore.Audio.IAudioColumns` / `AudioColumns`,
`MediaStore.Audio.IAlbumColumns` / `AlbumColumns`,
`MediaStore.Images.IImageColumns` / `ImageColumns`,
`MediaStore.Video.IVideoColumns` / `VideoColumns`,
`MediaStore.Audio.IArtistColumns` / `ArtistColumns`,
`MediaStore.Audio.IGenresColumns` / `GenresColumns`,
`MediaStore.DeletedFiles.IDeletedFilesColumns` / `DeletedFilesColumns`, and
`MediaStore.Files.IFileColumns` / `FileColumns`. The pinned
[interface generator](https://github.com/dotnet/java-interop/blob/87c5d3ea09de94c40424c1d3eb2bc2bd9a1449ff/tools/generator/SourceWriters/BoundInterface.cs)
omits registration on constant-only interfaces; the
[compatibility-class generator](https://github.com/dotnet/java-interop/blob/87c5d3ea09de94c40424c1d3eb2bc2bd9a1449ff/tools/generator/SourceWriters/InterfaceMemberAlternativeClass.cs)
places its obsolete projection in `mono/internal/`.
Both managed siblings must match their exact namespace, assembly, type and
member signatures, DocIds, string return types and JNI field identities.
An explicit field allow-list also requires the canonical declared Android
interface and the exact native String constant value. Values are verified
against the official API 37.2 implementation archive, SHA-256
`9d637c3af6654a2f1b696b3d19c54bb77e38df2b0106116934703f314b3d4e84`.
Inherited-only or removed fields, missing siblings, changed source declarations
or values, and unlisted projections are not mapped.
Playlists projections remain unsupported because their interface type metadata
does not match the unchanged guard.
Invalid metadata on an allow-listed projection cannot fall through
to ordinary JNI-field routing. Only direct placeholders
can change; authored or mixed channels, nested placeholders and existing
reference/attribution bytes are preserved. No prose is inferred from the
implementation. Native declaration fragments with page and fragment hashes
are retained in `provider-columns-source-fragments.txt`. Production tests
exercise all allowed fields in both siblings, attached-owner metadata and
source mutations, bounded writes, encoding/newline preservation, and persisted
zero-write repeats. The focused `--self-test-provider` suite also runs as part
of `--self-test`.

The exact `ArtistColumns.NUMBER_OF_TRACKS` summary and remarks describe an
album count. They are withheld for both allowed managed siblings when their
full known text, canonical declared source, JNI field and String value match.
The [pinned media database implementation](https://android.googlesource.com/platform/packages/providers/MediaProvider/+/a183e2b92c71d86ec7b104c116ef3f4dbcbb523f/src/com/android/providers/media/DatabaseHelper.java#1604)
counts distinct audio row IDs for tracks and distinct album IDs for albums.
Its file SHA-256 is
`30a8dbee47a14b170aa892ecd2fc6cecc084fa78ba666b26d1ac1af96df513dd`.
This is evidence for exclusion, not replacement prose. A safe new summary
does not enable the incorrect remarks paragraph. Corrected source and the
separate album-count field remain eligible. Authored content and prior copies
are preserved; there is no general prior-copy withdrawal.
`provider-artist-tracks-prior-docs.txt` retains actual bounded first-fill output,
its producer checksum and the output commit/blob identity. Registered tests
verify that copy's hash and preserve it in both attached managed siblings.

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
Source reference and attribution are attached only inside newly filled direct
remarks, atomically with that fill. Existing references in authored summaries
and excluded returns are not reconciled or removed. Mixed-authored regressions
exercise a one-operation remarks fill, accurate skipped-return reporting, and
literal protected-channel and outside-remarks byte preservation across LF/CRLF
and UTF-8 BOM states, including dry runs and persisted zero-write repeats.
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

The exact `ChooserSession.removeStateListener(StateListener)` detail is withheld
while its complete description names an `UpdateListener` callback. The guard
requires the canonical Android member URL and label, declaring type, full JNI
descriptor, managed signature and parameter types, and the original non-null
parameter contract. It rejects the entire ambiguous mapping before any first-fill,
enrichment, or repair can write, preserving existing authored and prior imported
Docs unchanged. No replacement listener prose is inferred. Corrected or removed
stale descriptions remain eligible, and unrelated Chooser members are unaffected.

The `FetchAndJoinCustomAudienceRequest.Builder.setFetchUri(Uri)` reference has
an extra `()}` after its correctly linked `getFetchUri()` label. Only the exact
official owner, member URL, hyperlink, label and complete paragraph permit
removal of that trailing token. Other malformed or changed references remain
withheld. This rendering fix does not infer missing parameter or return prose.

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

The exact `VibrationEffect.Builder.addEvents(long,List<Event>)` timeline
paragraph is withheld only when the registered JNI descriptor, managed member,
canonical Android source URL and label, full non-code remarks, and non-negative
shift parameter contract all match. Its claim that the non-negative shift can
move events backward is not rewritten. The safe lead, example, overlap
validation, and event-retrieval paragraphs remain eligible. An exact prior
importer-owned remarks block can lose only that paragraph when its complete
source-reference and attribution structure also matches; authored or mixed
documentation and changed future source are preserved.

Existing non-placeholder remarks are retained unless their full structure proves
they were generated by this importer: non-empty plain `<para>` or Java `<code>`
elements must be an ordered subset beginning with the exact first visible source
element, followed only by the exact mapped source-reference paragraph and Android
attribution (when applicable). Any extra nodes, markup, source mismatch, or
out-of-order content preserves the remarks verbatim. Eligible remarks are rebuilt
from the complete ordered visible source paragraphs and code blocks, rather than
having text appended. Nested Android code-container markup for one physical
sample is coalesced, while separate repeated visible blocks remain in order.

For `FormattedNumber.appendTo(A)` and `FormattedNumberRange.appendTo(A)`, the
Android anchor uses a type variable rather than its erased JNI type. A narrow
mapping requires the exact managed signature, JNI `Appendable` descriptor,
`A extends java.lang.Appendable` binding metadata, canonical source identity,
and `public A appendTo (A appendable)` source declaration. No general type-variable
guessing is added.

Three ICU Number channels are withheld for exact known source defects:
`getIdentityResult()` names the nonexistent `RangeIdentityType` in its value
description and mistakes endpoint equality for proof that a display fallback
was used; `UnitWidth.ISO_CODE` calls the alphabetic currency code three-digit;
and `RangeIdentityFallback.APPROXIMATELY` contradicts its approximation pattern
with an always-use-range instruction. Safe sibling channels remain eligible.
These guards preserve authored text and accept future corrected source; they
do not derive replacement prose from implementation code.
The registered self-tests use hashed native declaration fragments in
`tools/importer-fixtures/icu-number-source-fragments.txt`. They cover mapping
and provenance nonmatches, corrected source, contextual contradictions,
authored content, dry runs, bounded offline applies, byte preservation and
zero-write repeats.

Official pages are cached by URL hash. Network requests use a clear user agent,
bounded concurrency, a size limit, and deterministic retry/backoff. `--offline`
only reads the cache. `--report path` writes a deterministic JSON report and an
adjacent text report.

On apply, each changed file is reparsed before and after an atomic write while
retaining its original newline convention and UTF-8 BOM state. Report entries
remain `would_apply` until the corresponding atomic write succeeds; partial
failures retain the completed-file count and source counters. Run `git diff
--check` after a batch.

Remarks refreshes copy the original source-reference and attribution element
bytes, including nonbreaking spaces. Augmented remarks fills replace only the
placeholder span; they do not serialize retained metadata or change its newlines.
Empty paragraphs with attributes, comments, CDATA or processing instructions
are not replacement placeholders.

Exact source exclusions withhold the unfinished `AudioMetadata.Format.KEY_BIT_RATE`
summary and remarks, the malformed `BlendMode.COLOR_BURN` and `COLOR_DODGE`
equation remarks, and the `UnicodeBlock.PRIVATE_USE` / `PRIVATE_USE_AREA` prose
that calls object fields `int`. The exact `SoftApConfiguration.Builder.setBssid`
remarks are withheld because they refer to unlisted permissions after unresolved
source links. Safe separate channels remain eligible. No replacement prose or
equations are inferred, and existing documentation is not withdrawn.
Changed or removed defective source text does not enable these exclusions.

The exact `Dalvik.Bytecode.IOpcodes` constant-only interface and its obsolete
`Opcodes` compatibility class can use the native `dalvik.bytecode.Opcodes`
reference for their type descriptions, nineteen deprecated implementation
detail fields and thirty-nine never-implemented jumbo fields. The pinned
[interface generator](https://github.com/dotnet/java-interop/blob/87c5d3ea09de94c40424c1d3eb2bc2bd9a1449ff/tools/generator/SourceWriters/BoundInterface.cs)
omits registration on constant-only interfaces; its
[compatibility-class generator](https://github.com/dotnet/java-interop/blob/87c5d3ea09de94c40424c1d3eb2bc2bd9a1449ff/tools/generator/SourceWriters/InterfaceMemberAlternativeClass.cs)
uses the `mono/internal/` registration. Both sibling files must match the exact
managed owner, assembly and type signatures. Each listed field must match both
siblings' JNI names, managed signatures, DocIds, integer return types and pinned
[native values](https://android.googlesource.com/platform/libcore/+/fff4fcc0cf7f080cf2511cbb57561482b13b218f/dalvik/src/main/java/dalvik/bytecode/Opcodes.java).
The official page must declare the exact interface and field with that value.
The jumbo mappings do not include the implemented `OP_CONST_STRING_JUMBO` or
confuse `OP_CONST_CLASS_JUMBO` with the distinct `OP_CONST_METHOD_TYPE` field
that has the same numeric value. Other constants, missing prose and empty
channels are not filled. Native hashed
fixtures and offline production tests cover both projections, changed bindings
and sources, authored documentation, bounded writes, byte preservation and
persisted zero-write repeats.

Further reviewed exclusions cover exact ICU return and date-format errors, the
lost `java.text.MessageFormat` link qualification, incomplete Net and Provider
result lists, conflicting PeopleContract restrictions, the PeopleContract list
marker, and the WifiNetworkSuggestion priority range. Graphics exclusions hold
the proven blend and bitmap packing errors, omitted linear-transfer details,
equation-dependent TransferParameters prose, and the contradictory adaptation
ID sentence. The two BT.2020/BT.709 summaries that stop at `Rec.` are withheld
without changing the general summary splitter. Safe sibling channels remain
eligible, and authored documentation is not repaired or removed.
The four Java 21 `Spliterators.Abstract*Spliterator.estimateSize()` summaries
that stop inside `Consumer<?` are also withheld. Their valid remarks and return
channels remain eligible; no general generic-type or sentence-parser change
is made.
The exact Android `FilePermission` and `SerializablePermission` projections
have a separate, source-pinned legacy-security path. Java 21 permission checks,
action lists and retained permission names contradict the API 37.2 Android
stubs. Complete binding metadata, canonical declarations and full parsed source
fingerprints are required. Contradicted channels are reported as ambiguous,
without replacement prose or generic enrichment; safe constructor parameter
labels remain eligible. Changed source or binding identity is preserved and
reported rather than guessed.
Prior-copy withdrawal requires the complete exact original Docs snapshot,
independently reproduced by the immutable importer at
`8f08cbfd813a176044a947587916c153865f61d2`. The `Implies` exception additionally
requires the exact mixed-owner snapshot from review commit
`3c24283038340cf92288d8fcdee2f4ea390790ee`: only its independently unchanged
parameter and return spans are withdrawn, never its authored paragraph.
Other repairs remove only contradicted producer-owned spans and retain every
source-reference and attribution byte. Each member's repair is atomic within
the change budget. `java-io-legacy-security.json` retains original binding bytes,
independent producer inputs/outputs, the reviewed mixed snapshot, native source
fragments and SDK archive provenance. Registered offline tests cover first fills,
historical ownership, altered content/source/binding, bounded writes, BOM/newline
preservation and persisted zero-write repeats.
The exact `HardwareBufferRenderer.RenderRequest.setColorSpace` remarks are
withheld when their non-null requirement conflicts with the parameter's
documented null-to-SRGB behavior. The summary and parameter remain eligible.
The exact `WifiMode.FullLowLatency` enum summary is withheld when it includes
the stale dual-lock background fallback to `WIFI_MODE_FULL_HIGH_PERF`.
Independent fields remain eligible; no replacement behavior is inferred.
The Java 21 `TreeSet.subSet(E, boolean, E, boolean)` and
`tailSet(E, boolean)` return text is withheld when it promises fixed endpoint
inclusion despite the exact inclusion-flag contracts. Valid summaries,
parameters and unflagged overloads remain eligible.
The exact `MessageFormat.Field.ARGUMENT` remarks that promise only an Integer
array index are withheld because Map arguments use String names. Its safe
summary remains eligible. The seven `RelativeDateTimeUnit` weekday summary
and remarks channels are withheld only for the complete numeric-offset examples
rejected by the shipped formatter. Retained qualitative text is not replaced,
and numeric day units remain eligible.

Direct remarks placeholders use the same XML decoding and whitespace
normalization during selection and replacement. Repeated spaces, inter-word
newlines and numeric character references are accepted. Surrounding whitespace
and adjacent metadata retain their original bytes.

The focused preservation tests also run in `--self-test`. Their base64 source
fragments retain exact native declaration bytes, with full-page and fragment
SHA-256 values in `tools/importer-fixtures/preservation-source-fragments.txt`
and `tools/importer-fixtures/reviewed-source-fragments.txt`
(`name|page hash|fragment hash|base64`). The reviewed-source tests check exact
registered owners, channel boundaries, changed sources and valid siblings.
The preservation tests use isolated copies of the
registered XML inputs, cover LF/CRLF and UTF-8 BOM states, check protected bytes,
and require zero-write persisted repeats.

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
- The two `MultisensoryManager` drag-indicator token remarks retain their exact
  official continuous-effect sentence. Only their exact Android source URLs and
  generated `ERROR(MultisensoryContinuousEffectModifier/...)` link wrapper are
  normalized to the linked type name; unrelated malformed source markup remains
  a conservative skip.
- `Device.Builder.setUdi(String)` retains its complete official permission and
  upsert-exception paragraph only when the canonical member URL contains the
  unchanged `ERROR(/HealthPermissions#WRITE_DEVICE_UDI)` wrapper and complete
  source text. That wrapper is normalized to the official
  `android.permission.health.WRITE_DEVICE_UDI` value verified by
  `HealthPermissions.WRITE_DEVICE_UDI`. Rendering additionally requires the
  exact managed `SetUdi(String)` member, JNI descriptor, Android source label,
  and URL. Recovery runs only in method prose after parameter, return, and
  exception tables are separated; matching malformed table text is retained in
  its original channel and receives the ordinary conservative `ERROR(...)` skip.
  The recovery event is retained as member-scoped provenance through later prose
  formatting, including flat-list coalescing. A recovered contract that fails
  any managed/source identity check rejects the entire mapping so no derived
  summary or remarks can be written. Other managed
  identities, URLs, malformed wrappers, or changed paragraphs retain the
  ordinary conservative skip. Existing
  authored or mixed remarks remain protected by the ordinary importer-ownership
  checks.
- `ReadMedicalResourcesInitialRequest.Builder.setPageSize(int)` and
  `ReadMedicalResourcesPageRequest.Builder.setPageSize(int)` render the
  `pageSize` range as `ERROR(/android.health.connect.Constants#MINIMUM_PAGE_SIZE)`
  and `ERROR(/android.health.connect.Constants#MAXIMUM_PAGE_SIZE)`. These two
  tokens are replaced with `1` and `5000` only in the `pageSize` parameter cell,
  and only when the canonical member URL and title match, the member has one
  `Parameters` table with the exact parameter row, and one `Throws` table with
  the exact `IllegalArgumentException` row ("less than 1 or more than 5000").
  Each table must have exactly one data row. Rendering also requires the exact
  managed type, `Mono.Android` type and member assemblies, DocId, C# signature,
  return type, `pageSize` parameter, and `(I)` JNI descriptor; otherwise the
  mapping is skipped as `source_channel_ambiguous`. The global `ERROR(...)` skip
  is unchanged. The `ReadMedicalResourcesRequest.getPageSize()` return is not
  repaired because its page does not state the bounds.
- `CompanionDeviceManager.TrustPairingCallback` type remarks are withheld only
  when the exact official class link has an `int[]` request signature but visible
  scalar-`int` prose, and the managed type registration and signature also match.
  An exact prior importer-owned remarks block can be withdrawn to its placeholder;
  authored or changed documentation and corrected source remain eligible.
- `AuthenticationPolicyManager.createCrossDeviceAuthenticationRequest` omits only
  its exact stale trust-pairing paragraph when the official paragraph links an
  `int[]` request signature but visibly names a scalar `int`. The guard requires
  the exact managed owner, JNI descriptor, full source paragraph sequence,
  canonical URL and label. A strict prior importer-owned repair removes only that
  paragraph, retaining the valid source paragraphs, reference, attribution, and
  all other documentation channels.

The exact `MloLink.getChannel()` value channel is withheld when its complete
official range says `Value is 1 or greater`. The exported constructor initializes
the channel to zero and the getter returns that stored value unchanged
(`src/android/net/wifi/MloLink.java:129-141,185-188` in the official API 37.2
source archive, SHA-256
`9d637c3af6654a2f1b696b3d19c54bb77e38df2b0106116934703f314b3d4e84`).
The exclusion requires the exact managed type, assembly, property signature,
integer value type, JNI getter, native declaration, canonical Android source URL and label, and
complete defective value text. It reports `source_channel_ambiguous` without
inventing a replacement range. Safe sibling channels and future corrected
source remain eligible; existing documentation is not withdrawn or rewritten.
The native fragment in `mlo-channel-reference.html.b64` has SHA-256
`705820e68adc7e117272085c844b40952fe3a9299a2b08e6cd6f1889a987d4ad`,
from page SHA-256
`93a3d0408e5ed690d8634d9015d46b4fd3a4dc8fb57c0983a9a1acc485b2689b`.
Registered offline production tests cover the exclusion, attached-owner
metadata negatives, changed source and provenance, authored-content preservation,
bounded sibling writes, and byte-identical persisted repeats.

The exact `WindowInsetsAnimationController.getCurrentFraction()` value channel
is withheld when its complete official return equates progress 0/1 with
hidden/shown states. In the pinned API 37.2 implementation,
`InsetsAnimationControlImpl.finish(false)` supplies hidden insets with fraction 1,
and the applied fraction is stored independently of the insets.
The exclusion requires the exact managed interface/property, assembly,
signatures, JNI getter, canonical source URL/label, native declaration, and full
original return text. It affects only that value channel. Authored and prior
documentation is retained; safe sibling channels and corrected future source
remain eligible. No replacement progress contract is inferred.
The native fragment in `current-fraction-reference.html.b64` has SHA-256
`b33fd36224b7dc8b9a35acb670fcc9d85dd7714121bdae4de85b3081c7001567`,
from page SHA-256
`fd3b6aa53eccbbf5365f21ed6c56a91d22be741c6b46d700112fa36ad1b27205`.
Registered production tests cover the exclusion, meaningful attached binding
and source negatives, source-context changes, authored/mixed preservation,
bounded sibling writes, encoding/newline preservation, and persisted repeats.

The exact `ViewParent.getTextDirection()` value channel is withheld when its
complete official list omits the first-strong LTR and RTL modes. The pinned
API 37.2 `View.getTextDirection()` reads the resolved-direction bits, and
`resolveTextDirection()` explicitly supports both modes; `ViewGroup` inherits
that implementation while implementing `ViewParent`.
The guard requires the exact managed interface/property, assembly, signatures,
JNI getter, canonical source identity, native declaration, and full original
value text. It preserves authored and prior content and all other channels,
and does not infer a replacement enumeration. Corrected future source remains
eligible. The fragment in `view-parent-text-direction-reference.html.b64`
has SHA-256 `61c86689b01ac5f51b828a3d2280c0fc15a30f850ecfb0c43336bf2a04a60ee2`,
from page SHA-256
`ed32ac121eb51abb44b21bf89acaf1bca435dd7b76fb96010cac969d3f6dfa17`.
Registered bounded production tests cover exact exclusion, attached metadata
and source negatives, changed JNI, authored/mixed preservation, safe sibling
fills, corrected or removed source, and zero-write persisted repeats.
