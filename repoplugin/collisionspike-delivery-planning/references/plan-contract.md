# Delivery plan contract

Use this contract for every CollisionSpike delivery-plan pack. Keep the programme index navigational, area documents owned, and task evidence local.

## Directory model

```text
docs/plans/<plan-name>/
├── README.md
├── <business-domain>/
│   ├── <owned-area>.md
│   └── <task>.md          # only for a distinct boundary
└── <external-boundary>/
    └── <owned-area>.md
```

Use stable semantic names without wave numbers. Put dependency order in the index so reordering delivery does not rename files.

## Programme index

Include only:

- the finish line and explicit exclusions, linked to authority;
- the source-of-truth and conflict procedure;
- stable application and data invariants;
- a dependency-ordered table linking area documents or task anchors;
- shared ownership and merge hotspots;
- external approval and mutation boundaries;
- evidence-state definitions and maintenance rules; and
- the integrated acceptance journey.

Do not include task checklists, duplicated acceptance criteria, current assignees, status counts, progress percentages, dirty-path counts, mutable vendor versions, prices, cloud inventory, or a copied deferred-feature register.

## Area document

Begin with:

- purpose and operator/business outcome;
- authoritative sources and accepted decisions;
- policy owner, existing implementation owner, callers, persistence and adapters;
- area dependencies and shared failure/observability rules;
- implementations replaced or consolidated; and
- area-specific deferred-capability impact.

Then add ordered task sections. A task belongs in the area that owns its policy, even if other callers or adapters contribute.

## Task eligibility

Create a task only when it has:

- one independently observable outcome;
- one accountable policy owner;
- one real or intended caller;
- a bounded implementation and persistence effect;
- an independently testable failure boundary; and
- one rollout and rollback or recovery boundary.

Keep supporting routes, adapters, migrations, tests, telemetry, documentation, and cleanup in the same task. Do not create a task per file, class, endpoint, test, or architectural layer.

Move a task to a separate file only when at least one of these is true:

- it has a different policy owner from the surrounding area;
- it has a distinct production caller and can ship independently;
- it needs a separate external approval or credential/data boundary;
- it has an independent rollout and rollback boundary; or
- retaining it in the area file prevents a decision-complete hand-off.

## Required task content

Every emitted task must state:

1. **Outcome and evidence state** — initial state is normally `Planned`.
2. **Authority and decision gate** — direct links, confirmed facts, assumptions, and any prerequisite decision.
3. **Owner and dependencies** — policy owner, intended implementation owner, independent evaluator, prerequisites and consumers.
4. **Caller and contract** — real or intended entry point, inputs, outputs, ordered decisions, failure/unknown outcomes, actor/action-history behavior, edit ownership and concurrent/stale caller behavior.
5. **Change boundary** — Core policy, ports/adapters, persisted data/migration, transaction/concurrency mechanism, operator surface, observability, documentation, and replaced implementation. Name no more than three anchor paths unless additional paths prevent a concrete mistake.
6. **Scope** — included behavior and explicit exclusions.
7. **Implementation checklist** — outcome-oriented actions, not file operations.
8. **Validation checklist** — literal positive examples, contradictions, negative/failure paths, persistence/concurrency, parallel actors and stale clients for mutable state, actual caller, genuine inputs where relevant, and repository consistency.
9. **Acceptance criteria** — observable results with the evidence boundary and explicit limitations.
10. **Approval, rollout and rollback** — approval-triggering action, exact target scope, release/activation sequence, recovery and irreversible risk.
11. **Deferred-capability impact** — named deferrals affected, stable identity/data/contract or adapter seam, future migration, activation evidence/decision, and what is deliberately not built.
12. **Completion evidence** — exact command, exit result, input class, boundary exercised, what it proves, what it does not prove, and skipped evidence. Leave results blank while merely planned.

If one of these cannot be stated because material policy is missing, withhold the task; do not invent policy or write a vague placeholder task. A `Decision required before implementation` entry is permitted only when the emitted task remains independently shippable after the blocked behavior is explicitly excluded, or when it records a normal external approval/freshness gate whose target and safe local work are already defined.

For an external-permission task, also name the permission authority and grant type, exact resource scope, any additive/broader grant that could defeat the restriction, application-level identifier guard, and a negative test that proves rejection before the external client is called.

## Evidence states

| State | Required evidence |
|---|---|
| Planned | Reviewed sequence, boundaries and acceptance criteria exist |
| Implemented | Code or configuration exists in the working tree |
| Called | The intended production entry point reaches the behavior |
| Locally verified | Stated local checks pass on stated inputs |
| Deployed | The named environment accepted the deployment |
| Live verified | Fresh production-like traffic reached the expected path and result |
| Accepted | An authorised operator or stakeholder accepted the observed result |

Never collapse these states into `done`.

## Selective conflict handling

Write reconciliation evidence under ignored `artifacts/planning/<plan-name>/<timestamp>/` with:

- source statement and stable link;
- authority classification;
- conflicting or missing authority;
- chosen higher-authority rule, if one exists;
- affected task and dependency impact;
- whether the task was emitted, narrowed, or withheld; and
- user/ADR/approval needed to unblock it.

Requirement parity is clause-level, not section-level. Split a compound source bullet or sentence when its clauses have different owners, readiness, approvals or outcomes; map each normative clause exactly once.

Continue generating independent plan-ready tasks. Do not put the conflict report, mutable evidence, or an unresolved-decision ledger in the programme index.

Map a fully withheld outcome directly from parity evidence to the canonical open decision. Mention the absence as an area boundary or dependency where necessary, but do not manufacture an implementation checklist, caller or schema merely to give the decision a task-shaped home.

## Deferred-capability impact

Review the live questionnaire, remaining-requirements plan and relevant operator notes at each run. Do not rely on a copied list in the skill.

For each relevant deferral, state:

- what current identity, provenance, contract, data or adapter boundary preserves it;
- what migration or replacement a later implementation still requires;
- what evidence, scale, licence, product decision or approval activates it; and
- what dormant code, project, service, queue, table, endpoint, dependency, configuration or release gate is deliberately absent.

Foreclosing a named capability requires a direct user decision and, when architectural, an ADR.

## Maintenance

- Use relative links and stable headings.
- Record task evidence only in the owning task.
- Add no central status roll-up or generated board.
- Reconcile source changes before editing task instructions.
- Remove replaced plan surfaces after parity review; do not preserve two authoritative delivery plans.
- Keep operator notes and corpus read-only.
