# Plan — MCP-05

## Chosen approach

add thin Automation MCP tools over the delivered mail queries and commands. Reuse `existing Core mail use cases only`, keep Web/MCP callers thin, and place persistence or external mechanics only in `existing persistence/Graph adapters reached through Core`. This follows the repository's one-Core-owner rule and the existing convention rather than adding a workspace-specific policy copy.

A parallel UI-owned implementation was rejected because UI-10, Automation MCP and background processing would diverge. A generic mail-action framework was rejected because each action already has a concrete Core boundary and no second abstraction caller is proven.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: implement its exact-message, fail-closed, durable-history and workspace behaviour. Any unresolved mapping/mutation behaviour remains conditional on the checked operator answer; do not silently amend the FRD.
- `docs/design/README.md`: apply the established confirmation, error, focus, navigation and accessibility conventions.
- No new ADR is planned: the existing Core/Infrastructure/Web boundary carries the change.

## Ordered implementation

1. Re-read the current target files after prerequisite branches land and name the exact existing contracts/helpers/tests being reused.
2. Add or extend the smallest Core contract/policy required to add thin Automation MCP tools over the delivered mail queries and commands; validate identity, actor, reason, state and version before any write.
3. Implement the Infrastructure projection/transaction/adapter in existing persistence/Graph adapters reached through Core; preserve mailbox scope, idempotency, optimistic concurrency and append-only evidence.
4. Wire the real caller (Pegasus.Web Automation MCP tool registration) through the Core use case with no duplicated taxonomy, mapping or authorization logic.
5. Add focused Core and integration/Web tests for scope denial, attribution, replay/version parity, read tools and only staff-equivalent delivered mutations.
6. Run the locked restore/build and focused tests, then the relevant full suite; perform the four-lens simplification pass and record honest dispositions.
7. Update FRD/capabilities only where the delivered behaviour/evidence warrants it; do not claim deployment, live Outlook verification or operator acceptance from local tests.

## Dependencies and sequencing

last in lane after TICK-056 and owning MAIL actions.

## Proof

The post-implementation report will cite focused test output, Release build output, real-caller integration evidence and simplification findings. External-mailbox behaviour requires separately approved live verification and cannot be inferred from adapter tests.

## Risks and mitigations

- Identity or stale-state mistakes: exact mailbox/message keys plus optimistic concurrency and fail-closed validation.
- Policy duplication: one Core result consumed by Web, Worker and MCP.
- External side effects: local fakes/fixtures by default; no real Outlook/cloud write without exact approval.
- Scope growth: keep this ticket to its named capability and file follow-ups for independent behaviour.

## Full user-facing MCP parity — operator decision 2026-08-19

Automation MCP must expose every email-workspace option available to an authorised user through thin tools over the same Core owners. The inventory includes browsing/search/detail; classification/correction and queues; folder recommendation and confirmed move; suggestions; automatic/manual Case association, unlink and relink; read state, category, flag, folder, delete, restore and permanent-delete operations; and compose/reply/forward/send once their owning capabilities land. Preserve the exact user-facing authorization, confirmation, version, idempotency, attribution, history, failure and recovery contracts. Do not create MCP-only policy or arbitrary destination/delete/send authority.

After deployment, run the complete tool inventory through the live Automation MCP client. Exercise all reads. Demonstrate mutation-tool discovery and validation, and execute writes only within the exact-target approvals separately recorded by the owning MAIL tickets. Capture successful parity, replay/version behavior, attribution, and explicit denial of unapproved scope.
