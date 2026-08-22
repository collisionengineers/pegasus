# Files

Committed in `5414997d`.

| File | Change | Reuses |
| --- | --- | --- |
| `src/Pegasus.Core/Cases/CaseNotes.cs` (new) | `AddCaseNoteRequest`, `IAddCaseNote`, `ICaseNoteStore`, `AddCaseNote` — validation, staff-only rule, 2000-character bound | `StaffAuthorization` |
| `src/Pegasus.Infrastructure/Persistence/EfCaseNoteStore.cs` (new) | Writes a `CaseHistoryEntity` with event type `operator_note`, idempotent by operation key | the existing history table |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Registers both | — |
| `src/Pegasus.Web/Pages/Cases/Tasks.cshtml.cs` | `OnPostAddNoteAsync` — no lease, no expected version | the page's actor and redirect helpers |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml` | Heading becomes **Notes**, the last column becomes Detail, and the add-note form joins the panel | the existing table and actor rendering |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | Tab reads **Notes** | — |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | `operator_note` → "Note" | the one history-event label table |
| `tests/Pegasus.Core.Tests/Cases/AddCaseNoteTests.cs` (new) | Trimming, empty refused, overlong refused, automation refused | — |

## No new table and no migration

A note is a history row. That was the design decision, not a shortcut: it inherits the
timeline's ordering, attribution and append-only guarantee, and there is no second store to
keep in step with the first.
