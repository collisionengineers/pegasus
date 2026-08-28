---
id: PLAT-052
type: ticket
title: >-
  EvaSubmission page route is doubled
  (/Administration/Principals/EvaSubmission/{org}/{principal}/EvaSubmission)
status: backlog
area: platform-operations
assignee: ''
profile: fix
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
updated: '2026-08-28T08:58:50.335Z'
---

## What

`src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml` declares a relative `@page "{organizationId:guid}/{principalId:guid}/EvaSubmission"`, so the effective route is `/Administration/Principals/EvaSubmission/{organizationId}/{principalId}/EvaSubmission` (found by [[UIIMP-005]] while cataloguing the page; the Principals index link and the Test UI catalogue now use the route as it ships). Fix the template to a single, intentional route (leading `/` or drop the trailing segment) and update the catalogue entry, snapshot state and the `OrganizationAdministrationWebTests` URL together.

## Why

Introduced by [[TICK-077]] (PR #574). [[PLAT-050]] folds this page into the Principal settings dialog; if that lands first, this ticket closes with it.

## Verification

- [ ] One route; `Test-UiCatalogue.ps1` and snapshot verify pass.
