---
id: INTK-015
type: ticket
title: >-
  One grouped upload yields one Image-initiated Case, promptly, without a
  per-image case explosion
status: done
area: intake-processing
order: 1260
assignee: group-lane
profile: fix
stageEntered:
  implementing: '2026-08-20T03:35:13.660Z'
  review: '2026-08-20T04:16:01.270Z'
  verifying: '2026-08-20T04:40:05.491Z'
  done: '2026-08-20T12:44:47.945Z'
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
commits:
  - 86fa5a63
  - 7fc21009
  - 8ea7be87
  - c7109d97
  - 0605c431
prs:
  - '447'
deployment: production
archived: false
created: '2026-08-20T03:16:37.547Z'
updated: '2026-09-01T14:44:32.847Z'
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
