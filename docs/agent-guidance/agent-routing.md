# Agent and delivery routing

Use one accountable lead. Agents receive bounded, non-overlapping scope and
report facts, inference, limits, and next evidence; the lead integrates and
states what was actually verified. Read-only discovery or review may run in
parallel only when it cannot overlap a writer.

## Lifecycle

| Stage | Route and required outcome |
| --- | --- |
| Plan | Use `$repoplugin-planning:plan-repository-change` for material work. It researches authorities and real callers, records open questions, and obtains independent plan review before a pack becomes ready. |
| Interpret product/domain rules | Use `$repoplugin-planning:apply-collisionspike-domain`; read the live authorities it routes to rather than treating the skill as product authority. |
| Route Azure work | Use `$repoplugin-planning:route-collisionspike-azure` before Azure design or code work; external reads or writes remain separately authority-gated. |
| Implement | Use `$repoplugin-implementation:implement-plan-pack` only for a user-requested, reviewed ready pack. The implementation lead calls the actual harness `update_plan` before any edit or implementation delegation; Markdown cannot substitute for it. |
| Review | Use `$repoplugin-review:review-implementation` independently against authority, caller, scope, and evidence. Use `$repoplugin-review:triage-pr-feedback` to collect complete pull-request feedback before remediation. |
| Validate or debug | Use `$repoplugin-validation:test-and-validate-repository-change` for risk-based evidence. Reproduce a failure with `$repoplugin-debugging:debug-repository-failure` before fixing it; return findings to the accountable implementer, then revalidate and rereview material remediation. |
| Own repository documentation | Use `$repoplugin-documentation:bootstrap-repository-documentation` for zero-loss onboarding, `$repoplugin-documentation:maintain-repository-documentation` for bounded changes, and `$repoplugin-documentation:audit-repository-documentation` for independent authority/viability/contradiction checks. |
| Plan operator UI/UX | Use `$repoplugin-ui-ux:plan-ui-ux-change` for inventory, specification, wireframes, and reviewed image directions; use `$repoplugin-ui-ux:apply-collision-engineers-ui-style` for the packaged visual system. |
| Deliver | The lead confirms scope, performs only authorised Git/PR actions, and reports implemented, called, locally verified, deployed, live verified, and accepted separately. |

Task artifacts share one task ID under `.repoplugin/tasks/<task-id>/`; keep
plans, reviews, implementation evidence, and handoffs attached to that ID.
If a user changes a requirement, create `planning/changes/RC-NNN.md`, mark only
affected harness steps pending, reopen the relevant research, contradiction and
open-question review, and obtain independent plan review before resuming that
scope. Do not append a changed requirement silently.

External writes, including cloud changes, deployment, credential rotation,
account changes, and publication, require the user's explicit authority. A PR
or repository task does not broaden that authority.

## Bounded agent selection

| Need | Agent |
| --- | --- |
| Locate a narrow repository fact | `explorer` |
| Map callers, dependencies, or data flow | `codebase_mapper` |
| Research a current technical question | `researcher` |
| Interpret Collision Engineers workflow | `domain_analyst` |
| Inspect or vet Azure | `azure_researcher`, `azure_architect` |
| Implement a scoped .NET slice | `dotnet_implementer` |
| Design independent tests or corpus evaluation | `test_engineer` |
| Review completed work | `reviewer` |
| Simplify duplication | `codebase_simplifier` |
| Plan operator experience | `ui_ux_planner` |

Project agent definitions are under `.codex/agents/`. Skills supply detailed
workflow instructions; this page is the single concise lifecycle route.
