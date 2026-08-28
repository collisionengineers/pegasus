# Files — TICK-061

## Owns

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/Cases/PrincipalCredentials.cs` | New: record, state, commands (issue/reset, pause, resume, revoke), store/query/authentication ports, secret generation policy. |
| `src/Pegasus.Infrastructure/Persistence/PrincipalCredentialEntities.cs` | New: `PrincipalApiCredentialEntity` (unique PrincipalId, unique KeyId). |
| `src/Pegasus.Infrastructure/Persistence/PrincipalCredentialModelConfiguration.cs` | New: EF mapping, check constraint on State, unique indexes, FK to Principals. |
| `src/Pegasus.Infrastructure/Persistence/EfPrincipalCredentialStore.cs` | New: store, queries and authentication port; PBKDF2 via `PasswordHasher<T>`; replay receipts; ActionHistory. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | DbSet + configuration call. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/*_PrincipalApiCredentials*` and `*_GrantPrincipalApiCredentials*` (+ snapshot) | Schema and web-role grant. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Registrations. |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1` | `Get-ExpectedMatrix` census block. |
| `docs/capabilities.md` | API-01 / API-04 rows to Now / `0.1.0-alpha.1`; summary recount. |
| `tests/Pegasus.Core.Tests/Cases/PrincipalCredentialsTests.cs` | Lifecycle, fail-closed auth, one-secret-once. |
| `tests/Pegasus.IntegrationTests/PrincipalCredentialPersistenceTests.cs` | SqlServer-category store proof. |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | Two migration names appended to the census. |

## Existing code/resources reused

- `OrganizationAdministrationPolicy` conventions (Administrator right, expected version, reason, operation key) and `EfOrganizationAdministration` transaction/receipt/history shape.
- `PasswordHasher<T>` from the already-referenced Identity package — the same PBKDF2 that hashes staff passwords.
- AUTO-011 migration + grant migration + bootstrap census pattern.

## Explicitly not changed

No Web authentication scheme, endpoint, or UI (TICK-058, PLAT-050). No Key Vault, new package, or ADR. No live credential.
