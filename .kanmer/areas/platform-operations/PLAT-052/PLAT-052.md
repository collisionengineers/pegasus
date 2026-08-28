---
id: PLAT-052
type: ticket
title: >-
  EvaSubmission page route is doubled
  (/Administration/Principals/EvaSubmission/{org}/{principal}/EvaSubmission)
status: implementing
area: platform-operations
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-28T21:47:28.486Z'
taken_at: '2026-08-28T21:46:06.049Z'
branch: task/plat-052-eva-submission-route
worktree: 'C:/Users/PC/Documents/GitHub/pegasus-worktrees/plat-052-eva-submission-route'
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
refs:
  - docs/frd/frd-04-parties-accounts-and-access.md
archived: false
created: '2026-08-28T08:58:50.335Z'
updated: '2026-08-28T21:47:28.486Z'
---

## What

`src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml` declares a relative `@page "{organizationId:guid}/{principalId:guid}/EvaSubmission"`, so the effective route is `/Administration/Principals/EvaSubmission/{organizationId}/{principalId}/EvaSubmission` (found by [[UIIMP-005]] while cataloguing the page; the Principals index link and the Test UI catalogue now use the route as it ships). Fix the template to a single, intentional route (leading `/` or drop the trailing segment) and update the catalogue entry, snapshot state and the `OrganizationAdministrationWebTests` URL together.

## Why

Introduced by [[TICK-077]] (PR #574). [[PLAT-050]] folds this page into the Principal settings dialog; if that lands first, this ticket closes with it.

## Verification

- [ ] One route; `Test-UiCatalogue.ps1` and snapshot verify pass.
