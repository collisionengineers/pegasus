# Files

## Already committed (`1a86f5db`) — the projection half

| File | Change |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/CurrentIntakeAssociations.cs` | New `IntakeAssociations(Current, ReversedReceiptIds)` record with `AllocationMayStandIn` |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Allocation fallback gated on `AllocationMayStandIn(receipt.Id)` |

## This change — the cancel half

| File | Change | Reuses |
| --- | --- | --- |
| `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs` | `TerminalStateNames` beside `IsTerminal`, so the taxonomy has one owner; `ValidateClose` refuses the new outcome | existing `IsTerminal` |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | `IsTerminalWorkflow` reads Core's list instead of its own copy | — |
| `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` | `EnqueueDueAsync` reads Core's list instead of its own copy | — |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | `SourceEmailUnlinked` on `CaseLifecycleState` and `CaseClosureOutcome` | — |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | `ReverseIntakeLinkRequest` carries no new field; the store resolves the route | existing request |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | `ReverseLinkAsync` handles the accepted-origin route: write an inactive association row and close the case in the same transaction | existing `ExecuteAsync` envelope, which already holds case version + lease |
| `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml` | Optional `DialogConsequence` slot, rendered only when supplied | the partial itself |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Resolve whether this unlink cancels; pass the sentence | `GetExactAssociationAsync` |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Supply `DialogConsequence` | existing dialog wiring |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | `Cancelled — email unlinked` | the one label table |
| `docs/design/README.md` | Fourth approved necessary-copy sentence, in both places the list appears | — |

## Tests

| File | Covers |
| --- | --- |
| `tests/Pegasus.CoreTests/Lifecycle/…` | terminal agreement across the three former copies; generic close refuses the outcome; no reopen |
| `tests/Pegasus.IntegrationTests/Persistence/…` | spawning receipt → case closes, origin link survives; non-spawning receipt → case stays open; replay is idempotent |
| `tests/Pegasus.IntegrationTests/…MailWorkspaceWebTests` | the sentence shows only when the unlink cancels |

## Not touched

`EfCaseWorkflowStore.CloseAsync` keeps its own contract; this outcome never reaches it.
`CaseIntakeLinks` rows are never deleted.
