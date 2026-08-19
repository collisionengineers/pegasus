# Plan — MAIL-04

## Chosen approach

add one exact-message classification/correction command with append-only explainable history. Reuse `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs`, keep Web/MCP callers thin, and place persistence or external mechanics only in `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs and retained-mail projection`. This follows the repository's one-Core-owner rule and the existing convention rather than adding a workspace-specific policy copy.

A parallel UI-owned implementation was rejected because UI-10, Automation MCP and background processing would diverge. A generic mail-action framework was rejected because each action already has a concrete Core boundary and no second abstraction caller is proven.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: implement its exact-message, fail-closed, durable-history and workspace behaviour. Any unresolved mapping/mutation behaviour remains conditional on the checked operator answer; do not silently amend the FRD.
- `docs/design/README.md`: apply the established confirmation, error, focus, navigation and accessibility conventions.
- No new ADR is planned: the existing Core/Infrastructure/Web boundary carries the change.

## Ordered implementation

1. Re-read the current target files after prerequisite branches land and name the exact existing contracts/helpers/tests being reused.
2. Add or extend the smallest Core contract/policy required to add one exact-message classification/correction command with append-only explainable history; validate identity, actor, reason, state and version before any write.
3. Implement the Infrastructure projection/transaction/adapter in src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs and retained-mail projection; preserve mailbox scope, idempotency, optimistic concurrency and append-only evidence.
4. Wire the real caller (src/Pegasus.Web/Pages/Mail/Message.cshtml.cs) through the Core use case with no duplicated taxonomy, mapping or authorization logic.
5. Add focused Core and integration/Web tests for before/after history, evidence/policy version, stale concurrency, duplicate delivery and re-evaluation protection.
6. Run the locked restore/build and focused tests, then the relevant full suite; perform the four-lens simplification pass and record honest dispositions.
7. Update FRD/capabilities only where the delivered behaviour/evidence warrants it; do not claim deployment, live Outlook verification or operator acceptance from local tests.

## Dependencies and sequencing

before TICK-045 and all downstream action callers.

## Proof

The post-implementation report will cite focused test output, Release build output, real-caller integration evidence and simplification findings. External-mailbox behaviour requires separately approved live verification and cannot be inferred from adapter tests.

## Risks and mitigations

- Identity or stale-state mistakes: exact mailbox/message keys plus optimistic concurrency and fail-closed validation.
- Policy duplication: one Core result consumed by Web, Worker and MCP.
- External side effects: local fakes/fixtures by default; no real Outlook/cloud write without exact approval.
- Scope growth: keep this ticket to its named capability and file follow-ups for independent behaviour.

## Simplification pass — 2026-08-19

- **Reuse:** kept the existing Core taxonomy/result, receipt envelope serializer, retained-mail store and Razor exact-message caller; no second category list or policy implementation was introduced. The UI options are projected from `MailTaxonomy`.
- **Simplification:** retained a single correction use case and one persistence port instead of a generic mail-action framework. Before/after snapshots use one private persistence DTO because the Core category is deliberately construct-only and must be revalidated on read.
- **Efficiency:** one transaction updates the current decision and appends history; history is loaded only for exact-message detail, never the mailbox list. Existing page-wide queries remain batched.
- **Altitude:** Core owns authorization, validation and correction semantics; Infrastructure owns EF concurrency/serialization; Web only binds input and renders the dossier.
- **Dispositions:** fixed the first SQL-test finding by reusing the existing versioned-envelope serializer instead of writing a second JSON shape. Added original actor/time backfill after the evidence-lens review found the current decision otherwise lacked attribution. No unapplied behaviour-preserving finding remains.

### Simplification re-check — PR-010

Kept canonical validation on `MailCategory` beside the sole taxonomy list and invoked it from the Core command. Web performs only early parsing for useful validation feedback; it is not the trust boundary. One parameterized page-pipeline test exercises all four hostile inputs and checks both current version and empty history, avoiding four duplicated fixtures.
