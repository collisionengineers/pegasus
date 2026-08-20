# Files — TICK-049 / MAIL-07

## Implementation files after PR #469 lands

| Path | Change and reuse |
|---|---|
| `src/Pegasus.Core/Intake/RetainedMailFolderMove.cs` (new) | One concrete request/result, durable-operation port, exact provider move/probe port, errors, and Core use case. Reuse `StaffAuthorization`, `MailLogicalFolderPolicy`, `ApprovedMailbox` typed bindings, and retained-mail classification; add no generic mail-command framework. |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Extend the landed read-only recommendation with only `ApprovedMailbox.Version` freshness and expose current effective location/latest move result needed by detail rendering. Keep exact Graph identities out of the view/browser. Reconcile with PR #469's search overloads first. |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` and `MailboxModelConfiguration.cs` | Add one dedicated append-only folder-move operation/current-location entity with unique operation key and request fingerprint, exact server-owned coordinates, actor/reason, state/result timestamps, and restrictive relationships. Preserve `RetainedMailboxMessageEntity` unchanged as arrival evidence. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, one generated migration, and model snapshot | Register the concrete schema and indexes. Rebase after PR #469's search-document migration so migration order and snapshot include both changes. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs` (new) | Reserve/replay/conflict one operation; resolve exact source/current location; record succeeded/failed/uncertain recovery evidence and `ActionHistory`; expose latest successful current location. SQL reservation/completion are separate from Graph I/O. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Overlay latest successful location so Inbox queries exclude moved messages while direct detail preserves the original arrival facts and shows latest result. Reconcile with PR #469's search-before-paging projection. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Extend the existing `GraphMailClient` only with folder-scoped POST move and exact immutable-item parent-folder probe. Reuse its token, host/URI validation, error mapping, and immutable-id header. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` and existing Web composition | Register the Core/store boundary and the narrow adapter only in the locally exercised/fake-capable composition agreed for this ticket. Do not change Graph permission, deploy, or perform a live call. Reconcile with PR #469's read-only Graph client registration. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml(.cs)` | Reuse the authenticated message page, anti-forgery, list context and shared reason confirmation. Submit no transport identity or destination; post only internal id, freshness values, operation key and reason. Show durable failure/retry and success/current-location state. |
| `docs/capabilities.md` and `docs/current-architecture.md` | Record local implementation and fake/local evidence only. Do not claim deployment, permission, activation, or live mutation. FRD-08/design already own behaviour and change only if implementation reveals a real inconsistency. |

## Tests

| Path | Evidence |
|---|---|
| `tests/Pegasus.Core.Tests/Intake/RetainedMailFolderMoveTests.cs` (new) | Authorization, required fields, no browser destination, classification/policy/binding freshness, unavailable/already-moved/source-mismatch refusal, replay/conflict, failure, uncertain probe recovery, and explicit new-key retry. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Unique claim/fingerprint, concurrent reservation, append-only attempts/history, immutable arrival evidence, latest current location, Inbox exclusion and direct-detail visibility; preserve PR #469 search behaviour. |
| `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` | Fake-handler proof of exact mailbox/folder/message POST path, JSON `destinationId`, `Prefer: IdType="ImmutableId"`, 201 parsing, exact parent-folder probe, host confinement and failure mapping. No real Graph call. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Exact authenticated GET/POST, semantic Confirm/Cancel, required reason, anti-forgery, no destination field, stale refusal/reload, success removal, visible failure and staff-only new-key retry while classification remains saved. |

## Context files

| Path | Why read it |
|---|---|
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Separate confirmation; no arbitrary/bulk move; classification survives failure; durable visible result; explicit retry; successful location behaviour. |
| `docs/design/README.md` and `Pages/Shared/_ReasonDialog.cshtml` | Existing confirmation, focus, stale/conflict, replay and error conventions. |
| `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` | `ApprovedMailbox.Version` and typed exact folder bindings; reuse rather than copy. |
| `src/Pegasus.Core/Intake/MailboxIntake.cs` and `MailboxEntities.cs` | Exact immutable message/source identity and write-once arrival boundary. |
| `docs/runbook.md#live-operation-approval-matrix`, `docs/operations.md`, EPIC-006 context | No permission/cloud/deployment/live-mailbox write is authorized; local/fake evidence only. |
| TICK-064 and TICK-047 docs | MAIL-23 owns mapping/binding; MAIL-05 owns the read-only current recommendation. |
| PR #469 final diff after merge | Landed search/current read contracts and exact overlapping test/composition shapes. |

## Execution blocker and handoff

Do not take or create TICK-049's worktree until TICK-053 / PR #469 merges and releases its overlapping claim. Then fetch `origin/dev`, re-read every overlap above, create `../pegasus-worktrees/tick-049` on `task/tick-049-mail-07-confirmed-folder-move` from that exact head, and take the ticket.

## Out of scope

Reviving TICK-048; arbitrary/bulk destinations; a generic mail-action framework; read/category/flag/delete/send work; background retry or a new runtime; rewriting retained arrival evidence; MCP/Automation callers; Graph permission/RBAC changes; deployment/activation; any real Outlook or cloud write; or claims of live-mutation verification.

## Review-blocker changed-file refresh — 2026-08-20

- `src/Pegasus.Core/Intake/RetainedMail.cs` and `RetainedMailFolderMove.cs`: exact current-location-aware recommendation and safe same-key recovery result material.
- `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs`, `EfRetainedMailboxMessageStore.cs`, mailbox entity/configuration and existing unmerged folder-move migration/designer/snapshot: active claim serialization, latest-success current location and canonical search inclusion.
- `src/Pegasus.Web/Pages/Mail/Index.cshtml`, `Message.cshtml`, `Message.cshtml.cs`: search scope explanation, same-key status check and reclassification-aware list context.
- `tests/Pegasus.Core.Tests/Intake/RetainedMailFolderMoveTests.cs`, `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs`, `MailWorkspaceWebTests.cs`: interface fake plus exact claim/freshness/failure/reclassification/search/recovery evidence.
- No new project, top-level directory, policy list, search store, provider client, external write or deployment unit.
