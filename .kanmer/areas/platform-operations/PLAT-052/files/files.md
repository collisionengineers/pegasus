## Files touched

- `src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml` —
  the `@page` directive itself; the defect.
- `tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs` —
  only existing caller-backed proof surface for this route family; extended
  with the same shape already used for the sibling `Replace` route.
- `docs/design/test-ui/catalogue.json` — added the missing `EvaSubmission`
  entry (see "Correction — round 2" below; the original claim that no entry
  existed anywhere was wrong).
- `docs/design/test-ui/pages/administration-principal-eva-submission--default.html`
  — new prototype file the added entry's `states[0].file` points at.

## Files checked, not touched (no caller found)

- `src/Pegasus.Web/Pages/Administration/Principals/Index.cshtml` — the one
  in-app link (`asp-page=\"EvaSubmission\"` with `asp-route-organizationId`
  / `asp-route-principalId`). The tag helper resolves the URL from the
  page's own route template at render time, so it needs no edit and picks
  up the corrected route automatically.
- `docs/current-architecture.md`, `docs/operations.md` — mention
  `EvaSubmissions` (the EF entity/table) and the EXT-04 feature narrative,
  never this admin page's URL. Nothing to change.
- `tests/Pegasus.Core.Tests/Cases/OrganizationAdministrationTests.cs` —
  a Core business-rule test (`ADisabledPrincipalsEvaSubmissionSettingsCannotBeChanged`),
  not a route/Web test; out of scope.

## Reuse

- The fixed `@page` template copies the exact convention already used by
  the sibling page in the same folder, `Replace.cshtml`
  (`@page \"{organizationId:guid}/{principalId:guid}\"`), confirmed against
  its catalogued effective route
  `/Administration/Principals/Replace/{organizationId:guid}/{principalId:guid}`.
- The test additions extend the existing
  `AdministratorRoutesAreDiscoverableAndPostThroughCoreEfCallers` GET/POST
  walk and the existing `DirectOrganizationAndPrincipalRoutesDenyNonAdministratorSession`
  route array — the same two tests already prove `Replace` — rather than
  adding a new test or a new file.
- The catalogue entry and prototype file reuse content already captured by
  [[UIIMP-005]] on its own unmerged branch (`task/uiimp-005-test-ui-gate`,
  PR #609) rather than fabricating new content or running the (barred)
  snapshot capture script: `git show origin/task/uiimp-005-test-ui-gate:docs/design/test-ui/catalogue.json`
  and the matching `pages/administration-principal-eva-submission--default.html`
  were pulled verbatim, with only the entry's `route` field corrected to
  the single-segment route this ticket ships (the prototype file's own
  markup never embeds the route text, so it needed no edit).

## Correction — round 2 (adversarial verifier round)

The original claim here — "no entry exists for this page today ... the
ticket description assumed one existed; there isn't one" — was **wrong**
and is retracted. An entry exists, just not on `dev`: it's on [[UIIMP-005]]'s
own unmerged branch (`task/uiimp-005-test-ui-gate`, currently PR #609,
open), which is one of this ticket's own `links` and is named in this
ticket's own body as the ticket that found the doubled route "while
cataloguing the page." I did not check that linked branch before writing
the original claim; the verifier did
(`git show origin/task/uiimp-005-test-ui-gate:docs/design/test-ui/catalogue.json`)
and was right to call it out. See `plan` for the fix and the merge-order
hazard this creates with PR #609.
