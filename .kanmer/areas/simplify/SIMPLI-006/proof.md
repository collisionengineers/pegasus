# Proof — PR under review

- Branch `task/retire-now-rewrite-agents`, 13 commits. **PR #374 → dev:**
  https://github.com/collisionengineers/pegasus/pull/374
- `pwsh ./scripts/Test-DocumentationLinks.ps1` green (118 files) at every step.
- `requirements.md` (1,121 lines) → 1 PRD + 12 FRDs, all 51 heading slugs
  preserved; **301 references retargeted, 0 unmapped**; `requirements.md` deleted.
- Documentation model + PRD/FRD/ADR routing + ADR conventions + placement written
  into `AGENTS.md` (not an ADR); `docs/index.md` authority chain + placement updated.
- Every ADR carries YAML frontmatter; mis-filed 0010/0023 (governance) → AGENTS.md/index
  and 0012/0020 (feature rules) → FRD-06/FRD-09, tombstoned; the 8 mixed ADRs trimmed
  conservatively to technical cores; `adr/README.md` rebuilt as a thin `status: accepted`
  index; `capabilities.md` count reconciled to 231 rows.
- `NOW.md` retired: Worker-enabled state (live-verified 2026-08-13) + post-release-8
  facts → `operations.md`, Path → `open-decisions.md`; Kanmer is the claim mechanism.
- Correctness fix: FRD-08 aligned to the accepted ADR-0024 fresh-start model.
- Covers [[SIMPLI-002]] [[SIMPLI-004]] [[SIMPLI-005]].

Independent review in progress; merge to `dev` pends review pass + green CI.
