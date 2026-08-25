---
id: CASE-007
type: ticket
title: >-
  Case page shows only what matters: short read-only view, operator words,
  toggle edit
status: done
area: case-reference-workflow
order: 1110
assignee: claude-code
profile: fix
stageEntered:
  review: '2026-08-20T18:18:42.892Z'
  verifying: '2026-08-20T19:11:42.057Z'
  done: '2026-08-20T20:51:52.137Z'
labels:
  - ui
  - design
  - operator-reported
  - case-detail
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
archived: false
created: '2026-08-20T17:39:20.825Z'
updated: '2026-08-25T06:38:29.911Z'
---

## Why

Operator, 2026-08-20, verbatim: *"Case details page still has icons escaping."* · *"the page is extremely long, contravening our design. Also has 'intake' appear twice on the page."* · *"this all reeks of internal dev-speak - this shouldn't be showing to the user"* (EVA handoff panel) · *"this is also causing problems as it is lengthening the page beyond any reasonable level."* · *"these boxes are all disjointed. There also shouldn't be boxes for this if we arent in edit mode."* · *"Change the edit system on cases to a toggle button. When attempting to toggle off, if changes were made, popup asks if user wants to save their case changes."*

## What

- Read-only view renders only populated, relevant sections (new design rule): Lifecycle actions, Immutable report approval, Report-Sent evidence, Case tasks, Vehicle evidence, Case custody, EVA panels absent when empty or edit-only.
- EVA handoff becomes one compact card in operator words (readiness disclosure per the ENG-003 pattern; dev-speak bullets rewritten/removed).
- Edit mode becomes a toggle button in the action bar (existing lease acquire/release); toggling off with unsaved changes asks Save / Discard / Keep editing.
- "Accepted intake is incomplete" → "Details are incomplete" (writer changed; stored legacy string display-mapped — no data migration).
- Raw enum inspection-mode render → label map; escaping icons fixed in CSS; remaining narration lines removed.

## How to verify

A fresh Not-ready case's page ends shortly after Chase history; no "intake", no dev-speak, no empty panels read-only; edit toggle works with the dirty-check dialog; integration + browser suites green.

## Outcome
