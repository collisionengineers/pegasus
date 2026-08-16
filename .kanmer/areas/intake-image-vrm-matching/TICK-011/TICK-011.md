---
id: TICK-011
type: ticket
title: INT-17 — Automatic vehicle-registration reading from ordinary vehicle images
status: backlog
area: intake-image-vrm-matching
assignee: ''
profile: feature
labels:
  - capability
  - INT-17
  - now
links: []
archived: false
created: '2026-08-12T15:03:52.988Z'
updated: '2026-08-13T14:46:24.073Z'
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
- Canonical owner: [Ordinary-image VRM and image analysis](requirements.md#ordinary-image-vrm-and-image-analysis)
- Activation/boundary: Allocated but non-blocking for `0.1.0-alpha.1` acceptance; the engine is the in-process ONNX stack of [ADR-0019](adr/0019-in-process-onnx-vrm-recognition.md), scanning image-only material automatically in the intake pipeline. The operator accepted the threshold 2026-08-03 at the **0.80** bar with the accepted match rules (full-cohort run `20260803-092906`; the ADR-0019 index entry owns the numbers). Source-image-bound with recorded abstention/failure outcomes; no instruction invention or external upload, and the only autonomous actions are the `INT-28`/`INT-32` register+associate at the accepted bar.
