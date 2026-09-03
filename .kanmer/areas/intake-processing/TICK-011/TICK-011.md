---
id: TICK-011
type: ticket
title: INT-17 — Automatic vehicle-registration reading from ordinary vehicle images
status: done
area: intake-processing
order: 110
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-17T13:21:33.416Z'
  implementing: '2026-08-18T15:23:56.120Z'
  review: '2026-08-18T15:23:56.177Z'
  verifying: '2026-08-18T15:23:56.230Z'
  done: '2026-08-18T15:24:28.274Z'
labels:
  - capability
  - INT-17
  - now
groups:
  - HZN-003
links: []
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
commits:
  - ef3eb4c7
  - ba65c1ed
deployment: production
archived: false
created: '2026-08-12T15:03:52.988Z'
updated: '2026-09-03T09:06:42.647Z'
---

## What

Plan and research **INT-17**: Automatic vehicle-registration reading from ordinary vehicle images

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — INT-17.
- Canonical owner: [Ordinary-image VRM and image analysis](docs/frd/frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis)
- Activation/boundary: Allocated but non-blocking for `0.1.0-alpha.1` acceptance; the engine is the in-process ONNX stack of [ADR-0019](adr/0019-in-process-onnx-vrm-recognition.md), scanning image-only material automatically in the intake pipeline. The operator accepted the threshold 2026-08-03 at the **0.80** bar with the accepted match rules (full-cohort run `20260803-092906`; the ADR-0019 index entry owns the numbers). Source-image-bound with recorded abstention/failure outcomes; no instruction invention or external upload, and the only autonomous actions are the `INT-28`/`INT-32` register+associate at the accepted bar.

## Outcome

Retrospective reconciliation completed on 2026-08-18. INT-17 was already present on merged `main`; no TICK-011 source diff or PR was created. Independent review passed and the focused ImageIntake Core suite passed 78/78 on `origin/main` at `d8de29cb`.

**Corrected 2026-08-19 by [[DELIV-012]].** This Outcome previously cited commits `ae6f0c2d`, `ef3eb4c7` and `f7d99b18`. Two of those are unreachable pre-rebase objects — `git branch -a --contains` returns no refs for either — so the citation could not be reproduced. The reachable delivery commits are **`ef3eb4c7` and `ba65c1ed`**, both ancestors of `d8de29cb`.

The `deployment` field previously read `not-deployed`, which was false about the shipped code: `origin/main` contains 20 ImageIntake paths including the Core sources, `EfImageIntakeStore`, migration `20260803071539_ImageIntakeRegistration`, the Web pages and the tests. It now reads `production`, with the honest qualification recorded in `proof` and `open-questions`: **the code is shipped, but production caller execution has never been evidenced.** That is an activation gap, owned by [[INTK-006]] and [[INTK-008]], not a deployment gap — and release scoping must not treat INT-17 as undeployed work.
