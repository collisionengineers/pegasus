---
id: DELIV-011
type: ticket
title: >-
  Release 11: deploy PLAT-006 (centred shell, Upload redesign) and refresh the
  current-state docs
status: implementing
area: delivery-repository
order: 20
assignee: claude-code
profile: chore
stageEntered:
  implementing: '2026-08-19T08:09:38.115Z'
labels:
  - release
  - requires-live-approval
links:
  - PLAT-006
refs:
  - docs/runbook.md
archived: true
created: '2026-08-19T07:46:11.060Z'
updated: '2026-08-19T12:12:49.433Z'
---

## What

Promote `dev` to `main` (exact-SHA atomic fast-forward under DELIV-002 policy) once [[PLAT-006]] (PR 409) has merged, then run the numbered release route (release 11) so production serves the centred shell and the redesigned Upload screen, and refresh `docs/operations.md` / `docs/current-architecture.md` in the same task.

## Why

The operator reported the two visual defects against production and asked for the fix to be carried through to deployed and verified. `dev` beyond `main` also carries release-10 docs (PR 407) and the INT-31 doc reconciliation (PR 408) — documentation only.

## Route (runbook § Deployment and release, as run for releases 9 and 10)

Build-ReleaseArtifacts → Test-AzureDeploymentPlan (Local/Artifact/PreUpload/PreMigration/PreProvision) → oras push of the digest-pinned OCI image → (no pending migration expected — verify) → `azd provision` creating the digest-pinned Web revision → Worker `config-zip` → Invoke-ProductionSmoke → docs refresh PR → verify → proof.

## Outcome

**Superseded by [[DELIV-012]] (2026-08-19).** Release 11 was prepared locally at `feda958f` and validated (Local + Artifact modes) but the operator held the `main` push and Azure writes; `dev` then moved well past that SHA. No push to main and no Azure write happened under this ticket. Release 12 carries PLAT-006 and everything since. Archived, not deleted.
