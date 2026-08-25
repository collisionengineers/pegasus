# Research — INTK-001: truthful queued upload status

## Question

Why does the receipt-keyed Upload Status surface show retrying work as an older state, keep reloading wastefully, and miss a Case that became current through association rather than allocation?

## Findings

- **The public state collapse is the source of the stale label.** `QueuedIntakeStatusKinds.FromWorkState` in `src/Pegasus.Core/Intake/DurableIntake.cs` maps `Pending`, `Dispatching`, `Dispatched`, and `RetryScheduled` to the single public `Received` state. `tests/Pegasus.IntegrationTests/RecoveryTests.cs` explicitly asserts that a transient processing failure persists `RetryScheduled` yet projects `Received`. The UI therefore cannot distinguish safe work waiting for first processing from work whose processing failed and is due to retry.
- **The status projection omits the retry time.** `QueuedIntakeStatus` carries receipt identity, source name/time, public status, processed receipt, Case id, and failure code, but not `IntakeWorkItem.DueAtUtc`. `EfQueuedIntakeStatusQueries` reads neither the persisted work state nor the due time into the returned contract after converting the state. The page cannot render a truthful retry state or choose a due-aware refresh interval from its current input.
- **Automatic refresh is a fixed global two-second reload.** `UploadStatus.cshtml` emits `data-auto-refresh="2000"` for Received or Processing. `src/Pegasus.Web/wwwroot/js/site.js` schedules `window.location.reload()` after that delay and only pauses for an open `data-refresh-hold` element; it never checks `document.hidden` or handles visibility changes. The same helper is used by grouped-upload status, so a shared fix must preserve that caller.
- **The missing Case link is a query-semantics defect.** `EfQueuedIntakeStatusQueries` derives Case id only from `CaseIntakeLinks`. By contrast, `IntakeReceipt.CurrentCaseId` in `src/Pegasus.Core/Intake/IntakeContracts.cs` gives the existence of an `IntakeManualAssociations` version precedence: active association -> its Case; inactive/reversed association -> no current Case; no association row -> accepted `CaseIntakeLinks` Case. The status query can therefore show “Open receipt” for an active automatic or staff association, and simply coalescing both tables would be wrong after an unlink.
- **The repository already has the same association precedence in persistence.** `EfIntakeReceiptStore` materializes the inputs used by `IntakeReceipt.CurrentCaseId`; `EfImageIntakeStore` has a private `CurrentCaseId` helper with the same active/inactive/absent rule. Planning must consolidate or reuse that rule instead of adding another subtly different projection.
- **The current view contains narration the inherited scope explicitly removes.** `UploadStatus.cshtml` renders `Model.Message` in a `p.lede` while the outcome is nonterminal, and renders duplicate narration in another `p.lede`. `UploadStatusModel.StateMessage` owns Received/Processing/completion sentences. This conflicts with the ticket's inherited PLAT-015 scope and the design authority's page-economy/no-explanatory-copy rule.
- **The settled parent contract removes the implementation choice left in the old ticket wording.** [[INTK-041]] records the approved target: retrying or large work remains truthfully Processing rather than showing an older state; ordinary intake targets p95 <=10 seconds; immediate publication is owned separately by [[INTK-042]]. INTK-001 should repair the projection and presentation, not create another dispatcher or processing path.
- **Existing tests cover the right integration seams but not these defects.** `QdosIntakeWebTests` proves Pending -> Received with 2-second refresh, terminal completion, allocation-based “Open case”, authorization, and 404. `RecoveryTests` proves retry currently collapses to Received. No source test verifies background-tab refresh suppression; no status-page test points completed work at a receipt whose current Case exists only in `IntakeManualAssociations`.
- **Concurrent work must be preserved.** The active [[INTK-040]] worktree currently has uncommitted changes in `DurableIntake.cs`, `frd-02-intake-and-source-identity.md`, and intake tests for mailbox image intake. INTK-001 must not implement against or edit that worktree; its later branch should start after the overlapping work lands or explicitly rebase from the then-current `origin/dev`.

## Implications

- Project enough durable work information to distinguish an initial wait from a scheduled retry and expose the due time without exposing lease/storage details. Use the one Core-owned public state vocabulary; do not create a UI-only parallel state table.
- Keep automatic refresh only for genuinely moving work, make its delay bounded/due-aware where applicable, and make the shared script suspend reload while the document is hidden. Preserve manual refresh and grouped-upload behavior.
- Resolve the destination Case with exactly the same precedence as `IntakeReceipt.CurrentCaseId`, including the reversed-association case. A raw `CaseIntakeLinks ?? IntakeManualAssociations` coalesce is not correct.
- Remove Upload Status lede narration rather than replacing it with more explanatory copy. State belongs in the heading/values and available action.
- Scope implementation to status projection, presentation, the shared refresh helper, focused tests, and canonical behavior documentation. Immediate queue publication, recovery dispatch, Graph wake-up, reader performance, and sender correction remain their owning tickets.

## Open questions

None. The parent contract and ticket acceptance settle the target behavior; exact code shape belongs in planning.
