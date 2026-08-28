# Files this change touches

## Core — new heartbeat seam

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Workflow/CaseEditAuthority.cs` | add `HeartbeatInterval = 60s`; `IsHeld`/`RequireLease` untouched |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | add `HeartbeatCaseEditLeaseRequest`; add `HeartbeatAsync` to `ILeaseCaseForEdit` (L328-335) |
| `src/Pegasus.Core/Workflow/CaseCommandContracts.cs` | add `IHeartbeatCaseEditLease` after `IRenewCaseEditLease` (L84) |
| `src/Pegasus.Core/Lifecycle/CaseCommandSeams.cs` | add `HeartbeatCaseEditLease` + `CaseCommandSeamRules.ValidateHeartbeat` |

## Infrastructure

| File | Change |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs` | new `HeartbeatAsync` after `RenewAsync`. `EditLeaseDuration` (L20) unchanged |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | delete the lease yield at L107-112. **L510 unchanged** |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | register `IHeartbeatCaseEditLease` beside the existing lease seams (~L285) |

No migration and no new grant: the heartbeat writes only `CaseWorkflows`, on
which the Web role already holds `SELECT, INSERT, UPDATE`
(`20260729199000_RuntimeRoleReconciliation.cs:123`).

## Web

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` | `protected` heartbeat helper returning 204/409, touching no TempData |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | inject the port, add `OnPostHeartbeatLeaseAsync`; copy at L184, L230 |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | hidden `data-edit-heartbeat` form beside the lease forms (L63-84); extract the dialog at L296-306 |
| `src/Pegasus.Web/Pages/Shared/_EditFinishConfirm.cshtml` | **new** — the extracted CASE-007 dialog, rendered by both pages |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | rebase onto `CaseMutationPageModel`; claim/release/heartbeat handlers; rewire L216, L409, L442, L535 |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` | edit-mode controls in the existing `record__bar` (L88-109) |
| `src/Pegasus.Web/Pages/EditModeDisplay.cs` | delete the expiry clauses and the now-unread `availableAtUtc`, `WallClock`, `ResolveLondonTimeZone` |
| `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs` | copy at L148 |
| `src/Pegasus.Web/Pages/Triage/Details.cshtml.cs` | call site at L501 follows the `CaseHeldBy` signature change |
| `src/Pegasus.Web/wwwroot/js/site.js` | new IIFE after L536 |

Reused unchanged, named so no parallel copy gets built: `CaseMutationPageModel`
TempData plumbing, `EditModeDisplay`, `_StatusChip.cshtml:66-69` (`editing` /
`locked`), `record__bar-form` markup, the `fetch` + `new FormData(form)`
antiforgery convention at `site.js:298-300`, and `RestoreLeaseState`.

## Tests

Changed: `tests/Pegasus.Core.Tests/Lifecycle/CaseEditLeaseTests.cs:139`
(`RecordingLeaseStore` needs the new member),
`tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` (DI composition — the
mechanical blast radius), `CaseEditModeWebTests.cs:42`,
`AssessmentDamageAndCopyWebTests.cs:286`,
`AssessmentEstimateImportWebTests.cs:426`, `AutomationMcpIngressTests.cs:511`.

Unchanged, deliberately: `CaseWorkflowPersistenceTests.cs` expiry arithmetic
(`:1214,1224,1403,1431,1437,1461,1469`), `CaseTaskArchivePersistenceTests.cs:37`,
`CaseDataCompletenessPersistenceTests.cs:244`,
`Pegasus.Core.Tests/Workflow/CaseEditAuthorityTests.cs`.

New: heartbeat persistence and web tests, assessment edit-mode tests, and the
mail-association pair (see the checklist).

## Docs

`docs/frd/frd-01-case-identity-and-lifecycle.md:87`,
`docs/frd/frd-02-intake-and-source-identity.md:313-317`,
`docs/capabilities.md:151`, `docs/current-architecture.md:519,635`.
`docs/design/README.md` needs no change — recorded as a deliberate finding.
