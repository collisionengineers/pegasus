---
name: bootstrap-repository-documentation
description: Establish a zero-loss documentation baseline for a repository, including AGENTS.md routing, README boundaries, decision records, and context maps. Use when onboarding a repository into documentation standards or when its documentation lacks a dependable route for agents.
---

Use `$repoplugin-task-contracts:resolve-repository-task` to create or explicitly attach to the shared task. Persist all workflow records under `<task>/documentation/` with `$repoplugin-task-contracts:persist-repository-task-artifact`. Read [the documentation standard](../../references/documentation-standard.md).

1. Discover the repository's existing guidance, READMEs, ADRs/decisions, architecture and operations material, plans, scripts, plugin/agent configuration, and relevant tracked or task-attached proposed documentation. Do not treat legacy/reference material as present authority merely because it exists.
2. Write `inventory.md` and `claim-map.md` before moving, replacing, or consolidating documentation. Inventory unique claims, their source and status, not just filenames. Record exclusions and unknowns explicitly.
3. Run a blind-spot pass: separate stated requirements, known questions, likely unstated conventions, and unknown unknowns. Write `awareness.md`. Ask the user one question at a time when missing repository truth changes authority, structure, ownership, commands, or claim meaning.
4. Create a query-oriented `context-map.md` that routes each common task, topic, or path to the minimum authority first, then to live searches for the policy owner, real callers, configuration, tests, and validation commands. Prefer dynamic searches over copying mutable facts.
5. Propose a thin root `AGENTS.md`, nearest nested instruction files with only local deltas, human-facing READMEs, and decision/plan routes. Keep agent guidance distinct from human onboarding; keep repository-specific truth in repository documents, not this skill.
6. Run the contradiction and viability checks in the standard. Preserve every conflicting claim as `DOC-CON-NNN` in `contradictions.md`; only the user may choose a resolution. Do not silently delete, rewrite, or prefer a claim.
7. Write `bootstrap-plan.md` with each source claim's retained, moved, or explicitly superseded destination. Apply changes only after the map is complete and required questions/contradictions are resolved. Rescan all in-scope documentation after incorporation.
8. Document maturity horizons (`0.x` pre-alpha, `1.x` alpha, `2.x` beta, `3.x+` release). Account for later features through decisions, seams, and migration notes only; do not create dormant implementation.

An answered question is still blocking until its answer is incorporated and the applicable full rescan reports no unresolved contradiction or missing route. Use `$repoplugin-task-contracts:validate-repository-task` before reporting the baseline ready.
