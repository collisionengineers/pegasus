# Post-implementation report — UIIMP-008

Branch `task/uiimp-008-work-centre`, worktree
`../pegasus-worktrees/uiimp-008-work-centre`, PR #610 → `dev`.
PR state: OPEN, MERGEABLE / CLEAN.

## Corrections to the previous version of this report — 2026-08-29

An adversarial verifier re-ran this lane's build, tests and diff and
refuted two claims made here. Both were wrong; both are corrected below,
in place, rather than left standing with a footnote.

1. **"approved on #598" / "already APPROVED at 5a9ff906" was false.**
   There is no `APPROVED` review on #610 or on #598. `gh pr view 610
   --json reviewDecision` returns `""`; `gh api
   repos/collisionengineers/pegasus/pulls/610/reviews` returns three
   `chatgpt-codex-connector[bot] COMMENTED` and nothing else, and #598
   (CLOSED) is identical. **The full independent review required by
   CLAUDE.md workflow step 5 has never been performed on this lane.** It
   is owed on the whole branch, not as a delta over the merge commits.
2. **"No assertion was weakened, skipped or deleted" was false.**
   `DashboardCountersWebTests.ReceivedTodayCountsMailboxChannelOnlyNotManualUploads`
   — PLAT-012's only regression guard — was deleted whole in an earlier
   round while the production rule it guarded was still executing. It is
   restored in this round (see *Round 2*).

The four test files this lane edited outside its allocation were also
never disclosed here. They are named and justified below.

## What shipped

- `src/Pegasus.Core/Operations/OperationsSnapshot.cs` and
  `DashboardCounts.cs` — the needs-attention projection composed from the
  existing queries, plus `NeedsAttentionKind`, `NeedsAttentionPriority`
  and `NeedsAttentionItem`. No new table, no new store.
- `src/Pegasus.Web/Pages/Index.cshtml(.cs)` — header, the five-metric
  strip (`data-value="…"` + `.metric-value`, Blocked routed to
  `?tab=unidentified` per D14), the two-pane
  `integrated-home--expanded` layout and server-resolved selection.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — the three
  string-overload label helpers and the two one-list maps
  (`NeedsAttentionKind`, `NeedsAttentionPriority` + tone), appended in
  the lane's own region (`git diff --numstat`: 74 additions, 0
  deletions, nothing reordered).
- Tests: `Operations/DashboardBoundaryTests.cs`,
  `DashboardCountersWebTests.cs`, `WorkCentreLabelTests.cs` (new),
  `HealthEndpointTests.cs`, `TriageQueuesWebTests.cs`,
  `Browser/AccessibilityTests.cs`, `Browser/OperatorJourneyTests.cs`.

## Files edited outside this lane's allocation

`waves.md` gives lane A `Pages/Index.*` and
`Core/Operations/DashboardCounts.cs`; the ticket body adds
`OperationsSnapshot.cs`, `DashboardCountersWebTests.cs` and "related Core
tests". `Presentation/OperatorLabels.cs` is explicitly permitted by the
lane brief. Four further files were edited and were not disclosed before:

| File | Allocated to | Why the port cannot avoid it |
| --- | --- | --- |
| `tests/…/Browser/AccessibilityTests.cs` | PLAT-029 (wave 1) | asserted the `Dashboard` heading, and accepted `.metric-value` **or** `.metric__value` with the comment "Both spellings until the Work Centre port (wave 2) retires the legacy class". This is that port; the assertion was narrowed to one spelling, not widened. |
| `tests/…/Browser/OperatorJourneyTests.cs` | PLAT-029 (wave 1) | asserted the `Dashboard` heading, the ordered section labels `active cases` / `e-mail activity` / `today and this week` (all three sections removed) and `.metric__value`. Retargeted to the shipped headings and classes. |
| `tests/…/HealthEndpointTests.cs` | unallocated | asserted `Active cases` and `E-mail activity` in `/` markup — both removed. Replaced with the Work Centre heading plus two exact route assertions (`/Cases/Create`, `data-value="not_ready" href="/Cases?tab=not_ready"`), so the check is stricter than before, not looser. |
| `tests/…/TriageQueuesWebTests.cs` | CASE-025 (wave 2, lane C1) | scraped `/` for `data-state="not-ready"…metric__value">(\d+)</strong>` — markup this lane replaces. Only the `/` scrape, its assert message and two comments changed; CASE-025's renamed test, `.scope-button` rail assertion and `row-button` count are untouched. |

Every one is a markup retarget forced by replacing the page. None alters
another lane's subject, and none deletes another lane's assertion. They
are still edits outside the allocation and the orchestrator should count
them as such.

## Merging origin/dev

The branch had never merged `dev` and had gone 64 behind. It was merged
(not rebased, so the SHAs recorded in `scratch/notes.md` — `c07b4488`,
`f524b343`, `5a9ff906` — stay reachable), producing merge commit
`11f1f7de`.

### The one conflict, and why it had to be resolved by hand

`tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`. Dev's CASE-025
commits (`c56b5d5b`, `5c685460`) rewrote the file: it renamed
`NotReadyBadgeCountMatchesRowsAcrossBothOrigins` to
`NotReadyRailCountMatchesRowsAcrossBothOrigins`, swapped the badge
assertion for a `.scope-button` rail-count regex, and replaced
`NotReadyOriginFilterReturnsOnlyTheMatchingOriginsRows` with
`NotReadyMissingFilterReturnsOnlyTheMatchingRows`. This lane had edited
the same lines under the old name.

Neither side could be taken wholesale:

- **Taking dev's side** merges cleanly and then fails at runtime — dev's
  surviving test still scrapes `/` with the old markup
  `data-state="not-ready"…metric__value">(\d+)</strong>`, which this
  lane replaces on `Pages/Index.cshtml`.
- **Taking this branch's side** silently reverts CASE-025's rail
  rewrite.

Resolution: dev's complete file is the base, with only this lane's three
edits re-applied inside dev's renamed test — the `/` scrape becomes
`data-value="not_ready"[\s\S]*?metric-value">(\d+)</span>`, the assert
message becomes "Work Centre Not ready metric markup not found.", and
the XML summary and inline comment say "the Work Centre's Not ready
metric". Dev's test name, `.scope-button` rail assertion,
`Assert.Equal(2, Regex.Count(notReadyHtml, "class=\"row-button\""))` and
`railCount` variable are all kept; this branch's `badgeCount` naming is
discarded entirely. Diffed against `origin/dev`, the resolved file
differs by exactly those three hunks and nothing else.

That test is the proof the resolution is right: it scrapes CASE-025's
rail markup and this lane's new metric markup in the same run, so it can
only pass if both survived the merge.

### The file-ownership collision, stated both ways

`waves.md` allocates `src/Pegasus.Core/Operations/DashboardCounts.cs` to
wave-2 lane A (this ticket), and CASE-025 (lane C1) edited it anyway in
`95f69958` (`git show --stat 95f69958` confirms), together with
`TriageQueuesWebTests.cs`. That is what produced the conflict. It is a
two-sided overlap, not one lane's fault: this lane also edited
`TriageQueuesWebTests.cs`, which is CASE-025's file. Two tickets in one
wave sharing a path is exactly what "a ticket owns whole files" exists to
prevent, and the wave plan needs the retarget files assigned.

### Auto-merges verified by inspection

- `src/Pegasus.Core/Operations/DashboardCounts.cs` — carries dev's
  `CaseStageCounts(…, int Complete = 0)` and this lane's
  `NeedsAttentionKind` / `NeedsAttentionPriority` / `NeedsAttentionItem`.
  Diffed against `origin/dev`: additions only.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — carries dev's
  `TriageState`, `CaseRequirement`/`CaseRequirements`,
  `RequestOperationState` and the four `ServiceHealth*` helpers plus this
  lane's `UnidentifiedReason`, `UnidentifiedMediaKind`, `ChaseState`,
  `NeedsAttentionKind`, `NeedsAttentionPriority`,
  `NeedsAttentionPriorityTone`. Diffed against `origin/dev`: additions
  only, nothing reordered.
- `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` —
  dev's version with this lane's Work Centre retargets ("Dashboard" →
  "Work Centre", the read order, `.metric__value` → `.metric-value`).

### One post-merge defect, fixed

The merge kept `using Pegasus.Core.Operations;` from both sides, so
`OperatorLabels.cs` declared it twice — `CS0105`, a hard build failure.
Dev's sorted entry stays and the trailing duplicate was deleted
(`b8c0cf77`). No member was reordered; four lanes share that file this
wave.

## Round 2 — the verifier's findings

Full dispositions are in `plan/plan.md` under "Review findings —
dispositions (round 2) — 2026-08-29". The code changes:

- **PLAT-012's guard is restored.**
  `DashboardCountersWebTests.ReceivedTodayCountsMailboxChannelOnlyNotManualUploads`
  is back, re-pointed from the deleted E-mail activity tile onto
  `IDashboardQueries.GetMailActivityCountsAsync`, where the channel rule
  still lives. It stores one Mailbox and one ManualUpload receipt and
  asserts `ReceivedToday == 1`. Proven to bite: with the channel
  predicate removed from `EfDashboardQueries` it fails
  `Expected: 1 / Actual: 2`; the mutation was reverted and the tree is
  clean.
- **The false doc comment is corrected.** `MailActivityCounts` no longer
  claims `ReceivedToday` "backs the Dashboard's E-mail activity tile" —
  a tile this lane removed. It records that nothing renders the value,
  that the query still runs per load, and where the rule is guarded.
- **The orphaned property is on the board.** [[PLAT-058]] (EPIC-011,
  wave-5) decides whether `ReceivedToday` gets a surface or is deleted
  with its query. It could not be done here: `EfDashboardQueries.cs` and
  `IntakePersistenceIntegrationTests.cs` are outside this lane.
- **ExternalWork no longer shows a raw code.**
  `IndexModel.TitleLabel` routes the ExternalWork title through
  `OperatorLabels.Humanise` — the same helper
  `Pages/Operations/Index.cshtml` already uses for `ExternalKind` — so
  `document_custody` renders as "Document custody". No second label map.
- **English display copy left Core.** `OperationsSnapshot` built
  `$"{attempts} attempts"`, contradicting `NeedsAttentionItem`'s own doc
  comment. Core now carries `int? Attempts` and
  `IndexModel.DetailLabel` composes the words.
- **New guard:** `tests/Pegasus.IntegrationTests/WorkCentreLabelTests.cs`
  (3 tests) pins the ExternalWork title and detail labelling and that no
  other kind's recorded text is touched. It follows the existing
  plain-label-test convention of `MailClassificationLabelTests.cs`.

## Evidence — local, after round 2

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | exit 0 — 0 warnings, 0 errors |
| `dotnet test … --filter "…DashboardBoundaryTests\|…WorkCentreLabelTests\|…DashboardCountersWebTests\|…TriageQueuesWebTests\|…HealthEndpointTests"` | exit 0 — `Pegasus.Core.Tests`: Failed 0, Passed 8, Skipped 0, Total 8 · `Pegasus.IntegrationTests`: Failed 0, Passed 17, Skipped 0, Total 17 (1 m 12 s) |
| mutation check on the restored guard | fails as designed (Expected 1, Actual 2) with the channel filter removed; reverted, `git diff --stat -- src/Pegasus.Infrastructure/` empty |

Browser-category tests and the snapshot/catalogue scripts were not run
here — the orchestrator owns those.

**In this round no assertion was weakened, skipped or deleted; one
assertion deleted in an earlier round was restored.**

## Evidence — CI

The branch head had never scheduled a run before the merge: `gh pr checks
610` reported "no checks reported", and two empty `ci: retrigger` commits
(`812e3516`, `57a51500`) produced zero runs, because an empty commit does
not schedule one here. The content-bearing merge push scheduled run
[33212916874](https://github.com/collisionengineers/pegasus/actions/runs/33212916874).

**State at `b8c0cf77`: success.** `unit`, `browser`, `sql-integration`
1/2/3, `sql-integration-coverage`, `changes`, `documentation`,
`local-development-scripts` and `reference-data` all pass;
`infrastructure` skips.

The first attempt had one failure, `sql-integration (2)`: Failed 1 /
Passed 329 / Skipped 2 / Total 332. It was
`PrincipalCredentialPersistenceTests
.IssueResetPauseResumeRevokeAreHashOnlyReplaySafeAndFailClosed` — a
TICK-061 test, out of this lane (see below). Re-running that job alone
passed it on the same commit. That is recorded here rather than erased:
the first result was a genuine non-PASS, and the rerun is what makes the
run green.

Round 2 pushes new commits, so CI must run again on the new head before
merge; the run above does not cover them.

## Deviations from the plan

`plan/plan.md`'s simplification-pass finding 6 recorded "n/a — no merge
of `origin/dev`" on the grounds that dev had drifted with zero overlap
with this lane's files. That is superseded: dev did overlap, on two of
this lane's files, and the merge was performed. See the plan's dated
addendum.

## Reported, not fixed

- **Two empty commits stay in the history.** `812e3516` and `57a51500`
  are empty and produced no CI. Removing them means rewriting a pushed
  branch, which this lane may not do, and AGENTS.md rule 17 keeps
  recorded commits reachable. They carry no content.
- **Flaky TICK-061 test, ~1 run in 16.**
  `PrincipalCredentialPersistenceTests.cs` line 62 builds a "corrupted"
  secret as `firstSecret[..^1] + "A"`. When the real secret already ends
  in `A` that rebuilds the *correct* secret, authentication rightly
  succeeds and `Assert.Null` fails — which is exactly the observed
  output (expected null, actual `PrincipalCredentialAuthentication {
  State = Active, MaySubmit = True }`). `PrincipalCredentialPolicy
  .GenerateSecret` ends in `Base64Url(RandomNumberGenerator.GetBytes(32))`;
  32 bytes is 256 bits, the first 42 base64url characters carry 252, so
  the 43rd takes one of 16 values — `A` among them. Roughly a 6 % failure
  rate per run. The fix belongs to TICK-061's owner (mutate to a
  character guaranteed to differ); this lane never touched that test or
  its production code.
- **`ListQueueAsync` is unbounded** (carried forward from the plan's
  finding 5). The composition caps at 50 after composing, but the store
  query returns every open Unidentified row. The interface belongs to
  the Cases/Unidentified lanes (C1/C2); not changed here.
- **`MailActivityCounts.ReceivedToday` is queried and never rendered** —
  [[PLAT-058]], filed this round. Out of lane to fix.

## Review status

**Not reviewed.** No `APPROVED` review exists on #610 or on #598. The
independent review required by CLAUDE.md workflow step 5 is outstanding
and covers the whole branch.
