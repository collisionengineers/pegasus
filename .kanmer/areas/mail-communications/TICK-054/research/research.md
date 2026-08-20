# Research — MAIL-13

## Question

How should Pegasus perform separately authorised exact-message Outlook state/category/flag/delete mutations through Core-owned commands with durable attribution?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: The current workspace is intentionally read-only and GraphApprovedSources is an intake reader; no mutation port exists. FRD-08 says UI-10 itself does not change read state, so this later capability must remain a separate action surface.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/RetainedMail.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

# Research refresh — 2026-08-20

## Question

Against current `origin/dev`, what exact-message state actions belong to MAIL-13, which landed/planned seams can it reuse after MAIL-07, and which authorization and destructive-operation decisions still block implementation?

## Verified findings

- **Ref inspected directly:** `origin/dev` at `b36c6666`. TICK-054's full documents, TICK-049/MAIL-07, overlapping ticket maps, both epic contexts, governing/current-state docs, current Core/EF/Graph/Web code and focused tests were read.
- **The current application is read-only for Outlook state.** `GraphMailClient` in `GraphApprovedSources.cs` has GET-only delta/MIME methods. Production/runbook evidence records `Mail.Read`, and current architecture explicitly excludes Graph mutation. `Message.cshtml(.cs)` has no state-action handler.
- **The retained row is arrival evidence, not current provider state.** `RetainedMailboxMessageEntity` is documented write-once. Its `IsRead` is the value observed at retention; categories, flag, Graph `changeKey`, and current `parentFolderId` are absent. MAIL-13 must not overwrite the arrival row or pretend the retained boolean stays synchronized after Outlook/Pegasus changes.
- **Exact in-scope actions are narrower than a generic mailbox editor.** From the checked operator decision: one opened exact message may be set read/unread; one approved Outlook category may be added/removed without replacing unrelated categories; it may be flagged/unflagged (not completed or given arbitrary dates); it may be moved to Deleted Items and restored to the exact server-recorded prior approved folder. MAIL-07 continues to own ordinary policy-designated folder moves. MAIL-12 owns compose/reply/forward/send. No bulk/row/preview action, arbitrary folder, arbitrary category, client-supplied Graph identity, or source-evidence deletion belongs here.
- **MAIL-07 should establish the first reusable mutation convention, but it has not landed yet.** Its refreshed plan proposes the focused exact-message Core request, immutable mailbox/message/current-location resolution, uniquely fingerprinted operation claim, append-only history, Graph move plus recovery probe, current-location projection, reasoned confirmation, DI and fake-HTTP tests. MAIL-13 should rebase after it lands and reuse those concrete seams; current `origin/dev` contains none of them.
- **The Graph operations are distinct and should remain explicit.** Official Graph v1 documentation says PATCH can update `isRead`, `categories`, and `flag`; move to well-known `deleteditems` is the reversible delete operation; restore is another exact move to the recorded prior folder; `permanentDelete` is a separate POST. These require `Mail.ReadWrite`. Every relevant request must send `Prefer: IdType="ImmutableId"`; IDs are case-sensitive and stable only while the item remains in the same mailbox. Sources: https://learn.microsoft.com/en-us/graph/api/message-update?view=graph-rest-1.0, https://learn.microsoft.com/en-us/graph/api/message-move?view=graph-rest-1.0, https://learn.microsoft.com/en-us/graph/api/message-permanentdelete?view=graph-rest-1.0, https://learn.microsoft.com/en-us/graph/outlook-immutable-id
- **Provider concurrency needs a current-state probe.** The Core request should carry internal retained-message id, one enumerated desired action, expected Pegasus state/version, reason where required, and operation key. Infrastructure resolves mailbox/immutable/current-folder coordinates server-side, reads current `changeKey`, parent folder, read/category/flag state immediately before mutation, rejects stale/unsupported/mismatched state, and records the before/after provider facts. Category add/remove must preserve all unrelated current categories.
- **Use MAIL-07's external-operation recovery rather than an outbox fiction.** SQL and Graph cannot be one transaction. Reserve the operation fingerprint before Graph, persist success/failure/unknown and permanent `ActionHistory`, return an identical completed replay, conflict on key reuse with different input, and probe the immutable item after an uncertain PATCH/move response. A deliberate retry of a recorded failure uses a new key; no background retry is justified.
- **Permanent deletion is a qualitatively different checkpoint.** Graph v1 `permanentDelete` returns 204 and places the item in the Purges area; ordinary Outlook clients cannot recover it, although tenant hold/retention may retain it. Pegasus must never describe this as guaranteed physical erasure. Because absence after timeout cannot safely distinguish success from an ambiguous failure, an unknown permanent-delete result must not be retried automatically or treated as ordinary move recovery.
- **There is a binding authority conflict, so permanent deletion is not implementation-ready.** TICK-054's 2026-08-19 operator decision requests explicitly confirmed permanent deletion, but protected `docs/operator-notes.md` says Administrators have “No permanent deletion”; FRD-04 prohibits it for every staff role; accepted ADR-0004 says the domain permits it through no surface; and `docs/design/README.md` repeats the UI prohibition. Per repository authority rules, this cannot be silently resolved by the ticket. The operator must explicitly say whether the newer decision supersedes the protected business rule and, if so, which role may act; governing docs then need reconciliation before code.
- **“Approved Outlook categories” also lacks a canonical owner/value set.** No approved category names, fixed list, or administration contract exists on `origin/dev`; FRD-04/design prohibit a generic mailbox-rule editor before its policy is accepted. MAIL-13 cannot safely accept free-form strings or invent a settings surface. The operator must name the allowed set/owner and whether assigning a missing Outlook master category is permitted.
- **Permissions and live approval are separate gates.** Local implementation may use LocalDB and fake Graph HTTP only. Enabling the adapter requires explicit approval for the exact Entra application permission/admin consent change to `Mail.ReadWrite`, exact Exchange Application RBAC scope and a negative outside-scope test. Those grants still do not authorize a mailbox write. The live journey separately requires exact operator approval immediately before the disposable message/mailbox/folder/category/actions; each step rechecks identity/state. A fresh separate confirmation is required immediately before any resolved permanent-delete step.
- **Retained evidence survives every Outlook action.** Deleting or permanently deleting the provider item never deletes the Pegasus retained message, attachments, intake receipt, association, classification, action history, or confirmed event. Existing FRD-08 already says confirmed finality survives later Outlook move/delete.

## Implications

- Land and rebase after TICK-049, then add the smallest concrete MAIL-13 Core action vocabulary and state-operation persistence over its exact-message/operation/history/Graph conventions. A general mail-command framework remains unjustified.
- Reuse MAIL-07's move/location path for Deleted Items and restore. Add only focused PATCH/probe and, if authorized, permanent-delete transport methods. Project latest known provider state separately from immutable arrival evidence and label its freshness honestly.
- TICK-053 should stabilize retained folder/search/detail shapes before action work; TICK-054 then precedes UI-10 action composition. AUTO-003 consumes the landed Core actions later and must not call Graph directly.
- Planning/implementation must pause on the two unresolved authority questions below. A narrow FRD-08 update will be needed for the accepted MAIL-13 behavior, and any permanent-delete reversal also requires protected operator-notes/FRD-04/design/ADR reconciliation through their governing process.

## Open questions

Two operator decisions remain: the protected permanent-deletion conflict/role, and the canonical approved Outlook-category set/owner.
