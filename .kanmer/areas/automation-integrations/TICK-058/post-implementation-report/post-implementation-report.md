# Post-implementation report — TICK-058

## What shipped

- **Surface:** `POST /api/provider/v1/submissions` (multipart `files` +
  optional `providerReference`, required `Idempotency-Key`) and
  `GET /api/provider/v1/submissions/{id}`; JSON problem details on every
  failure; no cookie, no antiforgery; listed in `IsMachineSurface`.
- **Scheme:** `PegasusProviderApi` — `Authorization: Bearer pgs_<key id>_<secret>`
  verified through TICK-061's `IAuthenticatePrincipalCredential`; refusals
  are `SecurityEvents` (`provider_credential_missing`,
  `provider_credential_rejected` with the key id, `provider_credential_paused`),
  never the secret. Rate limited per key id (`ProviderApi` policy, 60/min).
- **Flag:** `Features:ProviderApi`, default off — nothing is registered or
  mapped without it (404).
- **Core:** `Pegasus.Core.ProviderApi.SubmitProviderInstruction` /
  `GetProviderSubmissionResult`; `ActorKind.Provider` (subject = Principal
  id) with `StaffAccessRight.SubmitProviderInstruction`;
  `IntakeSourceChannel.ProviderApi`; `ProcessIntake`/`AllocateIntake` bind
  the Principal from the retained submission (`IProviderSubmissionBindings`),
  skip mail-route selection and classify by that Principal, so a definitive
  provider instruction follows the same case-creation path as an equally
  definitive e-mail (FRD-09).
- **Migration:** `20260828111707_ProviderSubmissions` (table, FK to
  Principals, unique `(PrincipalId, IdempotencyKey)`) and
  `20260828111732_GrantProviderSubmissions` (Web SELECT/INSERT, Worker
  SELECT); census block in `Invoke-AzureDatabaseBootstrap.ps1`; names added to
  `IntakePersistenceIntegrationTests`.
- **Docs:** FRD-09 § Accepted API-01 submission contract.

## Evidence tier

Registered and composed behind a closed gate; integration tests exercise
the composed host in-process. Not deployed; not activated. Live activation
for a named provider and any credential issuance still need exact-target
approval (capabilities.md boundary). `docs/current-architecture.md` and
`docs/operations.md` are DELIV-030's and were not edited.

## What PLAT-050 consumes

The Principal settings dialog's "Pegasus API key" is TICK-061's issue/reset/
pause/resume/revoke; the key it shows once is the bearer this surface
accepts. PLAT-050 needs nothing from this ticket beyond the route to name
in operator-facing copy (`/api/provider/v1/submissions`) if it names one.

## Deviations / open items

- `ProviderSubmissions` is new because no receipt structure carried a
  provider identity (checked `IntakeReceipts`, `IntakeStagedReceipts`,
  `IntakeSubmissionGroups`).
- Automatic case allocation for a provider submission needs a typed
  classification, exactly as for e-mail; without the generated QDOS
  document tells the result reports `decision: CaseCreated`,
  `allocationFailure: CaseTypeUnavailable` — the truthful existing outcome.
- Tests were not run in this lane by instruction; the orchestrator's wave
  loop runs them.
