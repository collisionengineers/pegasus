# Files

Committed in `ca564ac5`.

| File | Change | Reuses |
| --- | --- | --- |
| `src/Pegasus.Core/Intake/IntakeAllocation.cs` | `EmptyCompleteness` → `AutomaticCompleteness` — the automatic route records the instruction and its images as complete, because its own precondition establishes that | — |
| `src/Pegasus.Core/Cases/CaseDataOperations.cs` | `CaseCompletenessPolicy` waives the staff-review requirements for an automatically definitive intake, mirroring `CaseCompleteness.IsReadyForReview` | the existing configuration toggles |
| `src/Pegasus.Core/Intake/AcceptIntake.cs` | Passes `automaticallyDefinitive` — a system-worker actor on a definitive receipt | the actor already on the request |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | The custody promotion calls `CaseCompleteness.IsReadyForReview` instead of restating it | Core's rule |
| `tests/Pegasus.Core.Tests/Cases/AutomaticCaseReadinessTests.cs` (new) | The waiver applies to staff review only; staff acceptance is never exempt; missing evidence still blocks; the rule and the policy agree | — |

## Not changed

No migration, no schema change. The live workflow configuration still requires all four
— the waiver is about who can satisfy them, not about lowering the bar.
