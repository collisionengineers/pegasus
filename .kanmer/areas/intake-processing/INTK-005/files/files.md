# Files — INTK-005

## Where the change lands

| Path | Exact responsibility |
|---|---|
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Add the durable submission-group/member records and Core ports/use case. Keep `IIntakeSubmission` as the one-file owner; the group orchestrator calls it once per member. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Add only group input/result contracts that are required across Core/Web/Infrastructure. Retain `IntakeSource`, per-file limits, and source identity semantics. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Map group and membership entities, foreign keys, ordinal uniqueness, staged-receipt uniqueness, token length, and delete restrictions. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` or a focused `EfIntakeSubmissionGroupStore.cs` beside it | Persist/replay groups and query members. Use a focused store only if adding group concerns to the work store would mix unrelated responsibilities. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_GroupedIntakeSubmission.cs` | Add group/member tables, constraints, indexes, runtime grants where existing migration conventions require them, and reversible Down operations. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | EF-generated model snapshot for the new schema. |
| `src/Pegasus.Web/Program.cs` | Register the chosen Core port and EF implementation using existing scoped-service conventions. |
| `src/Pegasus.Web/Pages/Upload.cshtml` | Bind a multiple file control, retain accessible label/help/error markup, and show a selected-file list using PLAT-006 conventions. |
| `src/Pegasus.Web/Pages/Upload.cshtml.cs` | Bind `IReadOnlyList<IFormFile>`/supported collection, preserve the form-level replay token, validate the full selection, create ordered sources, invoke the group use case, and return the batch result. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml(.cs)` plus the smallest group-result route under `Pages/Upload*` | Keep single receipt status intact. Add a group result/status page only if required to present all members; it must query existing per-receipt status for each member. |
| `src/Pegasus.Web/wwwroot/js/site.js` | Extend the existing Upload enhancement to display multiple selected/dropped files; the native multiple input remains functional without JavaScript. |
| `src/Pegasus.Web/wwwroot/css/site.css` | Style the member/result list using existing list, alert, and status patterns. |
| `tests/Pegasus.Core.Tests/Intake/` focused new group test file | Prove ordering, deterministic child tokens, one-member groups, duplicate filenames, replay, and mixed member results without duplicating `ReceiveIntake` policy. |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | Prove schema constraints, atomic group identity, member ordering, same-token replay, concurrency, and queries. |
| `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` | Extend multipart helpers to submit several named files with one form token. |
| `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` and `IntakeWebNegativeTests.cs` | Prove several accepted formats, original filenames, duplicate filenames, zero files, empty/oversize members, aggregate request rejection, partial durable outcomes, and retry. |
| `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` | Prove keyboard/no-JS selection and every member's visible status/action if browser coverage exists for the merged PLAT-006 surface. |

## Context files to read before editing

| Path | Required fact |
|---|---|
| `docs/frd/frd-02-intake-and-source-identity.md` | Receipt, replay, source occurrence, custody, and association authority. |
| `docs/frd/frd-12-operator-experience.md` | Staff-facing upload/status feedback. |
| `docs/design/README.md` | Upload/dropzone visual and accessibility conventions. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Sole durable one-file intake implementation and work lifecycle. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Existing entity/index/delete/grant conventions. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml(.cs)` | Existing single-receipt status owner to compose. |
| EPIC-007 `context.md` | Shared group invariants. |
| PLAT-006 ticket documents/diff | Markup and JavaScript boundary that must be preserved. |

## Ripple effects

- [[INTK-006]] must use the group query port introduced here; do not expose EF entities to Core.
- Request limits must be tested against the actual Web configuration. Keep the existing per-file 10 MiB bound even if the aggregate multipart limit is higher.
- Migration grants must include the deployed Web/Worker roles only when those callers actually read/write the new tables.
- Receipt and worker retry semantics remain per file. Group completion is derived from member states; do not add a second work queue.
- Search/status pages may link to a group result but canonical processing decisions remain receipt-owned.

## Out of scope

- Vehicle recognition, case matching, Image-Only creation, U-reference allocation, public request-scoped Uploads, mailbox envelope grouping, and cloud deployment.
- Removing transport/request limits or buffering arbitrary batches in memory.
- A generic aggregate framework, event bus, or new runtime.


## Parallel execution note — 2026-08-19

[[INTK-006]] may consume this branch's group contract before PR merge. Its worktree is intentionally based on `intk-005-grouped-upload`; review changes will be reconciled by a later rebase.
