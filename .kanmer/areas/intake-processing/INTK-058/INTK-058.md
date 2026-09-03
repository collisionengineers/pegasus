---
id: INTK-058
type: ticket
title: >-
  Extract the repairer name and location from instruction material into a
  per-case repairer record
status: backlog
area: intake-processing
order: 570
assignee: ''
profile: feature
labels:
  - extraction
  - repairer
  - inspection
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-09-03T10:53:09.881Z'
updated: '2026-09-03T15:15:28.061Z'
---

## What

Persist a repairer name and address on the Case, extracted from the
instruction material by the existing extraction process, so
[[CASE-041]]'s "Repairer location" inspect-at option resolves to a real
value instead of shipping permanently disabled.

## Why

Operator answer to [[CASE-041]]'s open question (2026-09-03): the repairer
location is in general extractable from the instruction document — see the
QDOS instruction e-mails and bodyshop reports used as reference material —
so it becomes part of the extraction process rather than manual entry.
Today the only repairer concept in production is the assessment flag
`costs.repairer_vat_registered`; no repairer name or address is persisted
anywhere. Repairer reference data as a first-class entity stays [[TICK-034]]
(post-alpha); this ticket persists only what the instruction states.

## Approach

- Add the repairer fields to the existing case data contract and its
  field-name allow-list; no new store.
- Extend the existing instruction extraction vocabulary and its prompts and
  fixtures; extraction confidence and fail-closed behaviour follow the
  existing rules (never guess an address).
- [[CASE-041]] consumes it: the Repairer location option becomes enabled
  once a value exists, with no change to its own logic.

## Verification

- [ ] A QDOS instruction carrying a bodyshop address yields the repairer
      name and address on the case.
- [ ] An instruction without one leaves the fields absent and the inspect-at
      option disabled.
- [ ] Extraction fixtures use the documented estate; no fabricated repairer.

## Outcome
