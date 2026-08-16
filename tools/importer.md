# XML documentation importer

`importer.cs` is a conservative file-based C# app that fills exact `To be added`
placeholders inside `<Docs>` from declared members on official Android developer
reference pages and official Java 21 API pages.

Run it with the .NET 10 SDK or newer:

```powershell
cd tools
dotnet run importer.cs -- --self-test
dotnet run importer.cs -- --path ..\docs\xml\Android.Animation\ArgbEvaluator.xml --member Evaluate --report ..\artifacts\argb-import
dotnet run importer.cs -- --path ..\docs\xml\Android.Animation --namespace Android.Animation --max-changes 10 --cache C:\temp\android-doc-cache
dotnet run importer.cs -- --path ..\docs\xml\Android.Animation\ArgbEvaluator.xml --member Evaluate --apply --max-changes 4
dotnet run importer.cs -- --path ..\docs\xml\Android.Animation --namespace Android.Animation --offline --cache C:\temp\android-doc-cache
```

Dry-run is the default. An unscoped scan is rejected, and `--apply` requires a
path or namespace write scope. `docs/xml/index.xml` is always excluded. The
default limit is 25 placeholder elements.

The importer uses the managed type registration, exact JNI names and descriptors,
and `JniField` owner metadata for projected constants. It skips members with
missing registrations, unknown type descriptors, overload mismatches, ambiguous
matches, inherited-only detail, missing documentation channels, or source text
that contains only a Java type, nullability marker, cross-reference heading, or
deprecation boilerplate. Android page license/trademark footers and update
timestamps are filtered, and literal Unicode escapes are decoded before XML
escaping. Imported remarks use `<para>` elements, with source links placed before
existing attribution. The importer never creates generic prose or falls back to
AOSP. Existing non-placeholder documentation is retained.

Official pages are cached by URL hash. Network requests use a clear user agent,
bounded concurrency, a size limit, and deterministic retry/backoff. `--offline`
only reads the cache. `--report path` writes a deterministic JSON report and an
adjacent text report.

On apply, each changed file is reparsed before and after an atomic write while
retaining its original newline convention and UTF-8 BOM state. Run
`git diff --check` after a batch.

Limitations:

- Java module routing is intentionally limited to `java.base`, `java.sql`,
  `java.xml`, and `java.net.http`.
- Documentation is imported as XML-escaped plain text; source HTML formatting
  is not reproduced.
- Only existing placeholders are replaced. Exception text is filled only when
  an existing managed `cref` has one unambiguous source exception match.
- Source-page layout changes cause conservative skips rather than guessed text.
