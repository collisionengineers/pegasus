# Files — TICK-064 / MAIL-23

## Where the change lands

| Path | Why |
|---|---|
| New focused policy beside `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` | Define the single typed logical-folder vocabulary and exhaustive `MailClassificationResult` → folder/no-recommendation policy. Reuse `MailCategory`; do not add queue fields to the taxonomy or duplicate MAIL-02. |
| `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` | Extend the existing approved-mailbox/version/actor boundary to carry mailbox-scoped administrator-approved logical-type → exact-folder-identity bindings. Keep arbitrary message/client destinations out of the contract. |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs`, `AdministrationPolicyModelConfiguration.cs`, and `EfApprovedMailboxStore.cs` | Persist the binding as a keyed collection under the approved mailbox, with the existing concurrency, replay, actor and history behavior. Do not add 13 nullable columns or copy the classification table. |
| A migration plus `Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Add only the normalized binding storage, uniqueness/length/delete constraints and actual runtime grants required by the existing mailbox administration callers. |
| Graph mailbox identity-resolution adapter under `src/Pegasus.Infrastructure/Email/` | Resolve configured logical folder choices to exact identities within the already-approved mailbox. Reuse the validation/token/error conventions in `GraphApprovedSources.cs`; read only, with no folder creation/rename/move. |
| `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml(.cs)` | Let authorized mailbox administrators review and approve the typed bindings through the existing mailbox policy workflow; never accept a message-specific arbitrary destination. |
| `tests/Pegasus.Core.Tests/Intake/Classification/` | Exhaustively prove every canonical classification has exactly one logical folder/no-recommendation outcome, corrections re-derive, Unidentified is no-recommendation, and operational destination remains separate. |
| Existing administration persistence, Graph resolver and mailbox-administration Web tests under `tests/Pegasus.IntegrationTests/` | Prove mailbox scope, exact identity, uniqueness, authorization, version/replay behavior, unavailable resolution, and no external mutation. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Reconcile the one stale Triage-without-VRM “Needs sorting” phrase to binding Unidentified vocabulary without collapsing Triage, and clarify MAIL-23 mapping/binding versus MAIL-05 recommendation and MAIL-07 confirmed move only if implementation exposes an ambiguity. |
| `docs/capabilities.md` and `docs/current-architecture.md` | Record delivered evidence/as-built ownership after implementation; do not claim deployment or live Outlook verification from local tests. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | The settled classification vocabulary and validation; it deliberately carries no queue, Triage or folder destination. Reuse it rather than creating a second taxonomy. |
| `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` | MAIL-02's one Core operational-queue owner. Folder type is a separate projection and must not reimplement this switch. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` and `src/Pegasus.Web/Mcp/MailMcpTools.cs` | The proven staff and Automation callers of MAIL-02 on current dev. They establish derivation from the current dossier; MAIL-23 itself adds no caller-specific policy. |
| `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` and `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs` | Existing mailbox authorization, exact-identity validation, optimistic version, idempotent operation and history conventions that the binding must preserve. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` | Existing Graph host/token/fail-closed identity resolution and mailbox/folder identity bounds; it currently resolves only well-known Inbox/Sent. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Its `FolderIdentity` is the retained source location, not approved destination policy. Read to avoid misusing or extending it in MAIL-23. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | The authoritative exhaustive category/operational destination/logical folder table and the 13 approved folder types. |
| `docs/operator-notes.md#unidentified-received-material` | Unidentified supersedes the broad former Needs sorting meaning while Triage/Blocked/Audit/Image Intake remain distinct. |
| INTK-007 proof and EPIC-006 `context.md` | The vocabulary replacement qualification, one-Core-owner constraint, and prohibition on unapproved local-alpha Outlook mutation. |

## Ripple effects

- [[TICK-047]]/MAIL-05 is the direct consumer and should be replanned after this contract lands. Its current map overlaps the Core classification area, approved-mailbox store, mailbox detail, FRD-08 and capabilities.
- [[TICK-049]]/MAIL-07 shares the exact Graph-folder boundary and likely composition registration; serialize any adapter/DI edits and require it to consume the approved binding.
- [[TICK-057]]/UI-14 consumes MAIL-02 queue policy, not this folder binding. Source work can proceed independently, but FRD-08/capabilities edits overlap.
- [[TICK-053]], [[TICK-056]] and [[TICK-050]] own `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, mailbox pages and `MailWorkspaceWebTests.cs`; keeping those out of MAIL-23 avoids a broad conflict wave.
- [[AUTO-003]] later extends `MailMcpTools.cs` after MAIL-05/07. It has no direct source-file overlap with a Core/config-only MAIL-23 implementation.

## Out of scope

Message-level recommendation UI/API (MAIL-05), confirmation and movement (MAIL-07), queue/list filtering (UI-14), generic workspace assembly (UI-10), MCP tool additions (AUTO-003), changes to read/category/flag/delete/send actions, per-message persisted recommendation state, arbitrary client-selected folder identities, Graph permission expansion, folder creation/rename/move, deployment, or live Outlook mutation.
