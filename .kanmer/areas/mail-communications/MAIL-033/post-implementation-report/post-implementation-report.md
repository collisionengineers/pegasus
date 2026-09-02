# Post-implementation report — MAIL-033

*The author's claim, written before merge. Proof is gathered after the merge by `kanmer-verify`.*

## Summary

**Nothing was authored for this ticket.** MAIL-033 is the adoption of PR #641, an
already-implemented, already-green fix that identified itself as MAIL-029 — a live backlog ticket
that owns missing Inbox attachment columns and keeps that meaning. The implementer validated the
recorded worktree and branch, confirmed the branch was 0 behind `origin/dev`, re-derived every
behavioural claim in the plan read-only against the code and against FRD-08, built for compiler
feedback, ran the four simplification lenses over the real diff, corrected the PR's identity, and
recorded traceability. The repository diff produced by this ticket is **zero lines**:
`git status --porcelain` is empty and no commit was made. What shipped is the pre-existing fix,
now correctly attributed.

The adopted change: `GraphApprovedInboxSource.ReadAsync` threw `InvalidDataException` whenever a
Microsoft Graph mail-delta entry omitted `receivedDateTime`, and it threw **before** the page
cursor was persisted, so every retry re-hit the same entry and that mailbox's poll cursor stalled
permanently (24 identical production exceptions on 2026-09-01 between 08:40 and 08:56Z, and the
direct cause of that mailbox's "Failed" service-health row). Graph guarantees only "at least the
updated properties" on a sparse delta entry, so a recurring known message legitimately arrives
without `receivedDateTime`. The fix skips such an entry — beside the existing `Removed` skip, in
the same loop — while still throwing when the property is present but unparseable.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | modified (+19 / −3, **by PR #641, not by this ticket**) | `ParseItem` records raw presence via `value.TryGetProperty("receivedDateTime", out _)` into the new `GraphDeltaItem.ReceivedDateTimePresent`; `ReadAsync` gains an `if (item.ReceivedAtUtc is null)` branch (line 637) that throws only when the property was present and otherwise `continue`s; the former inline `?? throw` becomes `item.ReceivedAtUtc.Value`. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | modified (+53 / −0, **by PR #641, not by this ticket**) | `InboxSkipsASparseDeltaItemMissingReceivedDateTimeWithoutFetchingMime` asserts the entry is excluded, that no request path ends `/$value`, and that the returned cursor parses to the delta path; `InboxThrowsOnAPresentButUnparseableReceivedDateTimeRatherThanSkipping` pins the negative case so the skip cannot swallow a corrupt value. |
| — | **no file changed by MAIL-033** | Adoption only. The plan set the expected diff at 0 lines and defined a code change as a reportable deviation; none was needed. |

Non-repository changes this ticket did make: PR #641's title now ends `(MAIL-033)`; its body's
single trailing footer line reads `Kanmer: MAIL-033` (verified: exactly one body line differs from
the pre-edit original, plus one trailing newline GitHub stores); the board carries both commit
SHAs and `prs: ["641"]`; the plan carries a dated `## Simplification pass (2026-09-02)`;
`open-questions` gained finding F3 and ASSUMPTION 1; the ticket's four Verification boxes are
ticked against the evidence below.

**The two commit trailers still read `Kanmer: MAIL-029`, deliberately.** Correcting them would
mean rewriting `712bfcf3` and `c6842a8c`, which the repository rules forbid (no rebase, no amend,
no force push) and which would discard a fully green check run. The board `prs` / `commits` fields
plus the PR title and footer carry the correction. This is a known, accepted mismatch, not an
omission. For the same reason the branch keeps its `mail-029` slug: renaming it would orphan
PR #641.

## Governing docs

- **`docs/frd/frd-08-email-mailbox-and-background-processing.md` — meets.** Lines 284-345 were
  read directly at this head. FRD-08 requires each mailbox to hold "its own lease and its own
  durable cursor, so one mailbox's failure or backlog never affects another", names the Worker
  "the sole owner of the mailbox lease, cursor/delta read", and requires an Outlook/Graph route to
  "maintain a durable cursor/checkpoint and idempotent occurrence processing". The pre-fix throw
  violated all three: one sparse entry pinned that mailbox's cursor indefinitely. FRD-08 already
  models advancing the cursor over an entry deliberately not retained — mail received before the
  fresh-start activation time "advances the cursor but is not retained" — which is the precedent
  the skip follows. No FRD sentence is modified, and no new ADR is owed: tolerating Microsoft's
  documented delta contract is conformance to an external contract, not a Pegasus design choice.
- **EPIC-011 `context.md` §1.3 / D22 — meets.** Mail freshness is a fixed 15 minutes with no
  backfill, so a stalled cursor is exactly the operator-visible freshness and service-health
  defect the incident produced. No decision in the set changes.

## Verification evidence (the ticket's four boxes)

1. **Sparse entry skipped, no MIME fetch, poller not wedged.** The `continue` sits at
   `GraphApprovedSources.cs:637`; `client.ReadMimeAsync` is at line 651, so the skip precedes
   every MIME fetch. Asserted by the first new test on the request path, not merely on the absence
   of a throw.
2. **Cursor advances exactly once, replay idempotent.** `consumed = cursor.SkipCount +
   available.Length` (line 668) is independent of how many entries were skipped, so `pageCursor`
   is identical whether an entry was retained or skipped, and the delta link stays the only cursor
   owner. `MailboxIntake.PollOneAsync` persists `page.NextCursor` through
   `pollStore.CompleteAsync` at `src/Pegasus.Core/Intake/MailboxIntake.cs:489`, **after** the page
   loop; `ValidatePage` (line 661) rejects a blank cursor but accepts an **empty** `Messages`
   list, which is precisely what a fully-skipped page returns.
3. **Ordinary and removal/change behaviour retained.** The `Removed` skip, the exact-folder
   `UnauthorizedAccessException` (lines 288-292), the Deleted Items `InvalidDataException`
   (lines 431-434) and `GraphApprovedSentSource` — which never reads `ReceivedAtUtc`, so the new
   record member is inert there — all lie outside the diff's four hunks.
4. **All required PR checks green** at head `c6842a8c3a36fe806a3103d067fef207d22651d3`, GitHub
   Actions run `33525322197`: `unit`, `browser`, `sql-integration (1)`, `(2)`, `(3)`,
   `sql-integration-coverage`, `test-ui`, `changes`, `documentation`, `local-development-scripts`,
   `reference-data` all **pass**; `infrastructure` **skipping**. `mergeStateStatus: CLEAN`, base
   `dev`, 0 behind `origin/dev` (`9b8f78a3`).

Also confirmed: no new failure-classification path. `ReceivedDateTimePresent` occurs at exactly
two sites, both in the changed file; nothing was added to `MalformedApprovedInboxMessageException`,
quarantine or the health surfaces; the only exception type is the pre-existing
`InvalidDataException`, narrowed to the genuinely corrupt case. No new runtime dependency,
package, configuration value, schema change, migration or grant ships, so there is nothing to
prove in the packaged artifact. The production caller is unchanged: `MailboxIntake.PollOneAsync`
through the existing `services.AddSingleton<IApprovedInboxSource, GraphApprovedInboxSource>()`
registration.

## Commands (exact, with cwd and exit codes)

Worktree `C:\Users\PGUSER\Documents\github\pegasus-worktrees\mail-029-graph-received-datetime`
throughout; board writes went through `kanmer-call.sh`.

| Command | Exit | Result |
|---|---|---|
| `git rev-parse --show-toplevel` / `--path-format=absolute --git-common-dir` (worktree and primary) / `git branch --show-current` | 0 | PASS — recorded worktree, one shared repository, branch `task/mail-029-graph-received-datetime` |
| `git fetch origin` | 0 | PASS |
| `git rev-list --left-right --count origin/dev...HEAD` | 0 | PASS — `0` / `2`: 0 behind, so **no merge, no push, no new commit** |
| `gh pr diff 641` | 0 | PASS — +72 / −3 across the 2 planned files, matching the plan |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | **1** | **FAIL, first attempt** — NETSDK1004, 7 cold `project.assets.json`. Kept, not replaced |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | PASS |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | PASS — 0 warnings, 0 errors |
| `gh pr checks 641`, `gh pr view 641 --json …` | 0 | PASS — see box 4 |
| `gh pr edit 641 --title … --body-file …` | **1** | **FAIL** — `token is missing required scopes [read:project]`. See ASSUMPTION 1 |
| `gh api -X PATCH repos/collisionengineers/pegasus/pulls/641 -F title=… -F body=@…` | 0 | PASS — title and the one footer line only; verified against the pre-edit body |

Test rail, run by the controller's test runner at this head (evidence under
`runs/20260901T215000Z-claude-controller/MAIL-033/tests/`): `1-restore` **PASS** (exit 0),
`2-build` **PASS** (exit 0, 0 warnings, 0 errors), `3-core-tests` **PASS** (exit 0, 1185 passed,
0 failed), `4-architecture-tests` **PASS** (exit 0, 100 passed, 0 failed), `5-sql-integration`
**INCONCLUSIVE** (exit 1, 710 failed — SQL Server LocalDB is absent on this workstation, error 52;
not a code signal). Neither new test appears among the recorded failures. The authoritative
evidence for that lane is the CI `sql-integration (1..3)` shards, green at this exact head in run
`33525322197`. The implementer ran no test command itself (M6).

## Simplification pass

Recorded in full under the plan's `## Simplification pass (2026-09-02)` heading
(`plan`@`74a5ddf0a5ef2ece`). Four lenses over `gh pr diff 641`; **nothing applied**, because a
code change on this adoption is a deviation by the plan's own terms:

- **F1 reuse (minor, not applied).** `ParseItem` probes `receivedDateTime` twice — once inside
  `OptionalInstant` → `OptionalString` (line 577) and once for the new flag (line 565) — encoding
  a tri-state (absent / unparseable / valid) as two record members. A combined helper would state
  it once. Not applied: zero-diff adoption; one dictionary probe on an already-materialised
  `JsonElement`; the shared helpers have six other callers left untouched. Not ticket-worthy on
  its own.
- **F2 simplification (cosmetic, not applied and not recommended).** The nested throw inside the
  null branch could be flattened, but that repeats the null test and separates the
  external-contract comment from one half. A wash.
- **Efficiency — no finding.** Net-negative work: a skipped entry no longer performs its MIME
  `GET .../$value`. Cursor arithmetic untouched, so no extra round trip.
- **Altitude — no finding.** Graph's wire contract stays in the Graph adapter; Core's
  `MailboxIntake` and the `IApprovedInboxSource` port are unchanged, so
  `LocalDurableApprovedInboxSource` is unaffected. The skip mirrors the adjacent `Removed` skip
  instead of inventing a second mechanism.
- **Tests — no finding.** Both facts reuse the existing `DelegateHandler`, `FixedCredential`,
  `Response`, `Options()` and `Lease()` helpers; no new double, no weakened assertion.

## Risks / follow-ups

Three risks, all recorded in `open-questions` (`@568172e0a56fb947`) as explicitly parked, none
blocking:

1. **F3 — an explicit `"receivedDateTime": null`** is classified as present-but-unparseable and
   therefore throws rather than being skipped, because `TryGetProperty` is true for a JSON null.
   Deferred: the property is non-nullable in the Graph message schema and sparse delta
   representations omit rather than null; the incident payloads are no evidence either way (the
   pre-fix code threw for absent, null and unparseable alike); and widening the skip on
   speculation would weaken the negative test. Reopens as a follow-up ticket if a mailbox stalls
   on a literal `null`.
2. **A sparse entry that also omits `parentFolderId`** would stall the same cursor by a different
   route (`UnauthorizedAccessException`, lines 288-292). Deferred: the exact-folder assertion is a
   security boundary that must not be loosened on speculation, and the incident proves
   `parentFolderId` was present in every observed payload.
3. **A message moved *into* the approved Inbox arriving sparse** would be silently skipped. An
   accepted, unconfirmed risk; the alternative is exactly the unnecessary MIME fetch this ticket
   forbids, and Graph re-emits a full representation for a genuinely new resource. The second
   commit cites an operator acceptance in a `plan.md` that does not exist as a board document on
   MAIL-029; that provenance now lives in this ticket's `open-questions`.

Reviewer note: the PR body's Test plan lists only the first of the two new tests, because the plan
required every body line except the footer to stay byte-identical. The second test is real and is
described above; this is a deliberate constraint, not a stale claim.

Also for the reviewer: the packet's `delivery.prTarget` is `main` (`policySource: default`), while
the binding repository override targets `dev`; PR #641 correctly targets `dev`. And ASSUMPTION 1
records that `gh pr edit` was unavailable for want of a token scope, so the identical two-field
edit went through the REST endpoint.

## Verification hand-off

For `kanmer-verify`, on the merged result in `dev`:

1. `dotnet restore ./Pegasus.slnx --locked-mode`, then
   `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — expect 0 warnings,
   0 errors.
2. The integration project `tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj` in
   Release with `--no-build` and the filter `Category!=Corpus&Category!=Browser` — expect
   `InboxSkipsASparseDeltaItemMissingReceivedDateTimeWithoutFetchingMime` and
   `InboxThrowsOnAPresentButUnparseableReceivedDateTimeRatherThanSkipping` both passing. This
   lane needs SQL Server LocalDB, which is absent on this workstation; if it is still absent,
   record the merge-commit CI `sql-integration` shards instead and say so rather than reporting a
   local failure as a code signal.
3. The `tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj` project in Release with `--no-build` —
   expect 0 failed (1185 at this head).
4. Confirm the merged `repository-check` run is green and that the merge commit's PR body carries
   `Kanmer: MAIL-033`.
5. No screenshots and no snapshot or catalogue regeneration: no routed Razor page changed.
6. Optional production confirmation, if an operator is available: the affected mailbox's poll
   cursor advances past the sparse entry and its service-health row leaves "Failed" — an
   observation, not a command an agent runs.
