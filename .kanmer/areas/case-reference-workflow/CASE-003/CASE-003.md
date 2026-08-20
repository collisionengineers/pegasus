---
id: CASE-003
type: ticket
title: >-
  Answer /Cases/Create without a receipt with the designed status page, not a
  500
status: verifying
area: case-reference-workflow
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-20T01:29:47.039Z'
  implementing: '2026-08-20T01:30:43.193Z'
  review: '2026-08-20T01:31:08.509Z'
  verifying: '2026-08-20T01:31:13.615Z'
labels:
  - ui
  - robustness
links:
  - PLAT-006
refs:
  - docs/frd/frd-12-operator-experience.md
prs:
  - '433'
deployment: production
archived: false
created: '2026-08-19T07:48:36.354Z'
updated: '2026-08-20T01:31:13.615Z'
---

## What

`GET /Cases/Create` with no `receiptId` (a typed URL, a stale bookmark) throws `ArgumentException: An intake receipt identifier is required. (Parameter 'query')` from `CreateModel.LoadAsync` and renders the developer exception page locally / a 500 in production, instead of the designed status-code answer (`Pages/StatusCode.cshtml`) or a redirect to Queues.

Found during the [[PLAT-006]] visual sweep (2026-08-19); out of that ticket's scope.

## Approach

Guard the empty/`Guid.Empty` receipt in `OnGetAsync` and return `NotFound()` (the designed 404 page) — or redirect to `/Triage` with the existing confirmation mechanism — before `LoadAsync` runs. One test in the Cases web tests asserting the status.

## Verification

- [ ] `GET /Cases/Create` → 404 designed page (or 302 to Queues), never 500.
- [ ] Existing `Cases/Create?receiptId=…` journey unchanged.

## Outcome
