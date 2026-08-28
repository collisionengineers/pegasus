# Files — MAIL-025

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | `MailWorkspaceScope` gains `UnreadOnly`, `OldestFirst`; `RetainedMailSummary` gains `AttachmentFileNames`; `IRetainedMailQueries.CountAsync`; `ListRetainedMail.CountAsync` + shared scope validation |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | filter extracted to `Scope(...)`; `CountAsync`; unread filter; sort direction; attachment names on the summary row (outside Owns — Core port needs its adapter; reported) |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml` | rewritten: page header + freshness, filter bar, three panes, scope list, rows with templates, preview pane, pagination |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` | scope counts, `unread`, `sort`, `selected`, preview view-model, scope list; JSON preview handler deleted |
| `src/Pegasus.Web/Pages/Mail/_Preview.cshtml` | new partial: the preview pane body (rendered server-side and inside each row's `<template>`) |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | rewritten: page header, record head, tablist (`?tab=`), decision card, corrections timeline, attachments table with Preview, thread rows, case tab, `[data-dialog]` dialogs |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | `section` → `tab`; attachment preview href resolution; unchanged handlers |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` | fake gains `CountAsync` (outside Owns; compile-only, reported) |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | retargeted pins (preview, tabs, decision rows, labels) |
| `tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs` | retargeted to `[data-select-href]` preview |
| `docs/design/test-ui/catalogue.json` | branch text for `/Inbox` states and `/Inbox/{id}` |

Not touched: `site.css`, `site.js`, `Pages/Shared/**`, `Pages/Cases/**`,
`Presentation/OperatorLabels.cs`.
