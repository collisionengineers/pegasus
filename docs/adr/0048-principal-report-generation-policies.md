---
id: ADR-0048
status: accepted
date: 2026-09-10
supersedes: [ADR-0038]
superseded_by: []
related_capabilities: [EXT-04]
related_frd: [frd-04, frd-07]
tags: [eva, report-generation, principals]
---

# ADR-0048: Principal report generation policies

## Status

Accepted. This record supersedes ADR-0038's manual-only EVA API policy.

## Context

ADR-0038 allowed only a manually initiated EVA API submission. The operator has
confirmed four Principal-level routes for new report work: Pegasus generation,
EVA ZIP export, manual EVA API submission, and EVA API submission when a Case
enters Review. The prior Boolean could neither represent ZIP export nor make a
Review-time choice durable.

Report-email addressing is also a Principal configuration. A Principal may
suggest the original instruction sender and any number of additional addresses;
the original sender comes from the Case's originating instruction provenance,
not a later reply. These remain suggestions for staff review and send. A Claim
Source is not copied implicitly.

## Decision

Replace the manual-only Boolean with `PrincipalReportGenerationPolicy`:
`Pegasus`, `EvaZip`, `EvaManualApi`, or `EvaAutomaticApiOnReview`. Persist the
recipient settings with the Principal.

An automatic policy creates a uniquely keyed intent in the transaction that
moves a Case into Review. The Worker claims and executes that intent through
the same validated EVA submission owner used by the manual API route. A
successful, partial, or rejected outcome completes the intent; an unknown
outcome shows check-EVA-and-retry advice and is not retried automatically. Policy changes
do not scan or enqueue Cases that were already in Review.

On failure, Pegasus tells staff to check EVA and retry if no Case was created.
The operator explicitly rejected an attestation workflow and persistent retry
blocking. Failed automatic API sends also permit a staff retry; uncertain
outcomes are not retried automatically. The existing ledger retains observed
attempts and supports exact completed-operation replay.

ZIP remains an export using the existing EVA package contract. It is presented
as an export and does not assert that EVA received it. Automatic EVA submission
does not send outward report email.

## Consequences

ADR-0038 is superseded. The UI presents the choice as two sequential
decisions in the Principal Contact — Pegasus or EVA, then, only for EVA, ZIP
export, manual API submission, or automatic API submission at Review — which
together persist one of the four policy values, alongside the recipient
settings. Report preparation freezes the resolved recipients in its immutable
delivery preparation and still requires a staff send action.

## Links

- [ADR-0038 — Manual-only EVA API submission](0038-manual-only-eva-api-submission.md)
- [FRD-04 — Parties, accounts, and access](../frd/frd-04-parties-accounts-and-access.md#contacts-administration)
- [FRD-07 — EVA and external engineering handoff](../frd/frd-07-eva-and-external-engineering-handoff.md#eva-handoff-routes)
