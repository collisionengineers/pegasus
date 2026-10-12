# Next action and ribbon gates: states

Captured in the mockup:

| `?state=` | What it shows | Capture |
| --- | --- | --- |
| `next` | Report sent recorded with open tasks: the Next action's Mark completed greyed with its hover reason, the Tasks list below it, and the Actions menu's Mark completed greyed | `current/captured/blocker-next-action.html` |
| `archive` | A closed Case with an open task: Archive greyed in the Actions menu | `current/captured/blocker-archive.html` |

Reachable in the source but not captured:

- Report sent recorded, no open task: Mark completed live in the Next action and the menu; no Tasks list.
- An Inspection + Audit Case with no Audit yet after the send: the step is Create audit, not Mark completed, and the Tasks list still shows below it.
- Before Report sent: no Tasks list, whatever tasks the Case holds.
