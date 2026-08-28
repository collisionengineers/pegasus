# Files

## Change surface

| File or module | Planned responsibility | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/Vehicle/LookupContracts.cs` or a focused sibling | Own the single candidate-generation and fail-closed resolution policy for machine-read `O` / `0` ambiguity | Candidate explosion or accepting before every result is terminal could select the wrong vehicle |
| `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | Invoke the shared policy for qualifying image-recognition reads before association or Image-initiated registration | Existing group precedence and recognition idempotency must remain intact |
| `src/Pegasus.Core/ImageIntake/VrmRecognition.cs` | Reuse the existing normalised standard-VRM constraints; do not weaken `VrmRegistrationMatching` substitution rules | Changing matching semantics would affect unrelated automatic association |
| `src/Pegasus.Core/Intake/*` (the future document-OCR result boundary) | Opt explicit document-OCR results into the same Core ambiguity operation when the real OCR caller is implemented | OCR is not currently active; no test-only or dormant production caller may be presented as wired |
| `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` and image-intake entities/configuration | Durably retain the machine read, candidate attempts/results, resolution, and replay key for pre-Case intake | A Case-bound lookup row cannot be reused by fabricating a Case ID; schema and grants must ship together if persistence changes |
| `src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs` | Reuse the existing one-registration adapter unchanged unless orchestration exposes a narrowly necessary defect | Provider calls, throttling, and provenance must not be duplicated in a second adapter |
| Worker/Web composition roots and background work routing | Wire one production caller for durable image/OCR ambiguity work, using the existing external-work convention where it fits | A registration-only inline network call would bypass retry and durability conventions |
| Core vehicle/image tests | Prove candidate order/bounds and resolved, no-match, ambiguous, unavailable/incomplete, and exact-route behavior | Tests must prove abstention, not only the happy path |
| Integration tests for image intake and vehicle lookup | Prove durable idempotency, provenance, no premature association, and unchanged non-machine routes | Requires controlled provider doubles; no fabricated domain evidence |
| EF migration, model snapshot, and worker grants if a new durable record is needed | Add storage and least-privilege access in one diff | Permission omission would leave the production Worker unwired |
| `docs/current-architecture.md` | Refresh as-built behavior only when implementation is actually wired | Must not describe deferred document OCR as live |

## Ripple effects

- Grouped image intake must wait for a terminal ambiguity result just as it
  waits for terminal recognition; it cannot let a member-level shortcut
  override group fail-closed behavior.
- Lookup rate and retry behavior increase with the number of generated
  candidates. The bounded generator and durable per-candidate idempotency are
  therefore acceptance requirements.
- If the resolved registration differs from the raw read, both remain
  reviewable with engine/model and DVLA/DVSA provenance.
- Case creation, reference allocation, matching, and vehicle enrichment must
  consume only the uniquely resolved registration.
- Existing exact staff refresh and automatic Case lookup retain their
  single-registration operation keys.

## Context files

| File | Why it must be read |
| --- | --- |
| `docs/frd/frd-02-intake-and-source-identity.md` | Owns grouped-image routing, fail-closed association, mileage tiers, and DVSA-for-every-Case behavior |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Owns vehicle lookup outcomes, evidence provenance, refresh behavior, and source limitations |
| `docs/current-architecture.md` | Distinguishes live image recognition, absent scan-like document OCR, and composed DVLA/DVSA lookup |
| `src/Pegasus.Core/ImageIntake/VrmRecognition.cs` | Existing recognition confidence and matching rules must remain authoritative |
| `src/Pegasus.Core/ImageIntake/ImageIntakeGroupRouting.cs` | Group-level precedence is the authority for association |
| `src/Pegasus.Core/Vehicle/LookupContracts.cs` | Existing provider request/result taxonomy to reuse |
| `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` | Shows current Case-bound idempotency and why pre-Case intake needs its own durable ownership |
| `src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs` | Existing production provider calls and provenance mapping |

## Out of scope

- Treating `O` and `0` as equivalent in ordinary case search or
  `VrmRegistrationMatching`.
- Reinterpreting a staff-confirmed or embedded-text instruction registration.
- Activating or selecting a Document Intelligence OCR provider.
- Historical backfill.
- Changing DVLA/DVSA credentials, provider selection, or live approval.
- Broad vehicle-image model changes or accuracy evaluation.
