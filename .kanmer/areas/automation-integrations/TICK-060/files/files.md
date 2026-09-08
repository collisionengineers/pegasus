# Files — TICK-060

## Current disposition map — 2026-09-08

No application file is authorized for modification by this reconciliation.
The old change map below is historical proposed scope, not active edit authority.
Current dev inspected: 498144b0bb55b68fd53b9a31ffc89ef90622c73a.

| Context path | Concrete finding / possible correction after root approval |
| --- | --- |
| src/Pegasus.Core/ProviderApi/ProviderSubmission.cs | Existing owner-scoped query; detailed result contract conflicts with recorded operator attribution; returns reference without active-ID check. |
| src/Pegasus.Core/Intake/IntakeContracts.cs | CurrentCaseId and CurrentCaseReference disagree after unlink; choose the existing appropriate owner, do not add a query abstraction. |
| src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs | Projects original acceptance and active manual link separately. |
| src/Pegasus.Web/ProviderApi/ProviderApiEndpoints.cs | Single existing bearer GET; no second result route is needed. |
| tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs | Reuse ownership/recovery fixtures; add inactive-link outcome evidence after accepted plan. |
| tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs | Reuse actual HTTP/auth/SQL result tests; no current 202/422 or unlink proof. |
| docs/frd/frd-09-provider-and-intermediary-routes.md | Detailed Result contract is current FRD; resolve material conflict before changing it. |
| docs/capabilities.md | API-02 scheduling conflicts with archived TICK-059; downstream reconciliation follows authoritative decision. |
| docs/operator-notes.md | Protected business statements do not settle this exact wire shape; no meaning edit authorized. |

Research records PR594/646/674 ancestry and proof limits. Root may approve a
bounded current plan later. No new migration, store, route, cloud resource,
credential, source edit, workspace, branch or claim is required for this
document-only reconciliation.

## Historical proposed map (preserved)

## Change map

| Path | Change |
| --- | --- |
| `src/Pegasus.Core/ProviderApi/ProviderSubmission.cs` | Reuse the existing result query and collapse its public result to unfinished, Case/PO success, or terminal failure while retaining Principal ownership policy. |
| `src/Pegasus.Infrastructure/Persistence/EfProviderSubmissionStore.cs` | If needed, scope the existing submission lookup by Principal; add no second projection or store. |
| `src/Pegasus.Web/ProviderApi/ProviderApiEndpoints.cs` | Keep the existing GET route and map empty 202, identifier-only 200, generic 422, and indistinguishable 404. |
| `tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs` | Pin Core ownership and the three result outcomes. |
| `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs` | Pin the public status/body contract, paused reads, revoked authentication, and cross-Principal nondisclosure. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Make API-03 the identifier-only result contract and remove API-02-style processing detail. |
| `docs/capabilities.md` | Record API-03 as the result owner and API-02 detailed status as retired. |

## Existing seams reused

- `IGetProviderSubmissionResult` and `GetProviderSubmissionResult`
- `IProviderSubmissionStore`
- `IQueuedIntakeStatusQueries`
- `IIntakeReceiptQueries`
- `ProviderApiEndpoints.MapPegasusProviderApi`
- API-01 authentication, rate limiting, feature composition, and test helpers

## Explicitly unchanged

No new route, result store, SQL projection, table, migration, queue, resource,
dependency, webhook, general Case lookup, report/file surface, or deployment.
