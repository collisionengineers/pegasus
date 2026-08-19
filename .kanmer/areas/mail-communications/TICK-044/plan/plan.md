# Plan — MAIL-02

## Chosen approach

add a pure Core operational-destination policy using the operator-confirmed exhaustive mapping. Reuse `a sibling policy under src/Pegasus.Core/Intake/Classification/`, keep Web/MCP callers thin, and place persistence or external mechanics only in `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`. This follows the repository's one-Core-owner rule and the existing convention rather than adding a workspace-specific policy copy.

A parallel UI-owned implementation was rejected because UI-10, Automation MCP and background processing would diverge. A generic mail-action framework was rejected because each action already has a concrete Core boundary and no second abstraction caller is proven.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: implement its exact-message, fail-closed, durable-history and workspace behaviour. Any unresolved mapping/mutation behaviour remains conditional on the checked operator answer; do not silently amend the FRD.
- `docs/design/README.md`: apply the established confirmation, error, focus, navigation and accessibility conventions.
- No new ADR is planned: the existing Core/Infrastructure/Web boundary carries the change.

## Ordered implementation

1. Re-read the current target files after prerequisite branches land and name the exact existing contracts/helpers/tests being reused.
2. Add or extend the smallest Core contract/policy required to add a pure Core operational-destination policy using the operator-confirmed exhaustive mapping; validate identity, actor, reason, state and version before any write.
3. Implement the Infrastructure projection/transaction/adapter in src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs; preserve mailbox scope, idempotency, optimistic concurrency and append-only evidence.
4. Wire the real caller (retained-mail detail/list projection) through the Core use case with no duplicated taxonomy, mapping or authorization logic.
5. Add focused Core and integration/Web tests for every taxonomy member, reply/Other, Ambiguous/Unclassified and Triage fail-closed routing.
6. Run the locked restore/build and focused tests, then the relevant full suite; perform the four-lens simplification pass and record honest dispositions.
7. Update FRD/capabilities only where the delivered behaviour/evidence warrants it; do not claim deployment, live Outlook verification or operator acceptance from local tests.

## Dependencies and sequencing

operator mapping answer; MAIL-23 consumes this result.

## Proof

The post-implementation report will cite focused test output, Release build output, real-caller integration evidence and simplification findings. External-mailbox behaviour requires separately approved live verification and cannot be inferred from adapter tests.

## Risks and mitigations

- Identity or stale-state mistakes: exact mailbox/message keys plus optimistic concurrency and fail-closed validation.
- Policy duplication: one Core result consumed by Web, Worker and MCP.
- External side effects: local fakes/fixtures by default; no real Outlook/cloud write without exact approval.
- Scope growth: keep this ticket to its named capability and file follow-ups for independent behaviour.

## Required in-repo governing catalogue — operator decision 2026-08-19

Modify the existing canonical `docs/frd/frd-08-email-mailbox-and-background-processing.md`; do not create a competing taxonomy document. Add one exhaustive classification catalogue with, for every direction/family/subtype:

- canonical identifier and staff-facing label;
- positive classification criteria;
- exclusions and precedence/ambiguity behaviour;
- identity/evidence inputs used (headers, sender/domain, body, attachment/document evidence, reply/thread signals, provider route and Case-correlation result);
- whether the method is deterministic rule, route-specific predicate, staff decision or explicit abstention;
- required evidence/policy version and correction-history behaviour;
- operational destination and Needs-sorting fallback;
- designated Outlook folder type, cross-referenced to MAIL-23;
- representative acceptance examples and counterexamples.

Known messages never collapse into a generic Other destination. Reasoned `Other` is only the extensible new-category mechanism. Needs sorting is a fail-closed routing outcome, not a classification.

## Course correction and simplification pass — 2026-08-19

The catalogue confirmed that operational destination is a deterministic projection of the durable classification decision. Persisting a second destination fact would create stale state and duplicate correction history, so step 3 is replaced by pure Core derivation. The current retained-mail workspace does not yet expose the complete category needed to call this policy; UI-14 and MAIL-23 are the two concrete downstream callers and must consume this owner when their separately planned read projections land. This ticket does not introduce a premature partial caller or database migration.

Four-lens pass:
- Reuse: retained `MailCategory`, `MailClassificationResult`, route predicates, and the existing immutable taxonomy; no second taxonomy or folder mapping was added.
- Simplification: removed a QDOS predicate-key fallback from the destination policy after the canonical `triage-request` subtype made it redundant.
- Efficiency: mapping is a pure switch over the already-loaded decision; no persistence query, additional write, or external operation occurs.
- Altitude: Core owns only category-to-operational-destination policy; folder identity/mutation stays with MAIL-23 and workspace filtering stays with UI-14.
