# EXT-01 post-implementation report — 2026-08-20

PR: https://github.com/collisionengineers/pegasus/pull/450 (base `dev`, branch `task/tick-020-ext-01-completion`, own commit `e2cc731b`, stacked on TICK-021's `64dbfc2f` / PR #448 — **merge #448 first**).

## Honest capability status established (research doc has the full trace)

EXT-01 was already substantially implemented: Core lookup contract with typed outcomes and validation, request/accept/correct workflow with reasons and permanent history, replay + production adapters (approved DVLA/DVSA hosts pinned), the complete production Worker execution path, resolved production Key Vault credentials, and the Case-workspace UI actions. Two verified gaps remained; both are closed by this PR:

1. **Production reachability** — the production Web composed no `VehicleLookupAvailability`, fell to the `Unavailable` default, and every staff request failed closed; the Worker's live adapter could never receive work. `Program.cs` now registers `ProductionLive` in the production profile (the Web records the request; the Worker executes it).
2. **Committed fields invisible** — `observation.Vehicle` (make, model, manufacture year, engine capacity, fuel type) was never rendered; the operator confirmed suggestions blind. Now shown on the latest observation.

## What shipped vs the plan

Exactly the four planned files: `src/Pegasus.Web/Program.cs`, `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml`, `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs` (assertion simplified per the recorded pass), `docs/current-architecture.md` (as-built sentence). No migrations, no new port/store/flag, no adapter changes.

## Evidence

Release build 0 warnings; Vehicle-filtered Core tests 23/23; full `Pegasus.Core.Tests` 703/703. No live DVLA/DVSA call was made (read-only research only).

## Boundaries kept

- **No deployment claim**: production serves release 13 (`2325ed4a`), which predates this change; deployment tracking stays unset.
- **Live acceptance evidence remains outstanding and approval-gated**: a real production lookup after a release containing this change is the remaining EXT-01 acceptance step (ticket label `requires-live-approval`). The provider/API/credential decisions were already made and deployed (DVLA VES v1.2, DVSA MOT History v1, resolved Key Vault references — `docs/operations.md`).
- `docs/capabilities.md` activation note left untouched — updating it belongs with the live acceptance evidence, not with composition.

## For the reviewer

- Confirm the stacking order (#448 → #450).
- The pass flagged rendering provider-returned `FuelType`/`Make`/`Model` strings verbatim (external evidence values, not state codes) — disposition recorded in the plan; confirm or ticket.
