# Plan — Consolidate ADRs (Stage A, do first)

Implement `docs/temp-plans/simplify/adr-consolidation.md` verbatim.

1. Author the 9 new self-contained ADRs `0001`–`0009` from the mapping table
   (no "supersedes" wording; each states its own current decision + rationale).
2. Delete the superseded ADR files; rewrite `docs/adr/README.md` as a concise
   9-row index. Assert `docs/adr/` = `0001`–`0009` + `README.md` only.
3. Move ADR-0013 product rules into `requirements.md`/`capabilities.md`; keep
   only technical boundaries in new `0001`/`0004`.
4. Make `docs/index.md` self-contained (documentation ownership + new-Markdown
   placement); keep task workflow pointer to `AGENTS.md#repository-task-workflow`.
5. Release-config: remove `remoteBuild` from `azure.yaml`; update
   `Test-AzureDeploymentPlan.ps1` to assert `minReplicas: 1` and reject
   remote build.
6. Retarget all tracked references to new ADR ids; grep to prove none remain.

## Acceptance
`docs/adr/` = `0001`–`0009` + README; no removed filenames/supersession wording
tracked; `azure.yaml` has no `remoteBuild`; deployment-test asserts
`minReplicas: 1` + rejects remote build.

## Verify
`pwsh ./scripts/Test-DocumentationLinks.ps1`;
`pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` (or CI if az flaky);
`git grep -nE '0010-adopt-single-context|0016-standalone|0023-restructure'` →
no live hits; `git diff --check`.

**Held for user review before any edit.** Open decisions in root plan: one PR
vs staged; whether release-config belongs here.
