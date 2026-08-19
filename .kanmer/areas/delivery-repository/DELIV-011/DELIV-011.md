---
id: DELIV-011
type: ticket
title: >-
  Release 11: deploy PLAT-006 (centred shell, Upload redesign) and refresh the
  current-state docs
status: backlog
area: delivery-repository
assignee: ''
profile: chore
labels:
  - release
  - requires-live-approval
links:
  - PLAT-006
refs:
  - docs/runbook.md
archived: false
created: '2026-08-19T07:46:11.060Z'
updated: '2026-08-19T07:46:11.060Z'
---

## What

Promote `dev` to `main` (exact-SHA atomic fast-forward under DELIV-002 policy) once [[PLAT-006]] (PR 409) has merged, then run the numbered release route (release 11) so production serves the centred shell and the redesigned Upload screen, and refresh `docs/operations.md` / `docs/current-architecture.md` in the same task.

## Why

The operator reported the two visual defects against production and asked for the fix to be carried through to deployed and verified. `dev` beyond `main` also carries release-10 docs (PR 407) and the INT-31 doc reconciliation (PR 408) — documentation only.

## Route (runbook § Deployment and release, as run for releases 9 and 10)

Build-ReleaseArtifacts → Test-AzureDeploymentPlan (Local/Artifact/PreUpload/PreMigration/PreProvision) → oras push of the digest-pinned OCI image → (no pending migration expected — verify) → `azd provision` creating the digest-pinned Web revision → Worker `config-zip` → Invoke-ProductionSmoke → docs refresh PR → verify → proof.

Approvals required and to be requested explicitly: `MERGE AUTH GRANTED` immediately before the atomic push; Azure writes for exactly ACR `pegasusprodacr252ow37gij`, Container App `pegasus-prod-web-252ow37gij` (via `azd provision`), Function App `pegasus-prod-worker-252ow37gij` (rg `rg-pegasus-prod`, sub `e6076573-…`).

## Verification

- [ ] `origin/main == origin/dev == <SHA>`; main-push CI guard green
- [ ] `/diagnostics/version` sourceSha == SHA; smoke exit 0
- [ ] `/Upload` on production shows the two-column layout; `.app-rail-main` centred at 1920
- [ ] operations.md release-11 row; current-architecture release sentence

## Outcome
