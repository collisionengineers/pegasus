---
id: INTK-021
type: ticket
title: >-
  Extraction auto-adds case details and reads the real document shapes (names,
  registrations, references)
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - extraction
  - operator-reported
  - case-detail
links: []
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-20T14:55:06.997Z'
updated: '2026-08-20T14:55:06.997Z'
---

## Why

Operator, 2026-08-20, verbatim: *"Case details are getting suggested rather than being auto-added. It looks like we're extracting nothing. We arent even getting names or registrations most of the time."*

## What

- Unambiguous, typed-valid extracted instruction fields land as auto-added values (Fact tier with extraction provenance) at case acceptance instead of Suggestions awaiting confirmation; conflicted or implausible candidates stay Suggestions. `CaseDataField.Current = Confirmed ?? Fact ?? Suggestion` carries them everywhere; instruction completeness counts them.
- Case detail rows show the auto-added value with its provenance instead of "Not recorded — Suggested X".
- Extraction rules verified against the real corpus instruction emails (claimant name, claim/our reference, registration at minimum) — label/synonym coverage extended until the real shapes extract; corpus-conditional tests assert it locally (corpus is local-only and never committed).

## How to verify

A fresh real instruction email produces a case whose details are populated (not suggested); corpus-conditional extraction tests pass locally; completeness clears when the auto-added set satisfies the policy.

## Outcome
