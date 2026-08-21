# Proof — MAIL-006 (verified on deployed release 16, 2026-08-21)

Type: visual. Deployment evidence bundle: [[DELIV-015]] proof. Verified on the production UI over real retained mail (the operator's morning test messages).

- **Inbox list**: the From cell shows only the bold effective original sender (`nduncombe@qdosassist.co.uk`, `jfleming@qdosassist.co.uk`) — the `desk@collisionengineers.co.uk` subline is gone; the operator-approved "Forwarded by Desk <…>" context line remains under the subject. Excerpts show the sender's own words (e.g. "Neil Duncombe Senior Claims Handler"), not the desk-wrapper Contact/LinkedIn junk.
- **Message page** (EREF6 → QDOS26006): rebuilt on the record container per the approved artboards — dark record header with Classified pill, Message / Attachments / Thread / Case tabs, bold effective sender with forward context, the forwarded `From:/Sent:/To:/Subject:` block rendered as a structured quoted header (never as body text), Decision card (Classification "New instruction · Inspection", Destination "Receiving work", Filed to QDOS26006, Decided timestamp) with the Correct classification action.
- Classification labels across list and detail come from the exhaustive family · subtype map (`OperatorLabels.MailClassification`).
- No horizontal overflow; the page carries no new guidance copy (design rails).
