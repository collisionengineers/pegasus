---
id: TICK-035
type: ticket
title: >-
  INT-04 — Activate additional providers through the shared intake/case workflow
  using separately accepted provider evidence and r…
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - capability
  - INT-04
  - next
  - post-alpha
  - blocked
  - requires-live-approval
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-12T15:03:53.493Z'
updated: '2026-08-25T06:38:34.079Z'
---

## What

Plan and research **INT-04**: Activate additional providers through the shared intake/case workflow using separately accepted provider evidence and rules

## Why

The capability inventory allocates this outcome to **Next / 0.2.0**. This capability is **not designated until post-alpha** (Next / 0.2.0). It is blocked from implementation until the activation evidence and decisions below are accepted.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.
- For each additional provider or intermediary route, define and obtain operator acceptance of the exact sender/document identity evidence, category predicates and exclusions, automatic Case/Triage/Sent matching predicates, no-match/conflict/ambiguity outcomes, multi-rule precedence and any confidence behaviour.
- Name the source-labelled genuine cohort and untouched holdout, acceptance and rollback thresholds, policy author/reviewer/activator roles, version/effective-time behaviour, and re-evaluation/notification rules before activation.
- Preserve ADR-0008's route-owned policies and the shared Core classification result; do not introduce a universal rules engine or duplicate taxonomy.

## Verification

- [ ] A task-level plan records each named route, its exact predicates/exclusions/failure outcomes, real caller, and required tests.
- [ ] Operator-reviewed cohort and untouched-holdout evidence meets explicitly accepted thresholds.
- [ ] Activation and rollback roles, exact mailbox/folder boundary, and required Graph scopes are accepted for the named route.
- [ ] The real provider route is live-verified without weakening fail-closed ambiguity or creating a second business-policy owner.

## Notes

- Source: `docs/capabilities.md` — INT-04.
- Canonical owner: [Owning FRD](docs/frd/frd-02-intake-and-source-identity.md#intake-and-source-identity)
- Evidence and decision gate: `docs/open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display`.
- Activation/boundary: adding reference evidence is not workflow activation. This ticket owns acceptance and activation of additional provider/intermediary policies; [[TICK-036]], [[TICK-037]], and [[TICK-038]] separately own automatic ingestion for the named shared mailboxes after this gate passes.
