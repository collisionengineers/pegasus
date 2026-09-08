# Files — PLAT-050

Research source: dev aefe4c32d078ad79c0368666b5666032e6865248.
This is the future contact continuation map, not permission to rewrite
historical settings/credential implementation.

## Where the change lands

| Path | Why |
| --- | --- |
| src/Pegasus.Core/Cases/CaseContracts.cs | Existing CreatePrincipalRequest gains nullable contact input; default null keeps known optional callers valid, not a legacy path. |
| src/Pegasus.Core/Cases/OrganizationAdministration.cs | Normalize contact in existing creation policy and project it in PrincipalAdministrationDetails; no new command or interface. |
| src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs | One optional property/configuration on OrganizationEntity; no Principal-generation duplication. |
| src/Pegasus.Infrastructure/Persistence/EfOrganizationAdministration.cs | Persist contact and bind request hash/history atomically; include contact in existing list/get customer projection. |
| src/Pegasus.Infrastructure/Persistence/Migrations/20260908053000_PrincipalContactEmailAddress.cs | Reserved normal nullable-column Up/Down migration. |
| src/Pegasus.Infrastructure/Persistence/Migrations/20260908053000_PrincipalContactEmailAddress.Designer.cs | Generated matching target-model metadata only. |
| src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs | Generated current EF model; only contact property delta. |
| src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml | Existing labelled field pattern for contact e-mail, type=email; no hint or new dialog. |
| src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml.cs | Bind optional contact and send through existing ICreatePrincipal; retain PRG/authorization/error/operation-key handling. |
| src/Pegasus.Web/Pages/Administration/Principals/Settings.cshtml | Read-only contact in existing customer facts when populated; no empty panel or edit action. |
| src/Pegasus.Web/Presentation/OperatorLabels.cs | One contact label reused by both Principal views. |
| docs/frd/frd-04-parties-accounts-and-access.md | Creation contact behavior, same-customer retention and explicit separation from route/Case-contact/delivery authority. |
| docs/design/README.md | Principal paragraph only: optional creation field and populated Settings display. |
| tests/Pegasus.Core.Tests/Cases/OrganizationAdministrationTests.cs | Normalization/invalid contact/admin refusal with existing fake/store convention. |
| tests/Pegasus.IntegrationTests/OrganizationAdministrationPersistenceTests.cs | Actual create/query/replay/hash mismatch/one-history/same-customer replacement contact retention; migration Up/Down probe in this existing class. |
| tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs | Real create contact and Settings readback, field validation/auth/PRG and existing credential/settings assertions retained. |
| tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs | Await INTK-064 ownership clearance: one actual restricted-Web Core/EF create/replay/get test using existing ConnectedContextFactory; Worker create denied. |
| tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs | Add reserved new migration to existing exact pending-list assertion only; retain DOCS-020 and PLAT-072 entries. |
| docs/design/test-ui/pages/administration-principal-create--default.html | Root-generated scoped actual Razor capture output. |
| docs/design/test-ui/pages/administration-principal-settings--default.html | Root-generated scoped actual Razor capture output. |
| docs/design/test-ui/index.html | Only conditional generation from these two routes after TICK-085 narrow ownership handoff; preserve all other scenarios. |

## Context files

| Path | Constraint |
| --- | --- |
| AGENTS.md | Scope/claims, no speculative compatibility, one heavy verifier. |
| docs/index.md | Authority and canonical documentation ownership. |
| docs/frd/frd-09-provider-and-intermediary-routes.md | Contact is not route identity; real activated route catalog and separate Provider API boundary. |
| docs/adr/0038-manual-only-eva-api-submission.md | Automatic submission explicitly superseded; do not restore the old second toggle. |
| src/Pegasus.Core/Intake/PrincipalMailRoutePolicy.cs | CE staff transport and exact accepted identities remain unchanged. |
| src/Pegasus.Core/Cases/ClaimSourceAdministration.cs | Existing optional contact length/trim convention; distinct role, not a Principal store. |
| src/Pegasus.Web/Pages/Administration/Principals/Settings.cshtml.cs | Existing customer query already carries details; keep manual EVA/location/credential handlers untouched. |
| src/Pegasus.Core/Reports/CaseReportDeliveryPreparation.cs | Report recipients remain CaseContact/ClaimSource; no Principal-contact auto-fill. |
| src/Pegasus.Infrastructure/Persistence/Migrations/20260729199000_RuntimeRoleReconciliation.cs | Existing Organizations table grants cover this nullable column. |
| scripts/Invoke-AzureDatabaseBootstrap.ps1 | Bootstrap derives existing grant matrix; no new grant or role census entry is needed. |
| scripts/Test-MigrationGrants.ps1 | Existing created-table grant census; run unchanged, not a substitute for actual role caller. |
| docs/design/test-ui/catalogue.json | Existing two route/scenario selectors are sufficient; no catalogue edit expected. |
| scripts/Update-TestUiSnapshots.ps1 | Root-only scoped capture/update/verify from actual Razor responses. |

## Ripple effects and ownership

One customer field travels through the existing create, list and get
contracts. Existing default/null callers remain valid; no new route, service
registration, command, role or sender. Same customer code replacement reads
the same row; it must not copy/reset contact or alter existing Case identity.
Operation replay compares normalized contact, and creation history contains
the accepted value in the same transaction.

Migration reservation: 20260908053000_PrincipalContactEmailAddress, checked
absent from accepted source on 2026-09-08. Up nullable nvarchar(320), no
backfill/index; Down loses only newly recorded contact values. Existing
Organizations table-level grants and bootstrap remain unchanged and are
verified through the actual restricted role.

INTK-064 owns AzureSqlRuntimeRoleMigrationTests.cs (map 07e40ec3beb8068c).
No edit before a merge/release or exact root-approved handoff.
TICK-085 retains its claim for fifth-PDF OCR; require only a narrow generated
index handoff, not a claim takeover. Any other newly discovered overlap stops
the affected edit. No claim or worktree exists for PLAT-050 yet.

## Out of scope

No new/edit-contact command, form or API; no mail auto-fill, test send, live
Principal creation, routing/domain remap, QDOS identity exception, default
seed/contact backfill, Case-contact change, new directory role, DI/service,
schema stream, dependency, generic validation framework, UI redesign, or
provider-result contract implementation. Preserve all existing EVA,
inspection-location and credential controls and historical evidence.
