# Research — MAIL-10

## Question

How should Pegasus let authorised staff link, unlink, relink or correct one exact message/Case association with reasoned append-only history?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: EfIntakeMutationStore already implements manual association and immutable action history with concurrency/version patterns; the Mail detail is currently read-only and should become the additional caller of that same Core action.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/IntakeContracts.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

# Research refresh — 2026-08-20

## Question

Against current `origin/dev`, which Case-association transaction and Case-search conventions can MAIL-10 reuse for one exact retained message, what are the exact link/unlink/relink semantics, and where must this ticket serialize after MAIL-09?

## Findings

- **Ref and authorities inspected directly:** `origin/dev` at `b36c6666`; TICK-051 and both shared epic contexts; FRD-02/08, `docs/design/README.md`, `docs/capabilities.md`, the live-operation matrix, current Core/EF/Web callers and focused tests. The repository working tree was not used as current-state evidence.
- **The reusable Core write already exists.** `LinkIntakeRequest` / `ReverseIntakeLinkRequest`, `ILinkIntake` / `IReverseIntakeLink`, and their `LinkIntake` / `ReverseIntakeLink` implementations own staff authorization, non-empty exact receipt/Case identity, non-negative expected versions, operation-key and required-reason validation. Both call the one `IIntakeMutationStore`; no mail-specific association command or generic mail-action framework is justified.
- **The reusable transaction already has the required correction evidence.** `EfIntakeMutationStore.ExecuteAsync` runs at serializable isolation, rejects operation-key reuse with a different fingerprint, reloads the receipt, checks the expected receipt version, verifies the exact Case is non-archived/nonterminal and protected by the actor's live edit lease and expected Case version, mutates the single `IntakeManualAssociation` row, increments receipt/Case versions, clears the Case lease, and appends both `IntakeMutationHistory` and `CaseWorkflowEvents` with actor, reason, operation key and structured before/after state. Delete is not used or granted.
- **Exact link behavior:** the message must resolve server-side to its mailbox Intake receipt. An unassociated receipt may be linked only after deliberate `ISearchCases` search, a business-readable target summary, required reason and explicit confirmation. `LinkAsync` refuses an already-active association. If the receipt has a prior inactive row, it reactivates that same row for the selected Case, increments its association version, clears old unlink/match-policy state and records a staff decision.
- **Exact unlink behavior:** only the exact current active association may be reversed. `ReverseLinkAsync` refuses a missing, inactive or different-Case relationship, marks the single row inactive, increments its version and records the unlink time/actor/reason. The accepted `CaseIntakeLink` lineage and original source identity are not deleted, and `IntakeReceipt.CurrentCaseId` becomes null because manual-association precedence remains authoritative after a reversal.
- **Exact relink/correction behavior is staged, not a hidden swap.** There is no accepted direct active-Case-to-active-Case relink request. The settled production journey and current transaction require a reasoned unlink of the current Case, followed by a separate reasoned link of the now-unassociated receipt to the replacement Case. The replacement is searched and summarized again and receives its own confirmation, current versions, lease and operation key. If the second transition fails or goes stale, the source remains honestly unlinked with the first decision preserved; staff may retry only the link. This is simpler and matches the accepted link → unlink → relink evidence journey.
- **The behavior is already proved independently of mail.** `CaseAcceptanceReplayTests.AcceptedOriginCanBeUnlinkedAndRelinkedWithoutDeletingLineage` proves accepted lineage remains, same-command replay is a no-op, a conflicting replay is refused, one association row is reused and three immutable mutation-history rows remain. `CaseMatchIntegrationTests` proves automatic association writes the same row/history family and never silently relinks after staff reversal.
- **A reusable real Web convention already exists.** `UploadCaseDecision` uses `ISearchCases`, `IGetCase`, `IGetIntake`, `IAcquireCaseEditLease` and `ILinkIntake`; the two upload confirmation pages share its typed suggestions, exact-reference fail-closed fallback, current-association replay handling and leased link. `_UploadOutcome.cshtml` plus `wwwroot/js/site.js` already implement the accessible, no-script-safe Case-search convention. MAIL-10 should reuse or minimally generalize this concrete orchestration for the third caller and add the reverse path, not copy it into `MessageModel`.
- **The current retained-mail caller is read-only for association.** `/Inbox/{id}` displays `RetainedMailSummary.CaseId` and preserves mailbox/folder/page context, but has no Case-search/link/unlink handlers. The summary already carries the exact retained message ID and server-derived `IntakeReceiptId`, which is sufficient to resolve the receipt afresh on POST; client-supplied receipt/version/Case facts must not become authority.
- **MAIL-09 is the hard serialization predecessor.** TICK-051 owns the general automatic-match caller, any atomic stale-evidence extension in `EfIntakeMutationStore`, and fixing `EfRetainedMailboxMessageStore` to project the same current-association precedence as `IntakeReceipt.CurrentCaseId`. MAIL-10 must start from TICK-051's merged transaction/result and retained projection. Otherwise it would race the same store/read model and could render a staff or automatic association incorrectly.
- **Classification is not a link gate.** FRD-08 explicitly permits a deliberate manual Case link while classification remains unresolved when link evidence is sufficient. A general chase that mentions several Cases remains one unlinked source occurrence; MAIL-10 never creates one-to-many association or copies a message/attachment.
- **The production caller already has the necessary database permissions.** The Web runtime grant matrix allows `SELECT/INSERT/UPDATE` on `IntakeManualAssociations`, `SELECT/INSERT` on `IntakeMutationHistory`, and the existing Case/receipt/workflow operations; DELETE is denied. MAIL-10 should require no schema, new association/history table or grant broadening after TICK-051 unless its merged shape proves otherwise.
- **Live acceptance does not touch Outlook.** Local implementation and tests mutate only disposable local databases. The accepted production link → unlink → relink journey is a production Pegasus/Azure SQL write and is required evidence, but the 2026-08-19 decision is not standing execution authority. Immediately before execution, exact-target approval must name the retained message, initial Case, replacement Case and approved reasons; capture displayed summaries, versions, actor, before/after/current state and all history entries; abort on mismatch or staleness. No Graph, Outlook mailbox, folder, category, read-state, move, delete or Box mutation is authorized by this ticket.

## Implications

- Land and merge TICK-051 first, then refresh MAIL-10 against its actual symbols and diff. Reuse the final `ILinkIntake` / `IReverseIntakeLink` transaction and current-association projection without a second store, table, result taxonomy or atomic relink abstraction.
- Keep the new behavior on exact retained-message detail. Resolve message → receipt on the server, reuse the existing Case-search/target-summary and leased association orchestration, and preserve the existing mailbox/folder/page return context into Case detail and back.
- Present link, unlink and later link-to-replacement as explicit reasoned transitions. Do not silently combine them or mutate Case/PO/reference identity. Existing concurrency and history are the recovery mechanism.
- Focus new delivery evidence on the real Mail caller and projection parity: authorized and unauthorized search/mutations, exact-message binding, link with unresolved classification, stale receipt/Case/lease, wrong current Case, replay/conflicting replay, link → unlink → replacement link, preserved accepted lineage/history, and return context. Existing transaction tests should be extended rather than copied into a mail-specific persistence suite.
- FRD-08 and the design authority already settle this behavior. Do not edit operator notes or add an ADR. Update capability/current-state wording only to the evidence tier actually delivered.

## Open questions

No unresolved operator/product question remains. Planning may choose the smallest refactor that lets the retained-mail caller reuse the existing shared Web association flow after TICK-051 lands, but it may not change the staged behavior, external-write boundary or one-Core-owner rule.

## Focused post-merge refresh — 2026-08-20

- Verified `origin/dev` is exactly `708706b83eb45104eb58cdcf6410e97278d2d040`, the merged TICK-051 head requested by the handoff.
- Re-read the landed MAIL-09 projection. `EfRetainedMailboxMessageStore` now derives each retained message's current Case through the shared current-association precedence; the detail already supplies the exact server-derived `IntakeReceiptId` and current `CaseId`. MAIL-10 needs no retained-mail schema or projection change.
- Re-read the landed write seam. `LinkIntake`, `ReverseIntakeLink`, and `EfIntakeMutationStore` remain the single Core/EF owners for authorization, versions, edit lease, operation-key replay, serializable current-association mutation, and append-only history. No Core contract or persistence change is required.
- Corrected one stale research premise: current `origin/dev` has no `UploadCaseDecision` type. The concrete reusable conventions are `ISearchCases` / `IGetCase`, `IAcquireCaseEditLease`, the existing Intake Details link/reverse command calls, and the shared reason/confirmation presentation. The smallest implementation is a thin Mail page caller over those existing ports, not a new presenter or generic action service.
- Exact Web journey: a side-effect-free search returns canonical `CaseSearchItem` summaries; selecting one reloads the canonical Case and renders its business summary and current version; the confirmed POST reloads the exact message and receipt server-side, checks that the receipt is still unassociated and the selected Case/version is still the reviewed target, acquires the existing edit lease, then calls `ILinkIntake`. Unlink similarly reloads the exact message/receipt/current Case and refuses any mismatch before lease + `IReverseIntakeLink`.
- Replacement remains two honest decisions: unlink completes first; only the resulting unassociated page offers a new search and separately reasoned confirmation. There is no active-to-active swap handler or optional replacement parameter.
- Evidence is local/fake Web and disposable SQL only. Production correction execution, Graph, Outlook, Box, cloud, permission, deployment, and external writes are excluded from this implementation task.

## Symbol correction from execution-time worktree inspection — 2026-08-20

The initial post-merge lookup was run from the root checkout and incorrectly reported that `UploadCaseDecision` was absent. Inspection inside the exact `708706b8` ticket worktree confirmed `src/Pegasus.Web/Presentation/UploadCaseDecision.cs` is landed and registered. The implementation therefore reuses its bounded `SearchAsync` suggestions for the Mail page. The Mail mutation POST remains deliberately stricter than upload attach: it compares the receipt and Case versions rendered with the reviewed target before acquiring the existing lease and delegating to `ILinkIntake` / `IReverseIntakeLink`. No second search policy, Core business rule, store, or schema was added.

## PR-048..050 focused blocker research — 2026-08-20

- The Core replay check is already correctly first in `EfIntakeMutationStore.ExecuteAsync`: it finds `IntakeMutationHistory` by operation key and checks the exact event/fingerprint before receipt version, association state, Case version, or lease authority. The Mail page currently prevents that owner from seeing successful replays by performing its own state checks and acquiring a fresh lease first.
- The exact association fingerprint includes receipt, Case, reviewed receipt/Case versions, actor, operation key, reason, and the lease token. Therefore a final confirmation must carry the already-acquired token; a post-success reacquire cannot reconstruct it because the canonical mutation clears the lease and the lease store retains only its hash.
- The existing Intake Details convention already separates lease claim from link/unlink confirmation and carries the lease token through TempData into the final form. MAIL-10 should use the same two-step shape: prepare exact target/current Case edit authority, then render the reasoned final confirmation with the token and reviewed versions. The final POST delegates immediately to Core after server-side message→receipt resolution, allowing exact replay and fingerprint conflict without a second Web replay taxonomy.
- `IReleaseCaseEditLease` is already registered. Triage association uses a `finally` compensation with `CancellationToken.None`; Operations uses a quiet-release helper. MAIL-10 can inject that existing port and release only after a definitive post-acquire refusal. Cancellation/unknown commit outcome must preserve the token/operation for same-confirmation recovery instead of releasing uncertain authority.
- The accessible-result defect is markup-only. Keep `UploadCaseDecision.SearchAsync`; move registration, claimant, and stage inside the one existing anchor so the visible text is also its accessible name. No component or script is needed.
- Required evidence belongs in `MailWorkspaceWebTests`: exact successful link replay; exact successful unlink replay; same-key changed-reason conflict; forced definitive link failure after lease claim followed by immediate lease reacquisition; success clears lease; and one focusable result link whose text includes reference, registration, claimant, and stage.

## PR-051/052 focused blocker research — 2026-08-20

The protected confirmation payload must be the exact preparation authority, not merely transport for the lease token. Bind it to route message id, server-derived receipt id and Link/Unlink intent, validate the complete submitted authority, and retain it after success so exact Core replay remains possible. A mismatch is a definitive no-write refusal whose already-acquired lease is resolved through the existing release port.

A recoverable release failure is itself unconfirmed compensation. Preserve the same protected payload and surface same-confirmation retry; retrying reruns the definitive refusal and release. Clear only after confirmed release. A fail-once decorator around the real release port gives exact SQL/Web proof without a new runtime abstraction.
