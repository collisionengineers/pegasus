# Plan — INTK-049: Resolve machine-read UK registration character ambiguity through DVLA/DVSA

## Objective

Resolve O/0 and I/1 ambiguity for machine-read UK registrations from both
vehicle-image recognition and document OCR, using preserved DVLA/DVSA evidence
and withholding automatic identity whenever the whole candidate set is not
uniquely conclusive.

## Starting state

Image recognition is live and routes a terminal suggested registration through
`ImageIntakeAutomation`. Vehicle lookup is durable but Case-bound and calls
`IVehicleLookupAdapter` for one exact registration. Confirmed-case image
matching deliberately rejects general substitutions. Scan-like document OCR is
not implemented: [[TICK-041]] remains in Backlog and blocks this ticket.

Evidence: `research/research.md`@`2a8f7795a424e9fe`,
`files/files.md`@`4098e0e14d1223bf`.

## Dispatch gate

Do not take INTK-049 or create its branch/worktree until [[TICK-041]] is merged
into `dev`. After it lands, rerun read-only source/file checks, replace the
document-OCR placeholder below with its exact production boundary, refresh the
evidence versions, and revalidate this plan. The approved one-ticket scope does
not permit an image-only partial implementation or a dormant OCR hook.

## Governing docs

- **Modifies with explicit operator authorization:**
  `docs/frd/frd-02-intake-and-source-identity.md`. Add the two machine-read
  caller boundaries, UK structure scope, terminal waiting and fail-closed
  intake outcomes without describing OCR as active before [[TICK-041]].
- **Modifies with explicit operator authorization:**
  `docs/frd/frd-06-vehicle-and-engineering-evidence.md`. Add the provider-
  backed candidate/outcome contract and retained raw/attempt evidence while
  preserving exact confirmed-registration matching.
- **Meets:** `docs/current-architecture.md`. Refresh the as-built description
  only after both named production callers are wired.
- **No new ADR:** the change uses the accepted Core policy boundary, existing
  external-work runtime, current SQL database and existing DVLA/DVSA adapter.
  It adds no provider, project, store, runtime or deployment unit.

## Required changes

- Normalize a machine read to uppercase ASCII alphanumeric text and recognize
  the GB current, prefix, suffix and dateless structures plus Northern Ireland
  letter/digit structures.
- Keep one Core confusion map containing only O/0 and I/1.
- Generate only structurally valid candidates, de-duplicate them, order a valid
  raw read first and the rest by substitution count then ordinal value, and
  enforce the proven maximum of eight.
- Persist one intake-owned request and every ordered candidate attempt without
  fabricating a Case.
- Reuse the existing provider adapter and typed outcomes once per candidate.
- Resolve only one Current/Stale/Partial candidate when every other result is
  NotFound. Classify multiple viable candidates as Ambiguous, all misses as
  NoMatch, and any unresolved/exhausted provider condition as Incomplete or
  Failed.
- Make image and document routing wait for the terminal result. Pass only a
  unique resolved registration into their existing downstream policies.
- Leave staff-confirmed, embedded-text instruction, Case search, exact Case
  lookup and confirmed-registration image matching unchanged.
- Add no Republic of Ireland or European grammar/provider support and no
  additional confusion pair without evidence.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `docs/frd/frd-02-intake-and-source-identity.md` | Govern route scope and fail-closed intake behavior |
| Modify | `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Govern provider-backed ambiguity evidence and outcomes |
| Add | `src/Pegasus.Core/Vehicle/MachineReadRegistrationResolution.cs` | Single confusion map, UK structural filter, ordering/bound and result classifier |
| Modify | `src/Pegasus.Core/Vehicle/LookupWorkItem.cs` | Reuse existing retry/outcome handling without changing exact Case lookup behavior |
| Modify | `src/Pegasus.Core/Custody/ExternalWorkProcessing.cs` | Declare and dispatch the new durable work kind |
| Modify | `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | Enqueue/wait/resume image ambiguity work before existing routing |
| Modify | `src/Pegasus.Core/Intake/**/*.cs` | Temporary bounded family for the merged OCR boundary; narrow to exact paths before take |
| Add | `src/Pegasus.Infrastructure/Persistence/MachineReadRegistrationResolutionEntities.cs` | Persist intake ownership, raw read, candidates, attempts and state |
| Modify | `src/Pegasus.Infrastructure/Persistence/ExternalWorkStatePersistence.cs` | Persist the work-kind state and publication transition |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfExternalWorkStore.cs` | Lease/dispatch the new work through existing conventions |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` | Image request/replay and terminal route resumption |
| Modify | `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Register the new entity sets |
| Add | `src/Pegasus.Infrastructure/Persistence/MachineReadRegistrationResolutionModelConfiguration.cs` | Configure uniqueness, relationships, lengths and indexes |
| Modify | `src/Pegasus.Worker/**/*.cs` | Temporary Worker family; narrow to exact composition/dispatch paths before take |
| Add | `src/Pegasus.Infrastructure/Persistence/Migrations/*_MachineReadRegistrationResolution.cs` | Create schema, constraints, indexes and Worker grants |
| Add | `src/Pegasus.Infrastructure/Persistence/Migrations/*_MachineReadRegistrationResolution.Designer.cs` | Generated migration model |
| Modify | `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Generated current model |
| Add | `tests/Pegasus.Core.Tests/Vehicle/MachineReadRegistrationResolutionTests.cs` | Candidate and classification unit coverage |
| Modify | `tests/Pegasus.Core.Tests/ImageIntake/VrmRegistrationMatchingTests.cs` | Regression proof that confirmed matching is unchanged |
| Modify | `tests/Pegasus.Core.Tests/**/*.cs` | Temporary test family; narrow to exact image/document caller tests before take |
| Add | `tests/Pegasus.IntegrationTests/MachineReadRegistrationResolutionTests.cs` | Durable work, provider outcomes, provenance and replay |
| Modify | `tests/Pegasus.IntegrationTests/GroupedImageIntakeConcurrencyTests.cs` | Group waits and concurrency results are retained |
| Modify | `docs/current-architecture.md` | Final caller-backed as-built shape |

Generated migration/designer/snapshot files are committed. The document-OCR and
Worker paths must become exact after [[TICK-041]] lands; their current
placeholders make this plan intentionally non-dispatchable.

## Do not modify

No path beyond Expected files is authorized. In particular, preserve
`docs/operator-notes.md`, `corpus/`,
`src/Pegasus.Core/ImageIntake/VrmRecognition.cs` and
`src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs`.

## Constraints

- `Pegasus.Core` is the single owner of the confusion map, format grammar and
  resolution policy.
- No new dependency, package, feature flag, service, queue, database or runtime.
- Candidate count never exceeds eight; over-limit or invalid input abstains
  rather than truncating.
- Every provider result is retained; no first-success short circuit.
- A retryable, unavailable, failed or conflicted attempt cannot be discarded.
- Work is owned by intake evidence until a unique registration exists; no Case
  is fabricated to reuse the existing vehicle lookup table.
- Migration, schema constraints, runtime grants and model snapshot ship in the
  same diff.
- Existing exact routes remain one coherent implementation, without shims or
  dual behavior.
- No live external or cloud write is authorized by this ticket.

## Ordered steps

### Step 1 — Revalidate the merged dependency and governing contract

- Preconditions: [[TICK-041]] is merged into `dev`; INTK-049 is still
  untaken in Preparing.
- Files: `docs/frd/frd-02-intake-and-source-identity.md`,
  `docs/frd/frd-06-vehicle-and-engineering-evidence.md`,
  `src/Pegasus.Core/Intake/**/*.cs`,
  `tests/Pegasus.Core.Tests/**/*.cs`.
- Symbols: the document-OCR registration result, its failure/review state and
  production composition entry.
- Change: refresh research/files/plan with the exact caller, then add the
  approved route, structure, terminality, outcome and evidence rules to FRD-02
  and FRD-06 before implementing code.
- Preserved behaviour: ordinary embedded-text extraction and confirmed values
  remain exact.
- Forbidden: changing operator-notes meaning or claiming a caller that is not
  composed.
- Negative cases: dependency missing, governing conflict, or no explicit OCR
  registration result stops the ticket in Preparing.
- Tests: n/a; validate document links and plan gates.
- Commands: `rg -n "OCR|registration|vehicle" src tests docs/frd`.
- Expected output: exact production and test paths replace every placeholder.
- Done when: the packet is concrete, current and passes `get_doc_gates`.
- Deviation stop: do not invent an OCR hook or split the ticket.

### Step 2 — Implement the Core candidate and result policy

- Preconditions: Step 1 complete and the ticket has been taken in its recorded
  worktree.
- Files: `src/Pegasus.Core/Vehicle/MachineReadRegistrationResolution.cs`,
  `tests/Pegasus.Core.Tests/Vehicle/MachineReadRegistrationResolutionTests.cs`,
  `tests/Pegasus.Core.Tests/ImageIntake/VrmRegistrationMatchingTests.cs`.
- Symbols: the new candidate generator/result classifier and existing
  `VehicleLookupOutcome`.
- Change: implement normalization, supported UK masks, the O/0 and I/1 map,
  deterministic ordering, de-duplication, eight-candidate guard and
  Resolved/NoMatch/Ambiguous/Incomplete/Failed classification.
- Preserved behaviour: `VehicleLookupRequest` validation and
  `VrmRegistrationMatching` remain unchanged.
- Forbidden: regex/list copies in callers, foreign formats or additional pairs.
- Negative cases: invalid characters, unsupported shapes, no ambiguous
  character, mixed/multiple positions, multiple viable results and any
  unresolved outcome abstain as specified.
- Tests: exhaustive structure-mask and outcome-table unit tests, including a
  regression for the inserted-`1` matching rule.
- Commands: `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --filter "FullyQualifiedName~MachineReadRegistrationResolution|FullyQualifiedName~VrmRegistrationMatching"`.
- Expected output: exit 0 with every supported family and negative state proven.
- Done when: the Core policy is deterministic, bounded and has no second owner.
- Deviation stop: stop if provider truth requires a new outcome or a second
  policy owner.

### Step 3 — Add intake-owned durable work and permissions

- Preconditions: Step 2 policy is stable and both caller identities are known.
- Files: `src/Pegasus.Infrastructure/Persistence/MachineReadRegistrationResolutionEntities.cs`,
  `src/Pegasus.Infrastructure/Persistence/ExternalWorkStatePersistence.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfExternalWorkStore.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`,
  `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`,
  `src/Pegasus.Infrastructure/Persistence/MachineReadRegistrationResolutionModelConfiguration.cs`,
  `src/Pegasus.Infrastructure/Persistence/Migrations/*_MachineReadRegistrationResolution.cs`,
  `src/Pegasus.Infrastructure/Persistence/Migrations/*_MachineReadRegistrationResolution.Designer.cs`,
  `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`,
  `tests/Pegasus.IntegrationTests/MachineReadRegistrationResolutionTests.cs`.
- Symbols: new ambiguity request/attempt entities, replay key, external-work
  kind and Worker role grants.
- Change: store route/source identity, raw read, policy version, ordered
  candidates, attempts/results, terminal state and unique resolution. Link one
  external-work item and enforce one request per source evidence/read/policy
  plus one attempt per candidate.
- Preserved behaviour: existing Case-bound `VehicleLookupRequestEntity` and
  exact operation keys do not change.
- Forbidden: nullable fake Case ids, a new queue/store, swallowed publication
  conflicts or ungranted runtime access.
- Negative cases: duplicate enqueue is idempotent; concurrent publication or
  lease conflicts are retried/deferred/surfaced; incomplete work cannot expose
  a resolved value.
- Tests: integration coverage for schema constraints, replay, provenance and
  runtime-role access.
- Commands: use the repository's existing EF migration command, then
  `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~MachineReadRegistrationResolution"`.
- Expected output: one generated migration family, current snapshot, exit 0.
- Done when: schema, grants and tests prove durable intake ownership.
- Deviation stop: stop if the existing database/external-work boundary cannot
  carry the change; a new store/runtime requires separate architecture work.

### Step 4 — Process every candidate through the existing adapter

- Preconditions: Step 3 work can be leased and replayed.
- Files: `src/Pegasus.Core/Vehicle/LookupWorkItem.cs`,
  `src/Pegasus.Core/Custody/ExternalWorkProcessing.cs`,
  `src/Pegasus.Infrastructure/Persistence/ExternalWorkStatePersistence.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfExternalWorkStore.cs`,
  `src/Pegasus.Worker/**/*.cs`,
  `tests/Pegasus.Core.Tests/Vehicle/MachineReadRegistrationResolutionTests.cs`,
  `tests/Pegasus.IntegrationTests/MachineReadRegistrationResolutionTests.cs`.
- Symbols: `IVehicleLookupAdapter`, existing vehicle retry policy, external
  work dispatcher and the new processor.
- Change: call the unchanged adapter once for each due candidate, validate and
  persist every result, retry using existing policy, and classify only after
  the whole set is conclusive.
- Preserved behaviour: ordinary exact Case lookups keep their current
  scheduling and result handling.
- Forbidden: parallel provider implementation, first-hit resolution, catch-all
  suppression or discarded concurrency results.
- Negative cases: throttling/unavailable remains Processing while retryable;
  exhausted/failed work ends honestly without a registration.
- Tests: controlled provider sequence tests for unique, none, ambiguous,
  throttled, unavailable, failed and replayed requests.
- Commands: focused Core and integration test filters for the new processor.
- Expected output: exit 0; every adapter call and terminal transition is
  asserted.
- Done when: one registered Worker caller processes and resumes durable work.
- Deviation stop: stop if existing retry semantics must change for unrelated
  Case lookup behavior.

### Step 5 — Wire both machine-read callers

- Preconditions: Step 4 processor is composed and the exact OCR path from Step
  1 is present.
- Files: `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`,
  `src/Pegasus.Core/Intake/**/*.cs`,
  `src/Pegasus.Worker/**/*.cs`,
  `tests/Pegasus.Core.Tests/**/*.cs`,
  `tests/Pegasus.IntegrationTests/MachineReadRegistrationResolutionTests.cs`,
  `tests/Pegasus.IntegrationTests/GroupedImageIntakeConcurrencyTests.cs`.
- Symbols: terminal image suggestion, grouped-image routing input, OCR
  registration result and their existing no-identification/conflict/technical
  outcomes.
- Change: qualifying machine reads enqueue or reuse ambiguity work; both routes
  wait while it is non-terminal. A unique result enters the existing downstream
  policy. NoMatch maps to no identification, Ambiguous to conflicting
  identification, Incomplete/Failed to the existing technical review/failure
  route.
- Preserved behaviour: no-confusion reads, staff-confirmed values,
  embedded-text extraction and exact confirmed-case matching stay unchanged.
- Forbidden: route inference from generic Case data, premature group routing,
  dormant registrations or test-only production reachability.
- Negative cases: invalid/foreign shapes, duplicate callbacks, out-of-order
  grouped members and non-terminal provider work never allocate or enrich a
  Case.
- Tests: image single/group and document-OCR tests for every terminal state and
  unchanged non-opt-in routes.
- Commands: focused Core and integration filters for image intake, grouped
  concurrency and the merged OCR boundary.
- Expected output: exit 0 with named production callers and composition entries
  asserted.
- Done when: both real callers consume the one Core policy.
- Deviation stop: stop if either caller lacks an existing honest destination
  state; do not invent a generic fallback.

### Step 6 — Simplify, verify and prepare the PR

- Preconditions: Steps 1–5 complete and all focused tests pass.
- Files: `docs/current-architecture.md`.
- Symbols: the complete branch diff and named production composition paths.
- Change: run independent reuse, simplification, efficiency and altitude
  lenses; apply behavior-preserving findings; record every disposition. Update
  current architecture to the caller-backed state, run canonical validation,
  commit, push and open one PR to `dev`.
- Preserved behaviour: no assertion is weakened and no unrelated cleanup enters
  the diff.
- Forbidden: merge, deployment, proof on unmerged code or starting another
  ticket.
- Negative cases: any failed command, stale dependency, unauthorized file,
  missing runtime grant or uncomposed caller stops before PR handoff.
- Tests: focused tests plus canonical solution gates.
- Commands: `dotnet restore ./Pegasus.slnx --locked-mode`;
  `dotnet build ./Pegasus.slnx --configuration Release --no-restore`;
  `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`.
- Expected output: all commands exit 0 and one PR targets `dev`.
- Done when: the post-implementation report records the exact head SHA, tests,
  simplification dispositions, schema/grant evidence and PR.
- Deviation stop: stop at the open PR; an independent agent owns review.

## Acceptance checks

- Manual: name the image, document-OCR and Worker production callers and their
  composition entries.
- Manual: prove the Worker artifact carries the unchanged provider/runtime dependencies.
- Manual: prove the migration, constraints, model snapshot and Worker grants together.
- Manual: prove one Core confusion/format list and no caller copy.
- Manual: prove the adapter received every ordered candidate and all results persisted.
- Command: prove unique, none, ambiguous, retryable/unavailable/failed and replay states.
- Command: prove ordinary exact registration routes and confirmed image matching did not
  change.
- Command: retain exact command output and exit codes without weakening assertions.

## Commands

Run from the recorded INTK-049 worktree on Windows with PowerShell 7:

`dotnet restore ./Pegasus.slnx --locked-mode`

`dotnet build ./Pegasus.slnx --configuration Release --no-restore`

`dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MachineReadRegistrationResolution|FullyQualifiedName~VrmRegistrationMatching"`

`dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MachineReadRegistrationResolution|FullyQualifiedName~ImageIntake|FullyQualifiedName~GroupedImageIntake"`

`dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`

Post-merge proof and any environment validation belong to kanmer-verify, not
this execution phase.

## Failure and deviation rules

Stop and report a missing dependency/caller, governing conflict, new package or
runtime need, unsupported provider behavior, unsafe migration, failing test,
unknown changed file, live external-write requirement or scope expansion.
Refresh the ticket packet rather than improvising. A later passing command does
not erase an earlier failure.

## Stop condition

Stop after one complete PR targeting `dev`, with the ticket in Review and its
post-implementation report current. Do not merge, deploy, write proof or start
another ticket.
