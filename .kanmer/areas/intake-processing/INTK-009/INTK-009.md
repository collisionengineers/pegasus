---
id: INTK-009
type: ticket
title: >-
  Rebuild the Queues surface: Unidentified as a tab with image/e-mail filters,
  Not ready split by case origin, operator-safe copy
status: done
area: intake-processing
order: 1140
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-19T23:21:44.007Z'
  review: '2026-08-19T23:59:46.931Z'
  verifying: '2026-08-20T00:16:02.719Z'
  done: '2026-08-20T01:29:44.762Z'
labels:
  - ui
  - queues
  - unidentified
  - design
  - operator-reported
links:
  - DELIV-012
  - INTK-007
  - INTK-008
  - PLAT-010
refs:
  - docs/design/README.md
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-02-intake-and-source-identity.md
prs:
  - '432'
deployment: production
archived: false
created: '2026-08-19T23:12:15.125Z'
updated: '2026-08-26T14:34:44.538Z'
---

## What

Restructure the queue surfaces per the operator's direction of 2026-08-19/20, given verbatim:

1. *"Unidentified should be a tab within queues. It should have seperate filters for images and emails."*
2. *"The 'Not Ready' queue is also supposed to have seperate filters for instructions and image initiated cases."*
3. On the current Unidentified page: *"a ton of slop … walls of text and links and theres no clear answer as to what is going on"*, and internal language leaking operator-facing: *"intake", "custody detail", "Intake receipt — 2b49d9d3-033d-40ff-bf93-277dc45617b4"*.

So:
- The top-level **Unidentified** navigation entry (added by [[INTK-007]]) is removed; Unidentified becomes a **tab on the Queues page** alongside the existing queues, with **filters for images and e-mails**.
- The **Not ready** queue gains **filters for Instruction-initiated and Image-initiated cases** (the two case origins the operator confirmed for [[INTK-008]]).
- Each Unidentified row answers *what is going on* at a glance: the U-reference, what the thing is (image / e-mail / document), an operator-meaningful handle (original filename, or e-mail subject and sender — never a GUID), when it was received, and the reason — one line, not a wall of links.
- No operator-facing "intake", "custody", raw GUIDs, or other internal vocabulary — the banned-terms rule (`docs/design/README.md:161`, "intake" banned by the recorded 2026-08-04 operator decision) and the internal-identifiers rule (`:168`) apply.
- The dashboard's Unidentified count links to the new tab.

## Why

Release 12 shipped INTK-007's Unidentified surface as a separate nav page whose rows identify items by internal receipt GUIDs and whose copy narrates mechanics. The operator reviewed it in production and gave the direction above. The Queues page (`/Triage`, nav label "Queues") already hosts queue tabs (`queue=not_ready|review|held`), so this is the existing convention extended, not a new pattern.

## Constraints

- UI restructure and copy only where possible; the Unidentified Core/store contracts ([[INTK-007]]) and lifecycle ([[INTK-008]]) do not change. New read-model queries may be needed for the filters (media kind; case origin) — keep them in the existing query owners.
- `docs/design/README.md` binds throughout (one H1, no ledes, one-sentence consequence guidance, no colour-only state, sprite icons).
- FRD-12 owns the queue surface behaviour — update it to match the new structure in the same PR.
- Resolution flow (staff resolving an Unidentified item) must remain reachable from the tab.

## Verification

- [ ] "Unidentified" no longer appears in the primary navigation; the Queues page shows it as a tab with image/e-mail filters that actually filter.
- [ ] Not ready tab filters by Instruction-initiated / Image-initiated and both filters return correct rows.
- [ ] No operator-facing GUID, "intake", or "custody" on the queue surfaces; rows carry filename/subject+sender, received date, reason.
- [ ] Browser + AccessibilityTests green; visual pass at 1920.

## Outcome
