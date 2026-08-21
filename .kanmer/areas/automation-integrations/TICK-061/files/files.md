# Files — TICK-061

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Cases/` | Add Principal-owned credential status, lifecycle commands, validation, authorization, and verification port beside existing Principal administration. |
| `src/Pegasus.Infrastructure/Persistence/` | Add one credential row per Principal, EF mapping/migration, hash verification, concurrency, replay, and permanent history in existing Azure SQL. |
| `tests/Pegasus.Core.Tests/`, `tests/Pegasus.IntegrationTests/`, `tests/Pegasus.ArchitectureTests/` | Prove lifecycle, hash-only persistence, isolation, and port dependency direction. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md`, `docs/capabilities.md`, `docs/current-architecture.md` | Record lifecycle behavior and the eventual as-built owner; ADR-0004 remains the accepted security boundary. |

## Existing code/resources reused

- `OrganizationAdministration.cs` and `EfOrganizationAdministration.cs`: authorization, expected version, reason, operation-key, transaction, and permanent-history conventions.
- Existing Azure SQL and Web managed identity: credential verifier and metadata storage/access.
- ASP.NET Core password hashing/cryptographic RNG: one-way verifier and one-time secret generation.
- `PLAT-028`: the only administrator UI caller.
- `TICK-058`: owns the first Web authentication handler because it supplies the first provider endpoint caller.

## Explicitly not changed

No Web authentication scheme or dormant endpoint is composed here. No Key Vault secret per Principal, Entra app registration, API Management instance, new store, multiple credentials, provider self-service, live issuance, or ADR reservation is introduced.
