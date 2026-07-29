# Repository documentation

## Start here

- [Product requirements](requirements.md)
- [Capability inventory](capabilities.md)
- [Open decisions](open-decisions.md)
- [Architecture](architecture.md)
- [Operations](operations.md)
- [Engineering workflow](engineering.md)
- [Operator authority](operator-notes.md)
- [Design authority](../design/README.md)
- [Decisions](decisions/README.md)
- [Change records](changes/README.md)
- [Reference evidence](reference/README.md)
- [Azure route](azure/README.md)
- [Source workspaces](../workspaces/README.md)
- [Agent mistake log](agent-mistakes.md)
- Retained historical evidence: [dependency-ordered delivery route](history/plans/delivery-roadmap.md), [mailbox policy dossier](history/plans/mailbox-categorisation-and-email-matching/README.md), [operator discovery questionnaire](history/product/project-discovery-questionnaire.md), and [feature-versioning worksheet](history/product/feature-versioning-worksheet.md)

Use the smallest authority that answers the question. Intended behavior,
allocation, implementation, real-caller proof, deployment, live verification,
and operator or management acceptance are different claims.

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

## Canonical ownership

| Question | Owner |
| --- | --- |
| What must Pegasus do? | [Requirements](requirements.md) |
| Which stable capability ID, horizon, and exact release owns it? | [Capabilities](capabilities.md) |
| What material question remains unresolved? | [Open decisions](open-decisions.md) |
| What exists now; what are the callers, dependencies, data flows, and boundaries? | [Architecture](architecture.md) |
| How is Pegasus developed, tested, run, deployed, diagnosed, released, and recovered? | [Operations](operations.md) |
| How is repository work planned, implemented, proved, reviewed, and delivered? | [Engineering](engineering.md) |
| What did Collision Engineers explicitly state about process, needs, constraints, and current systems? | [Operator notes](operator-notes.md) |
| What durable technical choice and rationale applies? | Published immutable decision bodies, with current navigation, status, and supersession in the [decision index](decisions/README.md) |
| What does one material change own? | [Change index](changes/README.md) plus one record per change |
| What is the durable UI rule and source/runtime mapping? | [Design](../design/README.md) and its three product contracts |
| What is supplied evidence rather than a requirement? | [Reference manifest](reference/README.md) plus retained raw evidence |
| What is Azure target, transition, or dated live-read evidence? | [Azure route](azure/README.md) and `.azure/deployment-plan.md` |
| What does an independently buildable source import own? | Its workspace/package README, minimal live technical owners, machine contracts, active ADRs, and required immutable ADR provenance |

## Source roles and mutation rules

| Path | Role and rule |
| --- | --- |
| `docs/operator-notes.md` | Binding operator/business authority. Maintainers may organize it under standing authorization but must preserve every material business statement and escalate meaning changes. |
| `docs/requirements.md` | Canonical intended product behavior, invariants, exclusions, success measures, and deferred seams. Reviewed changes only. |
| `docs/capabilities.md` | Canonical 229-ID allocation. It owns horizon and exact target, not implementation status. |
| `docs/open-decisions.md` | Unresolved material decisions and evidence blockers only. |
| `docs/architecture.md` | Current implementation, callers, data, dependencies, failures, and deployment boundaries; target state is explicitly qualified. |
| `docs/operations.md` | Supported procedures and evidence profiles, including what each check cannot prove. |
| `docs/engineering.md` | Repository lifecycle, caller proof, validation, review, and incident criteria; it links here and to operations rather than restating authority or commands. |
| `design/` | Durable UI/design authority and source/runtime map. Planned and current behavior remain explicit. |
| `docs/decisions/` | Published decision bodies, clauses, rationale, and dated provenance are immutable. `docs/decisions/README.md` owns reviewed navigation, current status, and supersession metadata; changed meaning uses an accepted addendum or new decision. |
| `docs/changes/` | One record per material change. Retain unique reviewed provenance; do not create generated status ledgers. |
| `docs/history/` and reviewed `docs/reference/reports/` | Subordinate, source-labelled evidence retained only where it carries unique accepted provenance, observations, dependency order, or unresolved research that cannot be reconstructed safely. It never becomes current product authority. |
| `docs/reference/` | Supplied/raw evidence subordinate to operator/product authority. Retain unique raw sources and compact provenance; retire only exact duplicates or material with complete verified destinations. |
| `docs/azure/` and `.azure/` | Dated live-read evidence, target design, and predecessor transition. None authorizes a cloud operation. |
| `src/`, `tests/`, `scripts/`, `.github/` | Current executable behavior and verification evidence. |
| `workspaces/` | Independently buildable source imports. Never application callers, dynamic dependencies, deployment units, or business-policy owners without an accepted integration contract and caller proof. |
| `corpus/` | Untrusted local ignored evidence. Immutable; never upload, publish, commit, rename, or modify. |

Repository-provided emails, PDFs, documents, images, datasets, examples,
software, dependencies, and services are permitted for development and testing.
Do not invent domain material or add unsolicited PII, DPA, DPIA, privacy,
retention, or licensing gates.

## Drift prevention

1. A live claim is written once. Every other document links to its owner.
2. Allocation, implemented, caller-proved, deployed, live-verified, and accepted remain distinct states.
3. Evergreen pages omit test counts, file counts, package versions, and live-resource state unless a value is itself the contract. Dated evidence names its head/checkpoint and scope.
4. Historical-only prose is removed only after every unique claim, observation, dependency, and provenance record has a verified destination or accepted typed disposition. Do not create a second status ledger or broad archive tree.
5. Published decision bodies, clauses, rationale, and dated provenance are never narrowed, rewritten, or deleted in place. Reviewed navigation, current status, and supersession metadata is maintained in the decision index; changed meaning uses an accepted addendum or decision.
6. Source workspaces retain current technical contracts, protected imported source, required immutable ADR provenance, adjacent legal material, machine contracts/provenance, and evidence needed to reproduce a current decision.
7. Raw evidence never becomes product authority. Retain unique reviewed evidence and provenance; delete only proven duplicates or derived material whose complete payload has a verified canonical destination.
8. Every surviving Markdown page is reachable from one appropriate route root: this index, `design/README.md`, `docs/reference/README.md`, `docs/azure/README.md`, `workspaces/README.md`, or its owning workspace/package README. Immutable ADR/change bodies route through their current index.
9. Every local link and anchor resolves. Forbidden-prefix destinations remain opaque lexical evidence routes and are never opened to validate existence or anchors.
10. Every rewritten, moved, or deleted material claim has one verified destination, accepted supersession/resolution, or typed duplicate/navigation proof. Equal bytes prove retention, not semantic equivalence.
11. Imported source-snapshot provenance and the current committed workspace manifest are separate identities. Neither substitutes for the other.
12. A material change updates every affected owner, deferred-capability impact, caller evidence, and review state in the same pull request.

## Question routes

| Question | Read first | Then prove |
| --- | --- | --- |
| Product/workflow rule | [Requirements](requirements.md), then [operator notes](operator-notes.md) | Capability row, Core owner, caller, and tests |
| Capability allocation | [Capabilities](capabilities.md) | Activated issue/milestone only when applicable |
| Current implementation | [Architecture](architecture.md) | Real entry point, owner, adapter, and exercised caller |
| Delivery procedure | [Operations](operations.md) | Exact command, target, result, and limitation |
| Repository workflow | [Engineering](engineering.md) | Active issue/change record, exact head, checks, and review |
| Azure design or live state | [Azure route](azure/README.md) | Fresh authorized read before relying on live state |
| Operator interface | [Design](../design/README.md) | Current runtime mapping and browser evidence |
| Raw supplied evidence | [Reference manifest](reference/README.md) | Canonical accepted claim destination; never infer authority from the raw source |
