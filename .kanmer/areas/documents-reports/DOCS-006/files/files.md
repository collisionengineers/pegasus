# Files — DOCS-006

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` (or sibling) | `InstructionEvidenceImages` selection rule (one owner of the promote/skip policy: embedded ≥ 40 KB, image-typed attachments, never inline; hash-deduped) + `DownloadIntakeAssetQuery` |
| `src/Pegasus.Core/Intake/DownloadIntakeSource.cs` (sibling class) | `DownloadIntakeAsset` — receipt-scoped asset download via `IIntakeReceiptQueries` + `IIntakeArtifactStore`, hash-verified, mirroring the source download |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | After the attachments pass, promote the receipt's evidence images through the same `RetainAcceptedIntakeAttachmentAsync`, ordinals continuing, op key `{OperationKey}:embedded:{assetId:N}` |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | `ListEvidenceImagesForCaseAsync(caseId)` — origin receipt + `CaseIntakeLinks` receipts → promoted image assets (id, file name, media type, length) |
| `src/Pegasus.Web/Pages/Intake/Asset.cshtml(.cs)` | Inline image-only asset endpoint (`/Intake/Asset/{id}/{assetId}`) with `Image.cshtml`'s protections |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml(.cs)` | Evidence tab gains the instruction-photo gallery (existing gallery pattern); `EvidenceCount` includes them |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | End-to-end with a real images-PDF email (corpus, skip-if-absent): photos land in local custody beside the source; logos/inline excluded; replay verifies |
| `tests/Pegasus.IntegrationTests/ProductionBoxCustodyTests.cs` | Box path expectations for the promoted files |
| Case web tests | Evidence tab shows the gallery and the count |

Reuse: `RetainAcceptedIntakeAttachmentAsync` (unchanged contract),
`IIntakeReceiptQueries`, `IIntakeArtifactStore`, the CASE-006 gallery
pattern and `Image.cshtml`'s response hardening. No new store on Core, no
migration, no grant change.
