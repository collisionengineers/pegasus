# Files — MAIL-007

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/StaffForwardBodyCleaner.cs` | `TrimProviderFooter(string)` — earliest-marker cut with the fail-open rules; markers as generated regexes beside the existing ones. The one owner of body-display policy extends; no second cleaner |
| `src/Pegasus.Web/Presentation/MailBodyPresentation.cs` | `Present` trims the footer after splitting the quoted header |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | The list excerpt derives from the trimmed body (both the search-text path and the fallback) |
| `tests/Pegasus.Core.Tests/Intake/StaffForwardBodyCleanerTests.cs` | Facts over real corpus body shapes: letter + signature footer trimmed at the boundary keeping the sign-off; signature-only body unchanged; markerless body unchanged; no instruction line lost |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` / `RetainedMailPersistenceTests.cs` | Excerpt/body expectations where fixtures carry footers (none currently do — verify, adjust only if needed) |

Search documents, classification inputs, and the retained `BodyPlainText`
are not touched.
