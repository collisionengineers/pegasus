# Files

Committed in `43488ea9`.

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` | `DataRow("Odometer", …)` → `DataRow("Mileage", …)` |

One line. The sweep the ticket asked for is the rest of the work, and it is recorded in the
plan rather than in a diff.

## Untouched, with reasons

**"Odometer reading"** on `Cases/Assessment/Suggestions.cshtml` and in
`AssessmentPolicy.cs`. That is the engineer's recorded reading during assessment — a
different fact from the vehicle mileage extracted from documents — and its label is owned
by Core.

**"Make" / "Model" vs "Vehicle make" / "Vehicle model."** Found by the sweep, left alone
deliberately: they read as obviously the same field, whereas "Odometer" and "Mileage" read
as two.
