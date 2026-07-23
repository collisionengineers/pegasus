# Agent routing

Use custom agents for bounded work, not as a substitute for ownership. The lead agent defines scope, integrates the result, and reports what was actually verified.

| Need | Agent | Typical output |
|---|---|---|
| Locate files or answer a narrow repository question | `explorer` | paths and cited findings |
| Map dependencies, callers, authorities, or data flow | `codebase_mapper` | evidence map and uncertainty |
| Research a current technical question | `researcher` | primary-source answer with links |
| Turn requirements into an executable sequence | `planner` | thin plan, proof, exclusions |
| Interpret Collision Engineers workflow | `domain_analyst` | rule table and open decisions |
| Inspect current Azure state | `azure_researcher` | timestamped read-only evidence |
| Vet Azure topology, identity, reliability, or cost | `azure_architect` | option and trade-off record |
| Implement a scoped .NET change | `dotnet_implementer` | code plus focused checks |
| Design adversarial and corpus-backed evidence | `test_engineer` | independent tests/evaluation |
| Review a completed change | `reviewer` | findings by severity, residual risk |
| Reduce duplication or indirection | `codebase_simplifier` | deletion/consolidation proposal |
| Plan operator experience | `ui_ux_planner` | workflow, states, and mockup brief |

Parallel work is appropriate only when subtasks are independent and write scopes do not overlap. Use read-only agents for discovery and review. Do not allow two implementation agents to edit the same boundary.

Model choice is intentional: `gpt-5.6-terra` handles read-heavy exploration and research; `gpt-5.6-sol` handles architecture, planning, implementation, and adversarial review. All project agents are defined under `.codex/agents/`.
