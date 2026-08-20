# Proof — TICK-046 (MAIL-04)

## Merge

PR #418, merge commit `181fe3313daaca072b170728391ef4c145460250` on `dev`/`main`.

## Deployment

Shipped in **release 12** (`ed3be51c95bc2a055606e5210131d37de9de2dd1`, deployed
2026-08-19 ~22:40–22:52Z). `181fe331` is a verified ancestor of `ed3be51c`.
See [[DELIV-012]] proof for the release-12 deployment readbacks: `efbundle`
applied all 8 pending migrations to production, head readback
`20260819180000_GrantEvaHandoffDownloadOperations` — this ticket's
classification-history migration `20260819104953` is among that applied set.

## Production evidence (this ticket's own behaviour)

- Migration `20260819104953` (classification evidence/policy-version/history
  schema) applied to production as part of release 12's 8-migration batch.
- The classification correction form is live on the deployed release-12
  build (part of the same Web image `ed3be51c` verified Healthy with
  `/diagnostics/version` match and smoke exit 0).

## Qualification

The correction form's liveness is inferred from the deployed image/version
match rather than a separate signed-in click-through of the correction UI
specifically; [[DELIV-012]]'s browser verification pass exercised the
classification-evidence panel on `/Inbox/{id}` (TICK-045's caller) but did
not separately narrate exercising a correction submission.
