# PR 671 hunk/behavior disposition against the integration base

Read-only analysis (Wave 0, 2026-09-06, Opus 5 under claude-fable-c).

## 1. Header

| Item | Value |
| --- | --- |
| PR | #671 "Show an optional known principal on image-initiated cases (CASE-045)", branch `task/case-045-image-initiated-principal`, base `dev`, OPEN, `mergeable: CONFLICTING` |
| Pinned SHA / live head | `743311a0f4ac68794672510e596abd7d89ae47bb` — identical, no drift |
| D | `3284f93fc3ea9fd3bbbea9405ec92dc7818378f2` |
| Merge base | `a2658300eaed31b5255085a596ceb83b75d6fc06` (CASE-042 #663, 2026-09-05); D is 28 commits ahead |
| Comments | none (REST `/pulls/671/comments` and `/issues/671/comments` empty) |

Commits: `0e9ff68cc` optional nullable principal contract; `591fe538f` persist and project; `a077dff16` show/set on detail page; `743311a0f` principal on Awaiting row/quick view, no-N+1 proof. 19 files, +8 275/−34 (7 641 are the migration Designer).

Files: `docs/design/test-ui/pages/vehicle-images-details--default.html`; `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`, `ImageIntakeLifecycle.cs`; `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`, `ImageIntakeEntities.cs`, `Migrations/20260905082255_ImageIntakePrincipal(.Designer).cs`, `PegasusDbContextModelSnapshot.cs`, `PegasusDbContext.cs`; `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`, `Pages/ImageIntake/Details.cshtml(.cs)`, `Presentation/OperatorLabels.cs`; tests `ImageIntakeLifecycleTests.cs`, `ImageIntakePersistenceTests.cs`, `ImageIntakeWebTests.cs`, `IntakePersistenceIntegrationTests.cs`, `IntakeWebTestSupport.cs`, `TriageQueuesWebTests.cs`.

## 2. Already at D

Nothing of CASE-045 is at D (no `PrincipalId`/`PrincipalCode`/`ListActivePrincipals`/`SetPrincipal` hit in any ImageIntake file). Reusable building blocks at D: `Principal` record (CaseContracts.cs:19–30), `PrincipalEntity` (PegasusDbContext.cs:1088–1107) and its config (:433–476; `InspectionMode`/`EvaManualSubmission`/`EvaAutomaticSubmission` have DB defaults), `EfOrganizationAdministration.ToPrincipal` (:693), `ImageIntakeRecord` (ImageIntakeContracts.cs:19–31), `ImageIntakeSummary` (:108–120), `ImageIntakeDetail` (:126–160), `IImageIntakeQueries` (:194) / `IImageIntakeStore` (:251, `ListHistoryAsync` default :282), `ImageIntakeLifecycleRules` (ImageIntakeLifecycle.cs:35–90), `ImageIntakeEntity` (ImageIntakeEntities.cs:8–52), `ImageIntakes` config (PegasusDbContext.cs:679–711), `EfImageIntakeStore` (`ListHistoryAsync` :527, `ProjectAsync` :858, `GetDetailAsync` :833, `ToDetailAsync` :844–855, `Map` :1129–1144), `/VehicleImages/{id}` page (Details.cshtml :66–110; DetailsModel :24–54), `OperatorLabels` (:34–36), B-owned `IndexModel.ImageRow` (Cases/Index.cshtml.cs:560–589), latest D migration `20260905010654_CaseSignOffEngineer`, census list (IntakePersistenceIntegrationTests.cs:120–124), `IntakeWebApplicationFactory` without any interceptor hook (IntakeWebTestSupport.cs:28–99).

Why CONFLICTING: only the snapshot (A-owned, regenerated), the census line (A-owned) and `OperatorLabels.cs` changed at D; the latter does not textually conflict (D edits at 1474–1613, branch inserts at 34–36). All C-owned ImageIntake files are untouched at D since the fork.

## 3. Disposition table

### C-owned Core contracts

| # | File | Hunk | Behavior | Disposition / target at D |
| --- | --- | --- | --- | --- |
| C1 | `ImageIntakeContracts.cs` | :1 | `using Pegasus.Core.Cases;` | retained-C |
| C2 | " | :29 | `ImageIntakeRecord` gains `Guid? PrincipalId = null` (last optional positional) | retained-C; append after `PendingExternalWorkId` (:31) |
| C3 | " | :119 | `ImageIntakeSummary` gains `string? PrincipalCode = null` | retained-C; after `Source` (:120) |
| C4 | " | :136 | `ImageIntakeDetail` gains `string? PrincipalCode = null`; summary prose corrected | retained-C |
| C5 | " | :186 | `SetImageIntakePrincipalRequest(Guid ImageIntakeId, Guid? PrincipalId, ActionActor Actor, long ExpectedVersion)` — expected-version only, no operation key/replay probe | retained-C; after `CloseImageInitiatedCaseRequest` (:177–181); re-word obsolete "image-initiated case" comment |
| C6 | " | :239 | `IImageIntakeQueries.ListActivePrincipalsAsync` default `[]` (justified: `IOrganizationAdministrationQueries` is paginated and gated behind `ManageOrganizationsAndPrincipals`) | retained-C with change: no silent `[]` default — use the file's `NotSupportedException` idiom (:282–296) or no default |
| C7 | " | :258 | `IImageIntakeStore` summary corrected | retained-C; re-word |
| C8 | " | :295 | `IImageIntakeStore.SetPrincipalAsync` default `NotSupportedException` | retained-C |
| C9 | `ImageIntakeLifecycle.cs` | :34 | `ValidateSetPrincipal`: null request/actor rejected; `StaffAuthorization.Require(actor, PerformCasework)`; empty id rejected; `Guid.Empty` principal rejected, null allowed; negative version rejected | retained-C; insert before `ValidateMerge` (:42), reuse `RequireId` (:83) |

### C-owned store

| # | Hunk | Behavior | Disposition |
| --- | --- | --- | --- |
| C10 | `EfImageIntakeStore.cs` :3 | `using Pegasus.Core.Cases;` | retained-C |
| C11 | :525 (48 lines) `SetPrincipalAsync` | serializable tx; load or `KeyNotFoundException`; `LifecycleVersion != ExpectedVersion` → `DbUpdateConcurrencyException`; non-null principal must be an active `Principals` row else `InvalidOperationException`; write + bump `LifecycleVersion` only on change; returns `Map(entity)` | retained-C with two flags: (a) reuses `LifecycleVersion` as the token (a principal save invalidates an open Merge/Close form; re-confirm deliberately); (b) inactive check runs before the no-change check, so re-submitting an already-stored principal that has since gone inactive throws — move the equality check first. Re-word "Image-initiated Case" message. Insert between `CloseAsync` and `ListHistoryAsync` (:527) |
| C12 | :560 `ListActivePrincipalsAsync` | `AsNoTracking().Where(IsActive).OrderBy(Code)` mapped with `ToPrincipal` | retained-C |
| C13 | :896 `ToDetailAsync` | second `Principals … SingleAsync` round trip for the code | retained-C with change: project through the same query / `Principal` navigation, no second round trip |
| C14 | :929 `ProjectAsync` | row gains `PrincipalCode = intake.Principal != null ? intake.Principal.Code : null` — LEFT JOIN in the one set-based projection | retained-C — load-bearing no-N+1 hunk (needs A1/A3 to compile) |
| C15 | :993 | summary materialisation passes `row.PrincipalCode` | retained-C |
| C16 | :1199 `Map` | `PrincipalId: entity.PrincipalId` | retained-C |

### A-owned schema (handoff C-F06)

| # | File | Behavior | Disposition |
| --- | --- | --- | --- |
| A1 | `ImageIntakeEntities.cs` :25 | `Guid? PrincipalId`, `PrincipalEntity? Principal` | retained-A-schema; after `ImageIntakeReference` (:27) |
| A2 | `PegasusDbContext.cs` :693 | `HasIndex(item => item.PrincipalId)` | retained-A-schema (:692–697) |
| A3 | `PegasusDbContext.cs` :709 | `HasOne(Principal).WithMany().HasForeignKey(PrincipalId).OnDelete(Restrict)` | retained-A-schema (:703–710) |
| A4 | `20260905082255_ImageIntakePrincipal.cs` | Up: `AddColumn<Guid>("PrincipalId","ImageIntakes","uniqueidentifier", nullable: true)`; `CreateIndex("IX_ImageIntakes_PrincipalId")`; `AddForeignKey("FK_ImageIntakes_Principals_PrincipalId", …Restrict)`; Down drops all three; no backfill | rejected (migration): regenerate at D; content is the specification |
| A5 | Designer (7 641 lines) | generated | rejected |
| A6 | `PegasusDbContextModelSnapshot.cs` | property/index/FK/navigation | rejected; conflicts with D (`SignOffEngineerId`, removed `UX_EvaSubmissions_CaseDelivered`); regenerate |
| A7 | `IntakePersistenceIntegrationTests.cs` :120 | appends `"20260905082255_ImageIntakePrincipal"` to the census | superseded by D's `CaseSignOffEngineer` line; A appends the regenerated id after it |
| A8 | same :169 | `sys.columns`/`sys.foreign_keys` assertions: `PrincipalId` nullable, FK `NO_ACTION` | retained-A-schema |
| A9 | `IntakeWebTestSupport.cs` :10, :38, :73, :200 | optional `DbCommandInterceptor? commandInterceptor` ctor param wired in `ConfigureWebHost` by replacing the host's `IDbContextFactory<PegasusDbContext>` (not the private schema provider) | retained-A-schema (shared test support); required by C's read-count test |
| A10 | grants/DI | no hunk; ticket evidence says both runtime roles already hold UPDATE on `ImageIntakes` (`scripts/Invoke-AzureDatabaseBootstrap.ps1:313–317`) and SELECT on `Principals` (`Migrations/20260729199000_RuntimeRoleReconciliation.cs:252,:289`) | n/a — A should re-verify at D; `Test-MigrationGrants.ps1` checks only tables a migration creates |

### C-owned Web

| # | File | Behavior | Disposition |
| --- | --- | --- | --- |
| C17 | `OperatorLabels.cs` :33 | `ImageIntakePrincipal = "Principal"`, `ImageIntakePrincipalNotKnown = "Not known"` | retained-C (no conflict) |
| C18 | `ImageIntake/Details.cshtml` :87 | `Principal` definition fact rendering `PrincipalCode ?? NotKnown`, always drawn | retained-C; between Lifecycle state (:85) and Case association (:86–95) |
| C19 | same :111 | `image-intake-principal-title` panel: `<select name="principalId">` with first option `value=""` = `Not known` (selectable), active principals from `PrincipalOptions`, hidden `expectedVersion` = `LifecycleVersion`, Save → `asp-page-handler="Principal"` | retained-C; between Record panel (:108) and Preserved origin (:110) |
| C20 | `Details.cshtml.cs` :24, :45 | `PrincipalOptions` populated from `ListActivePrincipalsAsync` in `OnGetAsync` | retained-C |
| C21 | same :51–92 | `OnPostPrincipalAsync(Guid id, Guid? principalId, long expectedVersion, ct)`: Forbid without actor; invalid model re-renders; success redirect; `ArgumentException` → field error; `DbUpdateConcurrencyException` → stale message; `InvalidOperationException` → summary error | retained-C; re-word "Image-initiated Case" to Image Intake terms (also C5/C7/C11) |
| B1 | `Cases/Index.cshtml.cs` :560–589 | `ImageRow` prepends `(ImageIntakePrincipal, principal)` fact and subtitle `"Principal: {value}"` | retained but B-owned — hand the exact hunk to Stream B; C3 is its only prerequisite |

### C-owned tests

| # | File | Behavior | Disposition |
| --- | --- | --- | --- |
| T1 | `ImageIntakeLifecycleTests.cs` :124 | `PrincipalAssignmentRequiresStaffAndValidIdentifiers`, `ImageIntakeRecordCarriesAnOptionalPrincipal` | retained-C; after `StaffCloseRequiresCaseworkReason` (:113–125) |
| T2 | `ImageIntakePersistenceTests.cs` :555 | `PrincipalAssignmentRoundTripsWithoutLifecycleHistoryOrInference`: absent → set → ALPHA → repeat no-op → inactive rejected → stale rejected → clear; history count unchanged | retained-C; after :498, before helpers :558 |
| T3 | same :605 | stray blank line | rejected |
| T4 | `ImageIntakeWebTests.cs` :148–232 | `StaffSetsReplacesAndClearsTheImageIntakePrincipal` + `PostPrincipalAsync`, `AssertPrincipalFact` (rejects `""`, `None`, `Unknown`, `Unassigned`) | retained-C; after :79–145 |
| T5 | same :153 | `ImageIntakeTestData.SeedPrincipalAsync(services, code, isActive)` raw inserts (legal at D thanks to DB defaults) | retained-C; use `using` directives instead of fully qualified names |
| T6 | `TriageQueuesWebTests.cs` :482 | `AwaitingRowsAndQuickDetailShowPrincipalWithoutIncreasingTheReadCount`: three rows, one GET `/Cases?tab=awaiting&selected={id}`, `Assert.Equal(14, ExecutedReaderCommands)`, `Principal: ALPHA` and `Principal: Not known`, quick detail `<dd>ALPHA</dd>` | retained-C but the `14` was measured at merge base `a2658300`, not D — re-measure at D before pinning |
| T7 | same :780 | `AwaitingRequestCommandCounter : DbCommandInterceptor` | retained-C |
| S1 | `docs/design/test-ui/pages/vehicle-images-details--default.html` | 78 lines | rejected (stale snapshot; see §6) |

## 4. Entity/field inventory for C-F06

| Entity | Field | CLR | Nullability | FK | Index | Config site at D |
| --- | --- | --- | --- | --- | --- | --- |
| `ImageIntakeEntity` (`ImageIntakes`) | `PrincipalId` | `Guid?` | nullable = `Not known`; no backfill/default/sentinel | → `Principals.Id`, Restrict (`NO_ACTION`), `FK_ImageIntakes_Principals_PrincipalId` | non-unique `IX_ImageIntakes_PrincipalId` | `ImageIntakeEntities.cs:27`; `PegasusDbContext.cs:696` (index), `:710` (FK) |
| `ImageIntakeEntity` | `Principal` | `PrincipalEntity?` | navigation, `.WithMany()` no inverse | — | — | `ImageIntakeEntities.cs:28`; `PegasusDbContext.cs:709–712` |

SQL type `uniqueidentifier`; no own concurrency token (reuses `LifecycleVersion`). Migration content as A4. Regenerate at D (branch Designer generated at the merge base). Grants: ticket says none needed; not confirmed at D by this analysis. No DI change. A9 interceptor hook and A8 assertions are A-owned; T6 needs A9.

## 5. Retained behaviors as testable statements

Core unit (ImageIntakeLifecycleTests.cs): (1) record without principal has `PrincipalId == null`, with one carries it; (2) `ValidateSetPrincipal` accepts staff + non-empty id and accepts null (clear); (3) non-staff actor → `StaffAuthorizationException`; (4) `Guid.Empty` id/principal → `ArgumentException`, negative version → `ArgumentOutOfRangeException`.
Store (ImageIntakePersistenceTests.cs): (5) set → record with id, `LifecycleVersion + 1`, detail code ALPHA, summary `PrincipalCode == "ALPHA"`; (6) replace; (7) clear; (8) same value at current version → same version, no write; (9) inactive → `InvalidOperationException`, nothing stored; (10) stale version → `DbUpdateConcurrencyException`; (11) `ListActivePrincipalsAsync` ordered by Code, excludes inactive; (12) `ListHistoryAsync` count unchanged, nothing inferred from matches/links.
Web (ImageIntakeWebTests.cs): (13) `<dt>Principal</dt><dd>Not known</dd>` exact; (14) select offers every active, never inactive, first option `value=""` selectable; (15) set/replace/clear over HTTP with antiforgery, 302 each, GET shows the value.
Queue (TriageQueuesWebTests.cs, needs A9): (16) `Principal: ALPHA` and `Principal: Not known` in row subtitles, `<dd>ALPHA</dd>` in quick detail; (17) Awaiting counts unchanged (`NotReadyAndAwaitingRailCountsMatchTheirRows` :103, `AwaitingCountExcludesReceiptLinkedBeforeMergeSynchronises` :546 still pass); (18) `ExecutedReaderCommands` equals the D-measured baseline with 3+ mixed rows.
Schema (A): (19) `sys.columns` nullable + FK `NO_ACTION`; (20) census ends with `CaseSignOffEngineer` then the regenerated migration; no pending migrations.

## 6. Test UI snapshot — not imported

`docs/design/test-ui/pages/vehicle-images-details--default.html` (78 lines): A-owned catalogue and explicitly excluded; also stale beyond CASE-045 (lifecycle state flips to `Awaiting definitive instruction`, association becomes `None`, a close dialog appears, the History panel is deleted). `docs/design/test-ui/**` unchanged at D since the fork; drift is branch-side; capture nondeterminism vs fixture change undeterminable read-only. `cases--default.html` was not regenerated by the branch (catalogued `/Cases` states do not exercise the Awaiting tab); A should re-run capture when B lands B1.

Cross-cutting: obsolete "image-initiated case" wording in C5/C7/C11/C21; `LifecycleVersion` reuse is deliberate per the branch plan but needs re-confirmation; C14 needs A1/A3 first; PR body records a Codex quota outage mid-task and three post-hoc corrections, the third (hard-coded 4 → measured 14) is base-dependent and must be redone at D.
