# v26 planning — pages

One folder per page of the application as mocked in `../current/`. Each page folder holds `dialogs/`, `states/` and `panels/` for the pieces that belong to it. Drop notes, sketches and decisions for a page into its folder.

| Page | Mockup route | Live source |
| --- | --- | --- |
| [Sign in](sign-in/README.md) | `pegasus_shell_v26.html#/signin` | `src/Pegasus.Web/Pages/Account/Login.cshtml` |
| [Connector consent](connector-consent/README.md) | `pegasus_shell_v26.html#/connect/authorize` | `src/Pegasus.Web/Pages/Connect/Authorize.cshtml` |
| [Secure file request](public-upload/README.md) | `pegasus_shell_v26.html#/uploads/{token}` | `src/Pegasus.Web/Pages/Uploads` |
| [Work Centre](work-centre/README.md) | `pegasus_shell_v26.html#/` | `src/Pegasus.Web/Pages/Index.cshtml` |
| [Inbox](inbox/README.md) | `pegasus_shell_v26.html#/inbox` | `src/Pegasus.Web/Pages/Mail/Index.cshtml` |
| [Message](message/README.md) | `pegasus_shell_v26.html#/inbox/{id}` | `src/Pegasus.Web/Pages/Mail/Message.cshtml` |
| [Upload](upload/README.md) | `pegasus_shell_v26.html#/upload` | `src/Pegasus.Web/Pages/Upload` |
| [Cases](cases/README.md) | `pegasus_shell_v26.html#/cases` | `src/Pegasus.Web/Pages/Cases/Index.cshtml` |
| [Create case](create-case/README.md) | `pegasus_shell_v26.html#/cases/new` | `src/Pegasus.Web/Pages/Cases/Create.cshtml` |
| [Case record](case-record/README.md) | `pegasus_case_workspace_v26.html` | `src/Pegasus.Web/Pages/Cases/Details.cshtml + Shared/_Case*.cshtml` |
| [AI suggestions review](ai-suggestions/README.md) | `pegasus_shell_v26.html#/cases/{id}/suggestions` | `src/Pegasus.Web/Pages/Cases/Assessment/Suggestions.cshtml` |
| [Triage](triage/README.md) | `pegasus_shell_v26.html#/triage/{id}` | `src/Pegasus.Web/Pages/Triage/Details.cshtml` |
| [Unidentified](unidentified/README.md) | `pegasus_shell_v26.html#/unidentified/{id}` | `src/Pegasus.Web/Pages/Unidentified/Details.cshtml` |
| [Image-initiated Case](image-initiated-case/README.md) | `pegasus_shell_v26.html#/images/{id}` | `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml` |
| [Received file](received-file/README.md) | `pegasus_shell_v26.html#/intake/{id}` | `src/Pegasus.Web/Pages/Intake/Details.cshtml` |
| [Search](search/README.md) | `pegasus_shell_v26.html#/search` | `src/Pegasus.Web/Pages/Search/Index.cshtml` |
| [Operations](operations/README.md) | `pegasus_shell_v26.html#/operations` | `src/Pegasus.Web/Pages/Operations` |
| [Administration hub](administration/README.md) | `pegasus_shell_v26.html#/admin` | `src/Pegasus.Web/Pages/Administration` |
| [Staff accounts](staff-accounts/README.md) | `pegasus_shell_v26.html#/admin/accounts` | `src/Pegasus.Web/Pages/Administration/Accounts` |
| [Contacts](contacts/README.md) | `pegasus_shell_v26.html#/admin/contacts` | `src/Pegasus.Web/Pages/Administration/Principals + Contacts` |
| [Configuration](configuration/README.md) | `pegasus_shell_v26.html#/admin/configuration` | `src/Pegasus.Web/Pages/Administration (rate cards, mailboxes, categories)` |
| [Mail](mail-settings/README.md) | `pegasus_shell_v26.html#/admin/mail` | `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml` |
| [Valuation presets](valuation-presets/README.md) | `pegasus_shell_v26.html#/admin/valuation-presets` | `src/Pegasus.Web/Pages/Administration/ValuationPresets` |
| [Service health](service-health/README.md) | `pegasus_shell_v26.html#/admin/health` | `src/Pegasus.Web/Pages/Administration/Health` |
| [Action logs](action-logs/README.md) | `pegasus_shell_v26.html#/admin/action-logs` | `src/Pegasus.Web/Pages/Administration/ActionLogs` |
| [Reports](reports/README.md) | `pegasus_shell_v26.html#/admin/reports` | `src/Pegasus.Web/Pages/Administration/Reports` |
| [AI jobs](ai-jobs/README.md) | `pegasus_shell_v26.html#/admin/ai-jobs` | `src/Pegasus.Web/Pages/Administration/AiJobs` |
| [Automation](automation/README.md) | `pegasus_shell_v26.html#/admin/automation` | `src/Pegasus.Web/Pages/Administration/Automation` |
| [Glass's credential](glass-credential/README.md) | `pegasus_shell_v26.html#/admin/accounts (Manage login)` | `src/Pegasus.Web/Pages/Administration/Glass/Index.cshtml` |
