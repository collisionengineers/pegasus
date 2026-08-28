# Files — TICK-077 (EXT-04) Direct EVA API integration

## New

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/Eva/EvaApiContracts.cs` | `EvaSubmissionOutcome`, `EvaSubmissionResult`, `EvaInstructionPayload`, `IEvaApiTransport`, `ISubmitCaseToEva`, `SubmitCaseToEvaRequest`. |
| `src/Pegasus.Core/Eva/CaseEvaApiMapping.cs` | `EvaReplayFields` → `EvaInstructionPayload`. Owns only the rename to EVA's field names and EVA's own required fields; reuses `CaseEvaMapping` normalisation. |
| `src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs` | Single owner of: which toggle permits which act, the once-per-case rule, and the outcome classification table. |
| `src/Pegasus.Infrastructure/Eva/EvaApiOptions.cs` | Positional record + validating `Create`, HTTPS and `sentry.evasoftware.co.uk` host allow-list. Mirrors `DvlaDvsaProductionOptions`. |
| `src/Pegasus.Infrastructure/Eva/EvaApiTransport.cs` | The network boundary. Token cache (`expires_in` × 60 s, 30 s margin), retry-once on 401, case-insensitive envelope, tolerates `text/plain`. |
| `src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs` | `ISubmitCaseToEva`. Sibling of `EvaHandoffStore`: Review gate, row lock, version check, operation-key replay, action history. |
| `src/Pegasus.Infrastructure/Persistence/EvaSubmissionEntities.cs` | `EvaSubmissionEntity`. |
| `src/Pegasus.Infrastructure/Persistence/EvaSubmissionModelConfiguration.cs` | Table, unique filtered index on succeeded-per-case, check constraints. |
| `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml(.cs)` | The Send to EVA confirmation page and the API submission handler. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/…_EvaApiSubmissions.cs` | `EvaSubmissions` table plus the two `Principals` columns. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/…_GrantWorkerEvaSubmissions.cs` | Worker role grants. Must ride the same diff. |
| `docs/adr/00NN-per-principal-eva-api-submission-setting.md` | The per-principal database-authored setting, following ADR-0018. |
| `tests/Pegasus.Core.Tests/Qdos/EvaApiMappingTests.cs` | Field mapping and required-field behaviour. |
| `tests/Pegasus.Core.Tests/Qdos/EvaSubmissionPolicyTests.cs` | Four distinct outcomes; once-per-case refusal; toggle permissions. |
| `tests/Pegasus.IntegrationTests/EvaApiTransportTests.cs` | `DelegateHandler` fixtures from the connector's recorded traffic. |
| `tests/Pegasus.IntegrationTests/EvaSubmissionTests.cs` | End-to-end through the store: Review gate, replay, dedupe, history. |

## Modified

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/Custody/ExternalWorkProcessing.cs` | `SubmitCaseToEva` kind, `IProcessQueuedEvaSubmission` arm, `EvaSubmissionRetryPolicy`. |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | Extract `LoadEligibleImagesAsync` into a shared reader so the API path does not copy it. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | `PrincipalEntity` gains the two flags; `EvaSubmissions` set. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register the transport and store; lazy options factory. |
| `src/Pegasus.Worker/WorkerDependencyInjection.cs` | Compose the transport in the Worker; the queued-submission processor. |
| `src/Pegasus.Web/Program.cs` | `Eva:*` config, added to the fail-fast required-key list. |
| `src/Pegasus.Worker/Program.cs` | Worker-side config. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | Export button becomes **Send to EVA**, routing to the confirmation page. |
| `src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml(.cs)`, `Replace.cshtml(.cs)`, `Index.cshtml` | The two toggles, plus a post-creation edit. |
| `src/Pegasus.Core/Cases/OrganizationAdministration.cs` | Carry the toggles on the administration summary and commands. |
| `src/Pegasus.Infrastructure/Persistence/EfOrganizationAdministration.cs` | Persist them. |
| `infra/main.parameters.json`, `infra/modules/platform.bicep` | `EVA_*_SECRET_URI` → Key Vault → `Eva__ClientId` / `Eva__ClientSecret` for Web and Worker. |
| `docs/frd/frd-07-eva-and-external-engineering-handoff.md` | The API route, the activation toggle, the once-per-case limitation, the four outcomes. |
| `docs/capabilities.md` | EXT-04 allocation and canonical owner. |
| `docs/boundaries.md` | The EVA row no longer excludes a network adapter. |
| `docs/open-decisions.md` | Resolve the EXT-04 activation decision. |
| `docs/current-architecture.md`, `docs/operations.md`, `docs/runbook.md` | As-built shape, deployed state, operator procedure. |

## Deliberately not touched

- `src/Pegasus.Core/Eva/EvaBundleSchema.cs` beyond reuse — the ZIP contract and
  its byte-identical replay guarantee are unchanged.
- `src/Pegasus.Web/Pages/Cases/Documents/Export.cshtml.cs` — the export handler
  is reached from the new confirmation page unchanged.
- `EvaFirstHandoffProxies` — check-constrained against claiming delivery.
