---
id: ADR-0038
status: accepted
date: 2026-09-07
supersedes: [ADR-0034]
superseded_by: []
related_capabilities: [EXT-04]
related_frd: [frd-07]
tags: [eva, configuration, principals]
---

# ADR-0038: Manual-only EVA API submission

## Status

Accepted, 2026-09-07.

## Context

ADR-0034 introduced independent manual and automatic EVA API submission
settings for each Principal. The current v1 design retains the staff-initiated
route and removes unattended submission when a case reaches `Review`.

EVA has no idempotency key and cannot withdraw a claim created by a successful
submission. The existing manual route already presents the mapped case and
images from the Case action, records the outcome, and leaves an `Unknown`
outcome for staff review before any explicit re-send.

## Decision

Each Principal carries one persisted `EvaManualSubmission` setting, off by
default. When enabled, it offers Send via API from the Case's Send to EVA
dialog. Staff may send from `Review` and explicitly re-send from `With
Engineer` under FRD-07's existing outcome and history rules.

Pegasus does not submit to EVA solely because a case reaches `Review`.
`EvaAutomaticSubmission`, its reconciliation sweep and its external-work kind
are removed. The application is pre-release; the approved target requires
manual submission only and no preservation of the obsolete automatic path.

The Principal create, replacement and administration operations carry only the
manual setting. A replacement Principal inherits it; a disabled Principal's
setting remains frozen with its historical identity.

## Consequences

- The manual API route, ZIP export and four distinct submission outcomes remain.
- No Worker timer or queue item initiates an EVA submission.
- Failure and `Unknown` recovery require a deliberate staff action; neither is
  silently retried.
- The automatic column and its runtime permissions are removed in the same
  clean-target schema change.
- ADR-0034 remains the historical record of the superseded two-setting design.

## Links

- [FRD-07 — Direct EVA API submission](../frd/frd-07-eva-and-external-engineering-handoff.md#direct-eva-api-submission)
- [ADR-0034 — Per-Principal EVA API submission settings](0034-per-principal-eva-api-submission-settings.md)
