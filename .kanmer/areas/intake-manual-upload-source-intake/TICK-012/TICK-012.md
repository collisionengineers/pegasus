---
id: TICK-012
type: ticket
title: INT-25 — Automatic case creation from definitive authorised intake
status: todo
area: intake-manual-upload-source-intake
priority: medium
assignee: ''
labels:
  - capability
  - INT-25
  - now
  - requires-live-approval
links: []
archived: false
created: '2026-08-12T15:03:53.008Z'
updated: '2026-08-12T15:03:53.008Z'
---

## What

Plan and research **INT-25**: Automatic case creation from definitive authorised intake

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — INT-25.
- Canonical owner: [Matching conflicts and reversible association](requirements.md#matching-conflicts-and-reversible-association)
- Activation/boundary: The durable processing path consumes every persisted typed QDOS case type and attempts one replay-safe allocation. An Audit is definitive only when its instruction and a separate original report are retained and the report carries exactly one literal outcome: `repairable` or `total loss`. It then creates its Case/PO and `a.` or `ap.` reference automatically, without staff confirmation. Unique existing-case matches bypass allocation. Failures retain a bounded allocation outcome separately from the processing decision and completed-work replay cannot retry; authenticated staff may retry the frozen command with a reason after correction.
