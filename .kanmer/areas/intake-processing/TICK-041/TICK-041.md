---
id: TICK-041
type: ticket
title: INT-16 — Azure OCR for scan-like and unusable-text-map PDF pages
status: verifying
area: intake-processing
order: 950
assignee: codex-v1-remediation-root
profile: feature
stageEntered:
  preparing: '2026-09-07T21:14:38.247Z'
  review: '2026-09-07T22:56:59.820Z'
  verifying: '2026-09-07T23:11:06.281Z'
taken_at: '2026-09-07T22:04:42.073Z'
branch: TICK-041-qualified-ocr
worktree: .worktrees/tick-041
claim_expires_at: '2026-09-08T02:17:11.125Z'
claim_controller: codex-v1-remediation-root
lease_id: 80597482-bbde-49de-afe1-a3a97a0d65c9
lease_revision: 17
lease_controller_run: 20260907T200500Z-v1-remediation
lease_worker_run: root-ocr
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\tick-041'
lease_provider: codex
lease_phase: verifying
lease_heartbeat_at: '2026-09-08T01:47:11.125Z'
labels:
  - capability
  - INT-16
  - now
  - evidence-required
  - azure
  - ocr
groups:
  - EPIC-009
  - EPIC-011
  - EPIC-014
links:
  - PLAT-065
  - TICK-085
blocks: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
docs_todo: true
commits:
  - 890f656be13149c140980b86837e2b7897d26117
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/686'
archived: false
created: '2026-08-12T15:03:53.610Z'
updated: '2026-09-08T01:47:11.125Z'
---

## What

Activate one provider-neutral Azure Document Intelligence OCR path for persisted scan-like PDF pages and for visually valid PDF estimate pages whose embedded character map is unusable.

## Why

INT-16 was previously allocated only to scan-like instruction pages. The operator has now required all supplied Glass's calculations to import, including YL69YFO: the document renders correctly but its embedded glyph mapping is unusable. Current ADR-0001 excludes that class, so implementation requires a new next-free ADR that supersedes ADR-0001 while preserving ADR-0005's intake limits.

## Approach

- Keep embedded PdfPig extraction first for ordinary readable PDFs.
- Qualify only two OCR input classes: persisted scan-like pages under the existing intake rule, and visually valid post-Case estimate pages whose text-map failure is positively detected.
- Never send corrupt, encrypted, non-renderable or merely ambiguous documents to OCR.
- Define one Core-neutral page/text/coordinate/confidence result consumed by deterministic provider parsers.
- Run the external call in Worker through the existing staged-blob, outbox, external-work, retry and attribution conventions.
- Use Azure Document Intelligence `prebuilt-layout`; pin and record the GA API/model version and response hash.
- Fail closed to staff review on low confidence, missing structure, inconsistent totals or provider outage.
- [[PLAT-065]] owns exact-target Azure provisioning and activation; this ticket does not authorize a cloud write.

## Governing changes

- Write ADR-0040 and mark ADR-0001 superseded; ADR-0039 belongs to release workstation support.
- Update FRD-05, FRD-07 and capabilities INT-16/EXT-12 alongside the current OCR contract.
- Leave ADR-0005 accepted for ordinary intake behavior.

## Verification

- [ ] Embedded-text PDFs never incur an OCR call.
- [ ] A scan-like fixture and the supplied YL69YFO calculation produce versioned coordinate/confidence evidence through the same port.
- [ ] Corrupt/encrypted/non-renderable input is rejected without an OCR call.
- [ ] Timeout, throttling, replay, low confidence and ambiguous results are durable and idempotent.
- [ ] No local/test profile calls Azure.

## Outcome

## Current authority — 7 September 2026

The operator explicitly authorizes Azure Document Intelligence provisioning and implementation for v1. The old no-live-approval assumption is superseded; exact resource/role/cost details will be recorded by [[PLAT-065]]. ADR-0040 is reserved for this application contract (ADR-0039 belongs to [[DELIV-048]]). Current runtime already has the intake OCR port/provider/Worker/store; extend and wire those, do not rebuild them. [[TICK-085]] retains the deterministic Glass's PDF parser and five-sample acceptance scope.

## Integrated contract and remaining acceptance — 8 September 2026

PR686 is integrated in dev at d367219669ad26d5f2b727bd10b582330febc906.
The accepted ADR-0040 and existing source-bound OCR/Worker contract are available
to [[TICK-085]], [[PLAT-065]] and [[INTK-049]]. Their execution no longer waits
for this ticket to reach Done: that former edge formed a completion cycle
because this ticket's caller/live acceptance needs TICK-085 and PLAT-065.
Remove only the obsolete execution-block edges; retain all links, unchecked
acceptance and Verifying status. This is not live activation or OCR-provider
success. Exact integrated/caller/provider evidence remains required before Done.
