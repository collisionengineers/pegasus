# Plan — TICK-061: Provider credential lifecycle

## Diff estimate

About 1,100 hand-written lines (Core ~300, store/entity/config ~400, tests ~350, docs/script ~50) plus generated migration designer/snapshot files.

## Approach

One credential per Principal. Core owns the lifecycle state machine and the secret format; Infrastructure owns hashing (PBKDF2 `PasswordHasher<T>`, the existing staff-password convention) and persistence. The clear secret exists only in the `IssuePrincipalCredential` outcome. No endpoint, scheme, or UI ships here.

## Steps

1. Core `PrincipalCredentials.cs`: `PrincipalCredentialState {Active, Paused, Revoked}`, `PrincipalCredentialRecord`, requests carrying `ActionActor`, `PrincipalId`, `ExpectedVersion`, `Reason`, `OperationKey`; `PrincipalCredentialPolicy` (normalise via the same rules as `OrganizationAdministrationPolicy`, plan transitions, `GenerateSecret(keyId)` = `pgs_<keyId>_<43-char base64url of 32 random bytes>`); ports `IPrincipalCredentialStore`, `IPrincipalCredentialQueries`, `IAuthenticatePrincipalCredential`; commands `IssuePrincipalCredential`, `PausePrincipalCredential`, `ResumePrincipalCredential`, `RevokePrincipalCredential`.
   Reuses: `StaffAuthorization`, `ActionActor`, Organization administration normalisation constants.
2. Infrastructure: entity, model configuration (unique PrincipalId/KeyId, State check constraint, FK to Principals, Version concurrency token), `EfPrincipalCredentialStore` implementing the three ports with Serializable transactions, receipt replay (`OrganizationAdministrationOperations`, same command-kind/hash shape), ActionHistory `principal_credential_issued|reset|paused|resumed|revoked` with hash never serialised; DbSet; DI.
3. `dotnet ef migrations add PrincipalApiCredentials` then `GrantPrincipalApiCredentials` (web role SELECT/INSERT/UPDATE); bootstrap census block; run `Test-MigrationGrants.ps1` and `Test-AzureDeploymentPlan.ps1 -Mode Local`.
4. `docs/capabilities.md`: API-01 and API-04 → Now / `0.1.0-alpha.1`, owner FRD-09, boundary "requires exact-target approval before any live credential is issued"; recount summary tables.
5. Tests: Core (lifecycle transitions, Administrator-only, secret format/once, fail-closed authentication outcome mapping); integration (issue → authenticate → reset invalidates old → pause = authenticated-but-blocked → revoke refused; replay; hash-only column; history rows); migration census names.
6. Simplification pass; post-implementation report; PR to `dev`.

## Verification

Build in Release; the two scripts exit 0. Tests are not run by the implementer (orchestrator runs the wave loop).

## Risks / deferred

Multiple simultaneous credentials, wire presentation of the credential, and live issuance remain deferred (open-questions Parked).
