# Plan — PLAT-069 (2026-09-02, gpt-5.6-terra high)

## Objective

At `897db953`, remove the Operations Service health table and add the
administrator-only D37 partial-data notice linking to Administration Service
health. Do not create or alter the Administration page.

## Starting state

`/Operations` currently renders the Service health table when
`GetServiceHealth` is composed. `ServiceHealthSnapshot` has row states and
`ExternalWorkLimitReached`, but no D37 predicate. `PLAT-051`'s destination
`Pages/Administration/ServiceHealth/Index.cshtml` is absent.

The Test UI capture already includes `OperationsWebTests`; its composed-health
fixture has a retryable external-work failure, producing a `Failed` health row.
The default test identity is Administrator, so a new Operations notice state is
capturable without changing a capture host.

## Planning assumptions

1. "Not current" means at least one `Partial` or `Failed` health row. The Core
   predicate deliberately excludes `Running`, `Configured`, and
   `ReviewRequired`, matching D37's mockup condition. It also excludes
   `ExternalWorkLimitReached`: that flag means the Operations projection is
   truncated, not that a health query is stale or failed.

2. Keep `Operations.LimitReached` as a separate condition, but reduce its
   notice to the `Partial data` label only. This is the smallest in-scope
   correction to pre-existing explanatory copy while touching the notice
   markup; do not merge its predicate into health state. The alternative is to
   leave its sentence byte-for-byte as an explicitly accepted existing
   violation.

3. PLAT-069 merges only after PLAT-051 has merged the Service health endpoint
   to `origin/dev`. An `asp-page` link has no valid endpoint in this revision,
   and the Administration rail itself omits future areas to avoid dead links.
   The implementation uses
   `asp-page="/Administration/ServiceHealth/Index"` once that page exists.

## Dependencies

- **PLAT-051:** must provide
  `src/Pegasus.Web/Pages/Administration/ServiceHealth/Index.cshtml` and its
  working endpoint before PLAT-069 renders or verifies the link. Do not edit
  that page or `Pages/Administration/Shared/_AdminNav.cshtml`.
- No migration is required; do not touch
  `src/Pegasus.Infrastructure/Persistence/Migrations/**`.
- Do not alter governing documents or current-state documents; D37 is already
  recorded by DELIV-041, and deployment-state refresh remains DELIV-030's
  responsibility.

## Shared-path lease

Before editing, take the capacity-one lease for exactly:

- `src/Pegasus.Web/Presentation/OperatorLabels.cs`
- `docs/design/test-ui/**` — only the new Operations state and its catalogue
  entry

Refresh the lane with `git merge --no-edit origin/dev`; do not edit any other
shared-lock path.

## Governing rules

D37 requires Administration-only Service health and no Operations table.
Notices contain labels and controls only: do not port "Some figures may be
behind." or the existing limit-warning explanation. Visible labels belong only
in `Presentation/OperatorLabels.cs`; preserve exact Core state labels. The
notice link is a live control, not an inert or disabled seam. PLAT-051's absent
destination is absent, not represented by a provisional link.

## Ordered steps

1. **Confirm PLAT-051 and acquire the two shared-path leases.**

   Verify the Administration Service health endpoint is present on refreshed
   `origin/dev`; if it is absent, pause rather than rendering a dead link.
   Record the two exact leases above.

   Files: none.

   Reuses: the EPIC lane-refresh convention
   `git merge --no-edit origin/dev` and the existing Administration rail's
   no-dead-link rule.

2. **Add the Core D37 notice predicate and focused boundary tests.**

   In `src/Pegasus.Core/Operations/ServiceHealth.cs`, add one
   `ServiceHealthPolicy` predicate over `ServiceHealthSnapshot` that returns
   true only for a `Partial` or `Failed` row. It must ignore
   `ExternalWorkLimitReached` and all other states.

   In `tests/Pegasus.Core.Tests/Operations/ServiceHealthTests.cs`, cover
   `Partial`, `Failed`, the excluded `Running`/`Configured`/`ReviewRequired`
   states, and the ignored limit flag.

   Reuses: `ServiceHealthPolicy`, `ServiceHealthSnapshot`,
   `ServiceHealthState`, and the existing `Build(Sources)` test fixtures.

3. **Replace Operations table markup with the two compliant notices.**

   In `src/Pegasus.Web/Presentation/OperatorLabels.cs`, add the Operations
   `Partial data` and `Service health` labels under one focused nested label
   owner.

   In `src/Pegasus.Web/Pages/Operations/Index.cshtml`, delete the entire
   Service health panel and its table. Keep the optional snapshot loading
   already supplied by `IndexModel`; no code-behind change is needed. Render
   the warning only when the snapshot is present, the Core predicate is true,
   and `User.IsInRole(StaffRoleNames.Administrator)` is true. Use the existing
   warning icon and notice classes, with the Administration link generated by
   `asp-page="/Administration/ServiceHealth/Index"`.

   Retain a separate `LimitReached` notice, replacing its explanatory sentence
   with the centralized `Partial data` label only.

   Reuses: `.notice.notice--warning`, `#icon-alert-triangle`,
   `User.IsInRole`, `asp-page`, `ServiceHealthPolicy`, and
   `OperatorLabels`.

4. **Replace table-focused web assertions with D37 coverage.**

   In `tests/Pegasus.IntegrationTests/OperationsWebTests.cs`, adapt the
   composed-health test to prove the table and its columns/internal vocabulary
   are absent; prove an Administrator with a failed row sees the label and a
   resolved Service health link; prove a User-role request does not see that
   notice or link. Request the resolved link as the Administrator to prove it
   is not dead after PLAT-051.

   Keep retry coverage on Attention required, since it remains the canonical
   action surface.

   Reuses: `Configure(..., withServiceHealth: true)`,
   `RecordingOperationsStore`, `NoServiceHealthFacts`, `CreateClient`, and the
   `X-Test-Roles` convention.

5. **Add and generate the reachable Operations snapshot state.**

   Add an `operations--partial-data` state to
   `docs/design/test-ui/catalogue.json`, backed by
   `docs/design/test-ui/pages/operations--partial-data.html`. Add its
   `StateMatches` marker in
   `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`.

   Generate the snapshot using the composed-health web test captured by the
   existing Test UI script. Commit only Operations snapshot files; do not touch
   UIIMP-014's Case-record states.

   Reuses: the existing Operations catalogue entries,
   `StateMatches`, `TestUiResponseCaptureMiddleware`, and
   `Update-TestUiSnapshots.ps1`.

## Acceptance conditions

- `/Operations` contains no Service health table, heading, columns, or rows.
- An Administrator sees one label-only partial-data notice with a working
  Service health link when a health row is `Partial` or `Failed`.
- Engineer/User requests do not see the D37 health notice or link.
- `Running`, `Configured`, `ReviewRequired`, and the independent
  `ExternalWorkLimitReached` flag do not trigger the D37 predicate.
- The independent limit warning remains separate and contains no explanatory
  sentence.
- The new Operations snapshot state is captured, committed, and verified.
- No Administration, migration, governing-document, shell, or unrelated
  snapshot path changes.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

Do not run `./scripts/Test-MigrationGrants.ps1`; this ticket has no migration.

## Stop condition

Stop when the scoped PR targeting `dev` is open and PLAT-069 is in Review.

## Wrapper notes (Claude, 2026-09-03, read-only checks)

- Codex ran once, exit 0, in `.worktrees/research` at origin/dev
  `897db953`; the checkout was clean afterwards.
- VERIFIED — `tests/Pegasus.IntegrationTests/WorkCentreLabelTests.cs:50-66`
  (UIIMP-008): in this app an `asp-page` that names an unknown page renders
  `href=""`; it does not throw. Merging before PLAT-051 would therefore ship
  exactly the dead link the ticket forbids, which settles planning
  assumption 3 on repository evidence. Step 4 should also assert the
  Operations response contains no `href=""` (the
  `TheWorkCentreRendersNoEmptyLink` pattern) so the class cannot regress.
- VERIFIED — `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs:281-288`:
  the test identity is `Administrator` unless `X-Test-Roles` (or
  `X-Test-Roleless`) is sent, so the User-role case in step 4 sends
  `X-Test-Roles: User`.
- VERIFIED — `src/Pegasus.Core/Operations/ServiceHealth.cs:271-285` and
  `OperationsWebTests.cs:646`: the composed fixture's retryable failed
  external-work item yields a `Failed` row, so the rewritten composed test
  renders the notice and is the capture that feeds the new snapshot state
  (`TestUiResponseCaptureMiddleware`, `TestUiSnapshotTests.StateMatches`).
- VERIFIED — `.kanmer/data/board.yml`: profile `fix` gates leave-preparing
  on `files` + `plan` and enter-done on `proof`; the checklist is written
  for execution, not for a gate.
- The step-3 role check uses the established spelling
  `User.IsInRole(Pegasus.Core.Identity.StaffRoleNames.Administrator)`
  (`_Layout.cshtml:12`).
- The simplification pass (CLAUDE.md workflow step 4) is added to the
  checklist; its dated "Simplification pass" heading is appended to this
  plan at execution time.
- The three operator questions stay open in `open-questions/`; the plan's
  defaults are recorded there beside each question.

## Resolutions (2026-09-03)

- Controller: the notice shows for Partial or Failed only.
- Controller: one label-only notice line each; the limit warning's hint sentence is removed.
- Controller: PLAT-069 may merge before PLAT-051; the Administration link is absent until `Pages/Administration/ServiceHealth` exists.
