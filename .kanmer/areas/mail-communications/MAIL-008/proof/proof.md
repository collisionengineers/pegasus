# Proof — MAIL-008 (verified on deployed release 16, 2026-08-21)

Type: visual. Deployment evidence bundle: [[DELIV-015]] proof.

- The operator-approved classification labels ("labels approved", 2026-08-21) render live from the exhaustive family · subtype map: Inbox rows show "New instruction · Audit", "New instruction · Inspection", "Receiving work", and the honest "Unclassified" / "Unidentified" pair on the EREF24-shape mail; the message page's Decision card shows the same vocabulary (`OperatorLabels.MailClassification` — one owner, throws on an unmapped value, exhaustiveness pinned by `MailClassificationLabelTests`).
- The correction dialog's classification options come from the same map (`MailClassificationSelection`) — confirmed live when the dialog opened under the production CSP.
- **Parked (operator direction 2026-08-21)**: the folder-move reason wording stays deferred pending the folders-vs-Outlook-categories decision recorded on this ticket — no effort spent on move-reason copy until that decision lands. The move dialog itself ships with the neutral confirmation shape.
