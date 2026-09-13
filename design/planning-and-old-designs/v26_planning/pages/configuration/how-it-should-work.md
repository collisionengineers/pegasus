# Configuration — how it should work (workflow settings)

Decided with the operator on 13 September 2026 while planning the Work Centre ([`../work-centre/how-it-should-work.md`](../work-centre/how-it-should-work.md) D3).

## New workflow settings: due targets

The Work Centre gives every kind of work a due instant. The targets that produce those instants are workflow settings on this page, beside the chase interval, edited under the same versioned lease. The defaults are the standard; the office changes them only when it has a reason.

| Setting | Meaning | Range | Default |
| --- | --- | --- | --- |
| Chase interval (days) | existing | 1 to 365 | 7 |
| Unidentified target (days) | an Unidentified item is due this many days after it was received | 0 to 365 | 0 (due by midnight on the day received) |
| Triage target (days) | a Triage record without a finding is due this many days after it opened | 0 to 365 | 1 |
| Held decision target (days) | a held Case is due this many days after the hold was placed, unless the hold carries its own Review on date | 0 to 365 | 7 |
| Review target (days) | a Case in Review, with or without an engineer, is due this many days after it entered Review | 0 to 365 | 1 |
| AI draft target (days) | an AI job in Draft ready is due this many days after the draft was written (Work Centre D9) | 0 to 365 | 1 |

Fixed by design, not settings:

- All values are **calendar days**. Working days are not modelled anywhere.
- The day boundary is **midnight Europe/London**. A target of 0 means due by the next midnight after the event.
- A change applies from the next Work Centre read; it does not rewrite recorded dates.

## Read view and edit form

- Read view: six definition rows, "Chase interval · 7 days", "Unidentified target · 0 days" and so on.
- Edit form: six number inputs with the ranges above, one Save. Validation messages name the range. Same lease, version and Take over behaviour as the existing form.
- Action log: one entry per saved version listing the changed values, as now.
