# Current implementation handoff

Handoff date: 2026-07-23, Europe/London

Implementation baseline: `9159f8b` (`feat: add local QDOS intake vertical slice`)

Repository state at handoff: pre-release, local-only, no v2 Azure deployment

## Outcome

The repository now contains the first genuine-input QDOS vertical slice. It deliberately proves one thin path rather than claiming the full MVP:

```text
Development-only Razor Page
  -> ProcessQdosIntake in Core
  -> MimeKit/PdfPig source reader in Infrastructure
  -> EF Core store
  -> persisted review, queue, and dashboard pages
```

The browser is the real caller. Business classification, field-candidate extraction, explicit case-creation authorisation, idempotency, and reference allocation live in Core/Infrastructure and are not duplicated in the PageModel.

## Run it locally

From PowerShell 7 at the repository root:

```powershell
pwsh ./scripts/Invoke-Doctor.ps1
pwsh ./scripts/Invoke-RepoCheck.ps1
dotnet run --project ./src/CollisionSpike.Web --launch-profile https
```

Open `https://localhost:7139/Intake/Qdos` or use the dashboard link. The launch profile supplies:

- `ASPNETCORE_ENVIRONMENT=Development`;
- `Database__Provider=Sqlite`;
- ignored local database `artifacts/local/collisionspike-v2.db`; and
- `Features__LocalQdosIntake=true`.

The route is deny-by-default. It returns 404 if the flag is absent/false and also returns 404 in Production even if someone sets the flag to true.

## Supported behavior

- Upload one genuine `.eml` or `.pdf`, maximum 10 MB.
- Confirm that it is a new QDOS instruction and authorise case/reference creation.
- Read email body, PDF attachments, or direct PDF embedded text.
- Classify strong instruction content ahead of weak transport signals such as a staff-forwarding sender.
- Show classification evidence, ten field suggestions, missing values, conflicts, page-labelled extracted text, document/attachment-level OCR-required status, and failure details.
- Default an absent instruction date from the receipt clock.
- Allocate the current-year `QDOS{YY}{NNN}` reference only for an authorised, confirmed QDOS receipt.
- Return the existing receipt/reference for duplicate source bytes.
- Show persisted Review and Needs sorting counts and filtered queues.

The test clock is fixed to 2031, so integration assertions use `QDOS31001` and a 2031 instruction-date default. Runtime uses the real current year.

## Evidence at the baseline

`pwsh ./scripts/Invoke-RepoCheck.ps1` passed after the implementation commit. It covered:

- repository structure and ignored-boundary guards;
- Release restore/build;
- 13 integration tests;
- 5 architecture tests;
- Bicep compilation; and
- project skill validation.

Seven integration tests exercise pinned genuine corpus inputs by SHA-256, including the staff-forwarded email shape that failed in the predecessor, an embedded-text-insufficient PDF, duplicate delivery, explicit authorisation, parallel reference allocation, and persisted queue counts.

The corpus remained local, ignored, and unchanged: 9,443 files and 6,041,636,339 bytes at the implementation checkpoint. Generated outputs remain under ignored `artifacts/`.

## Key files

| Responsibility | File |
|---|---|
| Business intake use case | `src/CollisionSpike.Core/Intake/Qdos/ProcessQdosIntake.cs` |
| Core contracts and ports | `src/CollisionSpike.Core/Intake/Qdos/QdosIntakeContracts.cs` |
| Email/PDF adapter | `src/CollisionSpike.Infrastructure/Intake/Qdos/MimeKitPdfPigQdosSourceReader.cs` |
| EF persistence and reference transaction | `src/CollisionSpike.Infrastructure/Persistence/EfQdosIntakeStore.cs` |
| Database model/migration | `src/CollisionSpike.Infrastructure/Persistence/CollisionSpikeDbContext.cs` and `Migrations/` |
| Web composition and safety gate | `src/CollisionSpike.Web/Program.cs` |
| Real manual caller | `src/CollisionSpike.Web/Pages/Intake/Qdos.cshtml.cs` |
| Review/queue/dashboard callers | `src/CollisionSpike.Web/Pages/Intake/` and `Pages/Index.cshtml.cs` |
| Genuine-input Web evidence | `tests/CollisionSpike.IntegrationTests/QdosIntakeWebTests.cs` |
| Route-denial evidence | `tests/CollisionSpike.IntegrationTests/LocalQdosIntakeAccessTests.cs` |
| Architecture boundary evidence | `tests/CollisionSpike.ArchitectureTests/DependencyDirectionTests.cs` |
| Embedded PDF decision | `docs/architecture/decisions/ADR-0003-pdfpig-for-first-qdos-slice.md` |

## Important limits

- The current upload stores derived metadata, evidence, field candidates, and the source hash; it does not retain the original uploaded bytes.
- Extraction suggestions are not yet typed, edited, or approved into a full case record.
- PdfPig handles embedded text only. Insufficient pages become explicit OCR candidates; no OCR service is called.
- DOC/DOCX, image-led intake, VRM OCR, Graph mailbox intake, Box, DVLA/DVSA, EVA export, and lifecycle management are not implemented.
- The Worker has no trigger and does not yet call Core.
- There is no application authentication, role enforcement, or authenticated audit actor.
- Local SQLite is disposable development state. Its concurrency result does not prove SQL Server locking behavior.
- An Azure SQL migration exists, but it has not been applied to a live v2 database.
- Bicep compilation proves syntax/type consistency only. No v2 Azure resources have been provisioned.
- No production route should be enabled from this slice.

The full gap list and recommended order are in `docs/plans/remaining-requirements.md`. Unresolved business decisions remain in `docs/plans/open-decisions.md`.

## Next bounded increment

Build the human review-to-case boundary:

1. freeze a human-approved QDOS expected-field cohort and untouched holdout;
2. add typed validation and editable operator confirmation for the existing ten field candidates;
3. persist the approved case draft and its provenance without adding a second parser; and
4. have an independent test author/evaluator compare the visible result with the expected cohort.

Do not start mailbox automation by copying the current rules into the Worker. The future Graph trigger must call the same Core use case and add durable source custody, delivery identity, and bounded failure handling at the adapter boundary.

## Cloud and predecessor boundary

No Azure resource, setting, role, secret, deployment, or predecessor asset was changed by this implementation. CollisionSpike v2 starts with fresh application data; the predecessor's pre-release test cases and application state are not migrated or preserved as a v2 release requirement.

The current read-only Azure inventory remains in `docs/azure/current-inventory.md`. Any resource creation, deployment, credential change, or retirement still requires explicit user approval for the exact targets. Shared Foundry, ACR/ValuationBot, capture, and default-workspace ownership must not be inferred from the predecessor resource group.
