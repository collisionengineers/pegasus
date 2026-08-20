# Files — CASE-003

Written retrospectively at closeout ([[DELIV-012]]): the fix shipped inside
[[INTK-010]]'s PR #433 (release 13), where the upload confirmation step made
`/Cases/Create?receiptId=` a live path and this 500 blocked it.

| File | Change |
|---|---|
| `src/Pegasus.Web/Pages/Cases/Create.cshtml.cs` | `OnGetAsync` guards `receiptId == Guid.Empty` and returns `NotFound()` before `LoadAsync` runs — the exact approach this ticket specified |
| `tests/Pegasus.IntegrationTests/CaseCreateWebTests.cs` | Test added: empty-receipt request → 404, not 500 |
