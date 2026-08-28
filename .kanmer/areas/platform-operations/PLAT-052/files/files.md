## Files touched

- `src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml` —
  the `@page` directive itself; the defect.
- `tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs` —
  only existing caller-backed proof surface for this route family; extended
  with the same shape already used for the sibling `Replace` route.

## Files checked, not touched (no caller found)

- `src/Pegasus.Web/Pages/Administration/Principals/Index.cshtml` — the one
  in-app link (`asp-page="EvaSubmission"` with `asp-route-organizationId`
  / `asp-route-principalId`). The tag helper resolves the URL from the
  page's own route template at render time, so it needs no edit and picks
  up the corrected route automatically.
- `docs/design/test-ui/catalogue.json` — **no entry exists for this page**
  today (confirmed: `Create`, `Index`, `Replace` are catalogued under
  `Administration/Principals/*`; `EvaSubmission` is not). The ticket
  description assumed an existing doubled-route entry to fix; there isn't
  one. Adding a new entry means a new snapshot capture, which requires
  `scripts/Update-TestUiSnapshots.ps1` — a script this lane is barred from
  running (orchestrator-owned gate). Left uncreated; flagged in the
  post-implementation report.
- `docs/current-architecture.md`, `docs/operations.md` — mention
  `EvaSubmissions` (the EF entity/table) and the EXT-04 feature narrative,
  never this admin page's URL. Nothing to change.
- `tests/Pegasus.Core.Tests/Cases/OrganizationAdministrationTests.cs` —
  a Core business-rule test (`ADisabledPrincipalsEvaSubmissionSettingsCannotBeChanged`),
  not a route/Web test; out of scope.

## Reuse

- The fixed `@page` template copies the exact convention already used by
  the sibling page in the same folder, `Replace.cshtml`
  (`@page "{organizationId:guid}/{principalId:guid}"`), confirmed against
  its catalogued effective route
  `/Administration/Principals/Replace/{organizationId:guid}/{principalId:guid}`.
- The test additions extend the existing
  `AdministratorRoutesAreDiscoverableAndPostThroughCoreEfCallers` GET/POST
  walk and the existing `DirectOrganizationAndPrincipalRoutesDenyNonAdministratorSession`
  route array — the same two tests already prove `Replace` — rather than
  adding a new test or a new file.
