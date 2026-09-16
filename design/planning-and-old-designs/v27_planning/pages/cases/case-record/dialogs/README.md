# Case record — dialogs

Every dialog the live page opens, as reproduced in the baseline (`data-dialog` name, the state that offers it, the shot that shows it):

- `case-hold-dialog` — Place on Hold: reason and optional Review on; editing, not closed (42)
- `case-release-hold-dialog` — Release Hold: reason; Held, editing
- `case-complete-dialog` — Mark completed: reason; Post report, editing
- `case-return-review-dialog` — Return to Review: reason; With Engineer, editing
- `case-return-engineer-dialog` — Return to Engineer: reason; Completed or Query, editing
- `case-archive-dialog` — Archive case: reason; closed, editing
- `case-unlink-evidence-dialog` — Unlink report evidence: reason; Sent evidence linked, editing
- `case-correct-principal-dialog` — Correct principal: replacement principal code and reason; editing
- `case-close-dialog` — Close case: outcome (Provider cancelled, Collision Engineers rejected) and reason; editing (43)
- `case-handoff-dialog` — Hand to Engineer, with Assign to me for an Engineer; Review, editing (44)
- `eva-handoff-dialog` — EVA handoff: Engineer, Sign-off Engineer form while editing, Export EVA ZIP or Send via API by policy; Review or With Engineer (48, 63)
- `case-report-sent-dialog` — Mark report sent: the detected Sent item and a reason; Report preparation, editing (49)
- `case-create-audit-dialog` — Create audit: original case, outcome, derived reference; Inspection + Audit with a generated report, editing (39)
- `import-estimate-dialog` — Import estimate: one file; editing With Engineer (47)
- `compare-estimates-dialog` — Compare estimates: estimate, state, net, VAT, gross; two or more versions (46)
- `delete-estimate-dialog` — Discard estimate: consequence and reason; an editable non-Current draft
- `send-to-ai-dialog` — Send to AI: direction and target %; editing With Engineer with an Engineer's Value (45)
- `create-upload-request` — Create upload request: recipient, reason, lifetime and limits; editing
- `revoke-upload-u1` — Withdraw link: reason; editing
- `remove-doc-<id>` — Remove file: reason; editing
- `case-chase-dialog` — Record chase: recipient, channel, content, outcome; editing with a chase scheduled
- `account-dialog`, `notifications-dialog` (50), `command-dialog` — the shell's three
- the image viewer (51) and the finish-edit confirm are not dialogs by name; the strip's Viewer button and a dirty Cancel open them

One subfolder per dialog as planning for it starts.
