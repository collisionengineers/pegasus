# Files — PR-018

| Path | Change / risk |
|---|---|
| `src/Pegasus.Core/Intake/IntakeContracts.cs`, `IntakeSearchProjection.cs` | Carry exact attachment ordinal in the existing projection. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, migration/designer/snapshot, `EfIntakeReceiptStore.cs` | Persist ordinal in the same child table. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`, `GraphApprovedSources.cs` | Correlate searchability/matches by ordinal. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs`, `ProductionGraphSourceTests.cs` | Duplicate-filename evidence. |

Context: attachment ordinal is already canonical in retained metadata. Out of scope: new identity/store.
