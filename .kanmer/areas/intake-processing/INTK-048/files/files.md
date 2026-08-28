# Files — INTK-048

## Where the change lands

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs` | Recognize the effective current Case association before applying original-decision eligibility; update the method contract comment. |
| `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs` | Pin mapping of a still-eligible, manually associated receipt to an Instruction Case. |
| `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs` | Exercise the real manual-link persistence shape, sweep, history, and replay behavior against SQL Server. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | `CurrentCaseId` and `CurrentCaseReference` are the one effective-association derivation and already encode manual-link precedence. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | `LinkIntake` commits through `IIntakeMutationStore`; Image Intake lifecycle synchronization is advisory and separate. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | Manual links and Case workflow events are written durably under replay and lease protection. |
| `docs/frd/frd-02-intake-and-source-identity.md` | An open U-item must resolve when its receipt reaches a formal Case, while genuinely unidentified work stays open. |

## Ripple effects

The existing Worker reconciliation sweep will repair historical rows, including
U38 and U39, after deployment. No DI, timer, migration, public API, or operator
surface changes follow. The focused integration test requires the existing
SQL Server test fixture.

## Out of scope

The unrelated `UnifiedWorkFunction` deadlock, Azure Resource Health provider
registration, manual SQL repair, link-flow redesign, and changes to immutable
intake decisions are excluded.
