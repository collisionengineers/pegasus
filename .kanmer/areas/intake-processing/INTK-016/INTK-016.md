---
id: INTK-016
type: ticket
title: >-
  Post-upload confirmation offers create-image-case, cancel, or merge into a
  case found by autocomplete search
status: done
area: intake-processing
order: 1510
assignee: uploadconf-lane
profile: feature
stageEntered:
  implementing: '2026-08-20T05:05:46.289Z'
  review: '2026-08-20T06:47:04.829Z'
  verifying: '2026-08-20T08:10:45.555Z'
  done: '2026-08-20T12:44:56.094Z'
labels:
  - upload
  - ui
  - operator-reported
links:
  - INTK-010
  - INTK-015
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-12-operator-experience.md
prs:
  - '465'
deployment: production
archived: false
created: '2026-08-20T03:16:37.615Z'
updated: '2026-08-25T01:27:00.558Z'
---

## What

Operator, 2026-08-20, verbatim (about the Upload received screen): *"Once uploaded, the next screen gives no real indication as to what happened or gives the user any options. It should suggest to automatically create an image initiated case (but they have the option to cancel, or to merge into an instructions case - there should be a search with autocomplete for our cases for this."*

So the post-upload surface must:
- state plainly what happened / is happening;
- offer **Create an image-initiated case** as the suggested action (for image uploads with no located case);
- offer **Cancel**;
- offer **Merge into an existing case**, with a case search box that autocompletes against our cases.

## Why

[[INTK-010]] shipped rows+states but the confirmation decision step the operator asked for is still not what production shows — the screen lists files and 'Received' with no options.

## Verification

- [ ] After an image upload, the operator sees the three options; each works end to end.
- [ ] Autocomplete returns matching case references as the operator types.
- [ ] Fail-closed rules hold: the staff decision is explicit; no silent automatic attach beyond INT-28's accepted bar.
- [ ] Browser + accessibility suites green.
