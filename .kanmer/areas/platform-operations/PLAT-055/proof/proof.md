# Proof — PLAT-055: restore the EVA client secret from Infisical

## What was verified, and where

Production `rg-pegasus-prod`, on **2026-08-28**. This ticket changed **no
repository file**, so there is no commit, PR or build to cite: it is a
production configuration remediation, and its evidence is live read-back.

Its worktree `../pegasus-worktrees/plat-055-restore-eva-secret` was confirmed
clean — no repository verification command is applicable because nothing in the
repository changed.

Written 2026-08-30 during the post-release-37 board reconciliation, from the
read-backs recorded in the post-implementation report at the time they were
taken. **No live command was re-run for this document**, and none was needed:
the ticket's subject is a secret version pin, and re-reading it would prove the
state today, not the state the work produced.

## The defect

EVA token authentication returned **HTTP 401** for trace
`00-5f00f120eff5dedf6d6bfd977a7eb2ae-4e772536a57a259c-00`. Key Vault access,
RBAC, version resolution and runtime synchronisation were all healthy — the
stored secret *material* was wrong, having been duplicated during manual entry.

## Evidence

Tier: **deployed and exercised** — the highest tier, and unusually the only one
available here, since no code path changed.

| Check | Result |
| --- | --- |
| Infisical retrieval of `eva_api_client_secret` | PASS — value not emitted |
| Key Vault read-back equality against Infisical | PASS, byte-for-byte — value not emitted |
| New Key Vault version | `2341bad53baa4b3ca33b0809d7d4a735`, enabled |
| Production Web reference | pinned to that version |
| Production Worker `Eva__ClientSecret` | pinned to the same version |
| Production azd `EVA_CLIENT_SECRET_SECRET_URI` | updated, so a later provision cannot restore the bad version |
| Active Web revision | Healthy / Provisioned; `GET /health/ready` → **200** |
| Worker resource | Running, Normal availability |
| EVA `POST /Connect/token` | **HTTP 200**, non-empty `access_token`, positive `expires_in` — body and token not emitted |
| EVA instruction endpoint | **not called** |

The equality check was a silent round-trip: the two values were compared without
either being written to command output, to Kanmer, or to the repository.

## Boundaries the ticket set, and kept

- The **prior Key Vault version was not deleted**, so rollback remains available.
- This corrected the existing **test** credential only. It did **not** perform
  ENG-019's live-key swap.
- **No case was submitted to EVA.** Authentication was proven with a token call
  alone.
- No code, schema, infrastructure or board-root change.

## A rejected command, and why it left nothing behind

The first Worker update was rejected by its guard. That guard restored the
briefly updated Web and azd references, and read-only checks confirmed the
rollback **before** the retry. So the failed attempt caused no Worker change and
left no partial state — recorded here because a rejected write on a production
secret is exactly the thing a proof should not pass over in silence.

## Secrets

No secret value, access token, or response body appears in this document, in the
ticket, in its scratch, or in any command output captured for it. Only the Key
Vault **version identifier** is recorded, which is an addressing handle and not
secret material.

## What this evidence does NOT prove

- **That EVA accepts a real instruction.** Only the token endpoint was called.
  End-to-end submission is ENG-019's and TICK-077's ground, not this ticket's.
- **That the credential is correct today.** This is 2026-08-28 evidence. Release
  37 provisioned the Web app on 2026-08-30; the azd environment update above is
  what should have prevented that provision from reverting the pin, but **that
  was not re-checked after the release**.
- **Nothing about the live key.** The production EVA integration still runs on a
  test credential.

## Follow-up worth naming

The one unverified link is whether release 37's `azd provision` preserved the
secret version pin. That is a read-only check
(`az containerapp show` → the `eva-client-secret` reference) and it belongs to
whoever next touches the EVA path, not to a new ticket on its own.

---

## Addendum, 2026-08-30 — the one open question above is now closed

The section above listed as unproven whether release 37's `azd provision`
preserved the secret version pin. That is a read-only check and it was run
rather than left dangling. **The pin survived the release**, on both runtimes:

```
az containerapp show -g rg-pegasus-prod -n pegasus-prod-web-252ow37gij \
  --query "properties.configuration.secrets[?name=='eva-client-secret'].keyVaultUrl" -o tsv
-> https://pegasusprodkv252ow37g.vault.azure.net/secrets/eva-client-secret/2341bad53baa4b3ca33b0809d7d4a735

az functionapp config appsettings list -g rg-pegasus-prod -n pegasus-prod-worker-252ow37gij \
  --query "[?name=='Eva__ClientSecret'].value" -o tsv
-> @Microsoft.KeyVault(SecretUri=https://pegasusprodkv252ow37g.vault.azure.net/secrets/eva-client-secret/2341bad53baa4b3ca33b0809d7d4a735)
```

Both name version `2341bad53baa4b3ca33b0809d7d4a735` — the version this ticket
created — read from the estate **after** release 37 provisioned the Web app.
The active revision at the time of this read was
`pegasus-prod-web-252ow37gij--0b3ec847aae4`, Healthy, created
2026-08-30T15:21:37Z, which is release 37's revision and not the one the
original remediation ran against.

So the azd environment update did its job: a full provision did not revert the
pin. That is the durable half of this ticket, and it is now evidenced rather
than assumed.

Still not proven, and unchanged by this addendum: that EVA accepts a real
instruction, and anything about a live (non-test) key.
