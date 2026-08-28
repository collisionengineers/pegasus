---
kind: proof
pr: "580"
merge_sha: "783b4b884d3f110e78efe25366b66950d04551fc"
verified_on: "783b4b884d3f110e78efe25366b66950d04551fc"
result: PASS
verified_at: "2026-08-28T03:08:00Z"
---

# Proof — ENG-023

Verified on merged `main`, which is this ticket's own merge commit
`783b4b88`. `main` and `dev` both read `783b4b88`.

## Every recorded fact was read from the estate, not from the plan

| Recorded in `operations.md` | Read back from |
| --- | --- |
| source `84132d01ccb0afca7af6c6ce519e6f3491aee160` | `release-manifest.json` `sourceRevision`, and `git rev-parse origin/main` at promotion |
| image `sha256:5ba65f61ad754639185764ed2c7795fc06938e6e397a3a9d5c7f7fe5c01bb032` | the manifest's `webImage.digest`, the digest `oras cp` reported on push, and the active revision's image |
| revision `pegasus-prod-web-252ow37gij--84132d01ccb0` | `az containerapp revision list`, `RunningAtMaxScale`, traffic 100, Single mode |
| manifest SHA-256 `A1E3707F…C76` | `Get-FileHash` over the retained manifest |
| both EXT-04 migrations applied, head at `20260827143200_GrantEvaSubmissions` | `SELECT TOP 3 MigrationId FROM __EFMigrationsHistory` against production SQL |
| four `EvaSubmissions` grants, `UPDATE`/`DELETE` to neither role | `sys.database_permissions` joined to `sys.objects` for that table |
| `EvaSubmissions` zero rows, no Principal with either toggle on | `SELECT COUNT(*)` on each |
| eighteen Worker Key Vault references `Resolved` | the `configreferences/appsettings` ARM endpoint — 18 of 18 |
| smoke matched exact source and version | `Invoke-ProductionSmoke.ps1` output, including inbox liveness |

## The claims the entry deliberately does not make

It does not say EXT-04 works. Pegasus has still never called EVA in any
environment, and the entry says so in those words. It records deployment,
schema, runtime permissions, secret resolution and configuration — each of which
was read back — and nothing about whether EVA accepts the payload.

It does not present the two hand-made prerequisites as incidental. The Key Vault
secrets and the two secret-scoped Web grants are permanent estate state a future
release depends on, and the entry says why they could not have come from bicep.

## Result

**PASS.** Both verification conditions hold: the documents match the estate as
deployed, and every claim traces to something read back.

## Residual

The Worker's vault-scope `Key Vault Secrets User` grant is now written down and
not dispositioned. It is a real least-privilege deviation from what the same
document describes elsewhere, and it wants either its own ticket or an explicit
decision that vault scope is acceptable for that identity. Recording it was in
scope here; changing a live identity's access was not.
