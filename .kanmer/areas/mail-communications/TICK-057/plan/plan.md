# Plan — UI-14

## Chosen approach

add operational-queue navigation and filtering without copying the mapping. Reuse `MAIL-02 operational-destination result in RetainedMail projections`, keep Web/MCP callers thin, and place persistence or external mechanics only in `query filtering in EfRetainedMailboxMessageStore`. This follows the repository's one-Core-owner rule and the existing convention rather than adding a workspace-specific policy copy.

A parallel UI-owned implementation was rejected because UI-10, Automation MCP and background processing would diverge. A generic mail-action framework was rejected because each action already has a concrete Core boundary and no second abstraction caller is proven.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: implement its exact-message, fail-closed, durable-history and workspace behaviour. Any unresolved mapping/mutation behaviour remains conditional on the checked operator answer; do not silently amend the FRD.
- `docs/design/README.md`: apply the established confirmation, error, focus, navigation and accessibility conventions.
- No new ADR is planned: the existing Core/Infrastructure/Web boundary carries the change.

## Ordered implementation

1. Re-read the current target files after prerequisite branches land and name the exact existing contracts/helpers/tests being reused.
2. Add or extend the smallest Core contract/policy required to add operational-queue navigation and filtering without copying the mapping; validate identity, actor, reason, state and version before any write.
3. Implement the Infrastructure projection/transaction/adapter in query filtering in EfRetainedMailboxMessageStore; preserve mailbox scope, idempotency, optimistic concurrency and append-only evidence.
4. Wire the real caller (/Inbox queue filters) through the Core use case with no duplicated taxonomy, mapping or authorization logic.
5. Add focused Core and integration/Web tests for Receiving/Queries/Other plus distinct Needs sorting/Triage, counts, paging and preserved filters.
6. Run the locked restore/build and focused tests, then the relevant full suite; perform the four-lens simplification pass and record honest dispositions.
7. Update FRD/capabilities only where the delivered behaviour/evidence warrants it; do not claim deployment, live Outlook verification or operator acceptance from local tests.

## Dependencies and sequencing

TICK-044 and TICK-064.

## Proof

The post-implementation report will cite focused test output, Release build output, real-caller integration evidence and simplification findings. External-mailbox behaviour requires separately approved live verification and cannot be inferred from adapter tests.

## Risks and mitigations

- Identity or stale-state mistakes: exact mailbox/message keys plus optimistic concurrency and fail-closed validation.
- Policy duplication: one Core result consumed by Web, Worker and MCP.
- External side effects: local fakes/fixtures by default; no real Outlook/cloud write without exact approval.
- Scope growth: keep this ticket to its named capability and file follow-ups for independent behaviour.

## Operator decision — 2026-08-19

Do not collapse known classifications into a generic Other queue. UI-14 consumes FRD-08's canonical classification registry and offers detailed category/subtype views, while Needs sorting remains a distinct fail-closed work queue and Triage remains its separate workflow. A reasoned custom Other classification appears under its recorded new category name and reasoning.

## Production queue acceptance — operator decision 2026-08-19

After deployment, authenticate to the production mailbox workspace and verify detailed classification views plus distinct Receiving work, Queries, Needs sorting, and Triage queues against current retained mail. Capture counts, filters, paging, preserved mailbox/folder scope, and exact detailed classifications where real examples exist. Treat an empty queue as an evidenced empty state, not a failure and not licence to fabricate or mutate data.
