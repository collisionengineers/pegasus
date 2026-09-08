# Files — DELIV-056

## Change surface

| Path | Why |
|---|---|
| `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` | Extend the existing helper with `DefinitiveQdosInstructionDocument(...)`, the one shared documented QDOS formal-instruction text shape. |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Preserve custody MIME/PDF paths and asset assertions while source documents become definitive. |
| `tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs` | Preserve draft parsing scenarios using real document content. |
| `tests/Pegasus.IntegrationTests/ImageViewingWebTests.cs` | Preserve image-viewing case seeds. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Preserve the mail workspace intake caller. |
| `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` | Preserve current QDOS/Triage scenario coverage. |
| `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` | Preserve queue behavior, excluding unrelated CASE-045 coverage. |
| `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs` | Preserve case-seed and image lifecycle paths. |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Preserve mailbox route/classification paths. |
| `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` | Preserve format-specific DOCX/DOC/MSG/EML/PDF/limit behavior. |
| `tests/Pegasus.IntegrationTests/RecoveryTests.cs` | Preserve recovery behavior. |
| `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs` | Preserve accepted-case seed and access denial while removing stale display markup expectation. |
| `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs` | Preserve upload-confirmation and attachment behavior. |
| `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs` | Preserve focused render setup. |

## Evidence and constraints

- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` owns extraction signature selection.
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs` / `PrincipalMailClassificationPolicy.cs` own document-content classification; formal title belongs in a document, not EmailBody.
- `docs/frd/frd-02-intake-and-source-identity.md` owns the normal definitive-evidence requirement.
- `docs/engineering.md` owns proportionate, serialized verification.

## Out of scope

No production, schema, source inventory, docs, corpus, parser, package, build or generated-output change. No file beyond this fourteen-file table.
