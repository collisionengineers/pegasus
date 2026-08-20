# Research — MAIL-11

## Question

How should Pegasus browse, paginate, search and view exact retained messages, attachments and scoped threads including read-only Deleted Items?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: ListRetainedMail/GetRetainedMail, EfRetainedMailboxMessageStore and /Inbox already provide paged list/detail/thread over retained Inbox/Sent data; missing work is scoped body/attachment-content search, Deleted Items retention/search, richer filters and accessibility/state preservation.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/RetainedMail.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

# Research refresh — 2026-08-20

## Question

Against the current shipped and integration refs, what retained-mail browse/search/detail/thread behavior already has a real caller, what remains for MAIL-11, and which existing owners and sibling tickets constrain the implementation?

## Verified findings

- **Refs inspected directly:** `origin/main` at `2325ed4a` and `origin/dev` at `b36c6666`. The local `dev` checkout was ahead 1/behind 103, so no current-state conclusion was taken from its working tree. Source and tests below were read with `git show`/`git grep` against the refs.
- **The shipped Web/Core browse slice is already real on both refs.** `MailWorkspaceScope`, `IRetainedMailQueries`, `ListRetainedMail`, `GetRetainedMail` and `GetRetainedMailFreshness` in `src/Pegasus.Core/Intake/RetainedMail.cs` are called by `GET /Inbox` and `GET /Inbox/{id}` in `src/Pegasus.Web/Pages/Mail/`. The list is mailbox/folder scoped, newest-first and SQL-paged; detail returns retained body text, attachment metadata and a chronological thread.
- **Thread isolation is already correct and tested.** `EfRetainedMailboxMessageStore.GetAsync` joins only rows with the same `MailboxId`, persisted `FolderScope` and `ConversationIdentity`; `RetainedMailPersistenceTests.AThreadNeverCrossesMailboxScope` and the Web detail tests cover this boundary.
- **The retained query contract has no search or queue input.** `ListAsync` accepts only `MailWorkspaceScope(MailboxId, Folder)`, page and page size. Results carry no search term, match kind, matching attachment, unsupported-content marker or queue refinement. `/Inbox` likewise binds only `mailbox`, `folder` and `pageNumber`.
- **Only Inbox rows have a writer.** `PollApprovedInbox` writes `RetainedMailboxMessage` after accepted intake and before cursor advance; `EfRetainedMailboxMessageStore.RetainAsync` hard-codes `MailFolderScope.Inbox`. Sent and Deleted Items are declared scopes and render honest empty/unavailable states, but `RetainedMailPersistenceTests.SentAndDeletedScopesHoldNothingAndDoNotClaimUnretainedHistory` proves neither is populated. The separate Approved Sent evidence pipeline does not populate `RetainedMailboxMessages`; no Deleted Items source exists.
- **Attachment search data does not currently exist in the retained model.** `RetainedMailboxAttachmentEntity` stores filename, media type, length and ordinal only. `LocalEmailDisplayReader` supplies display metadata; full `IntakeContentFragment` text from `MimeKitPdfPigOpenXmlIntakeSourceReader` is transient processing input, while receipt `EvidenceJson` stores derived evidence signals rather than a complete searchable attachment-text projection. Therefore attachment-content search cannot be added as an EF filter over existing retained columns.
- **`origin/dev` has an additional real caller from TICK-062.** `src/Pegasus.Web/Mcp/MailMcpTools.cs` exposes `pegasus_mail_list` and `pegasus_mail_get` through the same Core use cases; it supports mailbox/folder/page but not search. This file is a compile/ripple caller for any Core contract change. Exposing the new MAIL-11 search options to Automation is owned by linked `AUTO-003`, not a reason to duplicate search policy here.
- **The governing behavior is already settled.** FRD-08 requires explicit mailbox/folder scope, individual-message results, accessible pagination, visible body/attachment-name/attachment-content match locations, named matching attachments, visible unsupported content, retained-scope threads, preserved return context, and no historical reconstruction. The 2026-08-19 ticket decision permits a post-deploy read-only live journey only within the currently approved Graph scope; it grants no broader permission and no mailbox mutation.
- **Current-state docs lag source on the new MCP caller.** `docs/current-architecture.md` accurately names the Web/Core/store owners, while `docs/capabilities.md` still describes MCP-05 as allocation-only despite the merged dev caller. Per `docs/index.md`, code plus passing tests wins for current-state research; TICK-062 verification/closeout owns its delivery claim.

## Implications

- Extend the existing retained-mail query/read model; do not create a second workspace/search policy or a generic mail-action abstraction.
- Planning must treat body/filename search as an extension of the existing SQL-paged query, but must choose one canonical persisted source for searchable attachment text and unsupported status. It must reuse the existing intake reader output rather than parse attachment formats a second way.
- Deleted Items is not a UI-only tab change. Its accepted read-only search needs an actual bounded source/projection within approved mailbox/folder scope; it must not infer Deleted content from the empty enum branch or reconstruct historical Inbox artifacts.
- Preserve current callers by evolving the Core request/result shape compatibly. Web is the MAIL-11 delivery caller; the merged MCP list/detail caller must keep compiling and retaining its present behavior, while AUTO-003 later adds the new Automation-facing options.
- Sequence TICK-053 after TICK-064's current mapping refresh as the programme directs, and do not run it concurrently with TICK-056 or TICK-057: all three claim the retained Core/store/list page/Web tests. The action and association tickets also overlap message detail; their lanes should consume MAIL-11's final detail/return-context shape rather than race it.
- No new top-level project/store/runtime is justified. A schema change inside the existing retained-mail persistence boundary is allowed and, if selected, must include the normal committed migration/model snapshot and runtime-grant review.

## Open questions

No unresolved operator/product question was found. The remaining choice—where the existing intake reader's searchable attachment output is projected for retained-mail queries—is a planning/implementation decision constrained by one Core owner and one persisted representation, not a request for new product scope.
