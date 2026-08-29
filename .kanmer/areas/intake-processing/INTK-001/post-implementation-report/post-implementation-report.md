# Post-implementation report — INTK-001

Branch `task/intk-001-truthful-status`, worktree
`../pegasus-worktrees/intk-001-truthful-status` — the record taken on
2026-08-25 was reused, not retaken, per EPIC-011 D17.

## Starting state

One commit, `1594ff0e` (2026-08-26), never pushed, eight files. The branch was
behind `origin/dev` by seven merged PRs. `git merge origin/dev` (`4033e881`)
resolved **with no conflicts**, including `wwwroot/js/site.js` — PLAT-029's
shell rewrite did not touch the `data-auto-refresh` block, so the
already-applied behaviour survived the merge intact and did not need
re-applying onto the new file.

The unpushed work was preserved and built on, never discarded.

## What changed

| Path | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | `IntakeWorkState.RetryScheduled` maps to `Processing`, not `Received`; `QueuedIntakeStatus` gains `RetryDueAtUtc` and loses the unread `CaseId`; `QueuedIntakeRefreshDelay` removed from Core; the mapping's own doc comment corrected — it still claimed everything before a lease reads as Received. |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedIntakeStatusQueries.cs` | Projects `DueAtUtc`, surfaced only for `RetryScheduled`. The second copy of the current-Case association precedence is removed. |
| `src/Pegasus.Web/Presentation/UploadStatusRefresh.cs` | New. The one owner of the status pages' reload cadence: the retry due time where the work cannot move before it, 2 s otherwise, 60 s at most. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml.cs` | `AutomaticRefreshMilliseconds` from that owner, off the injected `TimeProvider`; `RefreshAutomatically` folded in. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml` | Duplicate fact restated as a `notice`. |
| `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml.cs` | One derivation for the shortest wait among moving members; `RefreshAutomatically` reads it; `TimeProvider` injected. |
| `src/Pegasus.Web/wwwroot/js/site.js` | One auto-refresh timer, cancelled while the tab is hidden, re-armed when it is shown. |
| `tests/…/RecoveryTests.cs`, `QdosIntakeWebTests.cs`, `UploadConfirmationWebTests.cs`, `UploadOutcomeQueriesTests.cs` | New and adjusted assertions, below. |

## Verification conditions

**1. "A retry-scheduled receipt is either labelled as such or stops the 2 s
reload."** Met, and proved end to end.
`RecoveryTests.TransientProcessingFailureSchedulesARetry` now walks the
retry-scheduled receipt to `/Upload/Status/{id}` against the same fixed
`AdjustableTimeProvider` the retry schedule used, and asserts
`<h1>Processing</h1>` and `data-auto-refresh="30000"` — the first retry delay
is 30 s, so the assertion is exact, not a clamp artefact.

**2. "A background tab does not reload."** Implemented; **not proved by an
executed test.** The scheduler cancels its timer on `visibilitychange` while
`document.hidden` and re-arms on show. Proving it needs a real browser, so it
belongs in `tests/…/Browser/`, which this lane is instructed not to run. Named
as a gap rather than claimed. This is code review, not activation evidence.

**3. "An auto-associated receipt's status page offers 'Open case'."** Met.
`UploadConfirmationWebTests.AttachAddsAnUnmatchedInstructionUploadToTheChosenCaseAndReplaysSafely`
now asserts `>Open case</a>` and `/Cases/Details/{caseId}` on a Case reachable
**only** through the association the attach recorded — the exact case the
ticket said showed "Open receipt". The resolution is `IntakeReceipt.CurrentCaseId`
via `UploadOutcomeQueries`, reused rather than copied.
`QdosIntakeWebTests.CompletedAllocatedUploadStatusLinksOnlyToItsCase` already
covered the accepted-link path and still passes.

**4. Inherited PLAT-015 scope: no lede beneath the H1.** Met.
`QdosIntakeWebTests` asserts the status page contains neither `class="lede"`
nor the state narration. One caveat, stated plainly: the duplicate sentence
that was in a lede is **kept**, moved to a `notice` — it is a value about this
file, `?duplicate=true` exists on the URL only to carry it, and
`QdosIntakeWebTests` has asserted it since before this ticket. Deleting the
copy would have meant deleting a live assertion.

## Evidence

Run on Windows + PowerShell 7 in this worktree, at `e739bc80`.

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | **Build succeeded. 0 Warning(s), 0 Error(s).** |
| `dotnet test … --filter "FullyQualifiedName~RecoveryTests"` | **Passed — 32 passed, 0 failed, 0 skipped.** |
| `dotnet test … --filter "(…RecoveryTests\|…QdosIntakeWebTests\|…UploadConfirmationWebTests\|…UploadOutcomeQueriesTests)&Category!=Corpus&Category!=Browser"` | **Passed — 61 passed, 0 failed, 0 skipped.** |
| `dotnet test … --filter "(…Upload\|…Intake\|…RecoveryTests)&Category!=Corpus&Category!=Browser"` | **Passed — 199 passed, 0 failed, 0 skipped.** |

Not run, deliberately: the full suite, the `Browser` and `Corpus` categories,
and `scripts/Update-TestUiSnapshots.ps1`. Snapshot regeneration is once per
merge on the merging branch only (EPIC-011, 2026-08-29 decisions); this lane
must not regenerate in its own worktree. `TestUiSnapshotTests` only executes
when `PEGASUS_TEST_UI_MODE` is set, so it did not run here.

`upload-status--*` and `upload-group-status--*` snapshots **will** need
regenerating on the merging branch: the status page markup changed (the lede
paragraphs are gone, the duplicate notice is new). The three
`StateMatch` needles for those pages — `data-auto-refresh="2000"`,
`needs a staff decision`, `<h1>Complete</h1>`, `Open case` — are all still
rendered, so the catalogue's own matching is unaffected.

## Notes for the reviewer and for INTK-047

- No page was ported to the design system. That is INTK-047's scope and was
  left alone deliberately; the changes here are behavioural plus the removal of
  narration the ticket inherited from PLAT-015.
- `QueuedIntakeStatus` lost a positional member. The only construction sites
  were `EfQueuedIntakeStatusQueries` and the `StatusOf` helper in
  `UploadOutcomeQueriesTests`; both are updated, and the `caseId:` arguments
  three of its callers passed were inert.
- `OperatorLabels.cs` was not touched.
- One assertion was changed rather than added:
  `RecoveryTests` previously asserted `Assert.Equal(QueuedIntakeStatusKind.Received, status.Status)`
  for a retry-scheduled item. That assertion pinned the defect this ticket
  exists to fix. It now asserts `Processing` **and** the due time, and the test
  gained two further page-level assertions — strictly stronger, not loosened.
  This was already the state of the unpushed commit `1594ff0e`.
