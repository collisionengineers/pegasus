# Files — PR-045

| Path | Change | Risk |
| --- | --- | --- |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` | Add live/replay caller tests and test-only delegating/recording fakes. | Preserve existing processor behavior; no production changes. |

Context: `src/Pegasus.Core/Intake/DurableIntake.cs` owns the two caller branches; `AllocationTestData.SeedRetainedMessageForReceiptAsync` supplies local retained evidence; `EfIntakeMutationStore` remains the real write owner. Out of scope: policy, production adapters, migrations, live mailbox writes.
