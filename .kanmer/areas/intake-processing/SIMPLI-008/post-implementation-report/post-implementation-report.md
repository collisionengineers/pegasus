# Post-implementation report — SIMPLI-008

Delivered on the combined branch `task/simpli-009` with [[SIMPLI-009]]; the authoritative file-by-file report for the whole PR is SIMPLI-009's `post-implementation-report`. This document isolates the SIMPLI-008 slice.

## What SIMPLI-008 adds

| File | Change | Why |
| --- | --- | --- |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | `QueuedIntakeStatusKind` (Received / Processing / Complete / Failed), `QueuedIntakeStatus` record, `IQueuedIntakeStatusQueries` port. | Core-owned read contract for the staff status surface. |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedIntakeStatusQueries.cs` (new) | Maps persisted work state to the four public states; returns processed receipt id, linked case id, bounded failure code. | Implements the port. |
| `src/Pegasus.Web/Program.cs` | Registers `IQueuedIntakeStatusQueries`. | Web is the consumer. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml(.cs)` (new) | Authorised `/Upload/Status/{id:guid}`; 404 unknown; heading/message per state; auto-refresh attribute only while nonterminal; manual Refresh; "Open case" or "Open receipt" on completion; failure text via `OperatorLabels.IntakeFailure`; one-time duplicate notice from TempData. | Ticket: staff see Received/Processing/Complete/Failed and reach the case or recovery view. |
| `src/Pegasus.Web/Pages/Upload.cshtml(.cs)` | Successful POST (incl. duplicate) redirects to the status page; old outcome card and destination routing removed. | The status page is the single post-upload destination. |
| `src/Pegasus.Web/wwwroot/js/site.js` | `[data-auto-refresh]` reload after N ms. | CSP-safe refresh. |
| `docs/design/README.md`, `docs/frd/frd-02-intake-and-source-identity.md`, `docs/current-architecture.md` | Status behaviour and page inventory recorded. | Design/FRD/as-built currency. |
| Tests | `QdosIntakeWebTests.ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage`, `UploadStatusIsStaffOnlyAndUnknownReceiptsReturnNotFound`, `CompletedAllocatedUploadStatusLinksOnlyToItsCase`; `RecoveryTests.QueuedStatusProjectsAnActiveProcessingLease`; `IntakeWebTestSupport.Landing` recognises the status route. | Four states, auth/404, destinations, processing-lease projection. |

## Verification

Shared with SIMPLI-009: Release build clean; Core 572 passed; Architecture 94 passed; focused integration set — result appended to the review scratch. No deployment or live claim.
