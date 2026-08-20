# Proof — TICK-043 (MAIL-01)

## Merge

PR #414, merge commit `33f002203b2579529a15e2f8997e0dde45c42167` on `dev`/`main`.

## Deployment

Shipped in **release 12** (`ed3be51c95bc2a055606e5210131d37de9de2dd1`, deployed
2026-08-19 ~22:40–22:52Z). `33f00220` is a verified ancestor of `ed3be51c`.
See [[DELIV-012]] proof for the release-12 deployment readbacks: `efbundle`
applied all 8 pending migrations, `__EFMigrationsHistory` head readback
`20260819180000_GrantEvaHandoffDownloadOperations` — this ticket's mailbox
identity migration `20260819093019` is among that applied set.

## Production evidence (this ticket's own behaviour)

- Migration `20260819093019` (mailbox/thread/message identity) applied to
  production as part of release 12's 8-migration batch.
- The approved-mailbox Sent-evidence poll advanced on the new worker
  deployment (`4ac36bca-65ec-42cb-a5ca-80eec955756c`, active): per
  [[DELIV-012]] proof, `ApprovedSentPollStates.LastCompletedAtUtc` advanced to
  `2026-08-19T22:52:15Z` (previously stuck since 2026-08-07) and
  `LastFailureCode` cleared — direct evidence the mailbox-identity-aware
  poll pipeline this ticket built is running and progressing against real
  mailbox data in production.

## Qualification

None beyond what [[DELIV-012]] already qualifies (the approved-mailbox
production data change was applied through the application UI, not SQL).
