Read from the live source on 18 September 2026.

## What this page does not show

- No help text on any of the six day-count fields beyond the min–max hint
  span next to each input; no worked example of what a target day counts
  from.
- The rate-card add row only appears while editing and only when the
  workflow settings themselves are not the thing being edited
  (`Model.IsEditing && !workflowEditing`) — the two editors share one Save
  button and cannot be open at once.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/Configuration.cshtml` | Case workflow panel, Labour-rate cards panel, the single edit form |
| `Pages/Administration/Configuration.cshtml.cs` | `WorkflowSettings` (the six settings and their ranges), `RateCards`, `Edit`/`Save`/`Cancel`/`NewCard` handlers |

## Behaviours

### Case workflow panel

Read-only: Instructions/Images ("Required"/"Not required") plus six day
settings in the planned order — Chase interval, Unidentified target, Triage
target, Held decision target, Review target, AI draft target
(`ConfigurationModel.WorkflowSettings`, "Configuration, 13 September"
comment) — each shown as `ConfigurationModel.Days(n)` ("1 day" / "N days").
Editing swaps the same six into number inputs with `min`/`max` from Core
policy shown as a hint, plus the two Required checkboxes.

### Labour-rate cards panel

Name, Panel and paint (£/hour), Enabled, and an Edit link per card; an "Add
rate card" button posts a blank editable row into the same table (not a
separate dialog) when nothing else is being edited.

### One edit scope, two panels

Both panels share one bottom form (`id="configuration-edit"`) and one
Cancel/Save pair — editing the workflow settings and editing a rate card are
the same edit scope, never simultaneous, which is why only one of the two
panel bodies shows its editable fieldset at a time.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
