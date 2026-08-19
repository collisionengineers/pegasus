# Proof — TICK-215

Verified on merged `dev` at `4d1bff3db4ed16692e7646ea07e7f4491365defd`, the merge commit for [DOCS-002 PR #413](https://github.com/collisionengineers/pegasus/pull/413), merged 2026-08-19T09:19:55Z.

## Decision-tier evidence

- `git rev-parse HEAD` → `4d1bff3db4ed16692e7646ea07e7f4491365defd`.
- `git log -1 --format=%H -- docs/adr/0028-run-integrated-renderer-in-web-container-app.md` → `169bcd5bbe1e334a52dbb18725d1ae46c6e8f6ab`.
- `gh pr view 413 --json state,mergedAt,url,mergeCommit,headRefName,baseRefName` confirmed PR #413 is MERGED into `dev` with merge commit `4d1bff3d`.
- `pwsh -NoProfile -File scripts/Test-DocumentationLinks.ps1` passed: all relative Markdown links resolve across 224 files.
- Direct inspection of ADR-0028 confirmed:
  - accepted status and stable ADR identity;
  - in-process execution inside the existing Pegasus Web Container App;
  - Chromium/Playwright native dependencies and approved fonts belong to the Web image;
  - the existing Flex Consumption Worker remains unchanged;
  - no renderer API, Container App, Container Apps Job, Function App, or queue consumer is deployed.
- Direct inspection of `docs/adr/README.md` confirmed the ADR-0028 accepted index row.
- TICK-215 retains refs to FRD-11, ADR-0025, and ADR-0028; its open-questions document has no unticked question and explicitly parks future detached execution behind measured evidence and a new accepted ADR.
- The ticket branch/worktree is clean and has no repository diff; traceability points to DOCS-002 source commit `169bcd5b`, merge commit `4d1bff3d`, and PR #413. Deployment is `n/a`.

## Result and evidence boundary

The production execution-location decision is proved: integrated report rendering belongs in the existing Web Container App, not Worker or a separate deployment unit. This proof does **not** claim renderer source integration, a real assessment caller, image/runtime readiness, capacity, deployment, recovery, or operator acceptance; those remain with SIMPLI-014 and PLAT-007. No Azure write or `main` update was performed.
