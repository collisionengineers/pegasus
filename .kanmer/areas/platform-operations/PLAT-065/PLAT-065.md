---
id: PLAT-065
type: ticket
title: Provision and activate Azure Document Intelligence PDF OCR
status: implementing
area: platform-operations
order: 860
assignee: pack_reconcile
profile: feature
stageEntered:
  preparing: '2026-09-07T23:01:32.665Z'
taken_at: '2026-09-07T23:16:06.247Z'
branch: PLAT-065-document-intelligence
worktree: .worktrees/plat-065
claim_expires_at: '2026-09-07T23:46:06.248Z'
claim_controller: codex-v1-remediation-root
lease_id: d9fbecef-2648-4b4b-9e34-5b8ae84342e4
lease_revision: 1
lease_controller_run: 20260907T200500Z-v1-remediation
lease_worker_run: pack-reconcile-plat065
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\plat-065'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-07T23:16:06.247Z'
labels:
  - requires-live-approval
  - azure
  - ocr
groups:
  - EPIC-011
  - EPIC-014
links:
  - TICK-041
  - TICK-085
blocks: []
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
deployment: not-deployed
archived: false
created: '2026-09-01T14:40:45.034Z'
updated: '2026-09-07T23:16:06.247Z'
---

## What

Provision and activate the approved Azure Document Intelligence resource and Worker caller needed for PDF OCR, including the supplied visually valid Glass's calculation whose embedded character mapping is unusable.

## Why

The current `rg-pegasus-prod` estate has no Document Intelligence resource. TICK-041 owns the application behavior and new architectural decision; this ticket owns the exact-target infrastructure, identity, deployment and live activation evidence.

## Approach

- Integrate TICK-041's accepted ADR-0040 and existing OCR source-context contract before activation; its current candidate is recorded in research.
- Provision the approved Document Intelligence account in the exact authorized region/resource group through existing Bicep conventions.
- Grant only the Worker managed identity the minimum Cognitive Services data-plane role; do not use application-stored service keys.
- Use `prebuilt-layout`, pin the GA API/model version, and retain response version/hash/confidence through the existing external-work evidence path.
- Keep local/test profiles on deterministic fakes or recorded responses.

## Verification

- [ ] Exact resource, region, SKU, input class, cost and role assignments receive explicit approval before any cloud write.
- [ ] The Worker can process the approved canary and the Web identity cannot call the service.
- [ ] Timeout, throttling, outage, low-confidence and ambiguous results fail closed and recover idempotently.
- [ ] Current-state and operations documents match the deployed estate.

## Outcome
