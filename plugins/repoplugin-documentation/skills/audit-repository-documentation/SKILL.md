---
name: audit-repository-documentation
description: Audit repository documentation, AGENTS.md guidance, decisions, plans, plugins, and task-attached proposals for authority, contradictions, viability, and navigability. Use before accepting a documentation baseline or when documentation may be stale, inconsistent, or misleading.
---

Use `$repoplugin-task-contracts:resolve-repository-task` to create or explicitly attach to the shared task. Persist `audit.md`, `contradictions.md`, `open-questions.md`, and `awareness.md` under `<task>/documentation/` with `$repoplugin-task-contracts:persist-repository-task-artifact`. Read [the documentation standard](../../references/documentation-standard.md).

1. Define the audit boundary. Include relevant tracked documentation, ADRs/decisions, plans, root and nested `AGENTS.md`, READMEs, plugin/agent guidance, and task-attached proposed or untracked documents. Record every exclusion and inaccessible source.
2. Build or refresh the inventory and claim map before judging coherence. Identify authority, status, owner, and supersession for each material claim; distinguish reference/legacy material from live authority.
3. Check that root and nested agent guidance route correctly, human READMEs do not become agent contracts, and the context map answers task/topic/path queries with minimum authorities plus live searches for owners, callers, configuration, tests, and commands.
4. Check repository-wide sanity and viability: links and anchors resolve; documented commands remain executable or are labelled stale; cited callers/owners/configuration/tests exist; version and supersession routes are coherent; plans and plugin guidance point to current authorities; maturity horizons are internally consistent.
5. Record every conflict as `DOC-CON-NNN`, retaining both claims, source paths, status, impact, and the user decision required. Do not select a winner. Only the user resolves contradictions.
6. Ask one question at a time where missing repository truth blocks an audit conclusion. Keep Answered items blocking until incorporated and a full applicable rescan finds no unresolved conflicts, missing routes, or new contradictions.
7. Separate findings into blocking contradiction, incorrect/stale route, missing evidence, improvement, and explicit exclusion. Recommend only proportionate remediation; do not invent a documentation state machine or duplicate authority.

Use `$repoplugin-task-contracts:validate-repository-task` before reporting. The handoff must identify coverage, evidence, exclusions, unresolved user-only decisions, and checks that could not run.
