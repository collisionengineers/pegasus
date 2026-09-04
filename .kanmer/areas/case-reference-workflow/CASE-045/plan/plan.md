# Plan — CASE-045 (2026-09-04, gpt-5.6-terra high; revised after plan review)

Starting state: CASE-032's branch is `ed0dc6a…` and is not merged; CASE-042 has no remote branch yet. Execute only after both merge, re-check their exact heads, and apply this as a delta to CASE-042's Awaiting row/quick-detail shape. D51 settles stored nullable `PrincipalId`; no matching or creation policy is open.

Governing constraints: FRD-02 keeps image records pre-Case until association; FRD-12 makes Awaiting instruction a dedicated Pre-Case queue with its own row shape. D51 requires the exact `Not known` display exception. No explanatory copy, no disabled/inert control, no new packages, one Core owner, and no sender/registration/case-association inference.

Verified once, during planning, and not to be re-litigated: there is **no** principal-authenticated image-intake registration route today. The Provider API is principal-authenticated but calls `ISubmitProviderInstruction`, never `IRegisterImageIntake`. Therefore D51's second writer does not apply and **the detail-page staff setter is CASE-045's only new writer**.

1. Extend the image-intake contract and assignment boundary.

   - Reuse `ImageIntakeLifecycleRules` for staff casework authorization and expected-version validation; reuse the default-member pattern already present on `IImageIntakeStore` (`MergeAsync`/`CloseAsync`, `ImageIntakeContracts.cs:260-268`) so unrelated test fakes remain valid. Reuse the existing canonical `Principal` record (`src/Pegasus.Core/Cases/CaseContracts.cs:19`) as the option shape; add a smaller record only if a field it carries cannot be populated for this read, and say which.
   - Touch:
     - `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`
     - `src/Pegasus.Core/ImageIntake/ImageIntakeLifecycle.cs`
     - `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeLifecycleTests.cs`
   - Add nullable `PrincipalId` to `ImageIntakeRecord`, and the projected optional principal **code** to `ImageIntakeSummary` and `ImageIntakeDetail`.
   - Add a staff-only `SetPrincipalAsync` request that accepts either an active principal ID or `null` for `Not known`. **It carries the expected lifecycle version only — no operation key and no replay probe** (see step 2 for why). A stale version is rejected with the store's existing stale-write error.
   - Update the now-false contract prose in the same diff: `IImageIntakeStore`'s summary (`ImageIntakeContracts.cs:229-237`) says only the lifecycle columns change and only through `MergeAsync`/`CloseAsync`; `ImageIntakeDetail`'s summary describes only registration time and association as added context. Both must state that registration identity — source, VRM and reference — stays immutable while the optional principal changes only through this staff mutation.
   - Preserve lifecycle and intake policy: no change to `IntakeDecisionPolicy`, registration, automatic association, or `RegisterImageIntakeRequest`. Add no automatic writer.
   - Acceptance: null remains valid; an empty non-null ID, an inactive principal ID, a stale version, or a non-staff actor is rejected before persistence.

2. Persist the optional relationship and project it in bulk.

   - Reuse `EfImageIntakeStore.ProjectAsync`, `ToDetailAsync`, and `PrincipalEntity` (`PegasusDbContext.cs:1086`, which carries both `Code` and `IsActive`) as the canonical source of the display code.
   - Touch:
     - `src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs`
     - `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`
     - `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`
     - `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs`
   - Add nullable `ImageIntakeEntity.PrincipalId`, a restrictive FK to `Principals`, and the model index created for that FK.
   - **The assignment writes no lifecycle event.** `ImageIntakeLifecycleEventEntity` (`ImageIntakeEntities.cs:67-84`) is the lifecycle-state vocabulary: it requires an `EventType`, a `Reason`, and before/after versions, and its replay contract (`EfImageIntakeStore.cs:557`) returns the *current* record because merge and close are terminal. A principal is replaceable and clearable, so that replay would silently return a later assignment as if it were the original result, and the required `Reason` has no operator meaning here. Writing one would also put a second concept into the lifecycle-event list. Instead: set the column under the entity's existing optimistic version, reject a stale version with the existing error, and prove a stale write cannot overwrite a later assignment. Repeating the same assignment is naturally idempotent because it is the same state.
   - No active-principals list exists that ordinary staff may call: `IOrganizationAdministrationQueries` (`src/Pegasus.Core/Cases/OrganizationAdministration.cs:74`) is paginated and gated behind `StaffAccessRight.ManageOrganizationsAndPrincipals` (`:142`), so it cannot serve this page. Add a narrow active-principal options query on the existing image-intake read surface, ordered by `Code`, filtered to `IsActive`. Validate the selected ID is active at write time; preserve a previously recorded value even if that principal later becomes inactive.
   - Assignment must not transition lifecycle state, modify case association, or queue external work.
   - Add the principal code to the existing `ProjectAsync` SQL projection and detail read. Do not add per-row principal reads or a principal matcher.
   - Acceptance: assigned principal ID/code round-trips through record, detail, and summary; null round-trips unchanged; an inactive principal is neither offered nor accepted; a stale write is rejected; a registration match or linked Case never supplies or overwrites this field.

3. Add the nullable schema migration and record the verified permission census.

   - Reuse EF's generated migration/designer/snapshot workflow.
   - Touch:
     - `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_ImageIntakePrincipal.cs`
     - `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_ImageIntakePrincipal.Designer.cs`
     - `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
     - `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
   - Generate after merging the post-CASE-032/CASE-042 migration tail. The Up migration adds nullable `PrincipalId`, its FK/index; Down removes them. No backfill: existing rows remain null and display `Not known`.
   - Grant/census result, verified during this planning pass and re-verified in the plan review: both runtime roles already hold `UPDATE` on `ImageIntakes` (`scripts/Invoke-AzureDatabaseBootstrap.ps1:313-317`, carried from the PLAT-020 lifecycle-state grant) and both already hold `SELECT` on `Principals` (`Migrations/20260729199000_RuntimeRoleReconciliation.cs:252` and `:289`). A column on an existing table needs no new grant, so **no `Invoke-AzureDatabaseBootstrap.ps1` census edit is made** — and because none is needed, the EPIC-012 `scripts/*.ps1` no-touch rule wins over the owned-path allowance. If that premise turns out false during implementation, stop, report `waiting`, and let the controller file the tooling change.
   - `Test-MigrationGrants.ps1` is run as required smoke, but it is **not** proof of these verbs: it checks tables a migration creates (`scripts/Test-MigrationGrants.ps1:56`), and this migration creates none. Prove the verbs instead by running the focused existing runtime-role test (`tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs:628`) and by recording the static file:line evidence above in the implementation report.
   - Add the generated migration ID to the chronological applied-migrations assertion and assert the nullable column/FK schema.
   - Acceptance: migration applies cleanly, rollback is limited to dropping the optional value, and no runtime permission is widened.

4. Add the detail-page fact and staff assignment control.

   - Reuse `DetailsModel.OnPostCloseAsync` error/reload handling (`Pages/ImageIntake/Details.cshtml.cs:48-86`), `StaffPageModel.TryGetActor` (`Pages/StaffPageModel.cs`), the detail page's definition-list markup, and `OperatorLabels` as the sole owner of operator text.
   - Touch:
     - `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml.cs`
     - `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml`
     - `src/Pegasus.Web/Presentation/OperatorLabels.cs`
     - `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs`
   - Add a `Principal` fact showing the stored principal code or the exact absent label. Both strings live in the CASE-045-delimited `OperatorLabels` block as `ImageIntakePrincipal` (the field label) and `ImageIntakePrincipalNotKnown` (the exact value `Not known`) — scoped names, because D51's exception is for this field only and a generic `NotKnown` would invite reuse elsewhere.
   - Add a labelled active-principal select and a real POST handler. Its `Not known` option submits null; it is a valid selectable state, not a disabled placeholder. Preserve antiforgery, authorization, the stale-version response and validation-summary behavior.
   - Render no helper prose, matching explanation, inferred suggestion, or new status. The fact remains visible even when no principal is recorded.
   - Acceptance: a staff member can set, replace, or clear the value; the detail page shows the exact value after redirect; a record with none shows exactly `Not known`, and the test rejects the alternates blank, `None`, `Unknown` and `Unassigned`; an inactive principal is not in the select.

5. Extend CASE-042's Awaiting row and quick view.

   - Reuse CASE-042's merged `ImageRow` and its quick-detail shape.
   - Touch:
     - `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`
     - `src/Pegasus.Web/Pages/Cases/Index.cshtml` (only if CASE-042's merged row does not render the new fact generically — see below)
     - `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`
     - `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` (only for the command-counting hook — see below)
   - At the reviewed head, `QueueRow.Facts` renders only in quick detail (`Pages/Cases/Index.cshtml:216`) while the row itself renders title, excerpt and metadata (`:124`), and CASE-042 introduces a separate quick-detail shape. So "no `.cshtml` change is needed" is **not** established in advance. At merge prep, name the exact CASE-042 property/renderer that receives the row-level Principal fact and the one that receives the quick-view fact, and include `Index.cshtml` if the merged row does not render facts generically.
   - Add the Principal fact using the projected code or `OperatorLabels.ImageIntakePrincipalNotKnown`; do not restore image records to Not ready, add a Principal filter, or alter CASE-042's source/count/custody facts.
   - Read-count proof: comparing "recorded" against "absent" on one row proves nothing — both could gain the same query, and a genuine N+1 would pass. Instead record CASE-042's exact Awaiting-request command count at merge prep and assert that same number after CASE-045, with **several** image rows in the fixture, some with a principal and some without, so the count is proven not to scale with rows. The counting idiom exists (`AssessmentPersistenceIntegrationTests.cs:1806`) but `IntakeWebApplicationFactory` (`IntakeWebTestSupport.cs:28-99`) exposes no interceptor hook today; adding that small hook to the shared test-support file is in scope (it is a test file, not on the EPIC-012 tooling no-touch list, which covers `TestUiSnapshotTests.cs`, `.github/workflows/ci.yml` and `scripts/*.ps1`). If the hook proves larger than a constructor overload, stop and report rather than reshaping the factory.
   - Acceptance: both known and unknown values appear in the Awaiting row and quick view; principal display is from `ImageIntake.PrincipalId` only; the Awaiting read command count equals CASE-042's recorded baseline and does not grow with row count.

6. Regenerate the scoped Test UI snapshots for the two changed routed pages.

   - Both pages this ticket changes are catalogued routes: `/Cases` and `/VehicleImages/{id:guid}` (`docs/design/test-ui/catalogue.json`; the image-intake detail state is `unidentified-details--default`). The EPIC-012 build policy makes the scoped capture the lane's own job, and the repository rule commits `docs/design/test-ui/` with the page change — so this is a CASE-045 step, not a hand-off.
   - Run `./scripts/Update-TestUiSnapshots.ps1` scoped to those pages, then `./scripts/Update-TestUiSnapshots.ps1 -Verify` and `./scripts/Test-UiCatalogue.ps1`. Running these scripts is not editing them; the no-touch rule stands.
   - Commit the resulting `docs/design/test-ui/**` delta, and record each regenerated file's byte size, doctype and the expected markers (the `Principal` label, and `Not known` on the absent state) in the implementation report, per "verify the artifact, not the gate". If the capture produces no delta, say so and why.
   - Add no `StateMatches` entry unless this ticket introduces a new catalogue state; do not otherwise touch `TestUiSnapshotTests.cs`.

Local validation, after the generated migration is present:

```powershell
pwsh ./scripts/Test-MigrationGrants.ps1
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntakePersistenceTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntakeWebTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~TriageQueuesWebTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IntakePersistenceIntegrationTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests"
./scripts/Update-TestUiSnapshots.ps1 -Verify
./scripts/Test-UiCatalogue.ps1
```

Do not run the whole integration or browser suite locally. Stop on any failed command, stale dependency shape, missing migration ordering, changed grant requirement, or required path outside the owned list. The stop condition is: scoped checks pass, report is written, PR targeting `dev` is open, and CASE-045 is moved to Review; do not merge or begin another ticket.

## Simplification pass

Not yet run — this is the planning document. Record the dated "Simplification pass" heading and dispositions in this document (or its own scratch note) after the branch's diff exists, per the repository workflow.

## Plan review (2026-09-04, gpt-5.6-sol xhigh; dispositions Claude Opus)

Read independently against `origin/dev` 80f0ca26. Verdict: REQUEST CHANGES. Every finding is dispositioned below; nothing was silenced. Findings 1–7 are the reviewer's; finding 8 was raised by the dispositioning agent during verification.

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | 5 | A "with vs without a principal" command-count comparison proves nothing: both sides could gain the same query, and a real N+1 would pass a one-row test. The counting idiom is private to `AssessmentPersistenceIntegrationTests.cs:1806` and `IntakeWebApplicationFactory` exposes no interceptor hook. | **Fixed.** Step 5 now requires CASE-042's exact baseline count, asserted with several rows of mixed state, and names the test-support hook plus its scope justification and its stop condition. Verified: `IntakeWebTestSupport.cs` has no `AddInterceptors`. |
| 2 | should-fix | 5 | "No `.cshtml` change is expected" is unsupported — at the reviewed head `QueueRow.Facts` renders only in quick detail (`Index.cshtml:216`), the row renders title/excerpt/metadata (`:124`), and CASE-042 adds a separate quick-detail shape. | **Fixed.** Step 5 now records that as unestablished, requires naming CASE-042's exact row and quick-view outlets at merge prep, and keeps `Index.cshtml` conditionally in the touch list (it was already an owned path). |
| 3 | should-fix | 1–2 | No reusable all-staff active-principal query exists and the plan did not say so; `IOrganizationAdministrationQueries` is paginated and gated behind `ManageOrganizationsAndPrincipals`. A canonical `Principal` record already exists at `CaseContracts.cs:19`. | **Fixed.** Steps 1–2 now record the search result with file:line, reuse the existing `Principal` record unless a field cannot be populated, name the new query and its `Code` ordering and `IsActive` filter, and require the inactive-principal tests. |
| 4 | blocker | 2 | The replay design cannot meet "replay returns the committed result": lifecycle replay returns the *current* entity (`EfImageIntakeStore.cs:557`), which is correct only for terminal transitions, while a principal is replaceable and clearable. `ImageIntakeLifecycleEventEntity` stores no principal and requires a `Reason` the form cannot supply. | **Fixed, by simplifying.** Step 2 now drops the lifecycle event and the operation key entirely: the assignment writes the column under the entity's existing optimistic version, a stale version is rejected with the existing error, and repeating the same assignment is idempotent by construction. This also keeps the lifecycle-event vocabulary a single concept. Step 1's request shape was changed to match. Verified `Reason`/`EventType` are `required` on the entity (`ImageIntakeEntities.cs:67-84`). |
| 5 | should-fix | 4–5 | `OperatorLabels.NotKnown` is too generic for a field-only exception, and the plan never placed the `Principal` field label itself in `OperatorLabels`. | **Fixed.** Both labels are now named — `ImageIntakePrincipal` and `ImageIntakePrincipalNotKnown` — in the CASE-045-delimited block, with the reason, and the detail-page acceptance rejects the alternates blank / `None` / `Unknown` / `Unassigned`. |
| 6 | should-fix | 3 | The grant conclusion is right but `Test-MigrationGrants.ps1` cannot prove it — it checks tables a migration creates (`:56`), and this migration creates none. | **Fixed.** Step 3 keeps the script as smoke, stops calling it proof, adds the focused `AzureSqlRuntimeRoleMigrationTests` run, and states that because no grant is needed the `scripts/*.ps1` no-touch rule wins over the owned-path census allowance, with a stop-and-report clause if the premise fails. Grant evidence independently re-verified at `Invoke-AzureDatabaseBootstrap.ps1:313-317` and `RuntimeRoleReconciliation.cs:252`/`:289`. |
| 7 | should-fix | 1 | Existing contract prose becomes false: `IImageIntakeStore`'s summary says only lifecycle columns change and only via `MergeAsync`/`CloseAsync`; `ImageIntakeDetail`'s summary lists only registration time and association. | **Fixed.** Step 1 now requires both summaries updated in the same diff, keeping source identity, VRM and reference immutable while naming the new staff mutation. Verified at `ImageIntakeContracts.cs:229-237`. |
| 8 | blocker | — (new step 6) | Raised on disposition, not by the reviewer. The plan deferred the scoped Test UI snapshot capture to "the EPIC-012 snapshot owner". Both changed pages are catalogued routes — `/Cases` and `/VehicleImages/{id:guid}`, state `unidentified-details--default` — and both the EPIC-012 build policy and the repository rule make the capture and its committed `docs/design/test-ui/**` delta the lane's own work. | **Fixed.** Added as step 6 with the three commands, the artifact-verification record (byte size, doctype, the `Principal` and `Not known` markers), the committed delta, and the no-touch rule restated: running `scripts/*.ps1` is not editing them, and `TestUiSnapshotTests.cs` stays untouched. |
| — | note | — | Reviewer confirmed every other named reuse symbol exists, that `PrincipalEntity` carries `Code` and `IsActive`, and — independently of the research — that no principal-authenticated ImageIntake **registration** route exists (the Provider API is principal-authenticated but calls `ISubmitProviderInstruction`, not `IRegisterImageIntake`). | **Accepted as evidence.** Recorded in the plan preamble; D51's second writer does not apply and the detail-page setter is the only new writer. |

No finding was rejected, and none needed escalation to the operator: D51 already settles every product question this review touched. Open questions remain empty.
