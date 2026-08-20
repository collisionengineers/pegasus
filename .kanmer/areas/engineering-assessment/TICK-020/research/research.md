# EXT-01 research — DVLA/DVSA vehicle details: what exists, what blocks production reachability

All premises verified by read-only checks on `origin/dev` @ `29b81000` (plus TICK-021's branch `64dbfc2f`, which this ticket stacks on) unless marked assumed. No live call was made.

## Verified: EXT-01 is substantially implemented

- **Core contract + workflow**: `src/Pegasus.Core/Vehicle/LookupContracts.cs` (`VehicleLookupRequest/Result`, `VehicleDetails(Make, Model, ManufactureYear, EngineCapacityCc, FuelType)`, `MotTestObservation`, typed outcomes Current/Stale/Partial/NotFound/Throttled/Unavailable/Failed with `EnsureValidFor` invariants); `VehicleWorkflow.cs` (`RequestVehicleLookup` gated by `VehicleLookupAvailability`, `AcceptVehicleSuggestion` accept/correct with reason — the operator-confirmed reconciliation); `VehicleMileagePolicy.cs` (ADR-0012 conservative estimate).
- **Adapters**: `src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs` (DVLA VES + DVSA MOT History, OAuth token + API keys, approved hosts pinned: `driver-vehicle-licensing.api.gov.uk`, `history.mot.api.gov.uk`) and `DvlaDvsaAdapters.cs` (`DvlaDvsaReplayAdapter`, dev replay returning explicit unavailable when evidence is absent).
- **Worker execution path (production)**: `WorkerDependencyInjection.cs:78` composes `AddProductionExternalAdapters` (non-offline), which registers `VehicleLookupAvailability.ProductionLive` and the production adapter (`Infrastructure/DependencyInjection.cs:553-558`); DB work row → dispatcher → queue `external-work` → `ExternalWorkFunction` → `ProcessQueuedExternalWork` → `ProcessQueuedVehicleLookup` → adapter. Secrets: the six DVLA/DVSA/Box Worker Key Vault references resolved in prod (`docs/operations.md`, live-verified 2026-08-04; `infra/modules/platform.bicep` Dvla/Dvsa app settings).
- **UI caller**: `Pages/Cases/Vehicle.cshtml.cs` `OnPostRequestVehicleLookupAsync` / `OnPostAcceptVehicleSuggestionAsync`; forms in `Pages/Cases/Shared/_CaseWorkflow.cshtml`. Persistence, permanent history, replay-idempotent request (`EfVehicleWorkflowStore.RequestAsync`), confirmed-registration precondition, Serializable transaction. Integration terminal tests exist (`tests/Pegasus.IntegrationTests/VehicleWorkflowTerminalTests.cs`).

## Verified: the one gap that makes it unreachable in production

`IRequestVehicleLookup` has exactly one caller (the Case workspace). `RequestVehicleLookup.ExecuteAsync` throws `VehicleLookupUnavailableException` when `availability.RequestsEnabled` is false. Composition:

- Web `Program.cs:544-547`: `VehicleLookupAvailability.DevelopmentOfflineReplay` is registered **only inside `if (developmentOfflineProfile)`**. There is no `else`.
- Infrastructure default `DependencyInjection.cs:60`: `TryAddSingleton(VehicleLookupAvailability.Unavailable)`.
- Web has exactly two runtime profiles (`Program.cs:100-125`): DevelopmentOffline and Production.

Therefore in the production Web every "Request vehicle lookup" post throws and surfaces the generic case error; no work row is ever written; the production Worker's live adapter can never receive vehicle work. The Worker side is live-ready; the Web-side gate never opens. This is the whole reachability gap.

## Verified: a display gap inside the committed field list

EXT-01 commits make, model, **manufacture year, engine capacity, fuel type**. The latest-observation panel (`_CaseWorkflow.cshtml`) shows outcome/registration/provider/retrieved (+ TICK-021's mileage/MOT rows) but never renders `observation.Vehicle` — year, engine capacity, and fuel type are invisible, so the operator confirms a suggestion without seeing what the lookup returned. `ConfirmedVehicleEvidence` deliberately has no year/engine/fuel fields (they are observation evidence, not confirmable case fields) — display belongs on the observation.

## Activation boundary honestly stated

- `docs/current-architecture.md:471` (as-built): "DVLA/DVSA adapters are implemented, but live entitlement, enabled Worker caller evidence, and acceptance remain separate gates."
- The provider/API selection and credentials are, in fact, resolved and deployed: `docs/operations.md` Integrations lists "official DVLA VES v1.2 and DVSA MOT History v1"; the production Key Vault holds `Dvla__ApiKey`, `Dvsa__ClientId`, `Dvsa__ClientSecret`, `Dvsa__ApiKey` with resolved references — real operator-obtained credentials. The ticket's activation note ("live adapter/provider contract remains unresolved") predates that.
- What this ticket does NOT do: make any live DVLA/DVSA call, claim live acceptance evidence, or record a production deployment. Production serves release 13 (`2325ed4a`), which does not contain this change — deployment stays unclaimed. Live acceptance evidence (a real production lookup) remains for a post-release verification with the required live approval.

## Stacking

`_CaseWorkflow.cshtml`'s observation block is edited by TICK-021 (PR #448). This branch is based on `task/tick-021-ext-02-mot-chronology` to avoid a same-region conflict; PR #448 must merge first.
