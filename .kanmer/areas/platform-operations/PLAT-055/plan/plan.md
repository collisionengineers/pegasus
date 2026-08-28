# Plan — PLAT-055

## Approach

Correct only the existing EVA test client secret. Create a new Key Vault version
from Infisical, silently prove equality, update the durable azd URI and both
runtime version-pinned references, then verify host health and token-only EVA
authentication. Keep the prior URI for rollback and never delete either version.

## Steps

1. Read Infisical `eva_api_client_secret` and the current Key Vault reference
   into process-local variables; expose neither.
2. Create a new enabled `eva-client-secret` version in
   `pegasusprodkv252ow37g`, then read it back and compare byte-for-byte to the
   Infisical source without printing either value.
3. Set production azd `EVA_CLIENT_SECRET_SECRET_URI` to the new versioned URI
   so later provisioning cannot restore the bad version.
4. Repoint the Web Container App's `eva-client-secret` Key Vault reference
   through its existing user-assigned identity. Repoint the Worker's
   `Eva__ClientSecret` Key Vault reference while preserving every unrelated
   setting.
5. Wait for Web and Worker to return healthy, confirm their live configuration
   names the new version, and confirm Key Vault synchronization succeeds.
6. Call EVA `POST /Connect/token` only, using the configured client ID and
   corrected secret. Require HTTP 200 and a non-empty token envelope; do not
   print or retain the token. Do not call `/Instruction/Inspection`.
7. Record sanitized proof and the outcome. If either host becomes unhealthy,
   restore both old references; retain the new Key Vault version for diagnosis.

## Governing docs

This preserves FRD-07's currently authorised EVA test route and changes no
submission behaviour. It explicitly excludes [[ENG-019]]'s live-key swap and
does not submit or duplicate a case.

## Risks

- Secret disclosure: values exist only in process-local variables and are never
  emitted to logs, Kanmer or command output.
- Split runtime state: Web, Worker and azd are all verified against the same new
  version before success is claimed.
- Restart disruption: preserve prior references and roll both hosts back on
  failed health.
- Vendor mutation: verification mints a token only; no instruction endpoint is
  called.
