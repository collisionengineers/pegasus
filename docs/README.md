# Documentation map

Start with the smallest route that answers the question. The map identifies
authorities and live owners; it does not claim that every planned capability is
implemented.

| Question | Read first | Then locate the live owner or evidence |
| --- | --- | --- |
| A product or workflow rule | [source of truth](agent-guidance/source-of-truth.md), then [the questionnaire](../PROJECT_DISCOVERY_QUESTIONNAIRE.md) | `rg -n "term" PROJECT_DISCOVERY_QUESTIONNAIRE.md docs/operator-notes docs/plans src tests` |
| An operator-provided fact | [operator notes](operator-notes/README.md) | Read the relevant note; do not edit it without explicit user authority. |
| A current feature's maturity, status, blocker, or plan | [plans index](plans/README.md), then [feature maturity](plans/feature-maturity-map.md) | `rg -n "feature ID or term" docs/plans PROJECT_DISCOVERY_QUESTIONNAIRE.md src tests` |
| A real implementation path or limit | [current implementation handoff](agent-notes/current-implementation-handoff.md) | `rg -n "entry point or use case" src tests` and prove the caller, not registration alone. |
| An architecture or dependency decision | [architecture index](architecture/README.md), then its ADR | `rg -n "term" docs/architecture src tests infra` |
| Azure inventory, target design, or release work | [Azure index](azure/README.md) | `rg -n "term" docs/azure infra .azure scripts`; cloud writes need explicit authority. |
| A local setup or supported command | [developer workstation runbook](runbooks/developer-workstation.md) | `rg -n "command or tool" scripts README.md docs/runbooks` |
| Validation scope or evidence labels | [validation guidance](agent-guidance/validation.md) | `rg -n "feature or rule" tests src scripts docs/evaluation` |
| An evaluation result | [evaluation index](evaluation/README.md) | Read the dated report and its input/limit statement; corpus remains local. |
| A legacy, raw, or supplied source | [reference index](reference/README.md) | Use it only as evidence and reconcile a proposed rule through the source-of-truth order. |
| Agent choice, task flow, or a requirement change | [agent routing](agent-guidance/agent-routing.md) | Search `.codex/agents`, `plugins`, and `.repoplugin/tasks` for the selected task ID. |

For any documentation change, read the nearest `AGENTS.md`, retain a
zero-loss claim route, and validate changed links and commands. Historical and
reference material stays in place unless a separately justified change proves
its replacement and inbound routing.
