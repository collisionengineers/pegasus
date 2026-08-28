## Plan (profile: fix, small/self-contained)

1. Confirm the defect and the intended fix by convention, not invention.
   - Read: current template is
     `@page "{organizationId:guid}/{principalId:guid}/EvaSubmission"`
     (relative route + a literal trailing page-name segment) → effective
     route `/Administration/Principals/EvaSubmission/{organizationId}/{principalId}/EvaSubmission`.
   - Convention check (read-only): the sibling `Replace.cshtml` in the same
     folder uses `@page "{organizationId:guid}/{principalId:guid}"` for the
     identical two-guid shape, catalogued as
     `/Administration/Principals/Replace/{organizationId:guid}/{principalId:guid}`.
     Confirms the fix the ticket names is exactly this repo's own pattern,
     not a new one.
2. Fix: change `EvaSubmission.cshtml` line 1 to
   `@page "{organizationId:guid}/{principalId:guid}"` — reuses the
   `Replace.cshtml` convention verbatim.
3. Callers: `git grep -in evasubmission` across `src/`, `tests/`, `docs/`,
   filtered to page/route hits (the raw term also matches unrelated
   business types — `EvaSubmissionModelConfiguration`, `EvaSubmissionOutcome`,
   `IUpdatePrincipalEvaSubmission`, etc. — not this page). Only in-repo
   caller is `Index.cshtml`'s `asp-page="EvaSubmission"` link, which is
   tag-helper-generated and needs no edit. No catalogue entry, no
   `docs/` route mention, no other test file references this route
   (see `files` doc for the full negative-result list).
4. Redirect-stub decision: **no stub**. Per the greenfield rule ("unless
   the brief names users or data, add no fallback/compatibility/deprecation
   path"), the doubled URL was a routing defect, not a published address:
   it has no external distribution (no email template, no bookmarked
   support flow, no doc reference), and its one in-app entry point is the
   dynamic `asp-page` link that will render the corrected URL the moment
   this ships. `docs/operations.md` release 36 (2026-08-28, today) shows
   this page already reached production, so an admin could in principle
   have the doubled URL in local browser history — but that is not "named
   users or data" in the ticket or operator-notes sense, and the app has
   no way to reach a private browser history entry anyway. No redirect
   stub added.
5. Tests: extend `OrganizationAdministrationWebTests` (the only Web-route
   proof surface for this admin folder) rather than adding a new file:
   - `AdministratorRoutesAreDiscoverableAndPostThroughCoreEfCallers`: after
     the existing principal-index assertions and before the `Replace` walk
     (so the principal is still active/unreplaced), GET the corrected
     single-segment `EvaSubmission` route, assert the page renders, POST
     `?handler=Update` with both EVA toggles + reason, assert redirect, and
     assert the `Principals` row now has `EvaManualSubmission = 1`,
     `EvaAutomaticSubmission = 0` — proves the fixed route resolves,
     round-trips through the real handler and the real EF caller, mirroring
     the existing `Replace` proof shape exactly.
   - `DirectOrganizationAndPrincipalRoutesDenyNonAdministratorSession`: add
     the corrected `EvaSubmission` route to the denied-routes array,
     mirroring the existing `Replace` entry.
6. Build (`dotnet build --configuration Release`) for compiler feedback,
   then run the focused filter
   `--filter "FullyQualifiedName~OrganizationAdministrationWebTests"` and
   record the pass count.
7. Simplification pass over this branch's own diff (two files, ~30 lines):
   reviewed manually against the four lenses (reuse, simplification,
   efficiency, altitude) given the diff's size — no separate agent
   invocation warranted for a diff this small. Findings: none; the fix is
   a one-line convention match and the test additions are a direct copy of
   an existing, already-reviewed pattern in the same file. Recorded here
   under a dated heading per the repository workflow.
8. Kanmer: `get_doc_gates` → `take_ticket` (done) → this `files`/`plan` pair
   → `move_item` to `implementing` → implement (done ahead of the doc walk,
   per lane instructions) → post-implementation report → `move_item` to
   `review`. Do not write `proof` or move to `done` — orchestrator-owned.
9. Commit to `task/plat-052-eva-submission-route`, push, open the PR
   against `dev`. Do not merge.

## Acceptance conditions

- Exactly one route for this page:
  `/Administration/Principals/EvaSubmission/{organizationId}/{principalId}`.
- `Index.cshtml`'s "EVA API" link still resolves (unchanged markup, tag
  helper regenerates the URL).
- `OrganizationAdministrationWebTests` passes and now exercises the
  corrected route both as an authorized round trip and as a denied route
  for a non-administrator session.
- Build green; no other file in the repo references the doubled route.

## Simplification pass — 2026-08-28

n/a for a separate agent pass at this size (2 files, ~30 lines changed).
Manually reviewed against reuse / simplification / efficiency / altitude:
no findings. The route fix reuses `Replace.cshtml`'s exact template; the
test additions reuse the existing test's own form-post and assertion idioms
verbatim rather than introducing a new helper or pattern.
