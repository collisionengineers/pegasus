# Plan — truthful queued upload status

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

## Deviations from the chosen approach — 2026-08-29

Recorded because the implementation did not follow two of the ordered steps as
written, and both changes are load-bearing.

**Step 3 is satisfied by deletion, not extraction.** The plan said to
"extract/reuse one current-Case resolution expression matching
`IntakeReceipt.CurrentCaseId`" and project it into `QueuedIntakeStatus.CaseId`.
Read on the merged branch, `QueuedIntakeStatus.CaseId` has **no reader anywhere
in `src/` or `tests/`**, and the "Open case" action the ticket asks for is
already served by `UploadOutcomeQueries.BuildForReceiptAsync`, which branches on
`receipt.CurrentCaseId` — Core's one owner of the accepted-versus-manual
precedence. The unpushed commit `1594ff0e` had filled that dead field by writing
the precedence a second time inside the EF projection, which is the third copy
the ticket body explicitly forbids. Both the copy and the field are removed. The
verification condition is met by the resolution that already exists, and is now
pinned by a test.

**`QueuedIntakeRefreshDelay` does not stay in Core.** A page's poll cadence in
milliseconds, clamped between two and sixty seconds, is presentation, not
business policy, and both of its callers are Web page models. It is now
`src/Pegasus.Web/Presentation/UploadStatusRefresh.cs`. Core keeps
`RetryDueAtUtc`, the durable fact.

## Simplification pass — 2026-08-29

Run over this branch's own diff (`4033e881..HEAD`) across the four lenses. All
findings are inside this lane's own files, so under EPIC-011 D19 all were fixed
here rather than deferred.

| # | Lens | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | Reuse | `EfQueuedIntakeStatusQueries` carried a second implementation of the accepted-versus-manual association precedence owned by `IntakeReceipt.CurrentCaseId`. | **Fixed** — removed, with the unread `QueuedIntakeStatus.CaseId` it fed. |
| 2 | Altitude | `QueuedIntakeRefreshDelay` put a UI poll cadence in `Pegasus.Core`. | **Fixed** — moved to `Presentation/UploadStatusRefresh`. |
| 3 | Simplification | `UploadGroupStatusModel` repeated the still-moving predicate in two properties and the 2 000 ms literal in two more places (a third and fourth copy of a constant Core already named). | **Fixed** — one derivation; `RefreshAutomatically` reads it as a boolean; the constant is named once, on `UploadStatusRefresh`. |
| 4 | Simplification | `UploadStatusModel.RefreshAutomatically` became a public property with one private caller. | **Fixed** — folded into `AutomaticRefreshMilliseconds`. |
| 5 | Correctness | The `visibilitychange` handler in `site.js` scheduled a *new* timer on every return to the tab without cancelling the previous one; timers accumulated for the life of the page, and each re-scheduled itself while a `data-refresh-hold` form was open. | **Fixed** — one timer, cancelled on hide, re-armed on show. |
| 6 | Correctness | Removing the lede left `?duplicate=true` round-tripping through `UploadStatusModel.IsDuplicate` and rendering nothing: dead state, and a real fact lost to the operator. | **Fixed** — restated as a `notice`, the convention `Intake/Details` already uses for exactly this fact, so `QdosIntakeWebTests`' existing assertion holds honestly rather than being deleted. |
| 7 | Efficiency | Both page models read `DateTimeOffset.UtcNow` directly, against a codebase where seven page models inject `TimeProvider`; the cadence was therefore untestable against the fixed clock the retry schedule uses. | **Fixed** — `TimeProvider` injected; the new `RecoveryTests` assertion depends on it. |

Nothing was found and left unapplied. No finding was rejected, accepted as
risk, or deferred to a ticket.

## Defects outside this lane — 2026-08-29

None found.
