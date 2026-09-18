Read from the live source on 18 September 2026.

## What this page does not show

- All three sections (Automation panel, AI settings panel, unavailable
  message) are mutually exclusive on composition state; the live page never
  shows more than the ones that apply (`registration is not null`, `connector
  is not null`, `!Model.IsComposed`).
- No client secret or channel token value is ever shown — only whether one
  is held ("Entered from Administration" / "Standard setting") and when it
  last changed.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/Automation/Index.cshtml` | Automation panel, AI settings panel, unavailable panel |
| `Pages/Administration/Automation/Index.cshtml.cs` | `Status`, `ConnectorSettings`, `SetEnabled`/`SaveAiSettings`/`ClearChannelToken` handlers |
| `Presentation/OperatorLabels.cs` (`AutomationAdmin` class) | Every label, the kill-switch consequence sentence, `AutomationScope()` |
| `Shared/_AdminNav.cshtml` | The composition gate this area's own nav entry (and the hub's card) shares |

## Behaviours

### Automation panel

A fact grid (Registered clients, Client identifier, Granted scopes — comma
joined via `AutomationScope()`, Active jobs, Failed jobs) plus a single
Stop/Start automation button. Only the Stop direction (an enabled switch)
carries danger styling and the one approved consequence sentence, "In-flight
work remains visible and no result is discarded." — Start carries neither,
since starting has no destructive consequence to state.

### AI settings panel

Channel token state and last-changed date (definition list, never the token
itself), then a form: Channel address, Timeout in seconds, New channel
token (password input, `autocomplete="new-password"`), a "Reviewed AI
proposals enabled" checkbox, Remove the channel token (danger, only when one
is held) and Save AI settings.

### Composition gate

Both panels render only when their respective model is non-null
(`registration`/`connector`); when `!Model.IsComposed`, a single
`unavailable-panel` replaces both with "This deployment does not have
Automation or AI configuration available." — the panel itself still renders
(it is not simply blank), unlike the nav entry and hub card, which are
absent outright.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
