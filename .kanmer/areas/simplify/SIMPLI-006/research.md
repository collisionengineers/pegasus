# Research — Consolidate ADRs

Root plan: `docs/temp-plans/retire-now-rewrite-agents.md` (shared with
[[SIMPLI-004]] [[SIMPLI-002]] [[SIMPLI-005]]). Agreed mapping already on disk:
`docs/temp-plans/simplify/adr-consolidation.md` — implement it, don't re-derive.

## Current state
- `docs/adr/` holds 24 ADRs: `0001`–`0016`, `0018`–`0024` (**ADR-0017 is
  absent** — a numbering collision was resolved into 0018/0019 per README).
  `README.md` is a rich supersession index.
- `docs/index.md:19,38` and ADR-0010/0023 currently own documentation-structure
  and new-Markdown-placement rules; the plan moves that ownership INTO
  `docs/index.md` (self-contained) and keeps the task workflow solely in
  `AGENTS.md`.
- ADR-0009 and ADR-0023 already record a **2026-08-12 operator decision:
  "AGENTS.md owns the task operating procedure"** — the direction is partly
  ratified.
- `azure.yaml` has `remoteBuild: true` on **both** `web` and `worker`.
- `infra/modules/platform.bicep:456` is already `minReplicas: 1` (warm), but
  `scripts/Test-AzureDeploymentPlan.ps1:190` still asserts `minReplicas: 0` —
  a stale assertion the plan's "require minReplicas:1" edit reconciles.
- `scripts/Test-AzureDeploymentPlan.ps1` already forbids remote build via
  `SCM_DO_BUILD_DURING_DEPLOYMENT` (line 201) but does not check `azure.yaml`
  `remoteBuild`. `-Mode Local` runs `az bicep build` (needs the `az` CLI).

## Target (from adr-consolidation.md)
9 self-contained ADRs `0001`–`0009` per the fixed mapping table; no supersession
chains. Remove ADR-0010, ADR-0016, ADR-0023. Move ADR-0013 product rules into
Requirements/Capabilities. Retarget every tracked reference to the new ids.
