# Research — MAIL-09

## Question

How should Pegasus automatically associate an exact retained message and its attachments to exactly one Case only on accepted, unambiguous evidence?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: EvaluateIntakeCaseMatch and EfIntakeMutationStore already own Case-match decisions and association history for intake; retained mail exposes current Case association, so this ticket must reuse that policy/transaction rather than add workspace matching.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/CaseMatching/EvaluateIntakeCaseMatch.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

# Research refresh — 2026-08-20

## Question

Against `origin/dev`, which current Case-match/query/write owners can MAIL-09 reuse for the accepted system-wide unique-VRM or mailbox-scoped exact-thread rule, and what gaps must be closed without creating a second association implementation?

## Verified findings

- **Ref inspected directly:** `origin/dev` at `b36c6666`. The ticket, TICK-052, both epic contexts, FRD-02/08, capabilities/current architecture, Core policy/contracts, EF stores, Web mail pages and focused tests were read.
- **The durable automatic-association write already exists and is the owner to reuse.** `IAutomaticCaseAssociationStore.AssociateFromMatchAsync` and `EfIntakeMutationStore` use a serializable transaction, operation-key fingerprint, one current association row, system-worker attribution, match policy key/version and append-only `IntakeMutationHistory`. Replay and “staff reversed means never silently relink” behavior are proved by `CaseMatchIntegrationTests`.
- **The existing evaluator is narrower in a different direction.** `EvaluateIntakeCaseMatch` is the accepted route/provider eliminator used by `ProcessIntake`: it selects an `IProviderCaseMatchPolicy`, filters `CaseMatchIndex` by `WorkProviderCode`, and may use provider claim token, VRM and claimant-name hits plus contradiction eliminators. Only `QdosCaseMatchPolicy` is registered. It must remain the QDOS-direct MAIL-09 subset; it is not itself the general workspace rule accepted in this ticket.
- **The accepted MAIL-09 rule is recorded in this ticket's 2026-08-19 operator decision.** An inbound Case/PO is not a MAIL-09 automatic key. Automatic association may use a normalized VRM only when it resolves to exactly one Case system-wide, or a durable conversation identity only when the retained thread in that exact mailbox already resolves to exactly one Case. Zero, several, stale, or contradictory candidates abstain. When both evidence types exist they must agree; an ambiguous or contradictory type cannot be hidden by the other.
- **The current `CaseMatchIndex` cannot prove system-wide VRM uniqueness.** `CaseMatchIndexProjector` creates rows only for providers with a registered `IProviderCaseMatchPolicy`; today that means QDOS, and `EfCaseMatchIndex.FindByAnyKeyAsync` additionally requires a provider code. Reusing it unchanged would silently ignore Cases outside that policy cohort.
- **The system-wide Case query already has the correct data/convention.** `SearchCases` normalizes registration by removing non-alphanumerics and uppercasing; `EfCaseQueryStore.SearchAsync` performs an exact registration match across the canonical Case projection. Its staff-authorized/paged API is not safe to call as the Worker policy, but its query/normalization convention should be shared rather than creating another VRM grammar or broadening the provider-specific index.
- **Exact-thread evidence is available but no association candidate query exists.** `RetainedMailboxMessages` stores durable `MailboxId` and `ConversationIdentity`, and maps each row to an intake receipt through `ExternalReceiptToken`. A mailbox-scoped query must resolve distinct current Cases from both accepted `CaseIntakeLinks` and active `IntakeManualAssociations`; zero/multiple/conflicting Cases abstain. The message thread display's additional folder scope remains a viewing boundary and must not be confused with the ticket's mailbox-scoped association evidence.
- **The existing write needs a narrow stale-evidence extension for MAIL-09.** `AssociateFromMatchAsync` rechecks association existence, archived Case and live edit lease, but its request carries no expected Case version or evidence snapshot and the transaction does not re-evaluate the VRM/thread candidate. A read-then-write caller could therefore race a Case-data correction or thread relink. The existing transaction/history owner should be extended or supplied an atomic revalidation seam; a parallel association table/command is not justified.
- **The current mail projection would hide the reused store's result.** `EfRetainedMailboxMessageStore.MapSummariesAsync` joins `CaseIntakeLinks` only. `AssociateFromMatchAsync` writes an active `IntakeManualAssociation`, so `/Inbox/{id}` would still say “Not associated with a case” unless the retained query adopts the same `IntakeReceipt.CurrentCaseId` precedence already owned by `EfIntakeReceiptStore`.
- **Current Web behavior is read-only for association.** `Message.cshtml(.cs)` displays the Case link when `RetainedMailSummary.CaseId` exists but has no link/unlink handler; `MailWorkspaceWebTests` proves the unassociated state. TICK-051 needs the automatic Worker caller plus honest resulting display, not a staff confirmation form. TICK-052 owns deliberate Case search/confirmation and link/unlink/relink controls.
- **Runtime grants already support the reuse path.** The Worker role has `SELECT, INSERT, UPDATE` on `IntakeManualAssociations` and `SELECT, INSERT` on `IntakeMutationHistory`, with DELETE denied. Any new read query/grant must be reviewed against the exact tables used; no new mutation store or migration is automatically required.
- **Live acceptance is a production data write, not an Outlook action.** The ticket requires one live automatic association, but the 2026-08-19 decision is not authority to perform it. Immediately before execution, exact-target approval must name the retained message and Case and the proved VRM/thread evidence. The test must abort on ambiguity, stale state, contradiction or target mismatch, capture before/after/history/attribution/replay, and perform no Graph, Outlook or mailbox mutation.

## Implications

- Add one focused Core MAIL-09 policy/use case over dedicated read facts (system-wide exact VRM candidates and mailbox-scoped thread association candidates), then call the existing `IAutomaticCaseAssociationStore`. Reuse the QDOS normalizers/query conventions where applicable, but do not make `EvaluateIntakeCaseMatch` pretend its provider-scoped algorithm is the new rule.
- Keep the association write, current-association row and history in `EfIntakeMutationStore`. Close the stale window inside that existing transaction boundary; do not create another association table, generic match framework or UI-owned policy.
- Reuse `EfIntakeReceiptStore`'s current-association precedence when projecting retained mail so automatic and later manual associations render identically. Attachments already remain receipt-owned evidence; association relates that exact receipt and its assets, so no attachment copying or mailbox/Box mutation belongs here.
- TICK-051 lands before TICK-052. Both touch `EfIntakeMutationStore`, retained association projection, message detail and association tests; TICK-052 should consume the final transaction/result shape for staff correction rather than race it. UI-10 then assembles the delivered automatic display and manual controls.
- The exact accepted general rule is currently durable in the ticket but only generic association behavior appears in FRD-08/operator notes. The implementation plan must include a narrow FRD-08 update making the MAIL-09 VRM/thread/fail-closed behavior canonical; no ADR is needed and protected operator-notes meaning must not be changed incidentally.

## Open questions

No unresolved operator question remains. Planning must decide the smallest atomic revalidation shape inside the existing store boundary, but it may not alter the accepted keys, uniqueness scope, fail-closed behavior or exact live-approval requirement.
