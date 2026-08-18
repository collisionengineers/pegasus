# Files — TICK-033

## Where the change lands

| Path | Why |
|---|---|
| `docs/capabilities.md` | Correct the INT-31 activation/boundary note: the superseded Box File Request UI/persistence removal is already in source; deployment and acceptance remain separate. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/frd/frd-02-intake-and-source-identity.md` | The binding behavioural contract: staff creation, expiry/revocation, isolated public response, custody, retry, limits and non-disclosure. |
| `docs/current-architecture.md` | The as-built source caller is only `/Uploads/{token}`; implementation/caller/deployment/acceptance are distinct claims. |
| `src/Pegasus.Core/Documents/RequestUploadPolicy.cs` | Core owns token, expiry, revocation, limit and idempotency policy; no duplicate policy is allowed. |
| `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs` | Existing persistence/custody implementation and replay/rollback behaviour. |
| `src/Pegasus.Web/Pages/Uploads/Request.cshtml(.cs)` | Existing anonymous surface, antiforgery, bounded response and request-local form. |
| `tests/Pegasus.IntegrationTests/CaseCustodyWebTests.cs` | Existing staff creation/revocation caller evidence. |
| `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` | Existing durable custody rollback/retry evidence. |
| commit `f43e3a2b` | Removes the obsolete Box File Request model/caller that the capability inventory still calls pending. |

## Ripple effects

The documentation correction must not claim deployment or operator acceptance. Targeted integration tests provide local source evidence only; no Azure, Box, Outlook or production mutation is in scope.

## Out of scope

- New upload-link behaviour, storage, migrations, public routes, limits or UI.
- Live activation, production tests, and any cloud or mailbox/Box mutation.
- Changes to `docs/operator-notes.md`, which remains protected business truth.
