# Impact — ADR modernization

## Files changed (all shipped in PR #374)
- `docs/adr/*.md` — YAML frontmatter on every ADR; 0010/0012/0020/0023 converted
  to superseded tombstones (their content relocated to AGENTS.md/index or the
  FRDs first); the 8 mixed ADRs trimmed to technical cores with Functional-
  behaviour pointers; **IDs unchanged (no renumber)**.
- `docs/adr/README.md` — rebuilt as a thin `status: accepted` index + a
  superseded/relocated table.
- `AGENTS.md` — documentation model + PRD/FRD/ADR routing + ADR conventions +
  new-Markdown placement (this is where the governance that used to be in
  ADR-0010/0023 now lives).
- `docs/index.md` — authority chain (`operator-notes > PRD > FRD > capabilities
  > ADR > …`) + placement rule.
- `docs/frd/frd-06`, `docs/frd/frd-09` — receive the relocated 0012/0020 content.
- `docs/capabilities.md` — inventory count reconciled to 231.

## Not changed (dropped with the renumber approach)
- `azure.yaml` / `scripts/Test-AzureDeploymentPlan.ps1` release-config edits —
  they belonged to the superseded renumber-to-9 plan, not the taxonomy.

## Coordination
Interlocks with [[SIMPLI-002]] (AGENTS.md), [[SIMPLI-004]] (NOW.md retirement),
[[SIMPLI-005]] (cleanup) — all in the one branch/PR #374.
