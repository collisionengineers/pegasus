# Files

## Change surface

| File or module | Planned responsibility | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/Vehicle/LookupContracts.cs` or a focused sibling | Own the O/0 and I/1 confusion map, UK structure validation, candidate order/bound and fail-closed result classification | A copied map or permissive grammar could select or call the wrong vehicle |
| `src/Pegasus.Core/Vehicle/LookupWorkItem.cs` | Reuse the current application retry schedule and outcome handling for each candidate | First-hit short-circuiting or swallowed retries would make the set inconclusive |
| `src/Pegasus.Core/Custody/ExternalWorkProcessing.cs` | Add the one new durable external-work kind and dispatch path | The dispatcher must surface unknown or failed work |
| `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | Request ambiguity work for qualifying terminal image reads and wait before routing | Existing recognition idempotency and grouped routing precedence must remain intact |
| `src/Pegasus.Core/ImageIntake/VrmRecognition.cs` | Reuse normalization constraints while leaving confirmed-registration matching unchanged | The inserted-`1` plate-furniture rule must not become a general substitution |
| [[TICK-041]] document-OCR result boundary | Call the same Core operation while OCR provenance is explicit | The caller does not exist yet; no dormant or test-only path is acceptable |
| `src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs` and a focused ambiguity-work entity if needed | Persist source evidence, raw read, policy version, state and final resolution | Work must remain intake-owned rather than fabricating a Case |
| `src/Pegasus.Infrastructure/Persistence/ExternalWorkStatePersistence.cs` and `EfExternalWorkStore.cs` | Link and lease the new work through the existing external-work mechanism | Publication and concurrency results may not be discarded |
| `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` | Enqueue/replay image-owned requests and resume routing after terminal resolution | Duplicate requests or premature routing could create the wrong identity |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` and model configuration | Map the durable request and candidate attempts with required uniqueness/indexes | Schema constraints must enforce the replay key and attempt identity |
| `src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs` | Reuse the existing one-registration DVLA/DVSA adapter | Do not duplicate provider calls or provenance mapping |
| Worker composition and external-work dispatch registration | Wire one production processor for the new work kind | Registered-but-unreachable code is not delivered |
| New EF migration and `PegasusDbContextModelSnapshot.cs` | Create tables/indexes and grant the Worker least-privilege access in the same change | Missing grants would leave production work unprocessable |
| `tests/Pegasus.Core.Tests/Vehicle/*` | Prove structures, ordering, bound and result classifications | Tests must cover abstention and mixed O/0/I/1 positions |
| `tests/Pegasus.Core.Tests/ImageIntake/*` | Prove image opt-in and unchanged confirmed-registration matching | Matching semantics outside the provider-backed route must not change |
| `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs`, grouped-image tests and focused vehicle tests | Prove durability, idempotency, provider provenance, retries and group waiting | Controlled provider doubles only; no fabricated domain documents |
| `docs/frd/frd-02-intake-and-source-identity.md` | Govern route scope, supported UK formats and fail-closed intake behavior | Must not claim document OCR is active before [[TICK-041]] lands |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Govern provider-backed ambiguity outcomes and preserved raw/candidate evidence | Must remain distinct from confirmed-case image matching |
| `docs/current-architecture.md` | Record the final caller-backed as-built shape | Update only after both named callers are genuinely wired |

## Reuse decisions

- Reuse `IVehicleLookupAdapter`, `VehicleLookupRequest`,
  `VehicleLookupResult` and `VehicleLookupOutcome`; do not add a second
  provider contract.
- Reuse the external-work publication, leasing and retry conventions; add only
  the intake-owned request state that the Case-bound vehicle table cannot hold.
- Reuse `ImageIntakeGroupRoutingPolicy` after ambiguity terminality; do not
  create a competing group resolver.
- Keep the supported confusion map and structural grammar in one Core policy.
  Callers pass provenance and consume the result; they do not reproduce lists.
- No existing port can own pre-Case ambiguity attempts because the current
  vehicle work store requires a Case. That verified mismatch justifies the
  focused intake-owned persistence.

## Ripple effects

- Grouped image intake must treat ambiguity work as non-terminal until the whole
  candidate set is conclusive.
- Provider traffic grows by the number of structurally valid candidates, but
  never above eight for one read.
- The resolved registration may differ from the raw read; both remain
  reviewable with recognition and provider provenance.
- Case creation, reference allocation, matching and enrichment consume only a
  unique terminal resolution.
- Exact staff refresh, embedded-text instructions, Case search and automatic
  Case lookup keep their current single-registration keys.
- Republic of Ireland and European support remains absent. No foreign provider,
  grammar or normalization is introduced.

## Context files

| File | Why it must be read |
| --- | --- |
| `docs/frd/frd-02-intake-and-source-identity.md` | Owns grouped-image routing and fail-closed association |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Owns lookup outcomes, provenance and confirmed-registration matching |
| `docs/current-architecture.md` | Distinguishes live image recognition from absent document OCR |
| `src/Pegasus.Core/ImageIntake/VrmRecognition.cs` | Existing confidence and matching rules remain authoritative |
| `src/Pegasus.Core/ImageIntake/ImageIntakeGroupRouting.cs` | Existing group precedence is reused |
| `src/Pegasus.Core/Vehicle/LookupContracts.cs` | Existing request/result taxonomy is reused |
| `src/Pegasus.Core/Vehicle/LookupWorkItem.cs` | Existing retry behavior is reused |
| `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` | Demonstrates why existing work is Case-bound |
| `src/Pegasus.Infrastructure/Persistence/EfExternalWorkStore.cs` | Durable publication and leasing convention |
| `src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs` | Existing production calls and provenance mapping |
| [[TICK-041]] | Supplies the document-OCR caller and its governed failure route |

## Out of scope

- Treating ambiguous characters as equivalent in Case search or
  `VrmRegistrationMatching`.
- Reinterpreting staff-confirmed or embedded-text instruction registrations.
- Republic of Ireland or European registration support.
- Adding confusion pairs without labelled evidence.
- Activating, selecting or provisioning a Document Intelligence provider.
- Historical backfill.
- Changing DVLA/DVSA credentials or provider selection.
- Broad vehicle-image model changes or accuracy work.
