# Plan - truthful queued upload status

## Chosen approach

Extend the one Core queued-status projection so `RetryScheduled` remains truthfully `Processing` and carries its due time. Use that due time for a bounded refresh delay and suspend shared reloads while the document is hidden. Resolve the current Case with the exact manual-association precedence already owned by `IntakeReceipt.CurrentCaseId`, including inactive association suppressing fallback. Remove Upload Status lede narration.

Reuse existing work state/due fields, status query, Core association rule, `data-auto-refresh` helper, Playwright convention, and upload/recovery fixtures. Add no state table, migration, endpoint, worker, or dispatcher.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: implement truthful Processing for retry/large work and current destination while preserving the single public state vocabulary.
- `docs/design/README.md`: align Upload Status with no explanatory copy and replace the obsolete fixed two-second behaviour.
- ADR-0032/INTK-042 govern scheduling; this ticket only projects durable facts.

## Ordered steps

1. Wait for INTK-042 and INTK-040 overlap; start from refreshed `origin/dev`.
2. Extend `QueuedIntakeStatus` and query projection with retry/due facts; map RetryScheduled to Processing.
3. Extract/reuse one current-Case resolution expression matching `IntakeReceipt.CurrentCaseId`: active manual association wins, inactive suppresses, absent falls back to accepted link.
4. Derive a bounded due-aware refresh interval for moving work; keep grouped status compatible.
5. Update shared script to schedule/reload only while visible and resume safely on visibility change.
6. Remove Upload Status lede narration; retain concise status values and actions.
7. Add Core/EF/Web/browser tests for retry state/delay, hidden tab, visible resume, active/inactive association precedence, allocation fallback, group caller, and no lede.
8. Update FRD/design text, run Release relevant/full validation and simplification lenses, then report/commit/push/open PR.

## Proof

Tests prove RetryScheduled never appears Received, refresh is bounded/due-aware and paused while hidden, current Case follows exact precedence, grouped refresh still works, and no lede is rendered. Merged-dev verification repeats focused tests; deployed UI/latency proof remains DELIV-021.

## Risks and mitigations

- **Third association rule:** reuse/extract the current Core owner and test inactive precedence.
- **Refresh loop/duplicate timer:** one visibility-aware scheduler with cleanup.
- **State vocabulary drift:** Core maps persistence to public states; UI does not invent another enum.
- **Overlap:** wait for blocking/claimed work, then fresh base.

## Deviations from the chosen approach - 2026-08-29

Recorded because the implementation did not follow two of the ordered steps as
written, and both changes are load-bearing.

**Step 3 is satisfied by deletion, not extraction.** The plan said to
"extract/reuse one current-Case resolution expression matching
`IntakeReceipt.CurrentCaseId`" and project it into `QueuedIntakeStatus.CaseId`.
Read on the merged branch, `QueuedIntakeStatus.CaseId` has **no reader anywhere
in `src/` or `tests/`**, and the "Open case" action the ticket asks for is
already served by `UploadOutcomeQueries.BuildForReceiptAsync`, which branches on
`receipt.CurrentCaseId` - Core's one owner of the accepted-versus-manual
precedence. The unpushed commit `1594ff0e` had filled that dead field by writing
the precedence a second time inside the EF projection, which is the third copy
the ticket body explicitly forbids. Both the copy and the field are removed. The
verification condition is met by the resolution that already exists, and is now
pinned by a test.

**The inherited `QueuedIntakeRefreshDelay` draft does not stay in Core.** That
symbol existed only in the unpushed inherited `1594ff0e` commit, never on
`origin/dev`. A page's poll cadence in milliseconds, clamped between two and
sixty seconds, is presentation, not business policy, and both concrete callers
are Web page models. The final branch uses
`src/Pegasus.Web/Presentation/UploadStatusRefresh.cs`; Core keeps
`RetryDueAtUtc`, the durable fact.

## Simplification pass - 2026-08-29

Run over this branch's own diff (`4033e881..HEAD`) across the four lenses. All
findings are inside this lane's own files, so under EPIC-011 D19 all were fixed
here rather than deferred.

| # | Lens | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | Reuse | `EfQueuedIntakeStatusQueries` carried a second implementation of the accepted-versus-manual association precedence owned by `IntakeReceipt.CurrentCaseId`. | **Fixed** - removed, with the unread `QueuedIntakeStatus.CaseId` it fed. |
| 2 | Altitude | `QueuedIntakeRefreshDelay` put a UI poll cadence in `Pegasus.Core`. | **Fixed** - moved to `Presentation/UploadStatusRefresh`. |
| 3 | Simplification | `UploadGroupStatusModel` repeated the still-moving predicate in two properties and the 2 000 ms literal in two more places (a third and fourth copy of a constant Core already named). | **Fixed** - one derivation; `RefreshAutomatically` reads it as a boolean; the constant is named once, on `UploadStatusRefresh`. |
| 4 | Simplification | `UploadStatusModel.RefreshAutomatically` became a public property with one private caller. | **Fixed** - folded into `AutomaticRefreshMilliseconds`. |
| 5 | Correctness | The `visibilitychange` handler in `site.js` scheduled a *new* timer on every return to the tab without cancelling the previous one; the first fix still waited a full delay after visibility returned. | **Fixed in verifier remediation** - one timer, cancelled on hide; visible return invokes the guarded reload immediately. |
| 6 | Correctness | Removing the lede left `?duplicate=true` round-tripping through `UploadStatusModel.IsDuplicate` and rendering nothing: dead state, and a real fact lost to the operator. | **Fixed in verifier remediation** - rendered as the labeled value `Duplicate / Already received`; the prior two-sentence notice violated the no-explanatory-copy rule. |
| 7 | Efficiency | Both page models read `DateTimeOffset.UtcNow` directly, against a codebase where seven page models inject `TimeProvider`; the cadence was therefore untestable against the fixed clock the retry schedule uses. | **Fixed** - `TimeProvider` injected; the new `RecoveryTests` assertion depends on it. |

The original pass missed the hidden-tab browser proof, visible-return latency,
and the failed status fallback. The verifier remediation below closes them.
No finding remains deferred to a ticket.

## Defects outside this lane - 2026-08-29

The verifier found one dangling `QueuedIntakeStatus.CaseId` reference in
`src/Pegasus.Web/Presentation/UploadOutcome.cs`. No live lane owned the file;
the one-line comment correction is included in `ce3c0cfe`.

## Verifier remediation pass - 2026-08-29

| Finding | Disposition |
| --- | --- |
| Hidden-tab behavior had no executed proof. | **Fixed** - `UploadStatusRefreshBrowserTests` executes the real shared script in Chromium. It failed against the old scheduler and passes after the fix. |
| Returning visible waited the full delay and repeated visibility changes could starve reload. | **Fixed** - hiding cancels the sole timer; returning visible invokes the existing guarded reload immediately instead of re-arming from zero. |
| Failed status could render a bare heading when actor resolution failed. | **Fixed** - `FailureReason` supplies the existing `OperatorLabels.IntakeFailure` value only when the richer outcome is unavailable; the missing-identifier request path is covered. |
| The deleted `CaseId` member remained in a comment. | **Fixed** - removed the dangling reference in `UploadOutcome.cs`. |
| Duplicate status was two explanatory sentences on a non-destructive surface. | **Fixed** - replaced with `Duplicate / Already received`; the old assertion was changed to assert the new correct markup and reject the prose. |
| Checklist counts and parked items were inaccurate. | **Fixed** - one governing-doc item remains parked; the executed Browser item is complete; the checklist is 8 checked of 9 total. |
| Auto-associated `Open case` was presented without distinguishing pre-existing behavior. | **Fixed in the report** - no user-visible change was needed; the branch deletes the dead second precedence projection and adds regression coverage. |

The remediation diff reuses `OperatorLabels.IntakeFailure`, the existing
`data-auto-refresh` handler and Browser fixture. It adds no dependency, state
list, service, or JavaScript test stack. One timer remains the only refresh
mechanism; the page-model property exists only to expose a Razor value.

## Review findings — dispositions (round 2, 2026-08-29)

Remediation done by Codex (gpt-5.6-sol), driven by the Claude lane wrapper per
EPIC-011's remediation protocol. All commands and diffs below were
independently re-run/re-read by the wrapper, not copied from Codex's report.

| # | Severity | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | medium | Hidden-tab behaviour was parked on a reason CI itself refutes (Browser category runs on every PR); the riskiest file in the diff (shared `site.js`) shipped with zero executed proof. | **Fixed.** Added `tests/Pegasus.IntegrationTests/Browser/UploadStatusRefreshBrowserTests.cs` — loads the real production `site.js` in Chromium, hides the tab, asserts no reload, then asserts an immediate reload on return. Verified independently: `dotnet test ... --filter "FullyQualifiedName~UploadStatusRefreshBrowserTests"` → exit 0, 1 passed. This test genuinely failed against the pre-remediation scheduler (reproduction evidence kept in the post-implementation report) and passes after the fix below. Checklist's Parked section now carries only the one item whose premise still holds. |
| 2 | low | `UploadOutcome.cs:198-201` comment still described `QueuedIntakeStatus.CaseId`, a member this branch had already deleted. | **Fixed.** Comment reworded to drop the dangling reference; no behaviour change. `git diff e739bc80..HEAD -- src/Pegasus.Web/Presentation/UploadOutcome.cs` shows exactly this. |
| 3 | low | `site.js`'s comment claimed a returning-visible tab "reloads then," but the code waited the full delay (up to 60 s); separately, re-arming from zero on every `visibilitychange` could starve a fast-flipping tab. | **Fixed.** `trackVisibility` now calls the guarded `reload()` immediately on becoming visible instead of `schedule()`; hiding just cancels the one timer via `clearTimeout`. No re-arm-from-zero path remains, so the starvation case is gone, and the comment now matches the code. Proved by finding 1's Browser test (asserts reload within 2 s of becoming visible). |
| 4 | low | On the Failed path, when `TryGetActor` fails, the page rendered a bare `<h1>Failed</h1>` with no reason, where the deleted lede always carried one. | **Fixed.** Added `UploadStatusModel.FailureReason` (renders `OperatorLabels.IntakeFailure(...)` as a `<dt>Reason</dt><dd>...</dd>` fact when `Outcome` is null) and a new test, `FailedUploadStatusShowsReasonWithoutAResolvedStaffActor`, that forces `TryGetActor` to fail by stripping the `NameIdentifier` claim. Verified independently in the focused run below. |
| 5 | low | Checklist said "7 ticked, 2 parked" while the board showed 8/9 with a stray unticked duplicate of "Update FRD/design authority text" sitting above the `## Parked` heading. | **Fixed.** Checklist rewritten: the duplicate unticked line is gone, the board now shows 8 ticked / 1 genuinely parked, and the report's own summary matches it. |
| 6 | low | Verification condition 3 ("auto-associated receipt offers Open case") needed no behaviour change — it already held on `origin/dev` — but the post-implementation report presented it in the same voice as the retry fix, without flagging that. | **Fixed (documentation).** Report's condition-3 section now states plainly that no user-visible behaviour changed, names `UploadOutcomeQueries`/`IntakeReceipt.CurrentCaseId` as the pre-existing owner, and frames the new assertions as regression protection for a deletion (the dead `QueuedIntakeStatus.CaseId` and its second precedence copy), not as new delivery. |
| 7 | low | The retained duplicate sentence (`SourceFileName was already received. No duplicate was created.`) was two sentences of operator-facing prose on a non-destructive surface — a design-rule judgement call needing an explicit disposition rather than silent acceptance. | **Fixed, not merely accepted.** Rendered as a labelled fact instead: `<dt>Duplicate</dt><dd>Already received</dd>`, with no narrative sentence. `QdosIntakeWebTests` was changed (not weakened) to assert the new markup **and** `Assert.DoesNotContain("No duplicate was created", ...)` — net a stronger assertion than before, not a loosened one. |

### Verification re-run by the wrapper (not copied from Codex)

- `dotnet build ./Pegasus.slnx --configuration Release` → **exit 0**, 0
  Warning(s), 0 Error(s).
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "(FullyQualifiedName~ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage|FullyQualifiedName~FailedUploadStatusShowsReasonWithoutAResolvedStaffActor|FullyQualifiedName~TransientProcessingFailureSchedulesARetry|FullyQualifiedName~UnexpectedProcessingFailureIsPersistedThenRethrown)&Category!=Corpus&Category!=Browser"`
  → **exit 0**, 6 passed, 0 failed, 0 skipped.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~UploadStatusRefreshBrowserTests"` → **exit 0**, 1 passed,
  0 failed, 0 skipped.
- `git diff origin/dev...HEAD -- tests/` read in full: every changed assertion
  is added or strengthened (renamed fixture parameters after a dead field was
  removed; one `Assert.Equal` changed from the defect value to the correct
  one, gaining three more assertions alongside it; one `Assert.Contains`
  replaced by a stricter markup assertion plus a new `DoesNotContain`). No
  assertion was weakened, skipped, or deleted.
- `git diff e739bc80..HEAD --stat` confirms the remediation touched only
  `UploadStatus.cshtml{,.cs}`, `UploadOutcome.cs`, `site.js`, and two test
  files — all inside INTK-001's own file list; no neighbour-lane file was
  touched.
- Push confirmed: `git rev-parse HEAD` == `git rev-parse
  origin/task/intk-001-truthful-status` == `6ff999b2`.
- PR #620 remained OPEN/MERGEABLE throughout remediation; no PR or board stage
  was touched by this pass.

No high/blocker finding was issued by the verifier. No finding was deferred to
a new ticket; nothing here needed one.
