---
id: PLAT-065
type: ticket
title: Provision and activate Azure Document Intelligence PDF OCR
status: backlog
area: platform-operations
assignee: ''
profile: feature
labels:
  - requires-live-approval
  - azure
  - ocr
groups:
  - EPIC-011
links:
  - TICK-041
  - TICK-085
blocks: []
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-09-01T14:40:45.034Z'
updated: '2026-09-01T21:59:49.338Z'
---

## What

Provision and activate the approved Azure Document Intelligence resource and Worker caller needed for PDF OCR, including the supplied visually valid Glass's calculation whose embedded character mapping is unusable.

## Why

The current `rg-pegasus-prod` estate has no Document Intelligence resource. TICK-041 owns the application behavior and new architectural decision; this ticket owns the exact-target infrastructure, identity, deployment and live activation evidence.

## Approach

- Wait for TICK-041's accepted next-free ADR and provider-neutral OCR contract.
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
