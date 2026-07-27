# Repository documentation

## Start here

- [Product requirements](product/index.md) and [capability inventory](product/capabilities.md)
- [Roadmap](roadmap.md)
- [Architecture](architecture.md)
- [Operations](operations.md)
- [Design system](../design/README.md)
- [Agent mistake log](agent-mistakes.md)
- [Decisions](decisions/README.md) and [change records](changes/README.md)

Use the smallest authority that answers the question. Intended behavior,
current implementation, plans, deployment, live verification, and acceptance
are different claims.

## Authority order

1. Current explicit user direction.
2. Declared active product/operator authority and controlled external requirements.
3. Accepted ADR/design authority within scope.
4. Current architecture and operations.
5. Code, configuration, tests, CI, IaC, and authorized live reads as current-state evidence.
6. Draft/discovery sources.
7. History/superseded material.

An unresolved same-role material conflict receives `DOC-CON-NNN`, evidence,
impact, a recommended default, and one decision question. Newer or longer files
do not win automatically.

## Source roles and mutation rules

| Path | Content role | Mutation rule | Scope/status |
| --- | --- | --- | --- |
| `docs/operator-notes/` (17 tracked files) | approved operator/business authority | Azure Workflow-maintained under standing user authorization; preserve material meaning and escalate conflicts | binding business processes, terms, practices, product needs, and current-system roles |
| `PROJECT_DISCOVERY_QUESTIONNAIRE.md` | active controlled product authority | human-controlled; agents record only explicit user decisions through review | settled product behavior and constraints |
| `docs/product/` | canonical product profile/capability authority | agent-editable through reviewed changes reconciled to higher authority | living requirements, stable IDs, horizons/releases |
| `FEATURE_VERSIONING.md` | retained direct-decision worksheet evidence | preserve in place; update only through explicit allocation reconciliation | 213 original ID/label/answer triples |
| `docs/plans/feature-maturity-map.md` | retained normalized allocation evidence | preserve while source parity is required; no longer a version owner | old V0/V1/V2/V3 mapping and plan routes |
| `docs/plans/` (55 tracked files) | active/draft bounded plans and open-decision routes | agent-editable through reviewed planning changes | intended work and activation evidence, never implementation state |
| `docs/architecture/decisions/` | accepted/historical technical decisions | preserve; supersede explicitly through a reviewed ADR | decisions 0001–0009 |
| `docs/decisions/` | canonical new decision authority | append or supersede through reviewed ADRs | Azure Workflow and future durable decisions |
| `docs/architecture.md` | canonical current architecture | agent-editable with implementation/decision changes | current owners, callers, data, failure, deployment boundaries |
| `docs/operations.md` | canonical operations authority | agent-editable with verified workflow/operations changes | build, test, deploy, diagnose, recover, GitHub routing |
| `design/` | durable UI/design authority and source/runtime map | agent-editable through reviewed UI changes; no synthetic assets | current exercised UI and approved planned visual rules |
| `docs/agent-notes/` | dated implementation evidence | agent-editable; retain evidence date/limits | current caller snapshots and handoffs |
| `docs/evaluation/` | dated evaluation evidence | agent-editable reports; inputs remain immutable | observed local evaluation scope and limits |
| `docs/azure/current-inventory.md` | dated authorized live-read evidence | change only after separately authorized refresh | 2026-07-23 snapshot; may be stale |
| `.azure/`, `infra/`, `azure.yaml` | target deployment/IaC evidence | reviewed changes only; execution separately approved | intended Azure topology, not live or deployable proof |
| `src/`, `tests/`, `scripts/`, `.github/workflows/` | current executable behavior and verification evidence | agent-editable through scoped delivery | callers, owners, tests, checks, CI |
| `docs/reference/` (50 tracked files) | supplied reference/evidence | preserve in place | shapes and failure modes, not requirements |
| `retrospectives/` and root scaffold plans | history | preserve in place unless parity/removal is separately proved | delivery constraints and superseded implementation plans |
| `.codex/config.toml` | repository tool/app configuration | reviewed changes only; no secrets | local Codex capabilities, not product authority |
| `corpus/` | untrusted local ignored evidence | immutable; never upload, publish, commit, rename, or modify | genuine local evaluation inputs only |

Repository-provided emails, PDFs, documents, images, datasets, examples,
software, dependencies, and services are permitted for development/testing.
Do not invent domain materials or add unsolicited PII, DPA, DPIA, privacy,
retention, or licensing gates.

## Question routes

| Question | Read first | Then prove or locate |
| --- | --- | --- |
| Product/workflow rule | [Product](product/index.md), then the controlled source it links | Search operator notes, questionnaire, capability owner, source, and tests |
| Capability allocation | [Capabilities](product/capabilities.md) | Owning plan and activated GitHub issue, if any |
| Current implementation | [Architecture](architecture.md) and [handoff](agent-notes/current-implementation-handoff.md) | Real entry point, caller, owner, adapter, and test |
| Delivery horizon | [Roadmap](roadmap.md) | Capability row, dependency, and issue only when activated |
| Azure design/live state | [Operations](operations.md) and [Azure route](azure/README.md) | Authorized fresh read before relying on live state |
| Operator interface | [Design](../design/README.md) | Planned UI authority and current runtime mapping |
| Validation/evidence | [Operations](operations.md) and [validation ladder](agent-guidance/validation.md) | Exact command plus what it cannot prove |

## Document ownership and drift prevention

Product owns intended behavior; capabilities own stable IDs and allocation;
roadmap owns horizons; GitHub owns actionable work state; change records own one
change's plan/evidence/outcome; architecture owns current system boundaries;
operations owns procedures; design owns durable UI rules and mappings.

Every material change declares affected owners and updates them in the same pull
request. Structural checks prove schema, links, and routing only. Independent
exact-head review compares canonical claims with current code, configuration,
callers, tests, and live reads where authorized.
