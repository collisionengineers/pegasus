---
id: TICK-012
type: ticket
title: INT-25 — Automatic case creation from definitive authorised intake
status: done
area: intake-processing
order: 120
assignee: claude-code
profile: feature
stageEntered:
  verifying: '2026-08-18T10:48:30.360Z'
  done: '2026-08-18T12:22:33.457Z'
labels:
  - capability
  - INT-25
  - now
  - requires-live-approval
groups:
  - HZN-003
links:
  - TICK-017
blocks: []
commits:
  - e6422250
  - 2b0df78c
  - f1e116c6
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/376'
  - 'https://github.com/collisionengineers/pegasus/pull/394'
deployment: production
archived: false
created: '2026-08-12T15:03:53.008Z'
updated: '2026-08-26T14:34:42.718Z'
---

## What

Plan and research **INT-25**: Automatic case creation from definitive authorised intake

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [x] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [x] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — INT-25.
- Canonical owner: [Matching conflicts and reversible association](docs/frd/frd-02-intake-and-source-identity.md#matching-conflicts-and-reversible-association)
- Activation/boundary: The durable processing path consumes every persisted typed QDOS case type and attempts one replay-safe allocation. An Audit is definitive only when its instruction and a separate original report are retained and the report carries exactly one literal outcome: `repairable` or `total loss`. It then creates its Case/PO and `a.` or `ap.` reference automatically, without staff confirmation. Unique existing-case matches bypass allocation. Failures retain a bounded allocation outcome separately from the processing decision and completed-work replay cannot retry; authenticated staff may retry the frozen command with a reason after correction.

## Outcome

INT-25 is live: the deployed Worker created case `QDOS26001` with real Box custody on 2026-08-14 (tier-5 evidence in proof), the fix landed via PR #376 (`e6422250`) and `main` #394, and release 9 codified the manual Worker grant hotfix as migration `20260814092852_AddWorkerCaseCreationGrants` (applied and matrix-verified in production). Follow-ups: DOC-01 UI link / dead-code removal → [[TICK-017]]; clearing the stuck pre-fix backlog via staff Retry allocation → operational task; QDOS-only provider breadth and OCR-literal Audit bound → candidate tickets. Closed out 2026-08-18.
