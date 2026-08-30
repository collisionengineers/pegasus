---
id: PLAT-052
type: ticket
title: >-
  EvaSubmission page route is doubled
  (/Administration/Principals/EvaSubmission/{org}/{principal}/EvaSubmission)
status: done
area: platform-operations
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-28T21:47:28.486Z'
  review: '2026-08-28T21:48:44.582Z'
  verifying: '2026-08-29T17:19:16.146Z'
  done: '2026-08-29T17:19:28.734Z'
labels:
  - routes
  - principals
  - follow-up
groups:
  - EPIC-011
links:
  - TICK-077
  - PLAT-050
  - UIIMP-005
  - PR-070
refs:
  - docs/frd/frd-04-parties-accounts-and-access.md
commits:
  - 4b24ca1702848ee7023b120427235ef0ac6a98a1
  - 0a0d9eee4137139a89b72e79849fa9ff00f3b908
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/614'
archived: false
created: '2026-08-28T08:58:50.335Z'
updated: '2026-08-30T20:24:53.952Z'
---

## What

`src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml` declares a relative `@page "{organizationId:guid}/{principalId:guid}/EvaSubmission"`, so the effective route is `/Administration/Principals/EvaSubmission/{organizationId}/{principalId}/EvaSubmission` (found by [[UIIMP-005]] while cataloguing the page; the Principals index link and the Test UI catalogue now use the route as it ships). Fix the template to a single, intentional route (leading `/` or drop the trailing segment) and update the catalogue entry, snapshot state and the `OrganizationAdministrationWebTests` URL together.

## Why

Introduced by [[TICK-077]] (PR #574). [[PLAT-050]] folds this page into the Principal settings dialog; if that lands first, this ticket closes with it.

## Verification

- [ ] One route; `Test-UiCatalogue.ps1` and snapshot verify pass.

## Remediation round 2 note (2026-08-29) — merge-order hazard with UIIMP-005 (PR #609)

`docs/design/test-ui/catalogue.json` now carries this page's corrected
single-segment route (added in PR #614, reusing content already captured
on [[UIIMP-005]]'s own unmerged branch). **UIIMP-005 (PR #609) still
carries the old, doubled-route version of the same entry on its own
branch.** Recommended order: **merge PLAT-052 (#614) before UIIMP-005
(#609)**; when UIIMP-005 lands after, resolve the resulting
`catalogue.json` conflict on the `EvaSubmission` entry by keeping this
ticket's single-segment `route` (dropping UIIMP-005's doubled one), while
keeping UIIMP-005's unrelated `Cases/Eva/Send.cshtml` entry and its
tooling/CI-gate changes intact. Full evidence in the ticket's `plan`
document under "Remediation round 2."

`Test-UiCatalogue.ps1` will still exit non-zero on `dev` after this PR
merges, for two unrelated pre-existing reasons this ticket does not own:
`src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml` is uncatalogued ([[CASE-012]],
PR #615 open) and `docs/design/test-ui/pages/vehicle-images-details--default.html`
has a stale broken reference to the already-deleted `/VehicleImages` list
prototype — filed as [[PR-070]] (no current owner among the in-flight
tickets). Neither is touched by this PR. See `plan` for full detail.
