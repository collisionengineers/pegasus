---
id: SIMPLI-007
type: ticket
title: Move the QDOS alpha acceptance gate out of application composition
status: done
area: platform-operations
order: 270
assignee: claude-code
profile: feature
stageEntered:
  review: '2026-08-17T12:30:24.172Z'
  verifying: '2026-08-17T12:49:53.151Z'
  done: '2026-08-17T13:08:33.407Z'
labels: []
groups:
  - EPIC-002
  - HZN-003
links: []
blocks: []
refs:
  - docs/adr/0013-qdos-alpha-implementation-contract.md
commits:
  - c9e657c3
  - 88fcde2a
  - d677a39d
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/388'
deployment: not-deployed
archived: false
created: '2026-08-13T12:12:48.841Z'
updated: '2026-08-26T14:34:42.993Z'
---

## What

Remove the QDOS alpha acceptance gate from Core and Web composition while retaining useful release validation in tooling.

## Why

A test-only manifest checker is currently part of the running application and carries obsolete release requirements.

## Approach

- Move the validator to release tooling.
- Remove the unused application-facing gate and interface.

## Verification

- [x] Application composition no longer registers the acceptance gate and release validation remains available — see `proof`.

## Outcome

Shipped in PR #388 (https://github.com/collisionengineers/pegasus/pull/388), merged to `dev` as `d677a39d` on 2026-08-17; not deployed. `Pegasus.Core` no longer carries the gate (only the `CoreAssembly` marker), Web registers nothing, the registration/manifest tests are gone. `scripts/Invoke-QdosAlphaAcceptance.ps1 -Profile OfflineCandidate` owns the coverage check with the alpha roster read from `docs/capabilities.md` (131 rows at `0.1.0-alpha.1`) instead of a hard-coded list that had drifted (demanded retired DOC-06, missed 15 later alpha rows), plus real evidence-file re-hashing; the `PEGASUS_QDOS_ACCEPTANCE_*` env contract is gone.

Shipped differently than the ticket's "move the validator": the C# class was deleted rather than relocated to a tooling project (no caller but the script; a new project would need its own ADR), and the roster is derived rather than moved — both decisions recorded in `open-questions` and confirmed by the independent reviewer. Note for [[SIMPLI-003]]: because the runner reads the alpha rows at run time, re-targeting a capability away from `0.1.0-alpha.1` shrinks the acceptance roster automatically.
