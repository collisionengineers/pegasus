Read from the live source on 18 September 2026.

## What this page does not show

- No separate "Add valuation preset" dialog — the create row's inputs live
  in the table itself and post through a hidden `form="preset-create"`, "a
  compact row rather than a separate creation panel, per the operator's
  request" (source comment).
- Remove posts straight from its row with no confirmation dialog; only
  Delete on other areas (e.g. Accounts) confirms first.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/ValuationPresets/Index.cshtml` | The one table: normal rows, one editable row, the add row |
| `Pages/Administration/ValuationPresets/Index.cshtml.cs` | `Presets`, `ValuationPresetLabels`, `Create`/`Save`/`CancelEdit`/`Remove` handlers |
| `Core/Assessment/ValuationCalculationPolicy` | `FormatMoney()`, the label length limit |

## Behaviours

### Table

Label, Amount, State (chip, Enabled/Disabled), and row actions. A row not
currently being edited shows Edit and, only for a taken-over edit, Take
over, plus Remove. The one row currently being edited (`editPresetId` in the
query string) instead shows its Label/Amount/Enabled inputs inline with
Cancel/Save. The add row at the foot is always present and always editable
— it has no separate "closed" state.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
