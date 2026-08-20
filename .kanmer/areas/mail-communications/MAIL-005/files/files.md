# Files — MAIL-005

| File | Change |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Case resolution: link first, else the allocation state's case (already loaded — no new query) |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml` | Outcome cell: chip + case link on one row |
| `src/Pegasus.Web/wwwroot/css/site.css` | Row alignment for the outcome cell |
| `tests/Pegasus.IntegrationTests/` (retained-mail web test file) | New: succeeded-attempt-without-link renders "Case created" + reference |

All premises verified live read-only (ticket body). No migration.
