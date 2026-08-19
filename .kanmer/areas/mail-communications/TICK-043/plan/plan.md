# Plan — MAIL-01

## Chosen approach

establish the canonical multi-dimensional inbound message identity and converge poll/provider ingestion on it. Reuse `src/Pegasus.Core/Intake/MailboxIntake.cs`, keep Web/MCP callers thin, and place persistence or external mechanics only in `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`. This follows the repository's one-Core-owner rule and the existing convention rather than adding a workspace-specific policy copy.

A parallel UI-owned implementation was rejected because UI-10, Automation MCP and background processing would diverge. A generic mail-action framework was rejected because each action already has a concrete Core boundary and no second abstraction caller is proven.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: implement its exact-message, fail-closed, durable-history and workspace behaviour. Any unresolved mapping/mutation behaviour remains conditional on the checked operator answer; do not silently amend the FRD.
- `docs/design/README.md`: apply the established confirmation, error, focus, navigation and accessibility conventions.
- No new ADR is planned: the existing Core/Infrastructure/Web boundary carries the change.

## Ordered implementation

1. Re-read the current target files after prerequisite branches land and name the exact existing contracts/helpers/tests being reused.
2. Add or extend the smallest Core contract/policy required to establish the canonical multi-dimensional inbound message identity and converge poll/provider ingestion on it; validate identity, actor, reason, state and version before any write.
3. Implement the Infrastructure projection/transaction/adapter in src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs; preserve mailbox scope, idempotency, optimistic concurrency and append-only evidence.
4. Wire the real caller (Graph/poll retained-mail ingestion) through the Core use case with no duplicated taxonomy, mapping or authorization logic.
5. Add focused Core and integration/Web tests for duplicate delivery, post-move provider ID, missing/contradictory identity and cross-mailbox isolation.
6. Run the locked restore/build and focused tests, then the relevant full suite; perform the four-lens simplification pass and record honest dispositions.
7. Update FRD/capabilities only where the delivered behaviour/evidence warrants it; do not claim deployment, live Outlook verification or operator acceptance from local tests.

## Dependencies and sequencing

none; this is a foundation for the workspace.

## Proof

The post-implementation report will cite focused test output, Release build output, real-caller integration evidence and simplification findings. External-mailbox behaviour requires separately approved live verification and cannot be inferred from adapter tests.

## Risks and mitigations

- Identity or stale-state mistakes: exact mailbox/message keys plus optimistic concurrency and fail-closed validation.
- Policy duplication: one Core result consumed by Web, Worker and MCP.
- External side effects: local fakes/fixtures by default; no real Outlook/cloud write without exact approval.
- Scope growth: keep this ticket to its named capability and file follow-ups for independent behaviour.

## Simplification pass — 2026-08-19

- **Reuse:** kept `PollApprovedInbox`, its existing SHA-256/receipt-token convention, `IRetainedMailboxMessageStore`, the EF retained read model, and Graph's existing immutable-ID request; no second identity service or policy list was introduced.
- **Simplification:** extracted the repeated existing-message lookup into one store helper; retained the existing positional contracts and migration stream rather than adding an identity wrapper or new project.
- **Efficiency:** duplicate detection and thread isolation remain SQL-side indexed queries; added the mailbox + RFC unique index and kept list/detail projection shapes unchanged.
- **Altitude:** FRD-08 owns behavioural identity rules, Core validates them, Infrastructure enforces persistence, and the existing Graph/poll caller supplies the facts. No UI or transport mutation leaked into MAIL-01.
- **Disposition:** applied the duplicate-query extraction. No unapplied behaviour-preserving finding remains.

## PR-004 simplification re-check — 2026-08-19

- **Reuse:** one public Core canonicalizer now supplies both the receipt-token and Infrastructure persistence paths; no duplicate normalization routine was accepted.
- **Simplification:** raw RFC identity remains the evidence field and one additional canonical column is solely the database key. This is smaller and clearer than relying on three different implicit equality rules.
- **Efficiency:** normalization is computed once per store lookup; the canonical column uses a binary-collated composite unique index, so duplicate detection remains an indexed SQL query.
- **Altitude:** canonical equality is Core policy, while collation/index mechanics remain Infrastructure. The existing poll is the real caller.
- **Disposition:** the blocking review finding is fixed. No unapplied simplification finding remains.
