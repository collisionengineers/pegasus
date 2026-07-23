---
name: collisionspike-delivery-planning
description: Reconcile CollisionSpike authoritative requirements, accepted decisions, current code and broad or monolithic plans into domain-organised, caller-backed delivery plan packs with dependencies, implementation and evidence checklists, acceptance criteria, approval boundaries, and deferred-capability impact. Use when creating, splitting, reviewing, or refreshing release, remainder, cross-cutting, architecture, API, schema, migration, or implementation plans under docs/plans.
---

# CollisionSpike delivery planning

Produce implementation-ready planning artifacts without recreating a ticket ledger or a second product specification.

## Prepare

1. Read the root and nearest `AGENTS.md`.
2. Read `docs/agent-guidance/source-of-truth.md`, the input documents, and only the relevant read-only operator notes, settled questionnaire answers, accepted ADRs, `docs/plans/remaining-requirements.md`, and `docs/plans/open-decisions.md`.
3. Apply `$collisionspike-domain` to business meaning and `$evidence-led-delivery` to caller and proof design. Follow `docs/agent-guidance/agent-routing.md` for material cross-cutting work.
4. Read [plan-contract.md](references/plan-contract.md) before writing or changing a pack. Use the templates under `assets/`; do not invent a parallel format.
5. Inspect the current repository for the policy owner, callers, persisted models, adapters, tests, registrations, duplicate implementations, and scoped working-tree changes.

Treat operator notes and `corpus/` as read-only. Never copy, modify, upload, rename, or publish corpus material. Keep generated reconciliation and evaluation evidence under ignored `artifacts/`.

## Reconcile before decomposing

Classify each material statement as one of:

- authoritative requirement or direct current-task decision;
- accepted architecture decision;
- verified current implementation evidence;
- proposal or implementation choice;
- assumption or unresolved material decision;
- explicit deferral or exclusion;
- approval or external-mutation boundary.

Apply the repository source-of-truth order literally:

- Replace a lower-authority contradiction with the higher-authority rule and record the rejected statement in the reconciliation evidence.
- For an unsupported rule that changes identity, workflow, permissions, retention, an external-system boundary, schema, API, or an irreversible choice, withhold the entire affected implementation task. Record the conflict under `artifacts/planning/<plan-name>/<timestamp>/reconciliation.md`, map it to the canonical open-decision register, and route the decision to the user. Do not emit a placeholder task whose caller, persistence or acceptance boundary is still unknown.
- Continue with independent plan-ready areas; do not turn one blocked rule into a whole-programme stop unless it invalidates the shared foundation.
- State a non-material implementation assumption only when it is reversible, evidence-led, and does not foreclose a named deferral.

Do not treat a broad plan, generated view, retrospective, code presence, registration, test-only caller, predecessor behavior, or corpus example as product authority.

## Trace the real delivery boundary

For each outcome:

1. Name the current or intended production entry point.
2. Trace it to the Core policy owner, external port/adapter, persistence, multi-actor/concurrency behavior, operator-visible result, and tests.
3. Label an absent entry point as **planned**. Never describe a registered, source-only, infrastructure-only, or test-only path as called.
4. Find the existing owner and implementation being extended, migrated, consolidated, or removed. Stop before creating a second owner or third copy.
5. Keep shared transaction and migration hotspots under one accountable task even when several callers will consume them.

Store volatile workspace facts, current prices, versions, cloud inventory, and live-service observations in dated evidence or execution-time freshness gates. Do not make them evergreen programme assertions.

## Decompose by ownership and outcome

- Use stable business or external-boundary domains as folders.
- Use one area document for a cohesive policy or integration owner.
- Use `##` task sections for independently observable delivery outcomes.
- Split a task into its own linked file only when it has a distinct policy owner, real caller, external approval, rollout, or rollback boundary, or the area document would no longer be usable.
- Keep routes, migrations, tests, telemetry, documentation, and cleanup with the outcome they enable. Do not create tasks per file, layer, test class, or trivial action.
- Represent delivery waves only as dependency order in the programme index. The domain owner remains authoritative.
- Put a cross-domain invariant with its policy owner and link consumers to it; never duplicate the rule in several area documents.
- Use a decision gate inside an emitted task only when the remaining outcome is independently implementation-ready after the blocked behavior is explicitly excluded. A task that cannot ship safely without the decision is withheld.

## Author the pack

Copy and fill [programme-index.template.md](assets/programme-index.template.md) and [area-plan.template.md](assets/area-plan.template.md).

- Use relative repository links and stable heading anchors.
- Cite requirements instead of restating whole source documents.
- Attach implementation, test, acceptance, approval, rollback, documentation, replacement, and deferred-capability details to the task they govern.
- For every mutable case, inbox, account, principal, configuration or document outcome, state who owns an edit, how concurrent actors and stale clients are refused or reconciled, and how abandoned ownership recovers. Write `Not applicable` only with a concrete reason.
- Use the precise evidence states `Planned`, `Implemented`, `Called`, `Locally verified`, `Deployed`, `Live verified`, and `Accepted`. Record state once in the owning task; never mirror it into a central roll-up.
- Exclude `<proposed_plan>` wrappers, agent assignments, progress percentages, generated counts, copied deferred-feature ledgers, and speculative future components.
- Preserve explicit external-system allowlists and approval boundaries exactly, without treating a plan as authority to perform a live call.

## Validate and maintain

1. Build an ignored requirement-to-task parity matrix. Map every authoritative in-scope normative clause exactly once to an owning task, explicit exclusion, named deferral, or withheld decision. Split a compound sentence or bullet whenever its clauses have different owners, readiness, approval boundaries or outcomes; never claim coverage only at section/group level.
2. Check every emitted task against [plan-contract.md](references/plan-contract.md).
3. Confirm dependency order is acyclic and every task has one policy owner, at least one real or intended caller, a failure outcome, observable evidence, and a rollback or recovery boundary.
4. Forward-test material skill revisions with raw repository artifacts in an isolated temporary directory. Do not disclose expected conflicts to the test agent.
5. Use a different agent to review authority precedence, caller reality, scope boundaries, deferred impact, and evidence limitations.
6. Run scoped link/format checks and the repository check. Resolve each relative Markdown target from its source document, verify every fragment against the target's actual heading anchor, and report file/link/error counts. Report scoped results separately from unrelated dirty-tree failures.
7. Update task evidence only when reality changes. Keep planned, implemented, called, verified, deployed, live-verified, and accepted distinct.

## Stop conditions

Stop the affected task when:

- material authority remains contradictory or absent;
- no policy owner or intended production caller can be named;
- the task would create a second source of truth or third implementation;
- an external mutation, PII-bearing transfer, billed run, deployment, or destructive action lacks exact approval and target scope;
- the only evidence is documentation, registration, mocks, or a broad green repository check;
- the design forecloses a named deferred capability without a direct user decision and, where architectural, an ADR.
