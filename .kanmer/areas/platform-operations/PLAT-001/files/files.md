# Files — Claude Design UI implementation

Every path is under `src/Pegasus.Web/` unless stated. Mapping is taken from the
design project's own `github.md` screen map and verified against the tree.

## Shell — changed once, affects every authenticated page

| File | Change |
| --- | --- |
| `Pages/Shared/_Layout.cshtml` | Replace `header.app-nav` + `.nav-inner` + `.nav-links` with the left rail: brand lockup, seven `.rail-link` routes with counts, identity/change-password/sign-out block at the bottom. Keep the skip link, the `_LucideSprite` partial, the `TempData["Confirmation"]` status card, the `inboxEnabled` composition gate and the `CurrentWhen` helper verbatim. |
| `wwwroot/css/site.css` | Add the `.app-rail` block (rail grid, `.rail-link`, `aria-current` treatment, count badge, responsive collapse). The **only** stylesheet change in the ticket — everything else already matches the design system byte for byte. |
| `Pages/Shared/_LayoutAuth.cshtml` | Verify only. `screens/ChangePassword.html` uses `AuthShell`/`AuthCard`, which this already emits. |
| `Pages/Shared/_LayoutExternal.cshtml` | Verify only. `screens/UploadLink.html` uses `AppShell` + `AppNav brandOnly`, which this already emits — the external screen keeps the top bar deliberately. |

## Screens

| Design screen | Repo file(s) | Nature of change |
| --- | --- | --- |
| `Dashboard.html` | `Pages/Index.cshtml` | `PageHeading` + `Refresh`; three `MetricStrip`s (active cases / e-mail activity / today-and-week); `QueueList` for case work due; Operations link. |
| `Inbox.html` | `Pages/Mail/Index.cshtml` | `Subtabs` folder row with mailbox `Select` in `end`; `DataTable` with grouped From and Subject cells; outcome chip + case link; `Pager`; `EmptyState` for Sent/Deleted. |
| `InboxMessage.html` | `Pages/Mail/Message.cshtml` | `BackLink`; `PageHeading` with an `actions` button; state `Panel` of three `EvidenceFigure`s; `Tabs` Message / Attachments / Thread. |
| `Upload.html` | `Pages/Upload.cshtml` | Centred 680px `Panel`, `.drop` dropzone, full-width `PrimaryAction`. |
| `UploadLink.html` | `Pages/Uploads/Request.cshtml` | `Eyebrow` + `PageHeading`, dropzone `Panel`, accepted-so-far `DataTable`, quota line. External layout, unchanged shell. |
| `Queues.html` | `Pages/Triage/Index.cshtml` | `Tabs` for stages with counts; `Subtabs` for what a Not-ready case waits on; `DataTable`; `Pager`; `EmptyState`. |
| `Cases.html` | `Pages/Cases/Index.cshtml` | `FilterBar` with a `more` `FormGrid`; results `DataTable`; `Pager`. |
| — | `Pages/Search/Index.cshtml` | Same backing query per the design authority; align its results table with `Cases.html` so the two do not drift. |
| `Case.html` | `Pages/Cases/Details.cshtml` + `Pages/Cases/Shared/_CaseSummary.cshtml`, `_CaseDocuments.cshtml`, `_CaseHistory.cshtml`, `_CaseWorkflow.cshtml` | `Crumb` → `Record` → `RecordHead` / `RecordBar` / `Tabs` / `RecordBody`; overview `.block` grid of `DataRow` + `Provenance`; Inspection, Evidence (`Subtabs` photos/documents/e-mails), Notes & history. |
| `Assessment.html` | `Pages/Cases/Assessment/Index.cshtml` | `BackLink`; `Record` with `RecordHead`/`RecordBar`; `Tabs` Details / Assessment / Evidence. Bound parts stay bound; deferred parts are unbound markup (see below). |
| `CreateCase.html` | `Pages/Cases/Create.cshtml` | H1 becomes the filename and the lede goes; `StatusCard` "File read"; `DataRow`+`Provenance` details; `Notice` for image-based assessment; `FormGrid`; completeness `ChoiceGroup` folded into the Case panel; `ButtonRow`. |
| `Operations.html` | `Pages/Operations/Index.cshtml` | Three labelled sections: retryable work `DataTable`, active upload links with `RowConfirm` withdraw, AI operations `DetailList` panels. |
| `Administration.html` | `Pages/Administration/Index.cshtml` | `AdminWorkspaces` grid of eight `AdminCard`s. |
| `AdminAccounts.html` | `Pages/Administration/Accounts/Index.cshtml` | `BackLink` + `Eyebrow` + `PageHeading`; `SplitMain` — accounts `DataTable` beside a `FormPanel`. |
| `AdminRoles.html` | `Pages/Administration/Roles/Index.cshtml` | `Notice`; role `DataTable`; inline role editor with `ChoiceGroup` and a `Gated` last-administrator checkbox. |
| `AdminAccess.html` | `Pages/Administration/Access/Index.cshtml` | `Notice`; access `DataTable` with a `RowConfirm` "Record reviewed". |
| `AdminOrganizations.html` | `Pages/Administration/Organizations/Index.cshtml` | `SplitMain`; organisations `DataTable` + `Pager`; create `FormPanel` with a stacked `ChoiceGroup`. |
| `AdminPrincipals.html` | `Pages/Administration/Principals/Index.cshtml` | `PageHeading` with a `PrimaryAction`; `Notice`; principals `DataTable` with `RowConfirm` replace; `Pager`. |
| `AdminConfiguration.html` | `Pages/Administration/Configuration.cshtml` | `SplitMain`; current-configuration `Panel`/`DetailList` with `Required` chips; update `FormPanel`. |
| `AdminMailboxes.html` | `Pages/Administration/Mailboxes.cshtml` | `Notice`; policies `DataTable` with an inline update editor; `SplitMain` of two `StatusCard`s beside the add `FormPanel`. |
| `AdminAutomation.html` | `Pages/Administration/Automation/Index.cshtml` + `Activity.cshtml` | Two registration/Send-to-AI `Panel`s with `RowConfirm`; correlation filter; activity `DataTable` + `Pager`. |
| `ChangePassword.html` | `Pages/Account/PasswordChange.cshtml` | `AuthCard` with three `Field`s and `AuthCardActions`. Navless layout, unchanged. |

## Unbound-markup sections (operator decision, 2026-08-17)

Rendered as static design markup with no page-model binding and no POST handler,
following the precedent already recorded in
`Pages/Cases/Assessment/Index.cshtml.cs`. Each carries an HTML comment naming its
capability ID and allocation so it cannot be mistaken for working behaviour.

| Section | File | Capability |
| --- | --- | --- |
| Estimate tabs, estimate lines, cost breakdown, confirm figures | `Pages/Cases/Assessment/Index.cshtml` | `EXT-09` Later/1.0.0 |
| CAP HPI figure beside the existing Glass's/Cazana evidence | `Pages/Cases/Assessment/Index.cshtml` | `EXT-13` Later/1.0.0 |
| Engineer's-value / retail / trade valuation inputs | `Pages/Cases/Assessment/Index.cshtml` | `EXT-10` Later/1.0.0 |
| Open in Glass's, Open in Audatex, Import assessment PDF | `Pages/Cases/Assessment/Index.cshtml` | `EXT-12` Later/1.0.0 |
| Generate-report dialog: damage marking, report-image roles and order | `Pages/Cases/Assessment/Index.cshtml` | `UI-15` Later/1.0.0 |
| Experian AutoCheck check and result block | `Pages/Cases/Details.cshtml`, `Pages/Cases/Assessment/Index.cshtml` | unallocated |
| Look up vehicle | `Pages/Cases/Details.cshtml` | unallocated |
| Engineer queries panel | `Pages/Cases/Details.cshtml` | unallocated |
| Upload-link copy dialog | `Pages/Cases/Details.cshtml` | unallocated |

## Documentation to refresh in the same task

| File | Change |
| --- | --- |
| `docs/design/README.md` | Record the shell change (top bar → left rail) and how `aria-current` now reads without colour; record the Lucide-over-PNG-marks divergence; state that the unbound sections carry capability IDs and prove nothing. |

## Deliberately not touched

`Pages/Intake/*`, `Pages/ImageIntake/*`, `Pages/Search/Index.cshtml` beyond table
alignment, `Pages/UploadStatus.cshtml`, `Pages/Error.cshtml`,
`Pages/StatusCode.cshtml`, `Pages/Account/SignIn|SignOut|AccessDenied.cshtml`,
`Pages/Cases/Documents/*`, `Pages/Administration/**/Edit|Create|Replace.cshtml`,
`Pages/Cases/Assessment/Suggestions.cshtml`, `Pages/Triage/Details.cshtml` — no
design screen covers them. Everything under `src/Pegasus.Core`,
`src/Pegasus.Infrastructure`, `workspaces/` and `corpus/` is out of scope.
