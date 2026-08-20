# Research — MAIL-07

## Question

How should Pegasus move an exact message only after staff confirms the current policy recommendation, preserving classification on failure and supporting explicit retry?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: The Graph intake adapter reads approved scopes but there is no Core move command or adapter port; FRD-08 already specifies separate confirmation, no automatic movement, retry only by staff, and message visibility after success.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/RetainedMail.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

## Current-state refresh — 2026-08-20

### Question

What exact Core, persistence, Graph and Web seams can implement a separately confirmed exact-message move after MAIL-23/05, while absorbing archived MAIL-06, preserving source evidence, and proving behavior without any live Outlook mutation?

### Verified findings

- Source truth inspected at `origin/dev` `b36c66662288adb0727299276f675337442a1e22`; the local `dev` checkout is stale and was not used as current code.
- [[TICK-048]] is archived and has no research, plan or implementation documents. Its body contains only the MAIL-06 allocation and dependency on MAIL-05. FRD-08 and this ticket's checked decisions already absorb its behavior: the move is initiated only from opened message detail, shows the current designated recommendation, requires a separate explicit confirmation and reason, and never accepts an alternative destination.
- The hard predecessor chain is [[TICK-064]] → [[TICK-047]] → this ticket. MAIL-23 owns the typed logical-folder outcome and mailbox-approved exact identity binding. MAIL-05 owns the current message-level recommendation. MAIL-07 must consume that recommendation and its freshness/version evidence; the POST must not bind a destination folder from browser input.
- `RetainedMailboxMessageEntity` already preserves the exact internal message id, Graph mailbox id, immutable Graph message id and source folder identity. `MailboxIntake.cs` obtains the immutable id using `Prefer: IdType="ImmutableId"`; all identities are case-sensitive transport facts and must remain server-side.
- The retained row is explicitly write-once source evidence. A successful external move cannot overwrite its original `FolderIdentity`. MAIL-07 needs an append-only move operation/current-location projection that list/detail queries can overlay so success leaves Inbox and is later findable by destination/search without destroying arrival evidence.
- No move command, port, operation record or Graph mutation exists. `GraphMailClient` and `GraphApprovedInboxSource` in `GraphApprovedSources.cs` issue only GET requests; `DependencyInjection.cs` composes the reader in the Worker profile and only the address resolver in Web.
- Microsoft Graph v1 moves within one mailbox using `POST /users/{mailbox}/mailFolders/{sourceFolder}/messages/{message}/move` with JSON `destinationId`, returning `201 Created`. The folder-scoped form binds the expected source location. Official docs: https://learn.microsoft.com/en-us/graph/api/message-move?view=graph-rest-1.0
- Graph immutable ids remain stable across a move within the same mailbox only when every relevant request carries `Prefer: IdType="ImmutableId"`. That lets an uncertain-response recovery probe look up the same item and accept success only when its current parent is the already-approved destination. Official docs: https://learn.microsoft.com/en-us/graph/outlook-immutable-id
- An external move and SQL history cannot be one transaction. A dedicated operation record must reserve a unique operation key/request fingerprint before Graph, then record succeeded or failed. Replay of an already succeeded operation returns that result; reuse with different inputs conflicts. After a timeout/unknown response, probe the immutable item at the approved destination before permitting a new Graph move. A recorded failure remains visible; only a deliberate staff retry with a new operation key may call Graph again.
- The Core request must validate authenticated casework actor, exact retained-message id, required reason, operation key and the expected recommendation/classification/binding versions supplied by MAIL-05. It must fail closed if the message is absent, already moved, recommendation changed, binding unavailable/stale, source and destination coincide, or the provider location no longer matches the expected source.
- Existing `ActionHistory` is the permanent cross-domain business feed and can receive success/failure attribution, but it is not a uniquely claimed external-operation state machine. The concrete move operation record is necessary for exactly-once/recovery behavior; no generic mail-action framework is justified.
- `Pages/Mail/Message.cshtml(.cs)` is the real staff caller. It already keeps list scope through GET/POST and owns the classification action. The shared `Pages/Shared/_ReasonDialog.cshtml` supplies the repository confirmation/focus convention; the move form should reuse it and use the existing hidden GUID operation-key pattern.
- FRD-08 requires saved classification to survive move failure, failure to remain visible, staff-only retry, and success to remove the message from Inbox without duplication. Therefore the Graph call must occur only after the Pegasus-side classification/recommendation checks; no rollback or reclassification follows provider failure.
- Current production documentation grants Graph `Mail.Read` only. The move API requires `Mail.ReadWrite`, and the runbook requires exact tenant/application/mailbox/folder/action approval plus a negative scope test before any Graph mailbox call. The operator explicitly declined a live move on 2026-08-19. This ticket may use local SQL/integration tests and a fake HTTP Graph endpoint only; it must not change permissions, deploy/activate the write caller, touch the linked mailbox, or claim live-mutation evidence.

### Implications

- Add one focused Core move use case/port beside retained mail; do not put transport mechanics in the Razor page or copy MAIL-23/05 policy.
- Add one concrete durable move-operation/current-location owner in Infrastructure. Preserve the arrival row and project the latest successful location into existing workspace queries.
- Extend the existing Graph client/adapter boundary with the one folder-scoped move plus exact-location probe. Keep mailbox, message and destination identities sourced from persisted approved data, never from the browser.
- Web presents only the server-derived recommendation and reasoned Confirm/Cancel action. A later reclassification or mailbox-binding change invalidates an open form and requires reload.
- Local evidence can establish validation, HTTP shape, replay/recovery, projection and caller behavior. Deployment, `Mail.ReadWrite`, Exchange Application RBAC, negative-scope proof and any real move remain outside this ticket's present authorization and evidence.

### Dependencies and exact overlaps

- [[TICK-064]]: hard prerequisite and overlap in approved exact-folder identity, Graph resolution/composition, FRD-08, capabilities and current architecture. Land/rebase it first.
- [[TICK-047]]: direct prerequisite and overlap in Core retained-mail/recommendation contracts, `Message.cshtml(.cs)`, mailbox tests and docs. Land/rebase it second.
- [[TICK-053]]/MAIL-11 and [[TICK-056]]/UI-10: exact overlap in `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, `Message.cshtml(.cs)`, `MailWorkspaceWebTests.cs` and current-location/list behavior. MAIL-11 stabilizes destination scopes/search before MAIL-07; do not execute these against the same files concurrently.
- [[TICK-054]]/MAIL-13 and [[TICK-088]]/MAIL-12: exact overlap in the Graph mail client/adapter, Core mail actions, message detail, DI and Graph tests. MAIL-07 should establish the first narrow mutation/recovery convention; later tickets reuse it without turning it into a generic command framework.
- [[AUTO-003]]: downstream only. It adds the Automation caller after the Core action lands; MAIL-07 adds no MCP tool and Automation must not bypass the same operation key, version and approved-destination contract.

### Open questions

None. Confirmation ownership, destination authority and the no-live-move decision are already resolved.

## Post-merge symbol refresh — 2026-08-20

### Verified baseline

- Fetched `origin/dev` is `a1775841297108db8de2d612a2ba82452b02242e`, the merge of PR #474 after MAIL-23. This is the only baseline used for this refresh.
- MAIL-05 landed `RetainedMailFolderRecommendation(MailLogicalFolderType? FolderType, string PolicyKey, int PolicyVersion, string Reason)`. Its XML contract explicitly requires a later move to re-read the exact approved binding instead of carrying an opaque Graph destination identity from the view.
- `GetRetainedMail.RecommendFolderAsync` re-derives the logical folder with `MailLogicalFolderPolicy.Map`, selects the approved mailbox by exact ordinal `MailboxIdentity == RetainedMailSummary.MailboxId`, and checks the typed `ApprovedMailboxFolderBinding`. No destination identity is returned to Razor.
- `ApprovedMailbox.Version` is the landed concurrency value for mailbox/binding administration, but MAIL-05 does not currently place it on the recommendation. `MailClassificationDossier.Version` is the landed classification freshness value. Therefore the smallest safe extension is a non-secret approved-mailbox version on the recommendation; the POST still carries no mailbox, source-message, or destination Graph identity.
- `RetainedMailboxMessageEntity` remains documented and implemented as write-once arrival evidence. Its exact `MailboxId`, `FolderIdentity`, and `ImmutableMessageId` remain the source coordinates; MAIL-07 must add a separate operation/current-location record rather than update them.
- `GraphMailClient` owns Graph-host confinement, token acquisition, folder-scoped URI construction, safe failure mapping, and `Prefer: IdType="ImmutableId"` for message reads. It has no POST move or location probe. The narrow move/probe methods belong here; no second Graph client or general command API is justified.
- Web's real caller remains `Pages/Mail/Message.cshtml(.cs)`, with casework authorization, anti-forgery, list-context preservation, classification version input, and the shared reason-dialog convention available for reuse.
- No current schema owns an external mail-move operation. `ActionHistory` is the permanent reporting feed but cannot uniquely claim and recover a provider operation, confirming the need for one concrete mail-folder-move operation/current-location owner.

### Active overlap and execution gate

TICK-053 / PR #469 is still taken in Review. Its current diff overlaps all important MAIL-07 seams: `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, `GraphApprovedSources.cs`, `DependencyInjection.cs`, `Message.cshtml(.cs)`, `MailWorkspaceWebTests.cs`, `ProductionGraphSourceTests.cs`, and `RetainedMailPersistenceTests.cs`. It also changes retained-mail scope/search contracts and composes `GraphMailClient` in Web for read-only deleted-mail search.

Per AGENTS.md, TICK-049 must not be taken while that overlapping claim remains. After PR #469 merges and TICK-053 releases: fetch the new `origin/dev`, verify it contains `a1775841` and the #469 merge, re-read the landed versions of every overlap above, then create `../pegasus-worktrees/tick-049` on `task/tick-049-mail-07-confirmed-folder-move` from that exact `origin/dev` and call `take_ticket`.

### Refined implication

The accepted move form carries only the internal retained-message id, classification version, recommendation policy key/version, approved-mailbox version, a fresh operation key, and the required reason. Core re-loads the exact message, classification, policy and approved binding on POST. Infrastructure alone supplies the persisted exact mailbox/source/destination transport identities to the one move adapter. This preserves the accepted read-only recommendation contract and makes changed classification, policy, binding, or current location fail closed without exposing or trusting browser transport data.

## Review-blocker refresh — 2026-08-20

Read-only inspection of PR #477 head and the independent review set confirmed five gaps: only operation-key uniqueness guarded claims; uncertain recovery was not reachable from Razor; any prior success suppressed a later reclassification move; successful moves were excluded from every retained list/search route; and the report named unimplemented failure evidence. Existing owners were sufficient: the dedicated move store, MAIL-05 recommendation, approved typed bindings, MAIL-11 retained search, authenticated message page and existing LocalDB/fake-provider fixtures. No live mailbox or external-state premise was used.
