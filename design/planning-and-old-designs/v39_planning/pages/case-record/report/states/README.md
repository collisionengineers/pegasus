# Report delivery: states

Captured in the mockup:

| `?state=` | What it shows | Capture |
| --- | --- | --- |
| `plain` | A Principal with the default rules: nothing is asked, nothing holds or stops | `current/captured/delivery-plain.html` |
| `rules` | Rules with undecided conditions: the questions, the override box for a possible Stop, a hold, the After sending list | `current/captured/delivery-rules.html` |
| `stop` | A rule's Stop applies: the Stopped notice and a required Override reason | `current/captured/delivery-stop.html` |
| `missing` | A companion the Principal requires is not generated: its notice, and no Send report | `current/captured/delivery-missing-companion.html` |

Reachable in the source but not captured:

- No form at all: outside the edit session, before a Confirmed generation with a confirmed report, or with no report sending facts (`canSendReport`, `plan is null`).
- A report already sent for this work: the "Already sent on … to …." line heads the form.
- A Case with no instruction e-mail in a mailbox: the From line reads "New message from {address} (no instruction e-mail on this Case)".
- No filed estimate where the Principal attaches one, and no separate fee note where the Principal expects one: the two warning lines.
