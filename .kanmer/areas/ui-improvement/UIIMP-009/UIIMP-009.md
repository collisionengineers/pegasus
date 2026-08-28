---
id: UIIMP-009
type: ticket
title: 'Remove superseded surfaces, the legacy CSS block and dead selectors'
status: backlog
area: ui-improvement
assignee: ''
profile: fix
labels:
  - ui
  - wave-5
  - removal
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-28T08:35:24.210Z'
updated: '2026-08-28T08:35:24.210Z'
---

## What

Wave 5 of [[EPIC-011]]. Delete the delimited legacy block from `site.css`, the redirect stubs and pages superseded by the new areas (`Administration/Organizations/**`, `Access/**`, `Roles/**`, `Automation/Activity.*`), orphaned partials and JS blocks (grep-proved zero callers), unplaced mark files if the operator confirms, and reclassify `docs/design/test-ui/catalogue.json`; matching test deletions.

## Owns

The deleted files, `site.css` legacy block, `catalogue.json`, tests.

## Blocked by

Every wave-4 ticket.

## Verification

- [ ] `Test-UiCatalogue.ps1` and snapshot verify pass; no selector in site.css lacks a caller.
