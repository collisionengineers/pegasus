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

## Correction — round 2, 2026-08-29 (adversarial verifier)

The "Deliberately not done" section above claimed no catalogue entry
existed anywhere for this page and that adding one would require the
barred snapshot-capture script. **Both claims were wrong and are retracted
here rather than edited away.** An entry already existed on [[UIIMP-005]]'s
own unmerged branch (`task/uiimp-005-test-ui-gate`, PR #609) — a branch
this ticket itself links and names as the one that found the doubled route
in the first place. I never checked that linked branch; an independent
verifier did (`git show origin/task/uiimp-005-test-ui-gate:docs/design/test-ui/catalogue.json`)
and called it out correctly.

**What changed to fix it:**

- Added the `EvaSubmission.cshtml` entry to `docs/design/test-ui/catalogue.json`,
  reusing UIIMP-005's entry with only the `route` field corrected to this
  ticket's single-segment route.
- Added `docs/design/test-ui/pages/administration-principal-eva-submission--default.html`,
  copied byte-for-byte from UIIMP-005's branch (its markup contains no
  route text, so the copy needed no edit for the corrected route to be
  accurate).
- Did **not** run `scripts/Update-TestUiSnapshots.ps1` (still barred) —
  reused already-captured, real content instead of fabricating it or
  capturing fresh.
- Left `docs/design/test-ui/index.html` unregenerated: it's a generated
  artifact rewritten wholesale by the barred capture script, and
  `Test-UiCatalogue.ps1` doesn't cross-check it against `catalogue.json`.

**Verification, re-run:**

- `pwsh -NoProfile -Command "dotnet build ./Pegasus.slnx --configuration Release"`
  — Build succeeded, 0 warnings, 0 errors (re-run after the catalogue
  change; unaffected since it's a data file, but re-run for completeness).
- `pwsh -NoProfile -Command "dotnet test ./Pegasus.slnx --configuration Release --no-build --filter 'FullyQualifiedName~OrganizationAdministrationWebTests'"`
  — **Passed: 2, Failed: 0, Skipped: 0, Total: 2** (unchanged from round 1).
- `pwsh -NoProfile -Command "& ./scripts/Test-UiCatalogue.ps1"` — this
  ticket's own page is no longer reported. The script still exits 1
  overall, on two pre-existing defects that are **not this ticket's
  file**: `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml` is uncatalogued
  ([[CASE-012]]'s file, its own PR #615 is open) and
  `docs/design/test-ui/pages/vehicle-images-details--default.html` has a
  stale broken reference to the already-deleted `/VehicleImages` list
  prototype (no clear current owner; likely Wave 5). Full reasoning,
  evidence and the UIIMP-005 (PR #609) merge-order handover are in `plan`
  under "Remediation round 2."

**Verification bullet in the ticket body** ("One route; `Test-UiCatalogue.ps1`
and snapshot verify pass") is **not** fully satisfied by this PR alone: this
ticket's own catalogue contribution passes; the full script does not,
for the two unrelated reasons above, and the snapshot-verify half of that
bullet needs the barred capture script the lane cannot run. Recording this
honestly rather than checking the box.

## Correction — round 4, 2026-08-29 (CI status honesty)

An earlier report characterised PR #614's CI as still running / pending at
push time. **That status has since resolved to failure and is corrected
here rather than left standing.** Independently re-checked, not repeated:

- `gh pr view 614 --json statusCheckRollup,mergeable,state` — PR is
  `OPEN`/`MERGEABLE` (not merged); the merge-commit run `33246469257`
  shows `sql-integration (1)` = **FAILURE**, every other check
  SUCCESS/SKIPPED.
- `gh run list --branch task/plat-052-eva-submission-route` confirms the
  two earlier commits on this branch (`4b24ca17`, `0a0d9eee`) both ran
  fully green; only the post-merge commit's run is red.
- **This is not a PLAT-052 regression.** The failing assertion is
  `tests/Pegasus.IntegrationTests/PrincipalCredentialPersistenceTests.cs:62`,
  owned by TICK-061 (`4aec2703`, in flight on its own branch), untouched
  by this branch (`git diff 0a0d9eee 48df8f58 --
  .../PrincipalCredentialPersistenceTests.cs` is empty). It is a
  ~25%-per-run flaky assertion (a wrong-secret probe that has roughly a
  1-in-4 chance of regenerating the original secret because of how the
  last Base64Url character is bit-constrained) that will redden
  `sql-integration (1)` intermittently for every lane in the epic, not
  only this one, until TICK-061 fixes it. Full reasoning and evidence in
  `plan` under "Review findings — dispositions (round 2), 2026-08-29",
  finding 2.
- The repo's own merge rule ("may merge into `dev` only after that review
  passes and CI is green") is therefore **not currently met** for PR #614,
  through no fault of this ticket's own diff. Recording this plainly:
  this PR should not be merged until either CI goes green on a re-run (the
  flake resolving) or TICK-061 lands its fix and this branch merges
  `origin/dev` again.

## Correction — round 4, 2026-08-29 (two small fixes closing minor findings)

- `docs/design/test-ui/pages/administration-principal-eva-submission--default.html`
  line 170: `status status--neutral` → `status status--navy`, matching
  `_StatusChip.cshtml`'s `"active" => "navy"` mapping (the drift was
  introduced by other lanes' merges to `_StatusChip.cshtml` landing after
  this page's prototype was first captured).
- `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`: added a
  `StateMatches` entry for `administration-principal-eva-submission--default`
  so the snapshot generator no longer relies on an ordinal-string
  tie-break to pick the real page over the error-page capture that this
  ticket's own denied-route test added as a second candidate.

Both are commit `1ac0fac6`, pushed. Full reasoning for both in `plan`,
round 2/4 dispositions 5 and 6.

## Verification, re-run

- `dotnet build ./Pegasus.slnx --configuration Release` — **0 Warning(s),
  0 Error(s)**.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "FullyQualifiedName~OrganizationAdministrationWebTests"` — **Passed: 2,
  Failed: 0, Skipped: 0, Total: 2**.
- `tests/` diff against `origin/dev` for this round: none — this round
  touched no test assertions, only the shared `StateMatches` dictionary
  (a new entry, nothing weakened, deleted, or reordered) and one static
  prototype HTML file.
