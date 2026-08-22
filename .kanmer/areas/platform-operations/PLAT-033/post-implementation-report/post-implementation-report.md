# Post-implementation report

**Branch:** `task/qdos26009-operator-fixes` · **PR:** #506 · **Commit:** `3d7f87d6`

## What was built

Origin reads **E-mail**. Two lines, because one label has two overloads — the typed
`IntakeSourceChannel.Mailbox` and the string `"mailbox"`. Changing one and leaving the
other is the same one-list-per-concept split that produced [[CASE-015]]'s Odometer/Mileage
confusion, so both moved together.

"Approved inbox" describes how the system is configured. The operator sees a case that
arrived by e-mail.

## Checked while there

The sibling `IntakeSourceChannel` labels — `ManualUpload` and `Automation` — were reviewed
for the same fault. Neither describes configuration rather than what the operator sees, so
both were left alone. Reporting that they were checked matters as much as changing the one
that was wrong.

No test asserted either string; grepped before changing.

## Evidence

- `Pegasus.Web` builds clean
- Live: a case created from a mailbox message showing Origin **E-mail** — Phase 6
