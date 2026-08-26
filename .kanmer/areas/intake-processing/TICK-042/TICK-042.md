---
id: TICK-042
type: ticket
title: INT-28 — Automatic matching of image-led and instruction-led records
status: done
area: intake-processing
order: 130
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-17T13:21:33.442Z'
  implementing: '2026-08-20T03:36:15.641Z'
  review: '2026-08-20T03:36:17.850Z'
  verifying: '2026-08-20T03:36:18.094Z'
  done: '2026-08-20T03:36:20.764Z'
labels:
  - capability
  - INT-28
  - now
groups:
  - HZN-003
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
deployment: production
archived: false
created: '2026-08-12T15:03:53.630Z'
updated: '2026-08-26T14:34:42.738Z'
---

## What

Plan and research **INT-28**: Automatic matching of image-led and instruction-led records

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — INT-28.
- Canonical owner: [Matching conflicts and reversible association](docs/frd/frd-02-intake-and-source-identity.md#matching-conflicts-and-reversible-association)
- Activation/boundary: Operator-directed and threshold-accepted 2026-08-03 at the 0.80 bar with the accepted match rules ([ADR-0019](adr/0019-in-process-onnx-vrm-recognition.md) index entry owns the numbers). Pairing runs in both directions — a confident scan matches waiting eligible cases, and an accepted case matches waiting Image intakes on exact registration equality only (a registered identity is immutable, so the completion rules apply only at scan time). Automatic association requires exactly one eligible pre-report case consistent with the confirmed registration and no contradictory identity evidence; anything else stays a reasoned staff decision.
