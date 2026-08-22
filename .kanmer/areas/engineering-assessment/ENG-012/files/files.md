# Files

Committed in `ca564ac5`.

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` | The MOT history table is removed; the observation panel drops "Latest lookup outcome", "Provider" and the provider version; the mileage row drops its "latest MOT observation" narration and keeps the observation date |

## Kept, deliberately

The lookups still run. The observation's registration, retrieval time, make, model,
manufacture year, engine capacity, fuel type and mileage stay, with the mileage's evidence
classification, because those are what the lookups are *for* — filling gaps the instruction
and the report leave.

The accept and correct forms stay: they are how an operator promotes a looked-up value to a
confirmed one, which is the gap-filling mechanism itself.

Nothing changes about collection. [[ENG-010]] proved MOT tests were being silently
discarded and the derived mileage depends on them; this ticket is about what is displayed.
