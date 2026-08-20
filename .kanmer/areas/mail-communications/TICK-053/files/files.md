# Files — MAIL-11

## Change surface

| Path | Purpose / risk |
| --- | --- |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Core-owned contract/policy; reuse existing vocabulary and avoid a second business implementation |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Persistence or external adapter boundary; preserve mailbox scope, idempotency and durable history |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | Real staff or Automation caller; thin orchestration only |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Focused acceptance and regression evidence |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Governing behaviour; modify only after explicit answers where the behaviour is unresolved |
| `docs/capabilities.md` | Update evidence/status only after delivery |

## Context files

| Path | What it establishes |
| --- | --- |
| `docs/design/README.md` | Accessible interaction and confirmation conventions |
| `docs/open-decisions.md` | Inactive predicates, confidence/holdout and live activation boundaries |
| EPIC-006 `context.md` | One Core owner and no local-alpha mailbox mutation |
| `src/Pegasus.Web/Program.cs` | Existing composition and feature-gate conventions |

## Out of scope

No new taxonomy, speculative abstraction, bulk action, arbitrary client-supplied destination, real mailbox/cloud write, deployment claim or duplicated UI/MCP policy.

# File-map refresh — 2026-08-20

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Extend the existing workspace request/result and query port for search/match disclosure while preserving authorization, paging and current Web/MCP callers. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Existing SQL-paged list/detail/thread owner; add scoped search and match projection without client-side materialization or cross-mailbox/folder joins. |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` and `MailboxModelConfiguration.cs` | Only if the selected single search projection needs persisted attachment text/searchability state; preserve the existing retained-message/attachment aggregate. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/` | A schema-backed projection requires one committed migration, designer and model-snapshot update plus review of Web/Worker grants; no parallel migration stream. |
| `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` and existing approved-mailbox composition | Supply the bounded read-only Deleted Items source/projection; reuse exact approved mailbox identity and do not broaden Graph permissions. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml(.cs)` | Add accessible search/filter/match UI and preserve explicit query-string scope/page/refresh behavior. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml(.cs)` | Preserve search/filter/page return context and the explicit no-longer-in-view state on detail. |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` | Contract validation, authorization, scope and match-shape policy. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | SQL paging/search, body/filename/content matches, unsupported content, folder/mailbox isolation and thread isolation. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Real authenticated Web caller, visible match locations, state preservation, empty/unavailable behavior and accessibility-oriented markup. |
| `docs/capabilities.md` and current-state docs | Update only to the evidence tier actually delivered; FRD-08 already owns the required behavior and needs no speculative rewrite. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | Inbox retention is inserted after accepted intake and before cursor advance; only Inbox currently creates retained rows. |
| `src/Pegasus.Infrastructure/Intake/LocalEmailDisplayReader.cs` | Existing display parser returns body and attachment metadata, not searchable attachment text. |
| `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` | Existing canonical multi-format reader already produces source-labelled content fragments and unsupported issues; reuse its output rather than introduce another parser. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` and `EfIntakeReceiptStore.cs` | Content fragments are processed downstream, but full attachment text is not retained in receipt evidence; this is the seam for one projection, not an alternate search store. |
| `src/Pegasus.Web/Mcp/MailMcpTools.cs` | Merged dev caller of the same list/detail use cases; must continue to compile, while AUTO-003 owns later Automation search exposure. |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | Existing query-string-only scope, page-size, folder parsing and freshness helpers are the convention to extend. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Settled search inputs/results, explicit unsupported disclosure, scoped thread behavior and no-backfill rule. |
| `docs/design/README.md` | Workspace filter preservation, accessible pagination, quick-preview and detail interaction requirements. |
| EPIC-006 `context.md` | One Core implementation; local-alpha work never mutates Outlook. |

## Ripple effects and exact overlaps

- **TICK-064 / MAIL-23:** overlaps `EfRetainedMailboxMessageStore.cs`, `Index.cshtml.cs`, FRD/capabilities and mail tests. Programme order says TICK-064 first; refresh after it lands.
- **TICK-056 / UI-10 and TICK-057 / UI-14:** exact overlap in `RetainedMail.cs`, the EF store, `Index.cshtml.cs` and `MailWorkspaceWebTests.cs`; do not execute concurrently. Both consume MAIL-11's final filter/result shape.
- **TICK-050 / MAIL-08:** overlaps `RetainedMail.cs`, EF store, message detail and Core retained-mail tests; run after the read shape stabilizes.
- **TICK-049 / MAIL-07 and TICK-054 / MAIL-13:** overlap `RetainedMail.cs`, `Message.cshtml.cs`, and Graph mail adapter work; action lanes should follow rather than race the read-only scope work.
- **TICK-051 / MAIL-09, TICK-052 / MAIL-10, TICK-047 / MAIL-05 and TICK-088 / MAIL-12:** overlap message detail (and TICK-088/047 touch adjacent Graph/mailbox identity boundaries); they are not Core search prerequisites but need sequencing for conflict-free delivery.
- **TICK-062:** already supplied the current thin MCP browse/detail caller on dev. **AUTO-003** is the downstream owner for exposing landed MAIL-11 search capabilities to Automation.

## Out of scope

No new taxonomy, queue mapping, Case-association policy, folder recommendation/move, message mutation, compose/send behavior, generic search service, separate search database, historical-mail reconstruction, arbitrary Graph mailbox/folder access, deployment claim or live mailbox write. Queue refinements belong to MAIL-23/UI-14; Automation exposure belongs to AUTO-003.

## Implemented surface — 2026-08-20

The conditional entity/configuration path resolved into the repository's actual owners: `PegasusDbContext.cs` owns the entity/model configuration and `EfIntakeReceiptStore.cs` owns atomic writes. Added/changed files beyond the refreshed map are:

- `src/Pegasus.Core/Intake/IntakeContracts.cs`, `IntakeSearchProjection.cs`, `ProcessIntake.cs`, and `DeletedMailSearch.cs`: canonical reader-result projection and bounded authorized Deleted Items port/use case.
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs`: exposes attachment descriptors from the existing parse.
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, `EfIntakeReceiptStore.cs`, and migration `20260820100724_RetainedMailSearchDocuments` plus designer/snapshot: single receipt-owned projection and least-privilege grants.
- `src/Pegasus.Infrastructure/DependencyInjection.cs`: unavailable default and production Graph source composition.
- `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` and `IntakePersistenceIntegrationTests.cs`: exact GET-only Graph and committed migration evidence.
- `docs/current-architecture.md`: exact local as-built shape; `docs/operations.md` remains unchanged because nothing was deployed.
