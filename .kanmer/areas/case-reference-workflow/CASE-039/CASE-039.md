---
id: CASE-039
type: ticket
title: 'Engineer notes: append-only staff notes to the Engineer as a Case section'
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - case
  - notes
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - UIIMP-014
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-12-operator-experience.md
docs_todo: true
archived: false
created: '2026-09-02T20:31:38.723Z'
updated: '2026-09-02T22:06:44.821Z'
---

## What

A Case section where staff leave notes for the Engineer, attributed and append-only, separate from the Notes history.

## Why

D32. Mockup source: `Pegasus_UI_v2_src/src/21-case-sections.js` §engineer-notes.

## Approach

- Reuse the Triage append-only note shape (INTK-054); one table, one migration with grants.

## Verification

- [ ] Notes are attributed and cannot be edited or deleted.
- [ ] They do not appear in the Notes history.

## Outcome
