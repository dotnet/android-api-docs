# Android API documentation updates

Use `tools/importer.cs` when replacing `To be added` placeholders with
documentation from the official Android developer reference or official Java
API documentation. Do not invent documentation or perform repository-wide
manual replacements.

Read `tools/importer.md` before running the importer. From the repository root:

```powershell
dotnet run tools\importer.cs -- --self-test
dotnet run tools\importer.cs -- --path docs\xml\<Namespace> --namespace <Namespace> --max-changes 10 --report artifacts\import-report
```

Dry-run is the default. Review both generated reports before adding `--apply`,
and keep every apply operation scoped with `--path`, `--namespace`, or
`--member` plus a conservative `--max-changes`. Use `--cache` and `--offline`
for reproducible follow-up runs.

The importer must preserve existing non-placeholder documentation, apply only
exact structural matches, and report ambiguous or missing source documentation
instead of guessing. Never update generated `docs\xml\index.xml`. After an
apply run, parse every changed XML file and run `git diff --check`.
