Read from the live source on 18 September 2026.

## What this page does not show

- No help text on any of the six day-count fields beyond the min–max hint
  span next to each input; no worked example of what a target day counts
  from.
- A rate-card row shows its posted (not saved) values only when that row's
  own save just failed (`Model.CardPostFailed && Model.CardId == card.Id`);
  every other row keeps showing the stored record.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/Configuration.cshtml` | Case workflow panel, Labour-rate cards panel, the workflow form and each rate-card row's own form |
| `Pages/Administration/Configuration.cshtml.cs` | `WorkflowSettings` (the six settings and their ranges), `RateCards`, `OnPostSaveWorkflowAsync`/`OnPostNewCardAsync`/`OnPostSaveCardAsync` handlers |

## Behaviours

### Case workflow panel

Always editable, no read-only state: the two Required checkboxes
(Instructions/Images) plus six number inputs in the planned order — Chase
interval, Unidentified target, Triage target, Held decision target, Review
target, AI draft target (`ConfigurationModel.WorkflowSettings`, "Configuration,
13 September" comment) — each with `min`/`max` from Core policy shown as a
hint span, posting through their own "Save workflow settings" form.

### Labour-rate cards panel

Every existing card is already an editable row — Name, Panel and paint
(£/hour) and an Enabled Yes/No select, each with its own Reset/Save form
(`SaveCard` handler, keyed by the row's own hidden `CardId`). An "Add rate
card" button (`NewCard` handler) appends one further blank editable row at
the foot of the same table, with its own Reset/Save/Cancel; no separate
dialog and no gating on any other row being edited.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
