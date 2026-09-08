---
id: DELIV-059
type: ticket
title: Restore release-39 history to the canonical operations record
status: preparing
area: delivery-repository
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-09-08T16:08:50.581Z'
labels:
  - documentation
  - release-evidence
  - corrective
links:
  - DELIV-054
  - DELIV-048
  - DELIV-047
refs:
  - docs/index.md
  - docs/engineering.md
  - docs/adr/0007-direct-terminal-azure-deployment.md
deployment: n/a
archived: false
created: '2026-09-08T16:07:49.662Z'
updated: '2026-09-08T16:08:50.581Z'
---

## What

Restore the missing dated release-39 facts and failures from PR #676 to current dev's canonical docs/operations.md, preserving the current source-versus-deployment documentation boundary.

## Why

The approved next-corrective-deployment plan D1 explicitly requires release-39 evidence to survive before PR #676 is disposed as superseded. [[DELIV-054]] / PR #703 already integrated its useful ZIP correction, while current operations describes release 38 and its linked historical ledger omits release 39. PR #676 must not be merged unchanged or closed as wholly superseded while its unique operational record is missing. [[DELIV-048]] portable release support and [[DELIV-047]] historical Linux work remain separate retained claims, not scope to reopen.

## Approach

- Research exact PR676 source/artifact/migration/Worker replacement/reset records against original retained evidence; record any unverified claim as such, not fresh observation.
- Change only current docs/operations.md with the dated historical record, retaining the rejected original ZIP, non-identical replacement/provenance deviation, partial migration sequence and separately authorized reset.
- Do not restore obsolete Linux-only guidance, stale source architecture, credentials, new commands, live status assumptions, or deployment permissions.
- After independent review and integration, hand root the exact documentation SHA needed alongside PR703 for an explicit PR676 superseded disposition. This ticket does not itself authorize closing foreign claims or deploying.

## Verification

- [ ] Historical statements trace to exact retained source/receipt and preserve failures and scope limitations.
- [ ] Current architecture/source descriptions and release procedures stay unchanged.
- [ ] Relevant documentation link/placement checks and independent semantic review pass; no dotnet build/test for prose-only edits.
- [ ] Exact merged-SHA proof precedes Done.

## Outcome
