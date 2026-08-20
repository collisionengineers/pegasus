# Files — TICK-050 / MAIL-08 post-merge slice

## Changed files

| Path | Change and risk |
|---|---|
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Add the concrete nullable suggested-Move read projection and populate it from the landed recommendation's `CanMove`. Risk is accidental duplication of MAIL-05/07 eligibility; avoid any new rule or I/O. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | Render the optional advice and gate the existing shared MAIL-07 confirmation control through it. Keep uncertain status recovery separate and retain all existing route/freshness fields. |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` | Extend existing `GetRetainedMail` tests for zero-or-one derivation, unavailable/already-current abstention and re-derivation. Reuse current fakes. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Extend authenticated message-detail evidence for labelled advice, unchanged MAIL-07 delegation and absence without an eligible writer. |

## Documentation files

| Path | Change |
|---|---|
| `docs/capabilities.md` | Replace MAIL-08 allocation-only text with the exact local Core/Web evidence, without deployment/live claims. |
| `docs/current-architecture.md` | Add the source-level optional suggested-Move projection to the existing mail-workspace paragraph; preserve the unavailable-by-default production-writer statement. |

## Context files

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/RetainedMailFolderMove.cs` | MAIL-07 owns authorization, freshness, confirmation, operation keys, persistence and provider mutation; MAIL-08 calls none of it. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Existing POST handler is the delegation target and requires no change. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs` | Existing current-location and operation owner; read only, do not modify. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Requires classification and folder move to remain separate, exact-message detail only, designated destination only and explicit confirmation. |
| EPIC-006 `context.md` | Requires one Core owner and forbids unapproved local mailbox mutation. |

## Out of scope

No EF/store/query change, persistence, migration, transaction, operation key, history, Graph/provider adapter, action enum/registry/framework, broad action matrix, Case/link/send/read/category/flag/delete suggestion, MCP/Automation surface, deployment, live mailbox check or external write.
