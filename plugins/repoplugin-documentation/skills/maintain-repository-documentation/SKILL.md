---
name: maintain-repository-documentation
description: Safely change repository documentation, AGENTS.md guidance, READMEs, ADRs, plans, and context maps without creating drift or losing existing truth. Use when a repository change requires documentation updates or documentation itself needs correction.
---

Use `$repoplugin-task-contracts:resolve-repository-task` to create or explicitly attach to the shared task. Write ordinary work records under `<task>/documentation/` with `$repoplugin-task-contracts:persist-repository-task-artifact`. Read [the documentation standard](../../references/documentation-standard.md).

1. State the requested change, affected audience, and the source that proves each changed fact. If it changes product scope, architecture, or a material requirement, route it to planning before claiming the documentation is settled.
2. Search the live repository for every affected authority, caller, command, configuration key, test, ADR, plan, AGENTS route, README, plugin, and task-attached proposed document. Record the result and exclusions in `maintenance-plan.md`.
3. Preserve existing unique claims. For a consolidation or relocation, extend `claim-map.md` with the source, destination, retained/superseded status, and visible route. Never use a rewrite as silent deletion.
4. Keep root `AGENTS.md` a route map and nested files local deltas; keep README content human-oriented. Update the query-oriented context map when a route, owner, caller, command, or authoritative location changes.
5. Run the standard's sanity, viability, links/anchors, commands, real callers, version/supersession, maturity-horizon, and contradiction checks. Audit tracked documents and task-attached proposed/untracked documentation relevant to the change.
6. Record contradictory claims as `DOC-CON-NNN`, preserving both sources. Ask the user one question at a time for a required choice. An answer is not complete until incorporated and the full applicable rescan is clear.
7. Record assumptions, unresolved risks, and future migration/seam implications in `awareness.md`. Treat 0.x/1.x/2.x/3.x+ as pre-alpha/alpha/beta/release horizons; document future work without creating dormant implementation.

Use `$repoplugin-task-contracts:validate-repository-task` before handoff. Report included scope, explicit exclusions, checks run, unresolved user decisions, and validation limitations.
