# Plan — MAIL-033: Advance the Graph delta cursor when sparse messages omit receivedDateTime

**Plan sizing / diff estimate: 0 changed repository lines.** This is the adoption of an
already-implemented, already-green pull request. The work is PR metadata, board
traceability, the simplification pass over the real diff, and the report. The diff being
adopted is +72 / −3 across 2 files. Any repository code change is a **deviation** to
report, not a task.

## Objective

Bring the existing PR #641 under its correct owner MAIL-033 — retitled, re-footered,
recorded on the board, simplification-passed and reported — so an independent reviewer can
merge a green PR whose stated ticket matches the work it does, with no behavioural change
to the fix itself.

## Starting state

Evidence: `files`@`c4e8f725d16b0765`, `open-questions`@`ae74331809ad98e7`;
EPIC-011 `context.md` read 2026-09-02; ticket `revision` `rev1:dd5d885dd6ecfc57` at planning.

- PR #641 — `OPEN`, base `dev`, head `c6842a8c3a36fe806a3103d067fef207d22651d3`,
  `mergeStateStatus: CLEAN`. Branch `task/mail-029-graph-received-datetime`, re-homed by the
  controller as worktree
  `C:\Users\PGUSER\Documents\github\pegasus-worktrees\mail-029-graph-received-datetime`
  (`--git-common-dir` = `C:/Users/PGUSER/Documents/github/pegasus/.git`), **2 ahead / 0
  behind `origin/dev`** at planning.
- Checks at that head: `unit`, `browser`, `sql-integration (1..3)`,
  `sql-integration-coverage`, `test-ui`, `changes`, `documentation`,
  `local-development-scripts`, `reference-data` all **pass**; `infrastructure` skipping.
- The two commits: `712bfcf3` (skip instead of throwing) and `c6842a8c` (distinguish
  malformed from absent). Both bodies end `Kanmer: MAIL-029`; the PR title ends
  `(MAIL-029)` and the body footer reads `Kanmer: MAIL-029`. MAIL-029 is live in `backlog`
  and owns missing Inbox attachment columns, so the identification is simply wrong.
- Diff, file by file:
  - `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` (+19 / −3):
    `ParseItem` now records raw property presence
    (`value.TryGetProperty("receivedDateTime", out _)`) into a new `GraphDeltaItem`
    member `ReceivedDateTimePresent`. In `GraphApprovedInboxSource.ReadAsync` the page
    loop gains, immediately after the existing `if (item.Removed) continue;`, a
    `if (item.ReceivedAtUtc is null)` branch that throws `InvalidDataException` when the
    property was present (unparseable) and otherwise `continue`s. The former inline
    `item.ReceivedAtUtc ?? throw new InvalidDataException(...)` at the message-construction
    site becomes `item.ReceivedAtUtc.Value`.
  - `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` (+53 / −0): two facts —
    `InboxSkipsASparseDeltaItemMissingReceivedDateTimeWithoutFetchingMime` (asserts
    `page.Messages` empty, no request path ending `/$value`, and the returned cursor parses
    to the delta path) and `InboxThrowsOnAPresentButUnparseableReceivedDateTimeRatherThanSkipping`.
- What the ticket's Verification boxes require, and where each already stands (read-only
  verification done at planning; the implementer re-confirms, it does not re-derive):
  - *sparse entry skipped with no MIME fetch, poller not wedged* — the `continue` precedes
    `client.ReadMimeAsync`; asserted by the first new test.
  - *cursor advances exactly once, replay idempotent* — the delta link stays the only cursor
    owner. `ReadAsync` computes `pageCursor` from `consumed = cursor.SkipCount +
    available.Length`, independent of how many items were skipped, and
    `MailboxIntake.PollOneAsync` persists `page.NextCursor` via `pollStore.CompleteAsync`
    **after** the whole page loop (`AdvanceAsync(message.NextCursor)` only per handled
    message). `ValidatePage` accepts an empty `Messages` list with a non-empty cursor, so a
    fully-skipped page completes normally.
  - *ordinary and removal/change behaviour retained* — the `Removed` skip, folder assertion,
    Deleted Items throw and `GraphApprovedSentSource` are untouched.
  - *no new failure-classification path* — nothing was added to
    `MalformedApprovedInboxMessageException`, quarantine, or the health surfaces; the only
    exception type is the pre-existing `InvalidDataException`, now narrowed to the genuinely
    corrupt case.
- The commit for `c6842a8c` cites an operator acceptance recorded in a `plan.md`; no such
  board document exists on MAIL-029. That acceptance is now recorded in this ticket's
  `open-questions` (parked), so the reviewer has a real provenance.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md` — **Meets.** FRD-08 requires
  each mailbox to hold "its own lease and its own durable cursor, so one mailbox's failure
  or backlog never affects another", names the Worker "the sole owner of the mailbox lease,
  cursor/delta read", and requires an Outlook/Graph route to "maintain a durable
  cursor/checkpoint and idempotent occurrence processing". The pre-fix throw violated all
  three: one sparse item pinned that mailbox's cursor permanently. FRD-08 also already
  models advancing the cursor over an item that is deliberately not retained (mail received
  before the fresh-start activation time "advances the cursor but is not retained"), which
  is the precedent the skip follows. No FRD sentence is modified.
- EPIC-011 `context.md` §1.3 / **D22** — **Meets.** Mail freshness is a fixed 15 minutes
  with no backfill, so a stalled cursor is exactly the visible freshness and
  service-health defect the incident produced. Nothing in the decision set changes.
- **No document change and no new ADR.** The Why is production evidence, not a design
  choice: 24 identical `AppExceptions` between 08:40 and 08:56Z on 2026-09-01, and the
  mailbox's "Failed" service-health row. Tolerating Microsoft's documented delta contract
  ("at least the updated properties") is conformance to an external contract, not a
  Pegasus architecture decision.

## Required changes

No behaviour change. The adoption changes only identification, traceability and the record:

1. PR #641 title becomes exactly
   `Advance the Graph delta cursor when sparse messages omit receivedDateTime (MAIL-033)`.
2. The PR body's final line changes from `Kanmer: MAIL-029` to `Kanmer: MAIL-033`; every
   other body line, including the Test plan and its recorded results, stays byte-identical.
3. MAIL-033 carries `prs: ["641"]` (already set) and `commits`
   `712bfcf3a695ab67c0bcde570ebd30ac9b25e740`, `c6842a8c3a36fe806a3103d067fef207d22651d3`.
4. This plan gains a dated `## Simplification pass` block with honest dispositions over the
   real diff.
5. A `post-implementation-report` exists (the `enter-review` gate).
6. Commit history is **not** rewritten: the two commit trailers keep `Kanmer: MAIL-029`
   (no rebase, no amend, no force push). The PR title and body carry the correction, and
   the report states this explicitly so the reviewer does not read it as an omission.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Inspect | `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Already changed on the branch. Read to confirm the diff matches this plan. Editable **only** under the contingency in Ordered steps step 4, as a reported deviation. Not generated. |
| Inspect | `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Already changed on the branch. Same contingency and reporting rule. Not generated. |

No structured `### Step N` sub-sections are declared: the expected diff is zero, so there
is no file-scoped bounded step for `get_execution_packet` to compile. Work the ordered
steps below as one pass.

## Do not modify

- `src/Pegasus.Core/**`, `src/Pegasus.Web/**`, `src/Pegasus.Worker/**`,
  `src/Pegasus.Infrastructure/Intake/**` — in particular
  `src/Pegasus.Core/Intake/MailboxIntake.cs`, which already persists the cursor correctly.
- `tests/Pegasus.Core.Tests/**`, `tests/Pegasus.ArchitectureTests/**`, `scripts/**`,
  `docs/**`, `AGENTS.md`, `corpus/**`, `.worktrees/**`.
- Every repository path not named in Expected files.
- The `dev` and `main` branches, the `kanmer-board` branch, the primary checkout
  `C:\Users\PGUSER\Documents\github\pegasus`, and any other ticket's worktree.
- MAIL-029 itself: do not retitle, re-scope, close or repurpose it. It keeps the missing
  Inbox attachment columns.
- The existing commit objects: no rebase, amend, force push or history rewrite.

## Constraints

- Adoption only. A code change is a deviation: stop, report, and let the controller decide.
- The implementer runs no test command, no snapshot or catalogue script, and no local host
  script — the test runner owns all of those. Build only, for compiler feedback.
- Refresh, if ever needed, is `git merge --no-edit origin/dev` in the ticket worktree. Never
  rebase. Merge commits only. The only permitted push is
  `git push -u origin task/mail-029-graph-received-datetime` from that worktree.
- The branch name keeps its `mail-029` slug: renaming it would orphan PR #641 and discard a
  fully green check run. The ticket-to-PR link is the board `prs` field plus the PR title
  and footer, not the branch name.
- No Razor page changed, so no `docs/design/test-ui/` snapshot or catalogue regeneration is
  owed, and none may be committed.
- Do not print or copy mailbox identifiers, tokens or connection strings from the incident
  evidence; the production detail already in the PR body is the whole permitted record.
- `gh` writes are limited to `gh pr edit 641`. No merge (the reviewer owns it under the
  standing delegation), no label, milestone or reviewer churn.

## Ordered steps

1. **Re-establish the workspace and the head.** In
   `C:\Users\PGUSER\Documents\github\pegasus-worktrees\mail-029-graph-received-datetime`
   assert `git rev-parse --show-toplevel`, both
   `git rev-parse --path-format=absolute --git-common-dir` values, and
   `git branch --show-current` = `task/mail-029-graph-received-datetime`; then
   `git fetch origin dev` and `git rev-list --left-right --count origin/dev...HEAD`.
   Expected `0	2`. If the left number is non-zero, `git merge --no-edit origin/dev`,
   push, and wait for a fresh green `repository-check` before continuing. Reuse: the
   controller's existing re-homed worktree and the existing branch — create and take
   nothing.
2. **Confirm no behavioural gap.** Read `gh pr diff 641` against the ticket body's four
   Verification boxes and against FRD-08 lines 284–345, using the Starting state above as
   the expected answer for each box; read `GraphApprovedInboxSource.ReadAsync` and
   `MailboxIntake.PollOneAsync` in the worktree to confirm the cursor claims still hold at
   this head. Reuse: the existing `IApprovedInboxSource` port and its
   `LocalDurableApprovedInboxSource` sibling (unchanged), the existing `GraphDeltaItem`
   record, the existing `Removed`-skip precedent in the same loop, and the tests' existing
   `DelegateHandler` / `FixedCredential` / `Options()` / `Lease()` fakes — the new tests add
   no new test double. Tick the four Verification boxes on the ticket body only if each is
   actually satisfied; a gap is a stop under step 4.
3. **Optionally build for compiler feedback.**
   `dotnet build ./Pegasus.slnx --configuration Release --no-restore` in the worktree, after
   `dotnet restore ./Pegasus.slnx --locked-mode` if the restore is cold. Expected: zero
   warnings-as-errors, zero errors. This is confirmation only; CI at the head is the gate.
4. **Deviation contingency.** If step 2 finds a real defect, stop and report with status
   STOPPED naming the exact line and the Verification box it fails. Do not fix it in this
   pass unless the controller re-dispatches with authority; if authorized, the change is
   confined to the two Expected files, needs a new commit (never an amend), a push, and a
   fresh green check run.
5. **Retitle and re-foot PR #641.** `gh pr edit 641 --title "Advance the Graph delta cursor
   when sparse messages omit receivedDateTime (MAIL-033)"`, then replace only the trailing
   `Kanmer: MAIL-029` line with `Kanmer: MAIL-033` by reading the body with
   `gh pr view 641 --json body`, editing that single line into a file, and applying it with
   `gh pr edit 641 --body-file <file>`. Verify by re-reading title and body. Reuse: the
   repository's existing `Kanmer: <ID>` footer convention.
6. **Record traceability on the board.** `update_item` MAIL-033 with
   `commits: ["712bfcf3a695ab67c0bcde570ebd30ac9b25e740","c6842a8c3a36fe806a3103d067fef207d22651d3"]`
   and `prs: ["641"]`, passing `expected_updated` and `expected_project`. Reuse: the board's
   existing commits/prs fields — invent no new field or label.
7. **Run the simplification pass over the real diff** — reuse, simplification, efficiency,
   altitude lenses (`/simplify` or the equivalent independent lenses) over
   `gh pr diff 641`, apply nothing that changes behaviour, and record every finding with its
   disposition under this plan's `## Simplification pass (2026-09-02)` heading via
   `set_ticket_doc doc: "plan", append: true` with `expected_version`. An unapplied finding
   is named with a reason or a ticket; "no findings" is a legitimate disposition and must be
   written as such. This is required before the PR is offered for review, and the diff does
   change code, so `n/a — docs-only` is not available.
8. **Write the post-implementation report** from the kanmer-execute template as the
   `post-implementation-report` doc: what was adopted rather than written, the head SHA, the
   check names and results, the four Verification boxes with their evidence, the retained
   `Kanmer: MAIL-029` commit trailers and why, the two parked risks, and the exact commands
   with cwd and exit codes.
9. **Move the ticket.** `move_item` MAIL-033 → `review` with `expected_updated`, once
   `get_doc_gates` shows `enter-review` passable. One boundary only.

## Acceptance checks

- Production caller named and unchanged: `MailboxIntake.PollOneAsync`
  (`src/Pegasus.Core/Intake/MailboxIntake.cs` line ~409) through the
  `IApprovedInboxSource` registration `services.AddSingleton<IApprovedInboxSource,
  GraphApprovedInboxSource>()` in `src/Pegasus.Infrastructure/DependencyInjection.cs`
  line ~674. No new registration or composition entry is required.
- No new runtime dependency, package or configuration value ships, so there is nothing to
  prove in the packaged artifact.
- No schema change: no migration, grant, runtime-role permission or rollback applies.
- The two new tests prove the claim without weakened assertions: the skip test asserts the
  absence of the MIME request path, not merely that no exception was thrown, and the
  companion test pins the negative case so the skip cannot silently swallow a corrupt value.
- PR #641 title and body footer both read MAIL-033; `gh pr view 641` shows base `dev`, head
  `c6842a8c…` (or the later merge head), `CLEAN`, and every required check green.
- MAIL-033 shows both commit SHAs and PR 641; `get_doc_gates` shows `enter-review` passable.
- This plan carries a dated `## Simplification pass` block with honest dispositions.

## Commands

Implementer (cwd `C:\Users\PGUSER\Documents\github\pegasus-worktrees\mail-029-graph-received-datetime`):

```powershell
git rev-parse --show-toplevel
git rev-parse --path-format=absolute --git-common-dir
git branch --show-current
git fetch origin dev
git rev-list --left-right --count origin/dev...HEAD
git log --oneline origin/dev..HEAD
gh pr view 641 --json title,body,files,commits,headRefOid,mergeStateStatus,baseRefName
gh pr diff 641
gh pr checks 641
dotnet restore ./Pegasus.slnx --locked-mode          # only if the restore is cold
dotnet build ./Pegasus.slnx --configuration Release --no-restore
gh pr edit 641 --title "Advance the Graph delta cursor when sparse messages omit receivedDateTime (MAIL-033)"
gh pr edit 641 --body-file <edited-body-file>
```

Test runner (cwd `C:\Users\PGUSER\Documents\github\pegasus-worktrees\mail-029-graph-received-datetime`) — the changed tests live in **`tests/Pegasus.IntegrationTests`** (`ProductionGraphSourceTests`, no category traits, so the non-Corpus non-Browser shard owns them):

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
```

No snapshot capture and no catalogue run: no routed Razor page changed, so the snapshot
update, snapshot verify, UI catalogue and local-development host scripts under `scripts/`
are all out of scope for this ticket.

Merge gate: the GitHub `repository-check` workflow at the PR head. The reviewer, not the
implementer, merges, with
`gh pr merge 641 --merge --delete-branch=false --match-head-commit <headRefOid>`.

## Failure and deviation rules

Stop and report, do not improvise, on: a failing or re-run-red check at the head; a
behavioural gap against any Verification box or against FRD-08; a non-zero behind-count that
a `git merge --no-edit origin/dev` does not resolve cleanly; any need to touch a file outside
Expected files; any need to rewrite history, rename the branch or re-point the PR; a
`gh pr edit` that changes anything beyond the title and the single footer line; an
`expected_version` / `expected_updated` conflict on a board write; or a request to merge.
A deviation is reported in the post-implementation report with the exact command, cwd and
exit code — never a silent redesign.

## Simplification pass

Not yet run. The implementer appends `## Simplification pass (2026-09-02)` — or the actual
date of the pass — with the reuse, simplification, efficiency and altitude findings over
`gh pr diff 641` and each one's disposition, before offering the PR for review. Because the
diff changes code, `n/a — docs-only` is not an available disposition.

## Stop condition

PR #641 retitled and re-footered at a green head, ticket moved implementing → review — stop
for the independent reviewer. Do not merge, do not promote to `main`, do not start or take
another ticket, do not dispatch.
