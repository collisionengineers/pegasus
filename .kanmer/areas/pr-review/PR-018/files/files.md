# Files — PR-018

| Path | Change / risk |
|---|---|
| `src/Pegasus.Core/Intake/IntakeContracts.cs`, `IntakeSearchProjection.cs` | Carry exact attachment ordinal in the existing projection. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, migration/designer/snapshot, `EfIntakeReceiptStore.cs` | Persist ordinal in the same child table. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`, `GraphApprovedSources.cs` | Correlate searchability/matches by ordinal. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs`, `ProductionGraphSourceTests.cs` | Duplicate-filename evidence. |

Context: attachment ordinal is already canonical in retained metadata. Out of scope: new identity/store.

## Re-review file delta

| Path | Change / risk |
|---|---|
| `src/Pegasus.Infrastructure/Intake/LocalEmailDisplayReader.cs` | Keep nameless attachment occurrences so display/persisted ordinals do not shift. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Render `IsSearchable` per retained attachment. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs`, `MailWorkspaceWebTests.cs` | Prove nameless-before-named identity and rendered disclosure. |

No schema, second parser/projection/store, or backfill.

## Final re-review file delta

| Path | Change / risk |
|---|---|
| `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` | Describe attached text parts in the canonical occurrence domain without introducing another text parser. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Put attached `text/plain` before a named attachment and prove the later ordinal remains stable. |

Context: `LocalEmailDisplayReader.cs` already includes attached text parts. No schema, store, parsing, or backfill change.
