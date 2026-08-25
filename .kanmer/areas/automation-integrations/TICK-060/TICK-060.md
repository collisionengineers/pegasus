---
id: TICK-060
type: ticket
title: API-03 — Return the provider's resulting Case/PO or fail
status: preparing
area: automation-integrations
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-21T14:20:04.506Z'
labels:
  - capability
  - API-03
  - next
  - post-alpha
  - blocked
  - requires-live-approval
groups:
  - HZN-002
  - EPIC-009
links:
  - TICK-058
refs:
  - docs/frd/frd-09-provider-and-intermediary-routes.md
archived: false
created: '2026-08-12T15:05:19.465Z'
updated: '2026-08-25T06:36:41.961Z'
---

## What

Plan and deliver **API-03**: retrieve the resulting Case/PO for one authenticated Principal's own API submission.

## Why

A provider must not use Pegasus as a general lookup surface. A lookup succeeds only when the identified submission created or linked to an actual Case/PO owned by that Principal.

## Approach

- Scope every request to the authenticated Principal and its own submission receipt.
- Return Case/PO only from the actual active Case link.
- While processing has not completed, return only a generic nonterminal response.
- Once processing completes, absence of an actual Case link is a terminal failure; never keep polling and never expose unrelated data.
- Expose no files, reports, source material, general Case detail, search, or outbound delivery.

## Verification

- [ ] An owned submission with an actual Case link returns its immutable Case/PO.
- [ ] A completed submission that did not create or link a Case fails terminally.
- [ ] Unknown, random, or cross-Principal identifiers reveal nothing.
- [ ] No general lookup or two-way file surface exists.

## Notes

- Source: `docs/capabilities.md` — API-03.
- Blocked by: [[TICK-058]].

## Outcome
