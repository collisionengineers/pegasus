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
| `Pages/Administration/ValuationPresets/Index.cshtml` | The one table: every stored row already editable, plus the add row |
| `Pages/Administration/ValuationPresets/Index.cshtml.cs` | `Presets`, `ValuationPresetLabels`, `OnPostCreateAsync`/`OnPostSaveAsync`/`OnPostRemoveAsync` handlers |
| `Core/Assessment/ValuationCalculationPolicy` | `FormatMoney()`, the label length limit |

## Behaviours

### Table

Every row is already editable, no Edit/Take-over toggle: a Label input, an
Amount input and an Enabled checkbox, each row posting through its own
Save/Reset form (`Save` handler, keyed by that row's own hidden
`presetId`/`expectedVersion`) plus its own Remove form, which posts
immediately with no confirmation dialog. The add row at the foot is always
present and always editable in the same way — it has no separate "closed"
state.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
