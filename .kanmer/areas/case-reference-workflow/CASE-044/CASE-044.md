---
id: CASE-044
type: ticket
title: >-
  Add evidence to a case: upload files or absorb an existing image-initiated
  case, reachable from the case and the rail
status: backlog
area: case-reference-workflow
order: 180
assignee: ''
profile: feature
labels:
  - case
  - evidence
  - image-initiated
  - upload
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
archived: false
created: '2026-09-03T10:53:18.687Z'
updated: '2026-09-03T15:15:27.266Z'
---

## What

One prominent "Add evidence" action for an instruction-initiated case,
offering two routes:

1. Upload files directly to the case, and
2. Link an existing image-initiated (Awaiting instruction) case, which is
   then absorbed into this case by the existing merge process.

Route 2 is the reverse of [[CASE-042]]'s "Add to an existing case", started
from the instructed case rather than from the pre-case queue. The operator's
preference is that the action is generally available rather than buried:
on the case action bar and on the main rail.

## Why

Operator request (2026-09-03), recorded alongside the [[CASE-042]] answer:
image material arrives before or after the instruction, and today only the
pre-case queue can join the two. The instructed case needs the same join
from its own side.

## Approach

- Reuse the existing upload path and the existing image-case absorption
  process used by the Awaiting instruction queue; add no second merge
  implementation.
- Placement follows the design authority: labels and values only, no
  explanatory copy, one consequence sentence on the absorb confirmation.

## Verification

- [ ] Uploading from the case attaches the files with normal custody.
- [ ] Absorbing an image-initiated case produces the same result as
      absorbing it from the pre-case queue, and the image case leaves the
      queue.
- [ ] The action is reachable from the case and from the rail.

## Outcome
