---
id: INTK-015
type: ticket
title: >-
  One grouped upload yields one Image-initiated Case, promptly, without a
  per-image case explosion
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels:
  - defect
  - grouped-upload
  - image-intake
  - operator-reported
  - production
links:
  - INTK-011
  - INTK-006
  - INTK-012
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-20T03:16:37.547Z'
updated: '2026-08-20T03:16:37.547Z'
---

## What

Operator, 2026-08-20, verbatim (about a ~8-image WhatsApp upload of a Suzuki Vitara, plate AU17 SEO, group id starting 520b2f69): *"whilst writing this, the images actually DID get put in as an image initiated case, HOWEVER, each individual image in the upload was entered as a seperate case. They should all have been the one case, as it was all the same upload. In addition the time it took for this was lengthy, and in the interim they were showing in unidentified. They still didnt make it to Box either."*

Fix grouped-image processing so one submission group with one readable VRM produces exactly **one** Image-initiated Case containing every member — promptly, without a detour through Unidentified.

## Why

[[INTK-011]] made the group outcome atomic against the fallback race, but production shows each member still registered its own case (AU17SEO-01, -02, …) — the group outcome is not collapsing members into one registration, and the deferral path parks members in Unidentified until the slow reconciliation sweep picks them up. This is the group-is-the-evidence-unit contract of [[INTK-006]] still not held end to end.

## Verification

- [ ] A multi-image single-VRM group registers one Image-initiated Case with all members, across repeated runs and under concurrency.
- [ ] No interim Unidentified appearance for a group that resolves to a case.
- [ ] Resolution happens on the next processing pass (minutes, not hours).
- [ ] Production readback: the AU17SEO cases consolidated/closed per operator direction, future uploads produce one case.
