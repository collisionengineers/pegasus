# Vault consolidation close-out

Task slug: `vault-consolidation-close-out`. Branch:
`task/vault-consolidation-close-out`. Taken 2026-08-04.

## Why this task exists

`task/vault-consolidation` (claimed by codex 2026-08-03) executed on
2026-08-03 but was never opened as a PR, so its claim stayed in `NOW.md`'s
`Doing` list and its evidence never reached a canonical file. Meanwhile
`operations.md` still carried the pre-consolidation statements, which had
become false.

This task is documentation truth-up only. It performs no Azure write. The
consolidation itself was executed by the earlier task under separately
approved external phases.

## What was verified live

Read-only Azure reads on 2026-08-04, operator-approved for exact targets in
subscription `e6076573-23a5-46a8-acef-7e22d264e5db`. No secret value was
retrieved, no resource created, changed, or deleted.

| Claim | Result |
| --- | --- |
| One vault in `rg-pegasus-prod` | `pegasusprodkv252ow37g` only |
| Six Worker Key Vault references | all six report `Resolved` |
| Worker reference targets | versioned URIs on the target vault |
| Web Box secrets | `box-config-json`, `box-client-secret` on target-vault versioned URIs, bound to `pegasus-prod-web-id-252ow37gij` |
| Referenced secret versions | all six present and `enabled` |
| Worker secret-scoped grants | exactly six `Key Vault Secrets User`, one per secret |
| Web secret-scoped grants | exactly two `Key Vault Secrets User` |
| Temporary `Key Vault Secrets Officer` | absent |
| Vault-scope role assignments | metadata-only `Key Vault Reader` only |
| Predecessor vaults | `cespkboxkvv76a47`, `cespkenrichkvgi62sd` soft-deleted 2026-08-03 |
| `rg-collisionspike-dev` | absent |
| Active Web revision | `pegasus-prod-web-252ow37gij--c6571f771aab`, `Provisioned`/`Healthy`, scaled to zero |

Two observations the earlier record did not carry:

- **The purge watch had the wrong shape.** `NOW.md` said one date,
  2026-08-09. There are five soft-deleted `uksouth` vaults on *two* dates:
  `cespk-pg-kv-dev`, `cespkevakvufa3ci`, and `cespklockva7tzj2` on
  2026-08-09 (deleted 2026-08-02), then the two consolidation predecessors on
  2026-08-10 (deleted 2026-08-03). The original line covered only the first
  three, so the watch would have been treated as clear a day early.
- **The live Web revision has moved on.** The execution record cites
  `ef987ac49cb4`; releases 4 and 5 have since replaced it with
  `c6571f771aab`, which is healthy and still resolving from the target vault.
  Consolidation therefore survived two subsequent deployments.

One wording deviation, recorded rather than corrected: the execution record
says "restored all history for the six approved secret names". Each secret
now holds exactly one version. That is consistent with predecessor vaults
that held a single version each, and it cannot be re-checked now that both
are soft-deleted. It is not a defect — every referenced version exists and is
enabled, and all eight references resolve.

## Changes

- `docs/operations.md` — replace the stale **Secrets** bullet in the
  production environment record with the consolidated end state plus the
  2026-08-04 live verification, and add a **Predecessor vaults** bullet
  carrying both purge dates.
- `docs/operations.md` — correct the recovery-section line that still said
  `rg-collisionspike-dev` "intentionally remains".
- `docs/open-decisions.md` — discharge the cost-entry watch item that said
  the web app "still resolves its Box secrets from the legacy
  `cespkboxkvv76a47` vault". The dated cost figures are left untouched as the
  historical record.
- `NOW.md` — close the codex vault-consolidation claim and re-state the purge
  watch with both dates.

`.azure/deployment-plan.md` is deliberately **not** edited. It is the
immutable executed 2026-08-02 release record; its statements were true when
written and the vault consolidation is a later event.

## Evidence classification

Live-verified configuration state, read-only. This proves the secret
references resolve and the predecessor resources are gone. It does **not**
prove Box, DVLA, or DVSA business behaviour, Worker caller traffic, the
production spine, deployment acceptance, or operator acceptance — those keep
their existing tiers and gates.

## Close-out

On merge, delete this plan. The now-redundant `task/vault-consolidation`
branch still exists on the remote; its execution record is preserved by this
task's `operations.md` changes, but it belongs to another agent and is left
for the operator to delete.
