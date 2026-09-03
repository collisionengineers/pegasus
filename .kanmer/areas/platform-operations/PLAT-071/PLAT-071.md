---
id: PLAT-071
type: ticket
title: Reconcile the documented DOC/MSG extraction deployment status
status: preparing
area: platform-operations
assignee: codex-root
profile: fix
taken_at: '2026-09-03T08:24:26.514Z'
branch: task/plat-071-doc-msg-deployment-status
worktree: ../pegasus-worktrees/plat-071-doc-msg-deployment-status
claim_expires_at: '2026-09-03T08:54:26.514Z'
claim_controller: codex-root
lease_id: d2208183-acaa-45ab-af7b-68bf1d3d7998
lease_revision: 1
lease_workspace: >-
  worktree:c:\users\alex\documents\github\pegasus-worktrees\plat-071-doc-msg-deployment-status
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-03T08:24:26.514Z'
labels:
  - documentation
  - operations
  - document-extraction
  - doc
  - msg
links: []
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
archived: false
created: '2026-09-03T08:19:46.266Z'
updated: '2026-09-03T08:24:26.514Z'
---

## What

Investigate the conflicting DOC/MSG extraction status in `docs/operations.md` and reconcile the current-state documentation with verified application and deployment evidence. Correct the statement only if the evidence confirms that automatic DOC/MSG extraction is currently implemented and deployed.

## Why

`docs/operations.md` says automatic DOC and MSG extraction remains deferred, while FRD-05, `docs/current-architecture.md`, the composed `IIntakeSourceReader` implementation, and the completed production-deployed [[SIMPLI-013]] record indicate that the CollisionDocNet-derived DOC/MSG readers are active. Operations documentation must describe deployed reality without treating code presence alone as deployment proof.

## Approach

- Verify the live/current deployment evidence and the exact DOC/MSG caller path.
- Determine whether the operations statement is stale, intentionally narrower, or describing a remaining evidence gap.
- If stale, update the existing canonical operations documentation to state the verified current condition and preserve any genuine limitations.
- Report any broader documentation conflict separately rather than expanding this ticket.

## Verification

- [ ] The conclusion is backed by caller and deployment evidence, not only source code.
- [ ] `docs/operations.md`, FRD-05, and `docs/current-architecture.md` no longer make conflicting claims about current DOC/MSG extraction.
- [ ] Relevant documentation checks pass.

## Outcome
