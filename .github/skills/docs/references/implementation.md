# Complete a namespace

Read current repository instructions and all of `tools\importer.md` before
work, and again after integrating importer changes.

## Count the full scope

Include every API XML file directly in `docs\xml\<Namespace>` plus
`docs\xml\ns-<Namespace>.xml`, including enums, nested types and
`InterfaceConsts`. Exclude similarly prefixed namespaces. Require a nonempty
directory, the namespace file, valid XML and required Docs blocks/channels.

Count all Docs descendants, including nested channels. A placeholder has no
child elements and its complete decoded, whitespace-normalized text is exactly
`To be added` or `To be added.`. Do not count `objects to be added` or count a
parent twice. List each match's file, owner and channel; list missing or empty
required content separately.

Record current file, Docs and placeholder counts; issue counts may be stale.
Save original bytes and note pre-existing changes for later comparison.

## Import reference prose

Use `tools\importer.cs` for official Android/Java prose, not manual replacements.
Require exact structural matches. Preserve existing non-placeholder text,
API metadata, references, attribution, BOM, encoding and newlines.

Run existing self-tests. Concurrent .NET file-app builds can share a cache:
use process-private TEMP/TMP and a unique session output directory. Check
`dotnet run --help` for the supported isolation flags below. Verify the running
binary belongs to this worktree and current source; check source hashes/PDBs
if uncertain. Do not apply with an unverified binary or kill other workers'
processes.

After setting private TEMP/TMP and creating session output directories:

```powershell
dotnet run --no-cache --disable-build-servers --artifacts-path $buildOutput tools\importer.cs -- --self-test
dotnet run --no-cache --disable-build-servers --artifacts-path $buildOutput tools\importer.cs -- --path "docs\xml\$namespace" --namespace $namespace --max-changes 10 --cache $cache --report $report
```

Keep `$buildOutput`, `$cache`, reports and source evidence in session storage,
not commits. Dry-run is the default. For every batch, review both JSON and text
reports before adding `--apply`. Retain the reports without overwriting them.
Keep preview/apply inputs and cache unchanged; use `--offline` for follow-up
runs.

Scope every apply with `--path` or `--namespace` and normally
`--max-changes 10` or fewer. `--member` alone is insufficient. Run the namespace
file separately if needed. Use documented `--managed-only` or `--repeated-only`
modes for their supported cases; do not broaden their matching rules.

Never edit `docs\xml\index.xml`, `docs\xml\_filter.xml` or generated
`docs\xml\FrameworksIndex`. After each apply, parse every changed XML and run
`git diff --check`.

## Resolve remaining channels

Recount the full scope after imports. Zero candidates does not mean zero
placeholders. Inspect every remaining channel and report missing, ambiguous or
contradictory source contracts.

If official prose is missing or a managed-only channel is unsupported, original
wording needs pinned official implementation, specification or binding-generator
evidence for each claim. Record the member/channel, immutable source revision
and location, supported contract, proposed wording and why import cannot fill
it. This does not permit bulk manual replacement or an AOSP prose fallback in
the importer.

Do not infer behavior from names/signatures, use filler, copy contradicted
sources, remove channels or weaken safeguards to reach zero. Without sufficient
evidence, leave the channel unresolved and report the issue as blocked.

For a proven importer defect, make a narrow repair with positive and meaningful
negative tests through the production path. Negative mutations must reach the
intended guard, not fail earlier: cloned XML members must stay attached to their
owner. Cover changed identity/source, authored content, bounded writes and
persisted zero-write repeats as applicable. Run the full isolated `--self-test`
suite and relevant mode tests. Use existing test tools.

## Check before review

| Check | Required result |
| --- | --- |
| Scope and structure | All scoped files parse; required Docs/channels exist |
| Content | Zero actual placeholders across the full scope |
| Bytes | Unchanged outside approved replacement spans, including BOM/encoding/newlines |
| API | Declarations, signatures, attributes, IDs and metadata unchanged |
| Documentation | Authored text and retained references/attribution preserved |
| Tests | `git diff --check` and suitable existing tests pass |
| Evidence | Counts before/after, reports, pinned sources, commands and binary/source checks retained |

Compare bytes, not just parsed XML. Approve reference/attribution changes by
span, not by exempting whole files. Repeat full-scope counts and preservation
checks after integration or review changes.
