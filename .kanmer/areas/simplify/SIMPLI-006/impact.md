# Impact — Consolidate ADRs

## Files changed
- `docs/adr/0001-…`–`0024-…` — replace 24 bodies with 9 new self-contained
  ADRs (`0001`–`0009`); delete the rest. Rewrite `docs/adr/README.md` as a
  concise index (no supersession chains).
- `docs/index.md` — make rows 19 & 38 self-contained (own documentation
  structure + new-Markdown placement; stop deferring to ADR-0023/0010).
- `docs/requirements.md`, `docs/capabilities.md` — absorb ADR-0013 product
  rules (image-intake status, gates, cancellation, Box retry, dashboard,
  sequence limits, EVA image rules, AI-05 allocation).
- `azure.yaml` — remove `remoteBuild: true` from `web` and `worker`.
- `scripts/Test-AzureDeploymentPlan.ps1` — assert `minReplicas: 1`; reject
  remote-build configuration.
- **Reference retargeting** to new ADR ids across: `docs/*` links, code
  comments, tests, `.azure/deployment-plan.md`, release-script comments,
  Kanmer tickets. (`CHANGELOG.md` is history — untouched.)

## Risk / coordination
- Overlaps [[SIMPLI-004]]/[[SIMPLI-002]] on `docs/index.md` and the "who owns
  workflow" question → do this stage first so index is self-contained before
  the NOW.md/AGENTS.md rewrite.
- ADR bodies are normally immutable; wholesale replacement is the explicit,
  user-authorised exception this task carries. Git history preserves originals.
- Not docs-only (azure.yaml + scripts). `-Mode Local` gate may need CI (az CLI
  flaky on this box).
