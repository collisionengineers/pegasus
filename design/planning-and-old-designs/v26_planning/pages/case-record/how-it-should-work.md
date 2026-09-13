# Case record — how it should work

Decisions that land on the Case record from other pages' planning. The Case record's own planning is still to come; this file collects what is already decided.

| From | Decision | Where on this page |
| --- | --- | --- |
| Work Centre D5 | Place on Hold gains an optional Review on date | [`dialogs/hold-release/how-it-should-work.md`](dialogs/hold-release/how-it-should-work.md) |
| Work Centre D1 | a blocking external failure reads in operator words at the point of use: Vehicle "Lookup failed", Files "Storage not ready" | Vehicle and Files sections |
| Work Centre P4 | the Work Centre's Assign Engineer action opens this Case's assignment dialog directly, not the Case page; Review Case opens the Case at its Review decision | assignment dialog; Review decision |
| Work Centre P8 | "Assign to me" for an Engineer on a ready Case with no engineer, callable from the Work Centre's Today pane | assignment dialog |
| Received file D2 | Add evidence on Files is where a message or file is attached to this Case, including the upload confirmation's attach-with-override; Remove with reason is the reversal | Files |
