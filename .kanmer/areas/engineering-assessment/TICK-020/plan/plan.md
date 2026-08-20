# EXT-01 completion plan

Feature contract: the already-implemented DVLA/DVSA lookup workflow (request → queued Worker lookup → typed observation → operator accept/correct) becomes reachable in the production composition, and the returned vehicle details (make, model, manufacture year, engine capacity, fuel type) are visible to the operator on the observation they are asked to confirm. Caller: the existing Case workspace request/accept actions — no new caller. Failure behaviour unchanged and fail-closed: `Unavailable` stays the Infrastructure default for any composition that registers nothing; typed NotFound/Throttled/Unavailable/Failed outcomes persist as explicit evidence, never as invented values.

## Steps

1. **Web availability** (`src/Pegasus.Web/Program.cs`): add the `else` to the existing `if (developmentOfflineProfile)` availability registration → `AddSingleton(VehicleLookupAvailability.ProductionLive)`. Reuses the existing `VehicleLookupAvailability.ProductionLive` record (already defined in Core and already composed by the production Worker); the two-profile invariant (`Program.cs:115`) guarantees the branch is the production profile.
2. **Observation details display** (`_CaseWorkflow.cshtml`): in the latest-observation `<dl>`, when `observation.Vehicle is { } vehicle`, add rows Make / Model / Manufacture year / Engine capacity / Fuel type ("Not returned" em-dash style for absent optionals, consistent with the panel's existing wording). Reuses the existing `<dl>` convention; no narration.
3. **Core test** (`VehicleWorkflowTests.cs`): extend `RequestRequiresAnAvailableProfileAndAuthorizedStaffActor` (or sibling fact) to pin that `ProductionLive` permits the request path (delegates to the store) — the composition's meaning, testable in Core.
4. **As-built doc** (`docs/current-architecture.md` ~471): reword the DVLA/DVSA sentence: adapters and the Web-triggered production caller are composed; live acceptance evidence (a real production lookup after release) remains a separate, approval-gated step.
5. **Verify**: Release build zero warnings; focused Vehicle Core tests; architecture tests.

## What this ticket deliberately does not do

- No live DVLA/DVSA call, no cloud/config change, no deployment or deployment claim (production is release 13 = `2325ed4a`, which predates this change).
- No new feature flag: availability is already the one composition gate for this capability; adding a second switch would be a duplicate list.
- No change to `docs/capabilities.md` activation notes or the FRD — post-release live acceptance evidence is the remaining gate and stays recorded as such.
- Precise per-exception UI message for `VehicleLookupUnavailableException` left out: with ProductionLive composed, the Unavailable mode is only the fail-closed default for unforeseen hosts; the generic refusal message already fails closed.

## Acceptance

- Production Web composition registers `ProductionLive`; dev-offline still registers `DevelopmentOfflineReplay`; naked Infrastructure default remains `Unavailable`.
- Operator sees make/model/year/engine capacity/fuel type on the observation before accept/correct.
- Tests pin ProductionLive-permits alongside the existing Unavailable-refuses.

## Stacking

Based on `task/tick-021-ext-02-mot-chronology` (PR #448) — same observation block in `_CaseWorkflow.cshtml`. Merge #448 first.
