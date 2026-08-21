# Files

Committed in `fef817b8`.

| File | Change | Reuses |
| --- | --- | --- |
| `src/Pegasus.Infrastructure/Custody/BoxDocumentContentStore.cs` | 346 → 240 lines. `FlatFileName` derives the name from the persisted address; `ResolveVersionFolderAsync` → `ResolveCaseFolderAsync` returning `root.Id`. Deleted: `ResolvePlainFolderAsync`, `ResolveBoundFolderAsync`, `VerifyBindingAsync`, `OccurrenceBinding`, `VersionBinding`, `RoleName` and the binding constants | `CustodyNames.SafeName` |
| `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs` | Intake files land in the case folder; folded image-case files use the collision rule already there | the existing collision rule |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | `RetainedCaseFile` record and `RecordRetainedCaseFilesAsync` writing `CaseDocumentEntity`/`DocumentVersionEntity`/`DocumentOccurrenceEntity` — records only, never a second upload; idempotent by operation key | the existing document entities and custody operation key |
| `src/Pegasus.Core/Eva/CaseEvaMapping.cs`, `EvaBundleSchema.cs`, `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | PLAT-031 correction — see that ticket | — |
| `tests/Pegasus.IntegrationTests/BoxDocumentContentStoreTests.cs`, `ProductionBoxCustodyTests.cs` | Flat layout; a second revision does not collide; records written once under retry | existing Box custody harness |

## Deleted, not replaced

Three folders and two JSON binding sidecars per document. The occurrence and revision
identity they carried is in SQL, which is already where the case root's own folder
identity lives, and it is expressed in the file's name.
