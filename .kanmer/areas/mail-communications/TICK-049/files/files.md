# Files — TICK-049 / MAIL-07

## Where the change lands

| Path | Why |
|---|---|
| New focused Core file beside `src/Pegasus.Core/Intake/RetainedMail.cs` | Define the exact-message move request/result, concrete provider-move port, durable operation port and one use case. Consume MAIL-05's recommendation/version contract; validate actor, reason, operation key and expected recommendation without accepting a destination from the caller. |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Extend the existing detail/list projection only with current effective location and latest move result/history needed by Web and later search. Preserve the original retained-message identity and classification dossier. |
| New focused move-operation entity/configuration beside `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` and `MailboxModelConfiguration.cs` | Persist one uniquely claimed request fingerprint, source/destination identities, actor/reason, pending/succeeded/failed outcome, provider result and retry/recovery facts. Do not turn the write-once retained row into mutable source evidence. |
| New `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs` | Resolve the internal message to exact mailbox/immutable/source identities, reserve/replay/conflict the operation, append permanent ActionHistory, and project latest successful location. Keep SQL and Graph calls in separate phases. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Overlay the latest successful move when filtering/projecting list and detail so a moved item leaves Inbox and remains available to later destination/search scopes; never overwrite the arrival `FolderIdentity`. |
| A migration and `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Add the concrete operation/current-location schema, uniqueness and length constraints, relationships/delete restrictions and only the Web grants actually required. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Extend the existing mailbox-agnostic Graph client with one exact folder-scoped POST move and immutable-id location probe, always sending `Prefer: IdType="ImmutableId"`; add no general mutation API. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` and, only if a separate Web composition is required, `src/Pegasus.Web/Program.cs` | Register the Core/store/adapter through the existing host boundaries. Do not smuggle Worker polling into Web or activate a production writer without the separately approved permission/scope. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml(.cs)` | Render the server-derived MAIL-05 recommendation and latest result; add the exact-message POST with recommendation versions, GUID operation key and required reason; preserve mailbox/folder/page context and visible failure/retry. |
| `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml` | Reuse unchanged if its current Confirm/Cancel, required reason and focus behavior fits. Edit only if a behavior required by this move cannot be expressed by its existing parameters. |
| `tests/Pegasus.Core.Tests/Intake/` focused retained-mail move tests | Prove authorization, validation, stale recommendation/binding refusal, replay/conflict, already-moved/source mismatch and provider result mapping. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Prove operation claim/history, concurrency, failure/retry, uncertain-response recovery, immutable arrival evidence and current-location projection. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Prove the exact POST path/body/header, source/destination/mailbox confinement, 201 parsing, location probe and safe HTTP failure mapping with the fake handler only. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Prove the real authenticated staff confirmation, anti-forgery, no destination input, stale reload, success/removal, visible failure and explicit retry while classification remains saved. |
| `docs/capabilities.md` and current-state docs | Record local implementation/evidence honestly. State that no live move, Mail.ReadWrite grant, deployment or production activation was performed. FRD-08 already owns behavior and changes only for a concrete inconsistency. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | The poll supplies the exact Graph immutable message id and source folder; these identities are retained once and remain server-side. |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` | `RetainedMailboxMessageEntity` is explicit write-once arrival evidence. Its `MailboxId`, `ImmutableMessageId` and `FolderIdentity` are the exact move coordinates, not editable UI fields. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` ActionHistory mapping | Permanent material actions already use aggregate/event/actor/outcome/correlation/reason/before/after/policy fields; reuse it for reporting, but not as the sole external-operation claim. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | `GraphMailClient` already owns token acquisition, Graph host confinement, immutable-id headers and content-safe failure mapping; current methods are GET-only. |
| `src/Pegasus.Core/Identity/StaffAuthorization.cs` | The casework authorization used by current staff/Automation mail callers. Do not invent a second UI-only authorization table; later Automation exposure still passes through Core. |
| TICK-064 research/files/open-questions | MAIL-23 owns logical folder type and administrator-approved exact identity binding, with no per-message persisted recommendation. |
| TICK-047 research/files/open-questions | MAIL-05 owns the current exact recommendation and read-only production viewer evidence; MAIL-07 must consume its landed types after rebase. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Separate confirmation, no bulk/arbitrary destination, classification survival, visible failure, staff-only retry and successful location behavior. |
| `docs/design/README.md` | Existing reason-dialog and mutation states: confirmation, stale version, dependency unavailable, replay, conflict, recovery and focus return. |
| `docs/runbook.md#live-operation-approval-matrix` and `docs/operations.md` | Outlook calls need exact RBAC/scope approval and negative proof. Production currently documents Mail.Read only; this ticket has no live-write authority. |
| EPIC-006 `context.md` | One Core implementation and no local-alpha mailbox mutation. |

## Ripple effects

- [[TICK-064]] and [[TICK-047]] are hard predecessors. They overlap exact destination/configuration, Core contracts, mailbox detail, tests and governing docs; rebase and refresh this plan only after both land.
- [[TICK-053]] and [[TICK-056]] overlap `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, message/list behavior and `MailWorkspaceWebTests.cs`. MAIL-11 must establish final folder/search scope before MAIL-07's current-location projection; do not edit these concurrently.
- [[TICK-054]] and [[TICK-088]] overlap the Graph client/adapter, Core mail actions, Web message page, composition and Graph tests. They should reuse MAIL-07's narrow exact-message external-operation convention after it lands.
- [[AUTO-003]] later adds `MailMcpTools.cs` and Automation tests. MAIL-07 has no MCP-file overlap and supplies only the Core use case it will call.

## Out of scope

Reviving [[TICK-048]], arbitrary/bulk folder selection, new taxonomy or recommendation policy, queue/search implementation beyond the minimum successful-location projection, read/category/flag/delete/send actions, a generic mail-action framework, background automatic retry, Worker function/outbox/runtime creation, Graph permission/RBAC changes, production deployment/activation, live Outlook calls, or claims of live-mutation verification.
