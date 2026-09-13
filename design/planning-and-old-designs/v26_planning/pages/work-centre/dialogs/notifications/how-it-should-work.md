# Notifications — how it should work

Decided 13 September 2026 (Work Centre D10).

**The bell is personal notifications and nothing else.** It never shows office-wide work, attention rows, counts of queues or anything not addressed to the signed-in person. The Work Centre owns office-wide work; the bell owns "things that happened that concern you". With no notifications the bell shows nothing and the dialog says so.

## What raises one

| Cause | Who is notified | Opens |
| --- | --- | --- |
| AI draft ready on a Case (Estimate, Query response, Unidentified resolution the Case is party to) | the Case's engineer; otherwise the person who started the job | the Case at the Estimate section, the message, or the Unidentified item, per kind |
| Case assigned to an engineer | that engineer | the Case |
| A Case the engineer is assigned to is edited by someone else, receives an e-mail, or a query arrives | that engineer | the Case at Notes, Files or the message |

Nothing else raises a notification. Market research does not (its files simply appear in Files).

## The dialog

- A list, newest first: the Case reference and registration, the cause in operator words ("Estimate draft ready", "Assigned to you", "E-mail received", "Edited by E Mawdsley", "Query received"), when, and a chip Unread until opened.
- Opening a row marks it read and goes to the place named above. "Mark all read" at the head. Nothing is deleted; notifications older than 30 days drop off.
- The bell shows the unread count.
- The same events feed the person's Mine view on the Work Centre (P2), so both agree.

## Open

- Whether a User (non-engineer) receives cause 3 for Cases they created.
- Whether e-mail (Outlook) notification is ever wanted in addition to the bell. Default: no.
