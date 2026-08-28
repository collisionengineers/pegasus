# Post-implementation report — PLAT-055

## Summary

Restored the production EVA test client secret from Infisical by adding a new
enabled Key Vault version and pinning the production Web, Worker and local azd
environment to it. The value matched Infisical byte-for-byte in a silent
round-trip check. Web readiness, Worker health and token-only EVA
authentication all passed. No EVA instruction was submitted.

## Changes

| Target | Change | Why |
| --- | --- | --- |
| Key Vault `eva-client-secret` | Added corrected enabled version `2341bad53baa4b3ca33b0809d7d4a735` | Replace duplicated secret material without deleting rollback history |
| Production Web Container App | Repointed `eva-client-secret` and restarted active revision | Load the corrected version immediately |
| Production Worker Function App | Repointed `Eva__ClientSecret` | Keep both runtime callers on the same version |
| Production azd environment | Updated `EVA_CLIENT_SECRET_SECRET_URI` | Prevent later provisioning from restoring the bad version |
| Repository files | No changes | This was production configuration remediation only |

## Governing docs

The correction preserves FRD-07's existing authorised EVA test integration and
changes no request mapping, submission behaviour or case state. It does not
perform [[ENG-019]]'s live-key swap and does not alter [[TICK-077]]'s code.

## Risks / follow-ups

The rejected first Worker command caused no Worker change; its guard restored
the briefly updated Web and azd references, and read-only checks confirmed the
rollback before retrying. The prior Key Vault version remains available for
rollback. No secret or access token was written to command output, Kanmer or
the repository.

## Verification hand-off

Read-only production verification completed on 2026-08-28:

- Infisical retrieval: PASS; no value emitted.
- Key Vault read-back equality: PASS; no value emitted.
- Web and Worker references: both pin version
  `2341bad53baa4b3ca33b0809d7d4a735`.
- Active Web revision: Healthy/Provisioned; `GET /health/ready` returned 200.
- Worker resource: Running with Normal availability.
- EVA `POST /Connect/token`: HTTP 200, non-empty `access_token`, positive
  `expires_in`; response body and token were not emitted.
- EVA instruction endpoint: not called.
- Ticket worktree: clean; no repository verification commands are applicable
  because no repository files changed.
