---
id: CASE-045
type: ticket
title: Show an optional known principal on image-initiated cases
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - image-intake
  - principal
  - ui
groups:
  - EPIC-011
  - EPIC-012
links:
  - CASE-042
  - CASE-032
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-04T10:21:48.548Z'
updated: '2026-09-04T10:21:48.548Z'
---

## What

Show the principal on an image-initiated case when it is known. The field is
optional because a principal is not always known when image material is
received.

Do not require a principal to create or retain an image-initiated case, and
do not infer or fabricate one solely to populate the field.

## Why

An operator should be able to see a known principal while preserving the
valid image-first intake path where no principal has yet been identified.

## Approach

Extend the existing image-initiated queue/detail projection owned by
[[CASE-042]] and its row projection work in [[CASE-032]]. Reuse the canonical
principal relationship and omit the value when none is recorded.

## Verification

- [ ] An image-initiated case with a recorded principal displays it in the
      relevant image-initiated case view.
- [ ] An image-initiated case without a principal remains valid and does not
      show a fabricated value.
- [ ] No new principal-matching or case-creation rule is introduced.

## Outcome
