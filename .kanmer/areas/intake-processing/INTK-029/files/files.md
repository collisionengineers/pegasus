# Files

## Already committed (`1a86f5db`) — the projection fix

| File | Change |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/CurrentIntakeAssociations.cs` | New `IntakeAssociations(Current, ReversedReceiptIds)` record with `AllocationMayStandIn` |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Allocation fallback gated on `AllocationMayStandIn(receipt.Id)` |

## Committed (`db1055a3`) — cancel on unlink

| File | Change | Reuses |
| --- | --- | --- |
| `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs` | `SourceEmailUnlinked` added to `IsTerminal`; `TerminalStateNames()` derived from it; `ValidateClose` refuses the outcome | the existing `IsTerminal` |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | `IsTerminalWorkflow` reads `CaseLifecycleRules.IsTerminal` instead of its own copy | Core |
| `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` | `EnqueueDueAsync` reads `TerminalStateNames()` instead of its own copy | Core |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | `SourceEmailUnlinked` on `CaseLifecycleState` and `CaseClosureOutcome` | — |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | `UnlinkCancelsCase` beside the other association derivations | `CurrentCaseId` |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | `ReverseLinkAsync` cancels the case when the receipt is its accepted origin; `CancelOnSourceUnlinkAsync` | the existing `ExecuteAsync` envelope, which already holds case version, lease, terminal guard, replay and history |
| `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs` | `CloseAsync` refuses the outcome, matching its `CreatedInError` guard | — |
| `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml` | Optional `DialogConsequence` slot, rendered only when supplied | the existing `.notice` class — no new CSS |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Supplies the sentence when `associationReceipt.UnlinkCancelsCase` | existing dialog wiring |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | `Cancelled — email unlinked` | the one label table |
| `docs/design/README.md` | Fourth approved necessary-copy sentence, in both places the list appears | — |

## Tests

| File | Covers |
| --- | --- |
| `tests/Pegasus.Core.Tests/Lifecycle/TerminalCaseStateTests.cs` (new) | the new state is terminal; `TerminalStateNames()` matches `IsTerminal` exactly; every name fits the 40-char column; the generic close refuses the outcome |
| `tests/Pegasus.Core.Tests/Intake/UnlinkCancelsCaseTests.cs` (new) | the four `UnlinkCancelsCase` cases: own case, relinked elsewhere, merely associated, already unlinked |
| `tests/Pegasus.IntegrationTests/CaseAcceptanceReplayTests.cs` | `AcceptedOriginCanBeUnlinkedAndRelinkedWithoutDeletingLineage` **replaced** by `UnlinkingTheAcceptedOriginCancelsTheCaseAndKeepsItsLineage` — cancellation, surviving `CaseIntakeLinks` row, replay idempotency, changed-replay conflict, and the relink refused until reopen |

## Not touched

No EF migration — `State`/`ClosureOutcome` are `string?`/`HasMaxLength(40)`.
`CaseIntakeLinks` rows are never deleted. `Mail/Message.cshtml.cs` needed no change:
the view already has the receipt.
