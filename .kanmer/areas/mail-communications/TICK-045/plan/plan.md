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

## Review response and second simplification pass — 2026-08-19 (takeover by claude-code, DELIV-012)

Taken over by operator decision after an independent review found PR #422 delivered no production code and a test that could not fail for the reason it claimed (see scratch note `takeover`). Two Codex P1 comments and the fabricated evidence were addressed as follows.

### Codex P1 #1 — exercise the policy instead of seeding a fabricated result

`OneCorrectionPolicyAppliesIdenticallyAndIndependentlyAcrossMailboxes` in `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` constructed `MailClassificationResult.Ambiguous(..., "shared-mail-policy", 9)` — a literal no registered policy emits, so the test could not detect a broken classification wiring. Fixed: the test now resolves `IMailClassificationPolicy` from the scope's DI container (`services.GetRequiredService<IMailClassificationPolicy>()`, the registered `QdosMailClassificationPolicy`) and calls `.Classify(readResult)` directly against content built to trigger a genuine Ambiguous outcome (a Triage phrase in the body plus an Audit notification title in an attachment — two of the policy's real predicates matching at once). Assertions on `PolicyKey`/`PolicyVersion` now compare against the resolved `policy` instance, not literals. **Verified the test can actually fail**: temporarily commented out the `AddSingleton<IMailClassificationPolicy, QdosMailClassificationPolicy>()` registration in `DependencyInjection.cs`, reran the test, watched it fail with `InvalidOperationException: No service for type 'IMailClassificationPolicy' has been registered`, then reverted (confirmed clean via `git diff`) and reran green.

### Codex P1 #2 — fabricated mailbox address

The test used `const string secondMailboxId = "claims"` / `"claims@collisionengineers.co.uk"` — not one of the four documented mailboxes in `docs/operator-notes.md` (`desk`, `engineers`, `info`, `instructions`). Fixed: replaced with `engineers` / `engineers@collisionengineers.co.uk`. No dedicated mailbox-approval command/test helper exists in this test support (checked: no `ApproveMailbox`/`AddApprovedMailbox` Core command; `AdministrationPolicyPersistenceTests.cs` manipulates `ApprovedMailboxes` directly via EF, not through a reusable helper this file already calls). The test's own pre-existing convention for the first mailbox already seeds only `ApprovedInboxPollStates` via the file's own `SeedPollStateAsync` helper (not a full `ApprovedMailboxes` approval row) — so the second mailbox now reaches the documented address through that same existing helper, consistently with how the first mailbox was already seeded, rather than inventing a new seeding path.

### MailOperationalDestinationPolicy — production caller (operator-directed mid-task)

Initial investigation (scope item 1 of the takeover brief) found `MailOperationalDestinationPolicy` had zero non-test callers, and the destination-mapping capability MAIL-02 was explicitly logged in `docs/capabilities.md` as `Next / 0.3.0` with "workspace caller ... remain separately allocated" — i.e. no invented caller. The operator then supplied TICK-044's `open-questions` resolution ("the retained mailbox viewer is meant to show this information ... a policy referenced only by tests is incomplete") and directed wiring it into the existing retained mailbox viewer. Delivered: `MessageModel.Destination(MailClassificationResult)` in `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` calls `MailOperationalDestinationPolicy.Map` directly (a pure function of the already-loaded classification decision, no new persistence); `Message.cshtml` renders it as two new rows ("Operational destination", "Destination policy") inside the existing "Classification evidence" `<dl>`, following the page's existing convention exactly; `OperatorLabels.MailOperationalDestinationLabel` maps the enum to operator words, reusing the literal `"Needs sorting"` already rendered elsewhere on this same page (`MessageModel.QueueLabel`, `MessageModel.OutcomeLabel(IntakeDecision)`) rather than inventing a second spelling of the same fail-closed state. Two Web integration tests in `MailWorkspaceWebTests.cs` prove it: the existing Unclassified-decision test now also asserts the fail-closed "Needs sorting" destination row, and a new test (`MessageDetailShowsTheOperationalDestinationDerivedFromAClassifiedDecision`) proves a Classified/NewInstructionReceived/inspection decision renders "Receiving work". Verified both can fail: temporarily made `Destination()` return a hardcoded Unclassified mapping, reran, watched the `<dt>Operational destination</dt>...` assertions fail, reverted, reran green.

`docs/capabilities.md`'s MAIL-02 row was rewritten to state exactly this: a real production caller now exists at `/Inbox/{id}`, proven by Web integration tests; UI-14 (categorised queue views) was explicitly left undelivered, not upgraded. TICK-044's own checklist/open-questions were left untouched (not this ticket) — the coordinator (operator, DELIV-012) will reconcile TICK-044 after this PR merges.

### Simplification pass (four lenses)

- **Reuse.** No new port, store, DI registration, or test-fake type was added. The classification test now reuses the already-registered `IMailClassificationPolicy`/`QdosMailClassificationPolicy` instead of adding a substitute test policy (the `ConsumerTypedClassificationPolicy` pattern from `QdosAllocationRecoveryTests.cs` was considered and rejected — it would prove DI resolves *a* policy, not that the *real* QDOS predicates produce the evidence being asserted). The viewer wiring reuses the page's existing static-helper-plus-`<dl>` convention (`DecisionLabel`, `QueueLabel`, `OutcomeLabel`) and `OperatorLabels`, adding one method to each rather than a new presentation layer.
- **Simplification.** `MailOperationalDestinationResult` is computed on every render rather than cached or persisted — it is a pure, cheap function of already-loaded data, so persisting it would be duplicate state for no benefit (repository rule: no duplicate persisted state where a deterministic projection suffices).
- **Efficiency.** No additional database round-trip: the destination is derived from the `MailClassificationDossier` the page already loaded via `GetRetainedMail`.
- **Altitude.** The diff stays at MAIL-03's evidence gap (real policy, real mailboxes) plus the one operator-directed MAIL-02 caller; it does not touch MAIL-05/06/07 (Outlook folder move/confirmation), UI-14 (categorised queues), or route-predicate/provider work, which remain separately allocated.

Disposition: no unapplied findings.
