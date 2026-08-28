# Post-implementation report — TICK-061

## Delivered

- Core `src/Pegasus.Core/Cases/PrincipalCredentials.cs`: `PrincipalCredentialState {Active, Paused, Revoked}`, `PrincipalCredentialRecord` (status and timestamps, never the verifier), one shared `PrincipalCredentialCommandRequest` (principal, expected version, actor, operation key, reason), commands `IssuePrincipalCredential` (create or reset; the clear secret is in the outcome once, `null` on a replay), `PausePrincipalCredential`, `ResumePrincipalCredential`, `RevokePrincipalCredential`, `GetPrincipalCredential`, and `AuthenticatePrincipalCredential`; ports `IPrincipalCredentialStore`, `IPrincipalCredentialQueries`, `IAuthenticatePrincipalCredential`. All commands require `ManageOrganizationsAndPrincipals`.
- Secret format: `pgs_<16-char key id>_<43-char base64url of 32 random bytes>`; key id is embedded so a presented secret names its row. Shape is checked before any store call.
- Hashing: PBKDF2 through `PasswordHasher<PrincipalApiCredentialEntity>` — the convention ASP.NET Identity already applies to staff passwords; no new package.
- Infrastructure: `PrincipalApiCredentials` table (PK/FK `PrincipalId`, unique `KeyId`, `SecretHash`, `State` check constraint, timestamps, `Version` concurrency token); `EfPrincipalCredentialStore` (Serializable transactions, replay through `OrganizationAdministrationOperations` receipts with command kinds `issue|pause|resume|revoke_principal_credential`, action history `principal_credential_issued|reset|paused|resumed|revoked` on aggregate `principal_api_credential` with before/after JSON that never contains the hash).
- Migrations: `20260828104130_PrincipalApiCredentials`, `20260828104139_GrantPrincipalApiCredentials` (web role SELECT/INSERT/UPDATE, no worker, no DELETE). Bootstrap census block added; migration census in `IntakePersistenceIntegrationTests.cs` extended.
- `docs/capabilities.md`: API-01 and API-04 → Now / `0.1.0-alpha.1` citing EPIC-011 D8, boundary "requires exact-target approval before any live credential is issued"; Now 142 / Next 27 / `0.1.0-alpha.1` 142 / `0.4.0` 3.

## What consumers get

- TICK-058: `IAuthenticatePrincipalCredential.ExecuteAsync(keyId, secret)` → `PrincipalCredentialAuthentication?` (null = refuse; `MaySubmit` false while paused — reads of prior receipts may proceed, submissions must be refused).
- PLAT-050 / PLAT-028: `IGetPrincipalCredential` for status, the four commands for the dialog; show `IssuePrincipalCredentialOutcome.Secret` in the immediate response only.

## Verification

- `dotnet build ./Pegasus.slnx --configuration Release` — exit 0, 0 warnings.
- `pwsh ./scripts/Test-MigrationGrants.ps1` — exit 0. `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — exit 0.
- Tests written (Core: `PrincipalCredentialsTests`; integration SqlServer: `PrincipalCredentialPersistenceTests`) but not run by the implementer; the orchestrator runs the wave loop.

## Deviations from the brief

- `SecretHash` is not on the Core record; the verifier never leaves the store.
- The four commands share one request record instead of four.

## Open questions

- None new. Parked: wire presentation of the credential (TICK-058), overlapping credentials, live issuance (exact-target approval).
