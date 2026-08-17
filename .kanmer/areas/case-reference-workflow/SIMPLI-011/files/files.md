# Files — SIMPLI-011

Estimated diff: ~20 files, ~+2000 / −1350 (≈40 % of the additions are the missing behavioural tests).

## New

| File | Content |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` | Abstract base for the capability pages: `TryGetActor`, `ExecuteCaseCommandAsync`, `ExecuteTransportCommandAsync`, `RedirectToDetails`, `Readiness`, `RequireOperationKey`, `NewOperationKey`, `LogCaseCommandFailed`, the lease TempData keys + `ClearLeaseState` / `PreserveLeaseState` / `HandleLeaseFailure` / `IsLeaseLoss` / `StoreLeaseAuthority` / `Peek*`, `RetainProposedValues` + `RetainableFormFields` / `BooleanFormFields`. Moved, not rewritten. |
| `Pages/Cases/Workflow.cshtml{.cs}` | `@page "/Cases/{id:guid}/Workflow"`; 7 handlers (Hold, ReleaseHold, ReturnToReview, AssignEngineer, StartWork, RecordEngineerFinding, CreateLinkedReplacement); 6 ports. |
| `Pages/Cases/Tasks.cshtml{.cs}` | `/Cases/{id:guid}/Tasks`; 7 handlers (CreateTask, AssignTask, CompleteTask, CancelTask, RecordManualChase, LinkReportEvidence, UnlinkReportEvidence); 7 ports. |
| `Pages/Cases/Custody.cshtml{.cs}` | `/Cases/{id:guid}/Custody`; 6 handlers (RetryCustody, UploadDocument [multipart, 10 MB], RemoveDocument, ConfirmThirdPartyVehicleEvidence, CreateRequestUploadLink [absolute URL via `Url.Page`], RevokeRequestUploadLink); 6 ports. |
| `Pages/Cases/Vehicle.cshtml{.cs}` | `/Cases/{id:guid}/Vehicle`; 3 handlers (RequestVehicleLookup, AcceptVehicleSuggestion, GenerateEvaHandoff); 3 ports + `IEvaHandoffQueries` if needed. |
| `Pages/Cases/Closure.cshtml{.cs}` | `/Cases/{id:guid}/Closure`; 4 handlers (RecordReportApproval, Close, Reopen, Archive); 4 ports. |
| `Pages/Cases/Eva/Download.cshtml{.cs}` | `/Cases/{id:guid}/Eva/Download`; the `EvaDownload` file response, mirroring `Cases/Documents/Download` (a file response does not belong on a mutation page). |
| `tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs`, `CaseTasksWebTests.cs`, `CaseCustodyWebTests.cs`, `CaseVehicleWebTests.cs`, `CaseClosureWebTests.cs` | Behavioural endpoint tests for the 22 uncovered handlers plus the moved covered ones, in the `CaseDetailsWebTests` idiom. |

## Edited

| File | Change |
| --- | --- |
| `Pages/Cases/Details.cshtml.cs` | Keep `OnGetAsync`, `ClaimLease`, `RenewLease`, `ReleaseLease`, `ConfirmCompleteness`, `Save`, the read side of lease/proposed-value restoration; drop 28 handlers and 27 ports (38 → 11 deps; ~1938 → ~650 lines). Inherit or reuse the base for the remaining shared helpers. |
| `Pages/Cases/Shared/_CaseWorkflow.cshtml` (30 forms), `Shared/_CaseDocuments.cshtml` (5 forms) | Each moved form gains `asp-page="/Cases/<Capability>"` (id already a hidden input); the EVA download form/link points at `/Cases/Eva/Download`. No structural markup change. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | 19 POST URLs retargeted; constructor-port assertion `:69-73` retargeted to the new models; HTML assertions `:52-54` follow the new `asp-page` targets. |
| `tests/Pegasus.IntegrationTests/CaseReportApprovalWebTests.cs` | 2 URLs. |
| `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs:127` | download-URL assertion follows the new route. |
| `docs/current-architecture.md` (implementation map row for the Case workspace callers), `docs/design/README.md` (page inventory row if it lists page files) | as-built page inventory. |

## Ripple effects / out of scope

- `Details.cshtml` unchanged; visible workspace unchanged (design README `:552-604` is the check).
- `TempData["CaseDetailsStatus"]` written-but-never-read is pre-existing → file separately unless trivially folded into the base's status keys.
- No Core/Infrastructure change; no ADR (page composition is Web-internal; FRD-01 says UI calls the same use cases).
