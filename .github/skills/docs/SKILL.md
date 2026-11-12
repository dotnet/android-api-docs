---
name: docs
description: Run Android API documentation campaigns with parallel sessions, deadlines, reviews, protected auto-merge, and archival.
---

# Docs

## 1. Start with the defaults

Read current repository instructions and `tools\importer.md`. Before assigning
or implementing an issue, read [implementation.md](references/implementation.md).

A request to start a documentation campaign uses these defaults; the user need
not name this skill. Do not ask them to repeat the workflow. Explicit changes
override defaults. Discussing example commands, editing the skill or asking for
status does not start work.

| Setting | Default |
| --- | --- |
| Issues | Find 12 open Android API namespace documentation issues |
| Workers | One isolated worker per issue; up to 12 concurrent |
| Reviews | `gemini-4-argon`, then Copilot, then `dalexsoto` |
| Human ping | Post exactly `@dalexsoto review` and formally request review |
| PRs | Open complete-issue PRs; set native protected squash auto-merge immediately after opening |
| Finish | Verify merge and issue closure, then archive each finished worker |
| Limits | Fixed batch unless a timed campaign is requested; no schedules or Agent merge |

**"Start a 12 session campaign until 6am tomorrow"** means keep up to 12 issue
workers active, selecting replacement issues after finished workers merge,
close their issues and are archived. Resolve "tomorrow" from the current date
in the user's timezone, or the session's local timezone if none was specified.
State the absolute cutoff and timezone once; no repeated setup questions.
At 6am, stop new assignments and active campaign monitoring, retain native
auto-merge on pending PRs, and report unfinished work. Do not cancel PRs or
archive unfinished workers. Already assigned workers may finish their own
issues through review and merge. This does not authorize schedules.

Request worker idle notifications. Keep the coordinator running until the
cutoff, checking live PR state at bounded intervals while workers await review.
This is monitoring within the requested run, not a scheduled automation.
If the host cannot keep the run active, report that limit; do not promise
continuous replacement or an exact-time cutoff report based only on notifications.

Select the oldest eligible issues first unless the user gives a priority.
Read their scope and confirm remaining placeholders. Skip issues already
covered by an active worker or open PR, unrelated issues and empty scopes.
If fewer than 12 qualify, use those available and report the shortage.

Record the selected issues, namespaces, settings and decision sources/times
in session storage. Label coordinator choices separately from user changes.
For a timed run, record the absolute deadline/timezone and unfinished-PR policy.
Do not add permissions beyond this workflow or the user's explicit changes.

"No automation, I'll oversee" prohibits schedules; it does not cancel an
existing request for native PR auto-merge. Keep that merge intent unless explicitly
revoked. Native auto-merge does not authorize schedules or Agent merge.

Track each issue in a second table:

| Issue / worker | Scope and evidence paths | PR / head | Reviews, checks, auto-merge | Dependency | Status / blocker |
| --- | --- | --- | --- | --- | --- |
| Current IDs | File/Docs/placeholder counts; reports | URL / SHA | Requests, comment IDs, reactions, results | PR and wake condition | See completion steps below |

Update both tables when state changes. On resume, check existing workers and
live PR state before starting replacements. For a status-only request, report
current state without starting work.

## 2. Assign one issue per worker

Use an isolated default-base worktree. Change the base only if requested or
needed for a dependency. Send a standalone kickoff with these fields filled:

```text
User request: "<relevant exact request>"
Assignment: <issue URL>, <exact namespace>, <namespace file>.
User requirements: <scope, permissions, reviews/model, merge method>.
Coordinator choices: <implementation decisions>.
Read: current repository instructions, tools\importer.md, and
.github\skills\docs\references\implementation.md.
Complete the full namespace and verify it as specified in that reference.
Review cycle: <reviewer order, exact ping, requirements after corrective pushes>.
Limits: <deadline, unfinished-work policy, dependencies, archival permission>.
Report: counts before/after, changed paths, unresolved channels, checks,
session-storage paths for cache/reports/sources, and PR/head/review state.
Do not expand the assignment, schedule or enable Agent merge without permission.
```

## 3. Verify, publish and review

Require the full-scope checks in the implementation reference before calling
an issue ready. A partial import or zero candidates is not completion.

Use the requested rubber-duck model. For `gemini-4-argon`, select it explicitly
and verify it was used; do not substitute another model. Fix findings and
repeat affected checks. Report an unavailable required model as a blocker.

If publication is authorized, open a full-issue PR after verification. Include
the correct `Fixes` reference, scope, sources and checks. A partial fix must
not claim to close the issue.

Read live PR/head state, reviews, unresolved threads, comments, reactions,
checks, conflicts and `autoMergeRequest`. Finish relevant pagination in small
pages; avoid large nested GraphQL queries.

Follow the recorded review order:

1. Request Copilot. Fix findings and reply/resolve in the original inline
   threads, not top-level comments.
2. Push corrections normally and request fresh reviews for the new head.
   Do not assume previous approvals still meet protection requirements.
3. After Copilot clears, formally request `dalexsoto` and post exactly
   `@dalexsoto review` once, unless the user specified a different reviewer.
4. After human-requested corrections, repeat the cycle, including one required
   ping after Copilot clears. Rerun Gemini first for substantive documentation
   or importer changes. Record head SHA, request and comment IDs.

Do not repeat a pending ping. If the user asks for an additional ping, post it
once per target PR and verify each comment's ID and text.

For acknowledgement questions, check reactions on the exact ping and other
relevant comments, including `EYES` and the reacting identity. A reaction is
not approval; no written review does not mean no acknowledgement. Use submitted
review evidence to report approval.

Set native protected squash auto-merge immediately after opening each campaign PR.

## 4. Handle conflicts and failures

Send new conflicts to the owner promptly. Sequence overlapping importer work;
merge current main normally and retain sibling rules, tests and docs. Do not
rebase without permission. Repeat the full isolated importer suite and full
namespace zero-placeholder/byte-preservation checks. Get the requested Gemini
integration review and fresh Copilot/human reviews; retain or re-enable
native auto-merge.

Record prerequisite PRs. Wake a waiting worker as soon as live state confirms
its prerequisite merged. Never message an archived session.

Use logs to separate publishing failures from documentation errors.
Use `az devops` for Azure DevOps builds and `binlogtool` for binlogs.
Try supported retries first. With permission and evidence of a transient
external failure, one tree-identical normal empty commit can be a last resort;
record why and request fresh reviews. Do not loop retries, hide failures with
publishing configuration changes, or claim a failed check passed.

## 5. Confirm completion, then archive

Track these states separately:

| State | Required evidence |
| --- | --- |
| Implementing / blocked | Remaining channels or specific blocker |
| Implementation ready | Full scope, zero placeholders, preservation and passing tests |
| Awaiting review / merge | Current head, reviews, checks and native auto-merge state |
| Merged | Live `MERGED` state, commit and time |
| Issue closed | Live linked issue closure |
| Archived | Permission, no work lost, archive-tool success |

Before archival, check local/remote state for uncommitted or unpushed work,
open PRs, needed artifacts, remaining duties, Agent merge and schedules.
Retain or transfer needed work and evidence. Confirm merge and issue closure,
archive with permission, and check tool success. Idle is not finished.
Use the creating/coordinating session for archival; if this session lacks that
ownership, report archival as blocked rather than trying another route.

Release the slot after cleanup. Start a replacement only if continuous backfill
is authorized, capacity and an eligible issue remain, and the deadline has not
expired. Do not expand a fixed batch. At the deadline, stop new assignments
and follow the recorded policy for existing PRs; do not invent cancellation,
indefinite polling or schedules.

On each wake or status request, check live state and fix authorized blockers.
Report counts by state and specific blockers, not routine wait notifications.
Do not use `ask_user` just to report waiting or call a coordinator's answer a
new user decision.

For handoff, give scope and counts before/after; XML/Docs, byte/API preservation
and test results; session-storage paths for cache, both reports and sources;
PR/head and remaining review, merge, closure or archival steps.

## Test the skill safely

Use [evals.json](evals/evals.json) with mocked/read-only inputs. No GitHub writes,
session changes, imports, merges, schedules or campaigns. Keep results in
session storage. These check decisions, not real protection or automatic skill
selection. Report only tests actually run.
