## What changed

- `src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml`:
  `@page "{organizationId:guid}/{principalId:guid}/EvaSubmission"` →
  `@page "{organizationId:guid}/{principalId:guid}"`. Effective route is
  now `/Administration/Principals/EvaSubmission/{organizationId}/{principalId}`
  (was doubled with a trailing `/EvaSubmission`). Matches the sibling
  `Replace.cshtml` convention in the same folder exactly.
- `tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs`:
  - `AdministratorRoutesAreDiscoverableAndPostThroughCoreEfCallers` now
    also GETs the corrected `EvaSubmission` route, POSTs `?handler=Update`
    with both EVA toggles set and a reason, asserts the redirect, and
    asserts the `Principals` row was updated
    (`EvaManualSubmission = 1`, `EvaAutomaticSubmission = 0`) — inserted
    before the `Replace` walk so the principal is still active.
  - `DirectOrganizationAndPrincipalRoutesDenyNonAdministratorSession` now
    includes the corrected `EvaSubmission` route in its denied-routes
    array, alongside the existing `Replace` entry.

## Callers checked (no code change needed)

- `Index.cshtml`'s `asp-page="EvaSubmission"` link — tag-helper generated,
  resolves against the page's route template at render time; unaffected by
  the fix except that it now points at the corrected URL.
- No other `src/`, `tests/`, or `docs/` file references the page's route
  text (verified with `git grep`, filtered away from unrelated
  `EvaSubmission*` business-domain identifiers — `EvaSubmissionModelConfiguration`,
  `EvaSubmissionOutcome`, `IUpdatePrincipalEvaSubmission`, etc., which are
  the EF entity / EVA API result type, not this page).

## Deliberately not done

- **`docs/design/test-ui/catalogue.json`**: the ticket description assumed
  an existing entry for this page to fix. There isn't one — `Create`,
  `Index`, and `Replace` are catalogued under
  `Administration/Principals/*`; `EvaSubmission` never was. Adding one now
  means a new snapshot capture via `scripts/Update-TestUiSnapshots.ps1`,
  which this lane is barred from running (an orchestrator-owned gate per
  wave-loop and my lane brief: "Do NOT run ... any snapshot/catalogue
  script"). Flagging for the orchestrator to add the missing catalogue
  entry + snapshot in whichever pass owns that script, or to fold it into
  PLAT-050 if that lands first per this ticket's own "Why".
- **Redirect stub for the old doubled URL**: none added. The doubled route
  was a routing defect, not a published address — no email template,
  support flow, or doc references it, and its only in-app entry point (the
  `asp-page` link) regenerates automatically. Per the greenfield rule, a
  fallback/compatibility path needs the brief to name users or data it
  protects; this brief doesn't. Full reasoning in `plan`.

## Verification

- `dotnet build ./Pegasus.slnx --configuration Release` — succeeded, 0
  warnings, 0 errors.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~OrganizationAdministrationWebTests"` — **Passed: 2,
  Failed: 0, Skipped: 0, Total: 2** (both tests in the target class; the
  filter matched 0 tests in `Pegasus.Core.Tests` / `Pegasus.ArchitectureTests`
  as expected).
- Full suite, Browser category, and snapshot/catalogue scripts intentionally
  **not** run — orchestrator-owned gates per lane scope.

## Scope respected

Touched only: `EvaSubmission.cshtml`, `OrganizationAdministrationWebTests.cs`.
No change to `Index.cshtml`, the Principals folder structure (left for
PLAT-028/PLAT-050), `AGENTS.md` (no command/convention change), or any
neighbour-lane file.
