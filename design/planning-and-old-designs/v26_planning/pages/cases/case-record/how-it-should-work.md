# Case record — how it should work

Decisions that land on the Case record from other pages' planning. The Case record's own planning is still to come; this file collects what is already decided.

| From | Decision | Where on this page |
| --- | --- | --- |
| Work Centre D5 | Place on Hold gains an optional Review on date | [`dialogs/hold-release/how-it-should-work.md`](dialogs/hold-release/how-it-should-work.md) |
| Work Centre D1 | a blocking external failure reads in operator words at the point of use: Vehicle "Lookup failed", Files "Storage not ready" | Vehicle and Files sections |
| Work Centre P4 | the Work Centre's Assign Engineer action opens this Case's assignment dialog directly, not the Case page; Review Case opens the Case at its Review decision | assignment dialog; Review decision |
| Work Centre P8 | "Assign to me" for an Engineer on a ready Case with no engineer, callable from the Work Centre's Today pane | assignment dialog |
| Received file D2 | Add evidence on Files is where a message or file is attached to this Case, including the upload confirmation's attach-with-override; Remove with reason is the reversal | Files |
| Work Centre D9 | an AI Draft ready job on this Case shows on the Next action panel in the right column with its per-kind action (Review estimate, Open query, Review) | Next action |
| Work Centre D9 | Market research files attach to Files as evidence, stored in Box like any other evidence, with the tag Market research; no review step and no use as a value | Files |
| Work Centre D10 | assignment, edits by someone else, received e-mail and queries on this Case raise the engineer's notification | header, Notes, Files |
| Operator, 13 Sep | Create audit on an Inspection + Audit Case creates the linked Audit Case `a.`/`ap.{Case/PO}` as a duplicate; the bars link the two; Case type shows as a ribbon chip | [`dialogs/create-audit/how-it-should-work.md`](dialogs/create-audit/how-it-should-work.md) |
| Operator, 13 Sep (v24 notes) | Overview › Principal shows the Principal record's Notes on every Case and the Claim source record's, read-only and locked, absent when empty; beside each an editable notes field for this Case | Overview › Principal column; [`../../administration/contacts/how-it-should-work.md`](../../administration/contacts/how-it-should-work.md) |
| Operator, 13 Sep | Valuation: a Valuation month (month and year) and one button per source that runs the valuation; Add valuation stays for typed figures | [`panels/valuation/how-it-should-work.md`](panels/valuation/how-it-should-work.md) |
| Notes audit, 13 Sep | Import, Send to AI, Compare and Preview draft open their dialogs; no placeholder actions remain on the Case record | [`panels/estimate/how-it-should-work.md`](panels/estimate/how-it-should-work.md) |
