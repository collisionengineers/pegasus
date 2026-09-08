# Plan — PLAT-050: Principal contact identity

## Objective

An Administrator can create one customer with an optional contact e-mail and
see that value in its existing Settings page. Preserve the original Settings,
manual EVA, inspection-location and Provider credential acceptance; no new
administration workflow.

## Starting state

Accepted source: dev aefe4c32d078ad79c0368666b5666032e6865248.
Evidence: research/research.md@aaafe0009e67b331,
files/files.md@51ed18132fd0d48a,
open-questions/open-questions.md@d7f08e1eacd5f451.
PLAT-050 is Preparing, untaken. Original body and EPIC-011/009 memberships
remain; EPIC-014 supplies the current contact authorization.
PLAT-028/TICK-061/TICK-035 integrated caller evidence is mapped in research;
TICK-058/TICK-060's separate result-contract conflict is not a contact feature
and remains with those owners.

## Governing docs

- **Modifies FRD-04**, authorized by the current user/root: one customer
  creation takes name, code and optional contact e-mail; blank is absent,
  invalid supplied e-mail is refused, the accepted value is recorded
  atomically and shown in Settings, and code replacement keeps it with the
  same customer. It is not routing identity, Case contact or send authority.
  Preserve role, credential, inspection, history and customer identity rules.
- **Meets FRD-09:** leave canonical accepted identities, forwarding, Provider
  API credentials and submission endpoints unchanged. Contact cannot activate
  a route or authenticate a provider.
- **Meets ADR-0038:** manual EVA only; the old second toggle is explicitly
  superseded, never reintroduced.
- Update only the Principal paragraph of design README to describe the new
  creation field and populated read-only value. Reuse existing semantic field
  and definition-list patterns; no hint/empty-state prose, new panel, modal,
  component, CSS or script. The Razor design skill informed this small shape.
  No new ADR or protected operator-notes change is required.

## Required changes

1. Add optional ContactEmailAddress at the end of existing
   CreatePrincipalRequest and PrincipalAdministrationDetails. The existing
   OrganizationAdministrationPolicy owns a 320-character bound, trimming,
   blank-to-null and one bare-address validation using the platform
   MailAddress.TryCreate parser plus exact address equality and control
   character refusal. Preserve entered address casing; no DNS/domain allowlist.
   This is one local policy method, no validation framework. Web uses the
   same bound and existing error handling; native type=email is a control,
   not the only authority.
2. Add nullable Organizations.ContactEmailAddress with matching EF model.
   Existing CreatePrincipal transaction includes normalized contact in the
   request hash, persists it on the new customer, and records it with customer
   name and the existing Principal result in the one creation history payload.
   Preserve event/receipt identity and counts; no historical record rewrite.
   Same key/contact replays once; same key/different contact conflicts.
   List/GetPrincipal return the value through the existing customer join.
   Same-customer code replacement naturally retains the same row, no copying
   or generation-specific contact field.
3. Add Contact e-mail after Name in the current Create form, with one shared
   OperatorLabels entry, optional input, normal label and validation span.
   Bind it through the sole existing ICreatePrincipal caller.
   Settings displays a populated Contact e-mail fact beside customer
   information; no edit button, hidden empty panel, auto-fill or mailto action.
4. Migration 20260908053000_PrincipalContactEmailAddress adds only nullable
   nvarchar(320); no default/backfill/index. Down removes only the new column,
   losing recorded contact values but not customer/case identity. No live
   migration or rollback is authorized in this phase. Existing Organizations
   table grants/bootstrap already cover the column; do not add redundant
   grants or grant lists. Prove actual restricted-role behavior instead.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | src/Pegasus.Core/Cases/CaseContracts.cs | Existing CreatePrincipalRequest gains nullable contact input; default null keeps known optional callers valid, not a legacy path. |
| Modify | src/Pegasus.Core/Cases/OrganizationAdministration.cs | Normalize contact in existing creation policy and project it in PrincipalAdministrationDetails; no new command or interface. |
| Modify | src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs | One optional property/configuration on OrganizationEntity; no Principal-generation duplication. |
| Modify | src/Pegasus.Infrastructure/Persistence/EfOrganizationAdministration.cs | Persist contact and bind request hash/history atomically; include contact in existing list/get customer projection. |
| Add | src/Pegasus.Infrastructure/Persistence/Migrations/20260908053000_PrincipalContactEmailAddress.cs | Reserved normal nullable-column Up/Down migration. |
| Add | src/Pegasus.Infrastructure/Persistence/Migrations/20260908053000_PrincipalContactEmailAddress.Designer.cs | Generated matching target-model metadata only. |
| Modify | src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs | Generated current EF model; only contact property delta. |
| Modify | src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml | Existing labelled field pattern for contact e-mail, type=email; no hint or new dialog. |
| Modify | src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml.cs | Bind optional contact and send through existing ICreatePrincipal; retain PRG/authorization/error/operation-key handling. |
| Modify | src/Pegasus.Web/Pages/Administration/Principals/Settings.cshtml | Read-only contact in existing customer facts when populated; no empty panel or edit action. |
| Modify | src/Pegasus.Web/Presentation/OperatorLabels.cs | One contact label reused by both Principal views. |
| Modify | docs/frd/frd-04-parties-accounts-and-access.md | Creation contact behavior, same-customer retention and explicit separation from route/Case-contact/delivery authority. |
| Modify | docs/design/README.md | Principal paragraph only: optional creation field and populated Settings display. |
| Modify | tests/Pegasus.Core.Tests/Cases/OrganizationAdministrationTests.cs | Normalization/invalid contact/admin refusal with existing fake/store convention. |
| Modify | tests/Pegasus.IntegrationTests/OrganizationAdministrationPersistenceTests.cs | Actual create/query/replay/hash mismatch/one-history/same-customer replacement contact retention; migration Up/Down probe in this existing class. |
| Modify | tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs | Real create contact and Settings readback, field validation/auth/PRG and existing credential/settings assertions retained. |
| Modify | tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs | Await INTK-064 ownership clearance: one actual restricted-Web Core/EF create/replay/get test using existing ConnectedContextFactory; Worker create denied. |
| Modify | tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs | Add reserved new migration to existing exact pending-list assertion only; retain DOCS-020 and PLAT-072 entries. |
| Modify | docs/design/test-ui/pages/administration-principal-create--default.html | Root-generated scoped actual Razor capture output. |
| Modify | docs/design/test-ui/pages/administration-principal-settings--default.html | Root-generated scoped actual Razor capture output. |
| Modify | docs/design/test-ui/index.html | Only conditional generation from these two routes after TICK-085 narrow ownership handoff; preserve all other scenarios. |

## Do not modify

- src/Pegasus.Infrastructure/DependencyInjection.cs
- src/Pegasus.Core/Intake/PrincipalMailRoutePolicy.cs
- src/Pegasus.Core/Reports/CaseReportDeliveryPreparation.cs
- src/Pegasus.Web/Pages/Administration/Principals/Settings.cshtml.cs
- src/Pegasus.Web/Pages/Inbox/**
- src/Pegasus.Web/Pages/Cases/**
- docs/operator-notes.md
- docs/frd/frd-07-eva-and-external-engineering-handoff.md
- docs/design/test-ui/pages/case-details*.html
- corpus/**

## Constraints

No new DI/service, command, edit-contact feature, schema stream, package,
route, domain remap, test exception, QDOS impersonation, mail change, backfill
or live customer/send. Use pegasustest and digital@collisionengineers.co.uk
only for positive customer/contact fixtures; malformed values are structural
validation probes, not fabricated domain instructions. No other positive
test-recipient address is introduced. Eventual live test To must be that one
address only, Cc empty, under root's separate execution.

INTK-064 owns AzureSqlRuntimeRoleMigrationTests.cs. TICK-085 owns the
conditional generated index while Verifying. Root must record precise
clearance; no historic claim forcing. Migration/model files were not in those
current maps, but recheck all claims and the next migration id before take.
A fresh authorized packet must use branch PLAT-050-principal-contact and
worktree .worktrees/plat-050 from the latest accepted origin/dev; do not reuse
or mutate TICK-085's retained author tree.

## Ordered steps

1. After root approval and ownership clearance, take the real isolated packet;
   confirm root/branch/common Git directory, accepted base and migration
   reservation. Amend FRD-04/design and the existing Core creation/read
   contracts/normalizer only as specified.
2. Extend the existing atomic EF create/hash/history and projections; add
   nullable migration/Designer/snapshot. Add its id to the existing exact
   pending-migration test list, retaining all previous entries.
3. Extend Create and populated Settings display using existing labels/markup.
   Preserve all existing credential/EVA/location actions and authorization.
4. Extend existing Core, persistence and Web fixtures. Add one restricted-Web
   real Core/EF create/replay/query probe using the role-test class's existing
   connection factory, plus Worker creation refusal. Prove upgrade over a
   populated customer without contact, nullable read, create with contact,
   and Down removing only that column in the disposable migration fixture.
5. Freeze source and report exact focused filters to root. Root alone runs
   compiler/runtime and two-route snapshot capture/update/verify/catalogue.
   Preserve every failed attempt. Author changes nothing until results return;
   after accepted evidence follow the fresh gates to independent review.

## Acceptance checks

- Core: null/blank accepted absent; permitted address trims and remains exact;
  malformed, multiple/display-name, control-bearing and overlong values
  refuse; non-Administrator cannot reach the store.
- Persistence: create/get/list carry exact contact, one customer/principal/
  lineage/receipt/history; exact replay unchanged, changed-contact same key
  refused without writes; duplicate name/code remains atomic; contact
  survives replacement with the same customer identity.
- Web: real authenticated POST -> Core -> EF -> PRG; GET Settings shows exact
  contact; blank creates without empty contact fact; invalid value keeps form
  without creating a row; non-Administrator refusal remains. Existing
  credential show-once/replay/stale/auth and manual/location assertions stay.
- Schema: migration Up/Down and pending list are correct; real restricted
  Web role can use the existing path; Worker cannot create customers.
  Bootstrap/table-grant source census unchanged; no new table or role.
- Only two scoped current Razor snapshots may change, plus generated index
  if needed. No manual visual or live send/deployment claim follows from them.

## Commands

Proposed root verification, cwd the approved ticket worktree, PowerShell 7:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Pegasus.Core.Tests.Cases.OrganizationAdministrationTests" --logger "trx;LogFileName=plat-050-core.trx"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OrganizationAdministrationPersistenceTests|FullyQualifiedName~OrganizationAdministrationWebTests|FullyQualifiedName=Pegasus.IntegrationTests.AzureSqlRuntimeRoleMigrationTests.PrincipalContactCreationUsesExistingWebRuntimePermissions|FullyQualifiedName=Pegasus.IntegrationTests.CaseWorkflowMigrationTests.CustodyEvidenceOrdinalsAndOperationsMigrateFromPreviousSchemaWithoutIdentityLoss" --logger "trx;LogFileName=plat-050-integration.trx"
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1
git diff --check
```

Root sets PEGASUS_TEST_UI_CAPTURE_DIR to the absolute path of the ticket's
artifacts/test-ui-capture directory (the existing script's capture root) and
PEGASUS_TEST_UI_SCOPE to
administration-principal-create,administration-principal-settings for that
same Web-test run; UI mode unset. Then reuse that capture:

```powershell
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -SkipCapture -Scope "administration-principal-create,administration-principal-settings"
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope "administration-principal-create,administration-principal-settings"
pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1
```

The method name for the new role test above is the planned exact name.
Root schedules final integrated CI once under EPIC-014; do not duplicate a
whole suite per lane. No commands in this plan have been run as validation.

## Failure and deviation rules

Any unresolved file owner, migration collision, unknown consumer, additional
schema/DI need, governing conflict or failure stops the affected work and is
reported in one consolidated batch. Never weaken existing assertions, copy
permission lists as caller proof, add a fake provider route or silently
expand into mail/contact editing.

## Stop condition

This assignment stops after whole research/map/plan/checklist readback for
root approval, still Preparing and untaken. After a separately authorized
execution, stop source-frozen for root checks, then at Review for independent
review when root permits publication. No self-review, merge, Done, cleanup,
live Principal, live e-mail, cloud or deployment in this author lane.
