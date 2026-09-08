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

# Current research — TICK-054, 2026-09-08

## Question and authority

Refresh the recoverable-only exact-message action scope against accepted dev
`19e6f523bf6760cab39104b4dca3674b0ac8a512`. This section supersedes the old
August current-state findings above; the old research is retained as history,
not execution authority. No source, test, mailbox or cloud action was taken.

Current ticket body and checked questions `06febe8dac9f44a2` settle read/unread,
one approved-category add/remove, flag/unflag, recoverable Delete, and Restore
to the server-recorded prior approved folder. Permanent deletion is forbidden
for every role. FRD-08's category catalogue, exact retained identity, deliberate
opened-message actions, retained evidence and outbound Flag/Delete clauses;
EPIC-006; and EPIC-011 D4/D22 agree. Ordinary policy-designated movement remains
MAIL-07. Compose/send/EVA detection and MAIL-031 Administration are not absorbed.
The fresh root instruction authorises planning only; no take or implementation.

## Findings

- **Existing mover, not a planned dependency:** Core
  `Intake/RetainedMailFolderMove.cs` contains `MoveRetainedMailFolder`,
  `IRetainedMailFolderMoveStore`, `IRetainedMailFolderMover` and the exact
  coordinates/result types. The EF implementation is
  `Persistence/EfRetainedMailFolderMoveStore.cs`; its journal is
  `RetainedMailFolderMoveEntity` in `MailboxEntities.cs`, configured in
  `MailboxModelConfiguration.cs`. Unique operation key plus one pending or
  uncertain operation per retained message already exclude concurrent writes.
  It reserves before Graph and probes the immutable item's parent after an
  uncertain move. The existing Message handler posts expected classification,
  recommendation and mailbox versions, reason and operation key.
- **Classification restriction is not a Delete gate:** existing MoveAsync
  resolves only the current classification's designated logical folder.
  `MailLogicalFolderType` contains business destinations, not Inbox or Deleted
  Items. Delete/Restore must use explicit Core actions and server-owned approved
  folder coordinates without invented classification or recommendation values;
  they reuse the move transport and journal/exclusion, not its destination rule.
- **Current state cannot overwrite arrival evidence:**
  `RetainedMailboxMessageEntity.IsRead/FolderIdentity/FolderScope` describe
  retention. The list's `BuildMatches` still uses arrival IsRead, and hides an
  Inbox row after any successful journal move. That latter rule would wrongly
  keep a restored-to-Inbox item hidden and must become effective-current-folder
  filtering for this caller. The same projection owns list counts, detail and
  search; do not introduce a second unread/folder rule in Razor. A small durable
  current Outlook state/version/observed-time projection in the existing EF
  owner is required; immutable source metadata is not a mutable cache.
- **Concrete transport reuse:** `GraphMailClient` in
  `Email/GraphApprovedSources.cs` already has `MoveMessageAsync`,
  `ReadMessageParentFolderAsync` and `ResolveDeletedItemsFolderAsync`.
  `GraphRetainedMailFolderMover` delegates move/probe, but no production
  composition registers it. DI line 101 still installs only
  `UnavailableRetainedMailFolderMover`. MAIL-028 owns that activation, not
  MAIL-031 or a claim that TICK-049's old production label proves a live writer.
  TICK-049 proof explicitly says no writer/Outlook mutation was activated.
- **Catalogue exists and is not Graph's master catalogue:**
  `ApprovedOutlookCategories.cs` owns the global entry and
  `ResolveApprovedOutlookCategory`; `EfApprovedOutlookCategoryStore` reloads
  active entries. `/Administration/MailCategories` is the real admin caller.
  MAIL-004 proof `c17ceee04e5daf70` explicitly leaves MAIL-13 consumption
  undelivered. Post only internal category ID; resolve current active exact
  display name and version before mutation. Never create a new catalogue,
  colours, Graph master-category synchronisation or search/linking semantics.
- **Dependency direction:** live links show TICK-049 and MAIL-004 block
  TICK-054; both implementations are integrated. TICK-054 blocks MAIL-031,
  whose backlog body owns Administration policy controls. MAIL-031 is not an
  unfinished prerequisite to the exact-message commands. MAIL-028 activation
  remains separate. MAIL-027's old flag/delete clauses duplicate this settled
  owner; its outbound/EVA scope remains untouched and it is not claimed here.
- **Why the stream did not deliver these actions:** Astra
  `v1_implementation_plans/DEFERRED-WORK.md:35` explicitly excludes
  TICK-054, MAIL-028 and MAIL-026/027 flag/delete clauses; browsing and staff
  send are distinct. The latest user request brings credential-free Preparing
  work back into scope. This is an explicit deferred residual, not evidence
  that PLAT-075 implemented Flag/Delete. PLAT-075 stays Verifying on its foreign
  `task/pegasus-v1-platform` / `../pegasus-worktrees/v1-platform` record;
  no force-take, release, cleanup or historical rewrite is authorised.
- **Recovery limit:** SQL and Graph are not atomic. Existing cancellation
  handoff can mark a move uncertain, but a process loss can leave pending.
  New state actions must not strand or blindly repeat that operation: use the
  same-key read-only probe, keep unresolved outcomes occupied, and distinguish
  an observed target state from proof that Pegasus caused an external change.
  A still-active pending request must not be released by a competing replay.

## Provider facts checked read-only

No project-declared sources exist (`get_sources`: declaredCount 0). Official
Microsoft Graph v1 pages were read, with no provider request:

- [Update message](https://learn.microsoft.com/en-us/graph/api/message-update?view=graph-rest-1.0)
  documents PATCH of isRead, categories and flag, Mail.ReadWrite and a returned
  updated message. Omitted properties remain unaffected; category replacement
  must be derived from the freshly read full collection, not caller strings.
- [Move message](https://learn.microsoft.com/en-us/graph/api/message-move?view=graph-rest-1.0)
  documents exact-message move and destinationId. Reuse move for Deleted Items
  and Restore, never DELETE or permanentDelete.
- [Immutable IDs](https://learn.microsoft.com/en-us/graph/outlook-immutable-id)
  documents the immutable-ID preference and same-mailbox stability. Every
  exact read/mutation keeps that header; never cross a mailbox boundary.
- [Message resource](https://learn.microsoft.com/en-us/graph/api/resources/message?view=graph-rest-1.0)
  defines changeKey as the message version and parentFolderId/read/flag fields.
  The update page does not establish a contractual If-Match guarantee for this
  endpoint. Do not manufacture one from generic OData guidance. Carry the
  returned ETag unchanged when available, send conditional PATCH, fail closed
  on stale/missing expected state, and keep provider enforcement as an explicit
  activation evidence obligation. A fake 412 test cannot prove live enforcement.

## Implications

One bounded extension of the existing retained-mail owner is sufficient:
closed exact-message actions, existing journal/exclusion/move adapter,
current-state projection, and thin authenticated Message handlers. No new
runtime, queue, dispatcher, package, general mail framework, permanent delete,
mailbox-wide synchronisation or TICK-088 work. Root owns verification; activation
and disposable-message evidence remain distinct from local implementation.


## Preparation refresh — accepted dev493f7460d, 2026-09-08

Read-only source census is pinned to
`493f7460d7728a6576d240d4feb7d0bf2a377ec5`; it does not activate a writer.
Compared with the prior `19e6f523bf6760cab39104b4dca3674b0ac8a512` baseline,
only six mapped paths changed: DependencyInjection's principal policy
composition, EfRetainedMailboxMessageStore's provisional sender helper,
RetainedMailPersistenceTests' QDOS policy selection, the INTK-063 restricted
Worker test, current-architecture's principal/image-recovery descriptions,
and the generated Test UI index. The folder-move command/store, categories,
Graph transport, entities/configuration, page, FRD-08 and migration inventory
are unchanged. Preserve those accepted principal-routing and image-recovery
deltas; none implements MAIL-13.

`MoveRetainedMailFolder`, its store, `ListApprovedOutlookCategories` and
the active category resolver are already registered in DependencyInjection
(lines 101–105 and the catalogue registration block). MessageModel already
injects the move command. Concrete action/refresh/check-status methods belong
on that same Core owner; a staff-only active-choice method belongs on the
already-registered category query, leaving its administrator method unchanged.
Razor can inject that existing query normally. No new command class, wrapper,
manual production construction, service locator or DI edit is required.

A complete port-consumer census found an omitted existing fixture:
`tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs:875`
`FolderMoveState` implements both retained move ports. Adapt it in place
when those contracts extend; retain its assertions that viewing a suggestion
does not move or probe. Other implementers are already mapped: Core
RecordingStore/empty/unavailable adapters, the Graph and EF adapters, and
RetainedMailPersistenceTests/MailWorkspaceWebTests fakes. The two explicit
EfRetainedMailFolderMoveStore constructions are both in the already-mapped
RetainedMailPersistenceTests (1198 and 1925); any constructor adaptation is
fixture-local, not a reason to change production composition.

Keep the actual runtime-role check in AzureSqlRuntimeRoleMigrationTests:
its existing RetainedMailFolderMovesUseExactWebOnlyAppendPermissions and
ConnectedContextFactory/EXECUTE AS pattern are the direct fit. Do not move the
test or introduce a new harness just to evade INTK-064's whole-file ownership.
Likewise the current as-built description remains a necessary coordinated
edit in current-architecture.md. Root must order these whole-file edits
before execution; this preparation claims neither file.

The eight actions, one journal, immutable-arrival/current-observation split,
recorded restore target and probe-only uncertain recovery remain unchanged.
A response/request header and fake 412 cannot establish provider enforcement.
MAIL-028 still owes the separately authorised live conditional-update canary;
no Graph request or new guarantee was made here. MAIL-031 remains downstream.
