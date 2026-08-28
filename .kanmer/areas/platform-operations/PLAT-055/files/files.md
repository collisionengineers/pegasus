# Files — PLAT-055

## Changed state

| Target | Change | Risk |
| --- | --- | --- |
| Azure Key Vault `pegasusprodkv252ow37g/secrets/eva-client-secret` | Add one corrected version | Secret exposure or wrong environment; values never enter logs |
| Production Web Container App | Repoint `eva-client-secret` to the new version | Revision restart; verify health before auth |
| Production Worker Function App setting | Repoint the versioned Key Vault URI if it pins the old version | Restart may affect schedules; preserve every unrelated setting |
| Kanmer PLAT-055 | Record commands and proof | Never record secret values |

## Context

| Source | Why |
| --- | --- |
| `src/Pegasus.Infrastructure/Eva/EvaApiTransport.cs` | Token request is form-urlencoded and can be tested without submitting a case |
| `infra/modules/platform.bicep` | Owns the Web and Worker secret-reference shapes |
| `docs/operations.md` | Records the deployed credential boundary |
| [[TICK-077]] / [[ENG-019]] | Keeps this test-secret correction separate from the live-key swap |

No repository source file changes are in scope.
