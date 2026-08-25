# Proof — PR-055

## Shipped result

The released application contains the serialized same-key export-recording transaction and concurrent SQL regression.

## Immutable release evidence

- Implementation PR: https://github.com/collisionengineers/pegasus/pull/539, merged as `d973ead358f75736bdbdec3aa123d7d88a0083bd`.
- Application source SHA: `7e9465b006033bb516f7a4dbcb951f9a74416f2f`.
- Release documentation PR: https://github.com/collisionengineers/pegasus/pull/541, merged at docs-only main/dev head `7afd18037acfa78927c4b4ffdf8e0f74c7ecc688`.
- Image digest: `sha256:08f5f605b511f3a8d16a6702a071aa72e1403281b0b8289ddaae46601c86f105`.
- Production revision `pegasus-prod-web-252ow37gij--7e9465b00603` was Ready at 100%.
- Production smoke passed.
- All nine Worker functions were present and enabled.
- Migrations `20260824123336_DropEvaHandoffTables` and `20260825001401_RemoveWorkflowCompletenessWaivers` were applied.
- Permission verification catalogued 512 rows and found 351 effective rows.
- PR #539 final-head CI was fully green, including unit, browser, three SQL integration shards, and SQL integration coverage.

## Verification boundary

Deployment health, runtime readiness, migrations, Worker registration, permissions and production smoke are proven. No real operator Export was executed and no exported package was imported into EVA during this verification, so that end-to-end operator/business handoff is not claimed.
