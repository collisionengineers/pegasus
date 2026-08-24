# Research — collapse the EVA hand-off into Export

Every claim below was produced by a grep or a file read in the task worktree
`../pegasus-worktrees/eng-016` (branch `task/eng-016-collapse-handoff-into-export`,
based on `task/eng-015-eva-field-values`). Assumptions are marked as such.

## What I searched for

`EvaHandoffRevisions`, `EvaHandoffDownloadOperations`, `EvaHandoffOperations`,
`EvaFirstHandoffProxies`, `IGenerateEvaHandoff`, `IDownloadEvaHandoff`,
`IEvaHandoffQueries`, `IEvaHandoffPersistence`, `EvaHandoffPolicyAuthority`,
`EvaHandoffPreparation`, `GenerateEvaHandoff`, `IExportCaseBundle`,
`MapForProduction`, `MapForOperatorExport`, `MapAcceptedCase`,
`IEvaHandoffProxy`, `EvaEvidenceStatus.Corrected`, `ExportDateSource`,
`IsAccepted` / `IsResolved`, `canExport`, `Invoke-AzureDatabaseBootstrap`,
`removedTables` — over `src/`, `tests/`, `scripts/`, `docs/`, `infra/`,
excluding `obj/`, `bin/`.

## Branch history — two claims in the brief are wrong, checked

- **PLAT-042 (PR #531) is NOT in this branch's history.** `9b23ece2 Bind the
  additive-migration rule to cutover, not to today (PLAT-042)` exists only on
  `task/plat-042-additive-rule-at-cutover`. `git merge-base --is-ancestor` →
  not an ancestor. `docs/runbook.md:1140` on this branch therefore still reads
  the unamended *"Releases keep migrations additive so the previous
  application runs against the newer schema; a migration that cannot honour
  that must ship an accepted recovery strategy instead."* This branch must
  satisfy the **unamended** rule, and say so in the PR rather than leaning on
  an amendment it does not carry.
- **DOCS-013 (PR #526) is NOT in this branch's history either.** It branches
  from the same base `a6acc782` as ENG-014's chain, so it is a **sibling**, not
  an ancestor. Its FRD-07 rewrite is not present here. Consequence: my FRD-07
  edits and DOCS-013's will collide when both merge. Recorded, not worked
  around.

History actually carried (newest first): `8156708b`, `ac749f8c`, `55089cf4`,
`c3cceb67`, `97159d92` (ENG-015) → `c1beaf3a`, `bb3d79c3`, `dc19f867`,
`3c274c1f`, `e65956a3` (ENG-014) → `a6acc782` (dev).

## Consumer inventory

### `EvaHandoffRevisions` — deleted

Non-migration, non-Designer consumers, complete:

| File | Use |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs:58` | `DbSet` |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffModelConfiguration.cs:9-31` | entity + 2 check constraints + 2 unique indexes + FK to `Cases` |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs:123,166,217,256,376,564,570,589` | read/insert, all inside hand-off methods being deleted |
| `tests/Pegasus.IntegrationTests/EvaHandoffPersistenceTests.cs` (whole file) | the hand-off's own suite |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs:1317` | asserts an **export** writes no revision — assertion becomes meaningless, the table is gone |
| `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs:53,127` | **pinned historic spec** — asserted after `MigrateAsync("20260729199000_RuntimeRoleReconciliation")` (line 461), not at HEAD. **Must not change.** |
| `docs/operations.md:410` | as-built fact (the table is empty) |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1:244` | comment only |

### `EvaHandoffOperations` — deleted

`PegasusDbContext.cs:64`, `EvaHandoffModelConfiguration.cs:33-50`,
`EvaHandoffStore.cs:361,629`, `EvaHandoffPersistenceTests.cs:59,86,423`,
`AzureSqlRuntimeRoleMigrationTests.cs:52,126` (pinned, unchanged),
`Invoke-AzureDatabaseBootstrap.ps1:244` (comment).

### `EvaHandoffDownloadOperations` — deleted

`PegasusDbContext.cs:62`, `EvaHandoffModelConfiguration.cs:79-99`,
`EvaHandoffStore.cs:200,278`, `EvaHandoffPersistenceTests.cs:424`,
`CaseWorkflowMigrationTests.cs:117` (**asserts the table exists after a full
migrate — will fail**), `IntakePersistenceIntegrationTests.cs:82` (names the
grant migration in an applied-migration census — that migration file stays, so
this line stays), `Invoke-AzureDatabaseBootstrap.ps1:241-251` (**live expected
grant matrix — will be wrong**), `docs/operations.md:327,411,709`.

Not in `AzureSqlRuntimeRoleMigrationTests`'s pinned spec — it was created later
by `20260811122654_CaseCustodyEvaRecovery`.

### `EvaFirstHandoffProxies` — **survives**, rewired

| File | Use |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs:78` | **the reason it survives** — "sent to engineer today / week" |
| `EvaHandoffModelConfiguration.cs:52-77` | PK on `CaseId`; the two `CK_EvaFirstHandoffProxies_*` constraints; **unique index on `RevisionId`**; **required FK to `EvaHandoffRevisions`** (`:73-76`) |
| `EvaHandoffStore.cs:119,379,574,607` | read/insert, all inside deleted methods |
| `CustodyOutboxIntegrationTests.cs:1320` | asserts an export writes **no** proxy — **this assertion inverts** |
| `AzureSqlRuntimeRoleMigrationTests.cs:51,125` | pinned historic, unchanged |

Verified against `docs/design/README.md`'s Operations shell block: *"New cases
today | Sent to Engineer: today / week | Reports sent: today / week"* — the
dashboard tile is real and specified.

Two entity columns have **no source in an export** once the hand-off is gone:
`RevisionId` (points at a deleted table) and `OperationKey` (an export carries
no operation key; the once-per-case idempotency is the `CaseId` primary key
itself). Both go. The two check constraints touch neither column, so they hold
unchanged — verified by reading `EvaHandoffModelConfiguration.cs:56-62` and the
`CreateTable` in `20260729182000_EvaHandoffPersistence.cs:82-84`.

### Routes, DI, MCP, UI

| Surface | Location |
| --- | --- |
| Hand-off download route `POST /Cases/{id:guid}/Eva/Download` | `src/Pegasus.Web/Pages/Cases/Eva/Download.cshtml` + `.cshtml.cs` |
| Hand-off generate handler | `src/Pegasus.Web/Pages/Cases/Vehicle.cshtml.cs:87-148` (`OnPostGenerateEvaHandoffAsync`) |
| EVA panel | `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml:384-432` |
| MCP `pegasus_eva_bundle_generate` | `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs:357-412` |
| MCP `pegasus_eva_handoff_status` | `AssessmentMcpTools.cs:415-467` |
| MCP result records | `AssessmentMcpTools.cs:96-133` (`EvaBundleGenerateToolResult`, `EvaHandoffImageToolItem`, `EvaHandoffRevisionToolItem`, `EvaHandoffStatusToolResult`) |
| DI | `src/Pegasus.Infrastructure/DependencyInjection.cs:395-402` |
| Composed onto `CaseDetails` | `src/Pegasus.Core/Cases/CaseQueries.cs:124,268,279-280,310,316,340` |
| Export route | `src/Pegasus.Web/Pages/Cases/Documents/Export.cshtml` (`@page "/Cases/{caseId:guid}/Documents/Export"`), handler `OnGetAsync` at `Export.cshtml.cs:30-82` |
| Export control | `src/Pegasus.Web/Pages/Cases/Details.cshtml:119-130` — a plain `<a class="btn">`, no form, no token |

`Pegasus.Core/Reports/AssessmentReportProjection.cs:302` mentions
`EvaHandoffPreparation` in a doc-comment only — a comment fix, no code.

### The evidence bar — confirmed single caller

`CaseEvaMapping.MapForProduction` has **exactly one** caller:
`EvaHandoffStore.MapAcceptedCase` (`:834-840`), itself called only from
`GetPreparationAsync` (`:53`) and `GenerateAsync` (`:446`). Both are deleted.
Nothing else in `src/` or `tests/` names it — the only other hit is a prose
reference in `EvaBundleSchema.cs:631`.

So `MapForProduction` and its private `ValidateAcceptedEvidence` (`:312-348`)
are dead on deletion, not merged. `MapForOperatorExport` survives as the one
mapping. Consequence, stated plainly: **the thirteen-field fail-closed guard is
gone**. A case with gaps is now exportable *and* recordable as sent to an
engineer. That is the operator's own decision for Export ([[CASE-019]],
2026-08-22, *"A blank field does not block the download"*), applied to one act
instead of two.

### The Review gate is UI-only — pre-existing, verified, not fixed here

`Details.cshtml:36` computes `canExport = workflow.State == Review` and its own
comment claims *"The rule is a Core precondition; this control reflects it."*
**It is not.** `IExportCaseBundle.ExecuteAsync` (`EvaHandoffStore.cs:677-730`)
checks only: non-empty `CaseId`, `PerformCasework`, case data exists, case row
exists, mapping switched on, ≥ 1 eligible image. No lifecycle check. The
hand-off's lifecycle gate lives in `EvaHandoffPolicy.Evaluate`
(`EvaBundleSchema.cs:491-524`), which the export path never called.

This ticket does not change it — closing it would mean re-imposing part of the
bar the ticket deletes, and choosing which part is a product decision. Recorded
as a finding for the PR and a follow-up ticket, not silently expanded into.

### `EvaHandoffPolicyAuthority` and `IEvaHandoffPersistence` become dead

`IEvaHandoffPersistence` declares exactly two methods, `GenerateAsync` and
`DownloadAsync`, both deleted. `EvaHandoffPolicyAuthority` exists only to be
passed to those two (`EvaBundleSchema.cs:153-165`), and is constructed only by
`GenerateEvaHandoff`/`DownloadEvaHandoff`. With both gone the authority has no
caller. The export path already calls the Core policy statically
(`EvaHandoffStore.cs:772`, `EvaHandoffPolicy.SelectEligibleImages`), so the
indirection has nothing left to protect. Deleting it follows the repo's own
*"No abstraction without a second concrete caller"* rail rather than adding
scope: it is a consequence of the deletions the ticket names.

`EvaHandoffCommandPolicy` (`EvaBundleSchema.cs:289-360`) likewise only serves
`GenerateEvaHandoff`/`DownloadEvaHandoff` and goes with them.

`IEvaHandoffProxy` / `LocalEvaHandoffProxy` **stay** — they are the
no-delivery-claim guarantee. `EvaHandoffProxyRequest.Revision`
(`EvaBundleSchema.cs:527-532`) and its `request.Revision <= 0` guard
(`LocalEvaHandoffProxy.cs:19`) lose their meaning with revisions gone; the
field is dropped rather than fed a fabricated `1`.

### Antiforgery — no new mechanism needed

Grep for `antiforgery` across `src/` returns only two `[IgnoreAntiforgeryToken]`
error pages, one `[ValidateAntiForgeryToken]`, and two explicit
`@Html.AntiForgeryToken()` calls inside JS-driven dialogs. Every other POST in
the app relies on Razor Pages' built-in auto-validation plus the form tag
helper's hidden token. The existing convention on this very page is
`Details.cshtml:103-109` (`ClaimLease`): `<form method="post"
asp-page-handler="…" class="record__bar-form">` with `<button type="submit"
class="btn">`. Export reuses that shape exactly.

### Migration mechanics — verified, not assumed

- **`scripts/Test-MigrationGrants.ps1`** only inspects tables matched by
  `CreateTable(` **inside the `Up()` body** (regex at `:62-73`). A migration
  that only drops tables adds no obligation. `CreateTable(` in my `Down()` is
  explicitly excluded by the comment at `:59-61`. **Stays green with no edit.**
- **`scripts/Test-AzureDeploymentPlan.ps1:295-309`** requires that every
  post-baseline migration containing `GRANT ` be named in
  `Invoke-AzureDatabaseBootstrap.ps1`. My migration contains no `GRANT` (SQL
  Server drops a table's grants with the table), so it adds no obligation —
  **but `20260819180000_GrantEvaHandoffDownloadOperations.cs` stays in the
  folder and still contains `GRANT`, so the bootstrap script must keep naming
  it.** Editing that block must not delete the migration name.
- **`Invoke-AzureDatabaseBootstrap.ps1:56-67`** already has the exact mechanism
  for this: a `$removedTables` list of migration filenames whose
  `DropTable(name: "X")` matches are excluded from the reconciliation baseline
  matrix. Three migrations already use it. Mine joins the list — reuse, not
  invention. The separately-added `EvaHandoffDownloadOperations` block
  (`:241-251`) is not baseline-derived and must be handled by hand.
- **`AzureSqlRuntimeRoleMigrationTests`'s specs are pinned to a historic
  migration**, verified by reading `:455-503`: `MigrateAsync(PreviousMigration)`
  then `MigrateAsync(RuntimeRoleMigration)` then the assertions. They describe
  2026-07-29, not HEAD. **No edit.**
- Historic `*.Designer.cs` files are regenerated only for the newest migration.
  `dotnet ef migrations add` writes one new `.Designer.cs` plus the snapshot;
  **no historic Designer is touched.**
- ENG-014's `20260824090400_DropEvaHandoffProvenanceAndManifest` drops three
  columns from a table this ticket drops entirely. Superseded but harmless and
  ordered before mine. **Not unpicked** — unpicking it would rewrite a merged
  migration for no behavioural gain.

### Non-additive, under the *unamended* rule

`docs/runbook.md:1140-1144` (this branch): migrations stay additive so the
previous application runs against the newer schema, *"a migration that cannot
honour that must ship an accepted recovery strategy instead."* Dropping three
tables is not additive: an application built before this change SELECTs and
INSERTs `EvaHandoffRevisions` on the hand-off path. The recovery strategy is
that **the hand-off is switched off in production and the tables are empty** —
`docs/operations.md:410-411` states `EvaHandoffRevisions` and
`EvaHandoffDownloadOperations` are empty, and Release 23's note
(`docs/operations.md:~573`) records *"The EVA panel is hidden while the
hand-off is switched off — it was verified to gate bundle generation only and
never review or export."* So rolling the application back behind this migration
degrades only a capability that is off and has never produced a row. `Down()`
recreates the three tables (empty) and restores the two proxy columns, so the
schema itself is reversible. Capability named in the PR as the rule requires:
**EXT-03**.

## Inherited ENG-014 review findings

- **F1** — two false comments. `CaseEvaMapping.cs:147-153` ("so
  provenance.json says where the value came from") and
  `infra/modules/platform.bicep:433` ("it is written into every exported
  provenance.json"). Comment-only, both in reach. **Taking both.**
- **F2** — `EvaEvidenceStatus.Corrected`. Readers, complete: `IsAccepted`
  (`CaseEvaMapping.cs:32`, treats `Corrected` and `Accepted` identically),
  and the four sites in `EvaHandoffStore.cs` (`:915-918`, `:1043`, `:1069`,
  `:1079-1082`) that *write* it. It is copied into
  `EvaFieldProvenance.Status`, which after F3 nothing reads. Once
  `MapForProduction` goes, `EvaAddressResolution.IsResolved`
  (`CaseEvaMapping.cs:39`) loses its only reader
  (`ValidateAcceptedEvidence`), and `EvaEvidenceValue.IsAccepted` keeps one
  (`Combine`, `EvaHandoffStore.cs:1081`) — which cannot tell the two apart
  either. **`Corrected` cannot change any observable output. Answer: remove
  it.** See the plan for the decision as written.
- **F3** — `ValidateSource` (`EvaBundleSchema.cs:613-676`) builds a 13-entry
  `EvaFieldProvenance[]` and returns it; `CreateOfflineReplay` reads only
  `normalizedSource.Fields` (`:600`). The per-entry **throws** are load-bearing;
  the array construction is not. **Taking the cheap half**: keep every throw,
  stop rebuilding the array.
- **F4** — a test comment at `EvaBundleContractTests.cs:141-146`. Out of this
  ticket's subsystem changes; **skipping**, named in the PR.
- **F5** — a golden-file test against `reference/eva_information/`. A new test
  asset and a new suite: real work, not a cheap fold-in. **Skipping**, named in
  the PR, worth its own ticket.
- **F6** — `docs/current-architecture.md:526` carries rationale rather than
  as-built fact. In a file this ticket must rewrite anyway. **Taking.**
- The CRLF-pin note (*CI does not guard the pin; production is Linux while every
  layout test is Windows*) is carried into the PR body as a caution, no code.

---

## Operator resolution and live re-audit — 2026-08-24

### Question

Re-evaluate ENG-016 after the operator clarified that manual Export is the current send-to-Engineer route, a future EVA API is the second route, and direct estimating-system integrations plus Pegasus engineering/reporting eventually replace EVA. Export must fail closed until Pegasus has everything required.

### Findings

- The earlier research conclusion is superseded. It said the permissive `MapForOperatorExport` bar should survive and the strict `MapForProduction` bar should be deleted. The operator has now explicitly chosen the opposite business result: one Export action, with the strict send-to-Engineer gate.
- `origin/dev` currently has two actions. `CaseEvaMapping.MapForProduction` requires an accepted case, confirmed completeness, resolved inspection mode/address, all thirteen non-empty accepted fields, source/version provenance, and accepted mapping. `MapForOperatorExport` allows suggested and empty fields and defaults a missing inspection date. ENG-016 currently deletes the strict method and calls the permissive method from the only surviving Export path.
- The lifecycle/custody half of the strict bar is also being deleted. `EvaHandoffPolicy.Evaluate` on `origin/dev` requires Review, current accepted evidence version, confirmed Case custody, Audit custody when applicable, accepted mapping, and at least one eligible image. ENG-016 currently leaves Review as a UI-only disabled button and enforces none of these server-side except mapping and at least one image.
- The repository documents disagree in two generations. Current `origin/dev` FRD-07 and current-architecture describe a strict hand-off plus a separate permissive operator export. ENG-016 collapses them but writes the permissive interpretation into FRD-07, capabilities, current-architecture, its ticket body, plan, tests and post-implementation report. `docs/operator-notes.md` and `docs/design/README.md` already preserve the strict result, but need the route model clarified: today's manual Export is the hand-off; EVA API is a future transport; direct estimate integrations/Pegasus engineering replace EVA later.
- CASE-019 is historical context, not current authority for ENG-016. Its 2026-08-22 decision explicitly called Export a read and not a hand-off. The operator's 2026-08-24 clarification supersedes that distinction for the target state.
- FRD-04 requires permanent action history for every download/export. ENG-016 records only the once-per-case `EvaFirstHandoffProxies` row. It writes no `ActionHistory` row for the first or any later Export. The existing `DocumentActionHistory` helper and `EfDocumentCustodyStore` export implementation are the repository convention: operation-keyed replay, attributed success history, structured evidence, and an atomic save.
- The first-sent proxy and action history are different facts. The proxy answers once per Case, "has Pegasus first sent this case to an Engineer route?" Action history answers for every successful Export, "who exported which exact package, when, through which policy/evidence version?" A second Export writes no second proxy but does write its own history event.
- ADR-0030 is already accepted on `origin/dev`. It explicitly permits dead tables/columns to be dropped before cutover, requires roll-forward rather than rollback, and accepts the interval between migration and new package activation. No expand/contract compatibility layer is required. The current migration/PR rationale is inaccurate only about blast radius: the old app's unconditional `CaseQueries.GetCase` projection reads `EvaHandoffRevisions`, so every case workspace can fail during that interval, not only EXT-03.
- The migration's `Down()` is local/disposable-schema tooling, not a production recovery promise. Once new proxy rows exist it cannot restore the required old revision FK without inventing data. The plan must stop calling this a safe rollback; production recovery is fix-forward under ADR-0030, while scratch-database up/down/up remains a development check only.
- Fresh fetch: ENG-016 is 53 commits behind `origin/dev` and 11 commits ahead. A no-worktree `git merge-tree HEAD origin/dev` predicts nine conflicts: `docs/capabilities.md`, FRD-07, `CaseEvaMapping.cs`, `EvaBundleSchema.cs`, QDOS instruction policy and its tests, `EvaHandoffStore.cs`, `QdosBoundaryContractTests.cs`, and a modify/delete conflict in `EvaHandoffPersistenceTests.cs`.
- Repository workflow permits merging `origin/dev` into the pushed task branch and forbids history rewriting. The conflict resolution must therefore be a normal merge, not a rebase or force-push. Take current `dev` wholesale for unrelated stacked changes, then reapply only ENG-016's focused final state.
- CI did run. On head `30bb2791`, the latest two repository-check runs were cancelled because the `changes` job has a five-minute timeout and its full-history `actions/checkout@v7` did not finish. Jobs depending on `changes` were then skipped. Documentation, reference-data and local-development-script jobs did pass. This is not a test failure and not evidence the branch passes; a new merge commit will trigger a fresh run.

### Implications

- Keep one Export route and one package builder, but reuse the existing strict mapping and eligibility policies server-side.
- Remove the permissive empty/default export behavior and its tests/result vocabulary.
- Add operation-keyed permanent history for every successful Export while keeping the first-sent proxy once per Case.
- Accept the pre-cutover migration window under ADR-0030, name the Case workspace impact accurately, and use roll-forward recovery; do not add compatibility code for a product that has not cut over.
- Merge `origin/dev` normally, resolve only the nine predicted conflicts, and prove the final diff contains ENG-016 rather than the 53 commits already on dev.
- Treat CI checkout cancellation separately from code correctness: push the resolved merge, let CI run, and only escalate the workflow timeout if the checkout failure repeats.

### Open questions

- None. The operator resolved the evidence bar and stated that production rollback compatibility is not a requirement before release/cutover.
