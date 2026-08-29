# Post-implementation report - INTK-001

Branch `task/intk-001-truthful-status`, worktree
`../pegasus-worktrees/intk-001-truthful-status` - the record taken on
2026-08-25 was reused, not retaken, per EPIC-011 D17.

## Starting state

One commit, `1594ff0e` (2026-08-26), never pushed, eight files. The branch was
behind `origin/dev` by seven merged PRs. `git merge origin/dev` (`4033e881`)
resolved **with no conflicts**, including `wwwroot/js/site.js` - PLAT-029's
shell rewrite did not touch the `data-auto-refresh` block, so the
already-applied behaviour survived the merge intact and did not need
re-applying onto the new file.

The unpushed work was preserved and built on, never discarded.

## What changed

| Path | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | `IntakeWorkState.RetryScheduled` maps to `Processing`, not `Received`; `QueuedIntakeStatus` gains `RetryDueAtUtc` and loses the unread `CaseId`; `QueuedIntakeRefreshDelay` removed from Core; the mapping's own doc comment corrected - it still claimed everything before a lease reads as Received. |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedIntakeStatusQueries.cs` | Projects `DueAtUtc`, surfaced only for `RetryScheduled`. The second copy of the current-Case association precedence is removed. |
| `src/Pegasus.Web/Presentation/UploadStatusRefresh.cs` | New. The one owner of the status pages' reload cadence: the retry due time where the work cannot move before it, 2 s otherwise, 60 s at most. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml.cs` | `AutomaticRefreshMilliseconds` from that owner, off the injected `TimeProvider`; `RefreshAutomatically` folded in. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml` | Duplicate fact rendered as the labeled value `Duplicate / Already received`; a failed status renders the existing operator failure label when actor resolution prevents the richer outcome. |
| `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml.cs` | One derivation for the shortest wait among moving members; `RefreshAutomatically` reads it; `TimeProvider` injected. |
| `src/Pegasus.Web/wwwroot/js/site.js` | One auto-refresh timer, cancelled while hidden; returning visible invokes the guarded reload immediately. |
| `tests/./RecoveryTests.cs`, `QdosIntakeWebTests.cs`, `UploadConfirmationWebTests.cs`, `UploadOutcomeQueriesTests.cs`, `Browser/UploadStatusRefreshBrowserTests.cs` | New and adjusted assertions, including real-browser visibility behavior, below. |

## Verification conditions

**1. "A retry-scheduled receipt is either labelled as such or stops the 2 s
reload."** Met, and proved end to end.
`RecoveryTests.TransientProcessingFailureSchedulesARetry` now walks the
retry-scheduled receipt to `/Upload/Status/{id}` against the same fixed
`AdjustableTimeProvider` the retry schedule used, and asserts
`<h1>Processing</h1>` and `data-auto-refresh="30000"` - the first retry delay
is 30 s, so the assertion is exact, not a clamp artefact.

**2. "A background tab does not reload."** Met and proved in a real browser.
`UploadStatusRefreshBrowserTests.ReturningToAHiddenStatusPageReloadsImmediately`
loads the production `site.js`, arms a 60-second timer, hides the document and
confirms there is no reload. Returning visible must reload within two seconds.
The test failed against the submitted re-arm behavior and passes after the
scheduler was corrected to invoke the guarded reload immediately.

**3. "An auto-associated receipt's status page offers 'Open case'."** No
user-visible behavior change was required: origin/dev already used
`IntakeReceipt.CurrentCaseId` through `UploadOutcomeQueries`, so this condition
was already met. The new assertions are regression protection for `Open case`,
the Case URL, and the absence of `Open receipt`. The substantive branch change
is deletion of the unread `QueuedIntakeStatus.CaseId` and its second association
precedence projection. `QdosIntakeWebTests` continues to cover the accepted-link
path.

**4. Inherited PLAT-015 scope: no lede beneath the H1.** Met.
`QdosIntakeWebTests` asserts the status page contains neither `class="lede"`
nor the state narration. Verifier remediation removed the retained two-sentence
notice as explanatory prose and renders the fact as the labeled value
`Duplicate / Already received`. The old assertion was changed, not deleted: it
now pins the value markup and rejects `No duplicate was created`.

## Evidence

Run on Windows + PowerShell 7 in this worktree, at `e739bc80`.

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | **Build succeeded. 0 Warning(s), 0 Error(s).** |
| `dotnet test . --filter "FullyQualifiedName~RecoveryTests"` | **Passed - 32 passed, 0 failed, 0 skipped.** |
| `dotnet test . --filter "(.RecoveryTests\|.QdosIntakeWebTests\|.UploadConfirmationWebTests\|.UploadOutcomeQueriesTests)&Category!=Corpus&Category!=Browser"` | **Passed - 61 passed, 0 failed, 0 skipped.** |
| `dotnet test . --filter "(.Upload\|.Intake\|.RecoveryTests)&Category!=Corpus&Category!=Browser"` | **Passed - 199 passed, 0 failed, 0 skipped.** |

Verifier remediation ran on the exact tree committed as `ce3c0cfe` and
`6ff999b2`:

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | **Exit 0 - Build succeeded. 0 Warning(s), 0 Error(s).** |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "(FullyQualifiedName~ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage\|FullyQualifiedName~FailedUploadStatusShowsReasonWithoutAResolvedStaffActor\|FullyQualifiedName~TransientProcessingFailureSchedulesARetry\|FullyQualifiedName~UnexpectedProcessingFailureIsPersistedThenRethrown)&Category!=Corpus&Category!=Browser"` | **Exit 0 - 6 passed, 0 failed, 0 skipped.** |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "FullyQualifiedName~UploadStatusRefreshBrowserTests"` | **Exit 0 - 1 passed, 0 failed, 0 skipped.** |

The Browser filter first failed against the submitted scheduler: exit 1,
1 failed, 0 passed, timeout at the assertion requiring an immediate visible
reload. That failure is retained as reproduction evidence; the later pass does
not erase it.

At `e739bc80`, the full suite, Browser/Corpus categories, and snapshot script
were deliberately not run. Verifier remediation then ran only the one Browser
class required by the finding. The full suite, Corpus category, and
`scripts/Update-TestUiSnapshots.ps1` remain orchestrator-owned and were not run.
`TestUiSnapshotTests` only executes when `PEGASUS_TEST_UI_MODE` is set.

`upload-status--*` and `upload-group-status--*` snapshots **will** need
regenerating on the merging branch: the status page markup changed (the lede
paragraphs are gone, the duplicate notice is new). The four
`StateMatch` needles for those pages - `data-auto-refresh="2000"`,
`needs a staff decision`, `<h1>Complete</h1>`, `Open case` - are all still
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
  gained two further page-level assertions - strictly stronger, not loosened.
  This was already the state of the unpushed commit `1594ff0e`.

## Verifier remediation summary

No high or blocker finding was issued. Every medium/low finding is closed:
Browser proof added and executed; visible-return starvation removed; failed
fallback rendered and tested; dangling `CaseId` comment fixed; checklist
corrected to 8/9 with one genuine parked item; the pre-existing `Open case`
behavior is now described accurately; and duplicate prose is a labeled value.
No new ticket or accepted risk remains.
