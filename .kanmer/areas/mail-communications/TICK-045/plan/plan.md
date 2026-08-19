# Plan — MAIL-03

## Chosen approach

prove the canonical classification contract works identically across approved mailboxes without universalising route predicates. Reuse `the MAIL-04 exact-message Core command and MailClassificationContracts`, keep Web/MCP callers thin, and place persistence or external mechanics only in `existing retained-mail projection`. This follows the repository's one-Core-owner rule and the existing convention rather than adding a workspace-specific policy copy.

A parallel UI-owned implementation was rejected because UI-10, Automation MCP and background processing would diverge. A generic mail-action framework was rejected because each action already has a concrete Core boundary and no second abstraction caller is proven.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: implement its exact-message, fail-closed, durable-history and workspace behaviour. Any unresolved mapping/mutation behaviour remains conditional on the checked operator answer; do not silently amend the FRD.
- `docs/design/README.md`: apply the established confirmation, error, focus, navigation and accessibility conventions.
- No new ADR is planned: the existing Core/Infrastructure/Web boundary carries the change.

## Ordered implementation

1. Re-read the current target files after prerequisite branches land and name the exact existing contracts/helpers/tests being reused.
2. Add or extend the smallest Core contract/policy required to prove the canonical classification contract works identically across approved mailboxes without universalising route predicates; validate identity, actor, reason, state and version before any write.
3. Implement the Infrastructure projection/transaction/adapter in existing retained-mail projection; preserve mailbox scope, idempotency, optimistic concurrency and append-only evidence.
4. Wire the real caller (two approved-mailbox exact-message callers) through the Core use case with no duplicated taxonomy, mapping or authorization logic.
5. Add focused Core and integration/Web tests for cross-mailbox invariance, ambiguity and unsupported/stale mailbox failures.
6. Run the locked restore/build and focused tests, then the relevant full suite; perform the four-lens simplification pass and record honest dispositions.
7. Update FRD/capabilities only where the delivered behaviour/evidence warrants it; do not claim deployment, live Outlook verification or operator acceptance from local tests.

## Dependencies and sequencing

implement after TICK-046 and keep only missing evidence.

## Proof

The post-implementation report will cite focused test output, Release build output, real-caller integration evidence and simplification findings. External-mailbox behaviour requires separately approved live verification and cannot be inferred from adapter tests.

## Risks and mitigations

- Identity or stale-state mistakes: exact mailbox/message keys plus optimistic concurrency and fail-closed validation.
- Policy duplication: one Core result consumed by Web, Worker and MCP.
- External side effects: local fakes/fixtures by default; no real Outlook/cloud write without exact approval.
- Scope growth: keep this ticket to its named capability and file follow-ups for independent behaviour.

## Evidence correction — one linked production mailbox — 2026-08-19

Production currently has only one linked mailbox. Keep the two-mailbox invariant in local/integration acceptance using two distinct configured mailbox identities, but do not make a two-mailbox production check a TICK-045 gate or claim it occurred. A read-only production check may cover the currently linked mailbox only. The first real second-mailbox evidence belongs to the relevant mailbox-ingestion ticket ([[TICK-036]], [[TICK-037]], or [[TICK-038]]) when that mailbox is connected.

## Simplification pass — 2026-08-19

- **Reuse:** the branch adds no production policy, port, store, or caller. It exercises the existing `CorrectRetainedMailClassification`, `IRetainedMailQueries`, retained-message store, and existing SQL fixture helpers delivered by MAIL-04.
- **Simplification:** the acceptance remains one coherent two-mailbox scenario rather than adding a second fake or wrapper. No behaviour-preserving simplification was identified.
- **Efficiency:** both mailbox outcomes are proved in one migrated database and one dependency-injection scope; no repeated host, migration, or taxonomy list was introduced.
- **Altitude:** the diff stays at MAIL-03's missing cross-mailbox evidence boundary. Route-specific predicates, folder mapping, mailbox ingestion, and external verification remain with their owning capabilities.

Disposition: no code changes required after the pass; the existing Core owner completely carries the capability and the branch contains only the missing integration proof and evidence-status update.
