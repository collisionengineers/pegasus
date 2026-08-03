# Task plan: image-led-intake

Branch `task/image-led-intake`, claimed in `NOW.md` on `origin/dev`.
Scope note: the operator decided open decision 1's mechanism on 2026-08-03
(ADR-0018, in-process ONNX engine) and added full implementation of that
engine to this task. Threshold acceptance (open decision 1's remainder)
stays open: this task produces the first local evaluation evidence and a
proposed provisional bar, not an accepted threshold.

## What changes

### Already done on this branch

- Claim commit and initial plan (`e9a74ad`, `5a6832f`).
- `e84097d` — decision-1 candidate evidence appended to
  `docs/open-decisions.md` item 1.
- `b165860` — `docs/adr/0018-in-process-onnx-vrm-recognition.md` accepted
  (vendored hash-pinned fast-alpr YOLOv9 plate detection + fast-plate-ocr
  global CCT recognition, run in-process via `Microsoft.ML.OnnxRuntime`;
  suggestion-first, abstention over guessing, no egress), plus the
  `docs/open-decisions.md` item-1 rewrite and the `docs/capabilities.md`
  `INT-17` row update.
- `4ed2c67` — `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` and
  `src/Pegasus.Core/ImageIntake/ImageIntakeLifecycle.cs`: the domain
  contract this task now implements. Fixed points the rest of the work
  must honour: `ImageIntakeReferenceFormat.Create` (`{VRM}-{NN}`,
  two-digit minimum, expands past `-99`, sequences start at 1, never
  reused); `IImageIntakeStore` with register replay probe
  (`ProbeRegisterReplayAsync` returns null only for an unseen operation
  key, throws `ImageIntakeOperationConflictException` for a committed key
  with a different fingerprint); `ImageIntakeCaseLinkRequest` carrying
  expected image-intake and case versions plus `CaseEditLeaseToken`;
  `ImageIntakeLifecycleRules.IsCaseEligibleForAssociation` (eligible only
  in `NotReady`/`Held`/`Review`/`ReportPreparation` with no report-sent
  evidence) which the store must enforce inside the link transaction; and
  use cases `RegisterImageIntake`/`LinkImageIntakeCase`/
  `UnlinkImageIntakeCase`.

### Step 1 — Core query additions (web-layer gaps)

`src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`:

- Add to `IImageIntakeQueries`:
  - `GetByOriginReceiptAsync(Guid intakeReceiptId, ...)` — an intake
    receipt has at most one Image intake; Intake pages need this to show
    an existing registration and its outcome label.
  - `ListByOriginReceiptsAsync(IReadOnlyCollection<Guid> receiptIds, ...)`
    returning `(Guid ReceiptId, ImageIntakeSummary Summary)` pairs — the
    intake list page labels a whole page of rows in one query
    (`ImageIntakeSummary` deliberately has no receipt id).
  - `ListForCaseAsync(Guid caseId, ...)` — the Case side lists its linked
    Image intakes.
- Add `IImageIntakeOriginResolver` with
  `Task<ImageIntakeOrigin?> ResolveOriginAsync(Guid intakeReceiptId, ...)`.
  Reason: `RegisterImageIntakeRequest.Origin` requires
  `EvaluationRevisionId`, but the web-facing `IntakeReceipt` record
  exposes no evaluation revision; the resolver returns the receipt's
  source identity, source hash, and latest completed
  `IntakeEvaluations.Revision` for the processed receipt (the pair
  `EfTriageStore.CreateAsync` already validates).

### Step 2 — EF persistence, migration, DI

`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`:

- New entities + `DbSet`s, modelled on the Triage block
  (`PegasusDbContext.cs:503-528, 627-643`):
  - `ImageIntakeEntity` (`ImageIntakes`): Id, OriginReceiptId,
    SourceChannel, ExternalReceiptToken, SourceHash (64 fixed),
    EvaluationRevisionId, NormalizedVehicleRegistration (max 20),
    ImageIntakeReference (max 30), LinkedCaseId?, CreatedAtUtc,
    CreationOperationKey, Version, ConcurrencyToken; implements
    `IApplicationManagedConcurrencyToken`; `Version` and
    `ConcurrencyToken` both concurrency tokens; check
    `CK_ImageIntakes_Version >= 0`; unique indexes on OriginReceiptId,
    (SourceChannel, ExternalReceiptToken), ImageIntakeReference, and
    CreationOperationKey; FK OriginReceiptId → `IntakeReceipts`
    (restrict), FK LinkedCaseId → `Cases` (restrict).
  - `ImageIntakeSequenceEntity` (`ImageIntakeSequences`): key
    NormalizedVehicleRegistration (max 20), LastAllocatedSequence; check
    `>= 0` only — deliberately no ceiling (unlike
    `CK_CaseSequences_LastAllocatedSequence <= 999`), because the
    reference format expands past `-99` instead of exhausting.
  - `ImageIntakeHistoryEntity` (`ImageIntakeHistory`): Id, ImageIntakeId
    FK (restrict), EventType, Actor, Reason, OperationKey (unique),
    RequestHash (64 fixed), OccurredAtUtc, BeforeVersion, AfterVersion,
    AfterLinkedCaseId? — matches `ImageIntakeHistoryEntry` exactly;
    append-only, no state column (an Image intake has no state machine).

`src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` (new),
mirroring `EfTriageStore` mechanics exactly:

- Serializable transaction per mutation; replay lookup =
  `ImageIntakeHistory` row by trimmed operation key; `EnsureReplay`
  compares event type + SHA-256 request fingerprint (+ image intake id)
  and throws `ImageIntakeOperationConflictException` on mismatch; replay
  mapping reconstructs the historical record from the history row's
  after-fields (as `EfTriageStore.MapReplayAsync` does).
- `RegisterAsync` (event `image_intake_registered`): validate via
  `ImageIntakeLifecycleRules.ValidateRegister`; verify the origin against
  the persisted `IntakeReceipts` row (receipt exists; channel, token, and
  hash match — `IntakeSourceIdentityConflictException` otherwise) and
  that `IntakeEvaluations` contains the revision for that processed
  receipt (both checks copied from `EfTriageStore.CreateAsync:44-88`);
  enforce one Image intake per origin receipt (return the existing
  record only for an identical origin+VRM, conflict otherwise); allocate
  the per-VRM sequence inside the same transaction using the
  read-increment-save pattern of `EfCaseAcceptanceStore.cs:177-197`
  (create the `ImageIntakeSequences` row on first use; no exhaustion
  branch); build the reference with `ImageIntakeReferenceFormat.Create`;
  append history with BeforeVersion −1 semantics as
  `EfTriageStore.AppendHistory` does for creation.
- `LinkCaseAsync` / `UnlinkCaseAsync` (events `image_intake_case_linked`
  / `image_intake_case_unlinked`), mirroring
  `EfTriageStore.ChangeCaseLinkAsync:661-773`:
  - replay probe first; then reject an operation key already present in
    `CaseWorkflowEvents` or `CaseHistory`;
  - load the image intake, enforce `ExpectedImageIntakeVersion`
    (`ImageIntakeVersionConflictException`);
  - load `CaseWorkflows` row; `CaseMutationGuard.Require(workflow,
    actor, expectedCaseVersion, leaseToken, now)` (lease-holder +
    SHA-256 token hash + case version + not-archived + not-terminal);
  - parse `workflow.State` to `CaseLifecycleState` and enforce
    `ImageIntakeLifecycleRules.IsCaseEligibleForAssociation(state,
    workflow.ReportSentEvidenceId is not null)` for both link and unlink
    (association and its reasoned reversal are both pre-report-only per
    `docs/requirements.md:204`), throwing
    `ImageIntakeCaseNotEligibleException`;
  - set/clear `LinkedCaseId` (link requires currently unlinked; unlink
    requires exactly the supplied case), `CaseMutationGuard.Complete`,
    append a `CaseWorkflowEventEntity` and an `ImageIntakeHistory` row in
    the same transaction; never delete or rewrite any prior row —
    unlink+relink allocates no new reference (the reference lives on the
    `ImageIntakes` row and is permanent).
- Queries: `ListAsync(bool? associated)`, `GetAsync`,
  `GetByReferenceAsync` (exact match on the unique reference column,
  uppercase-trimmed input), plus the Step-1 additions and
  `IImageIntakeOriginResolver`.

Migration (generated with `dotnet ef migrations add
ImageIntakeRegistration`, producing the `.cs`, `.Designer.cs`, and
`PegasusDbContextModelSnapshot.cs` update under
`src/Pegasus.Infrastructure/Persistence/Migrations/`): the three tables,
their indexes/FKs/checks, plus `migrationBuilder.Sql` runtime-role grants
following `20260729199000_RuntimeRoleReconciliation.cs` /
`20260801220500_GrantWebMigrationHistoryRead.cs`: web role
`SELECT, INSERT, UPDATE` on `ImageIntakes` and `ImageIntakeSequences`,
`SELECT, INSERT` on `ImageIntakeHistory`; same for the worker role
(deny nothing new; no DELETE anywhere).

`src/Pegasus.Infrastructure/DependencyInjection.cs` (next to the Triage
registrations at ~line 58-77): `EfImageIntakeStore` scoped;
`IImageIntakeStore`, `IImageIntakeQueries`, `IImageIntakeOriginResolver`
forwarding to it; `IRegisterImageIntake → RegisterImageIntake`,
`ILinkImageIntakeCase → LinkImageIntakeCase`,
`IUnlinkImageIntakeCase → UnlinkImageIntakeCase`.

### Step 3 — Web: intake surfaces (INT-13 registration, labels)

`src/Pegasus.Web/Pages/Intake/Details.cshtml` and `.cshtml.cs`:

- A "Register Image intake" form shown when the receipt retains at least
  one image asset and `GetByOriginReceiptAsync` returns null: staff-typed
  VRM (normalised uppercase, validated by
  `ImageIntakeLifecycleRules.ValidateNormalizedRegistration` semantics),
  required reason, hidden fresh operation key (the page's existing
  `Guid.NewGuid().ToString("N")` idiom). Handler
  `OnPostRegisterImageIntakeAsync` builds the origin via
  `IImageIntakeOriginResolver`, calls `IRegisterImageIntake`, and
  surfaces expected exceptions as page messages (the
  `IsExpected(exception)` pattern of
  `Pages/Triage/Details.cshtml.cs:465-466`).
- When an Image intake exists: show its reference, registered time, and
  outcome label — exactly `Image intake registered` when unlinked and
  `Associated with Case` when linked (`docs/requirements.md:944`) — with
  a link to the Image intake details page.

`src/Pegasus.Web/Pages/Intake/Index.cshtml` and `.cshtml.cs`: after
loading a page of receipts, call `ListByOriginReceiptsAsync` for the
listed ids and override the row outcome label (`Image intake registered`
/ `Associated with Case`) for receipts with a registration; other rows
keep the existing `DecisionLabel` values. `IntakeDecision` itself is not
extended — the labels are presentation over the retained decision plus
the Image-intake record.

### Step 4 — Web: Image intake pages (INT-27/29/30, UI-07)

New folder `src/Pegasus.Web/Pages/ImageIntake/`:

- `Index.cshtml(.cs)`: authorised list
  (`[Authorize(Roles = StaffRoleNames.Administrator + "," +
  StaffRoleNames.Engineer + "," + StaffRoleNames.User)]`, the Triage-page
  convention) of Image intakes with an associated/unassociated filter
  (`ListAsync(bool?)`) and an exact Image Intake Reference search box
  (`GetByReferenceAsync`); rows are full-row focusable links with the
  reference, VRM, outcome label, and registered date/time per
  `docs/requirements.md:944`.
- `Details.cshtml(.cs)`: record, preserved origin (receipt link, source
  channel/token, source hash, evaluation revision — INT-30), full
  append-only history, and the link/unlink actions. The handler copies
  the Triage case-association choreography from
  `Pages/Triage/Details.cshtml.cs:319-425` verbatim in shape: check the
  target case has no foreign active lease, claim a lease via
  `ILeaseCaseForEdit`, call `ILinkImageIntakeCase`/
  `IUnlinkImageIntakeCase` with the lease version+token, release the
  lease on failure, and surface `CaseAssociationUnavailableReason` via
  TempData on lease conflict. Every action requires a typed reason
  (INT-29).

### Step 5 — Web: Case side and UI-07 search

- `src/Pegasus.Web/Pages/Cases/Details.cshtml(.cs)`: an "Image intakes"
  section listing `ListForCaseAsync` results (reference links to
  `ImageIntake/Details`) with a reasoned unlink action, and a link action
  that accepts an Image Intake Reference (resolved via
  `GetByReferenceAsync`, rejecting an already-associated intake). Both
  reuse the same lease choreography as Step 4.
- `src/Pegasus.Web/Pages/Cases/Index.cshtml(.cs)`: when the existing
  `CaseReference` or `Query` input, uppercase-trimmed, exactly matches a
  registered Image Intake Reference, render an Image-intake result row
  (reference, VRM, outcome label, registered timestamp, full-row link to
  its details page) alongside the case results. `CaseSearchFilters` and
  `EfCaseQueryStore` are unchanged — an Image Intake Reference is not a
  Case reference and must not pretend to be one.

### Step 6 — ADR-0018 engine implementation (INT-17)

Core port (Core owns the port per ADR-0018; the peer precedent is
`IVehicleLookupAdapter` in `src/Pegasus.Core/Vehicle/LookupContracts.cs`):

- New `src/Pegasus.Core/ImageIntake/VrmRecognition.cs`:
  - `IVrmRecognitionEngine` — input: image bytes plus the retained
    source-image identity (intake receipt id, intake asset id, storage
    key, content hash); output `VrmRecognitionOutcome`, a closed
    taxonomy distinguishing `Suggested` (one or more candidates, each
    with plate text, supplied confidence, and plate bounds),
    `NoReadableResult`, and `TechnicalFailure`, plus engine key, engine
    version, and pinned model hashes — the operator surface must
    distinguish these and never render an empty value as success
    (`docs/requirements.md:447-452`). The engine never mutates anything.
  - `IVrmSuggestionStore` port to persist each run's outcome bound to
    its source image, and `ISuggestVehicleRegistration` use case:
    read the asset bytes via `IIntakeArtifactStore.ReadAsync` (verify
    content hash), run the engine, persist the suggestion record, return
    the outcome. Abstention is a first-class recorded outcome.

Infrastructure:

- New `src/Pegasus.Infrastructure/Vision/` folder:
  - `OnnxVrmRecognitionEngine.cs` (+ `PlateDetector.cs`,
    `PlateRecognizer.cs`): YOLOv9 plate detection (letterboxed input,
    confidence threshold) then global CCT recognition on each crop;
    below-threshold or empty results map to `NoReadableResult`; any
    decode/inference error maps to `TechnicalFailure`; thresholds are
    named constants documented as provisional pending open decision 1.
    Sessions are lazy singletons (one `InferenceSession` per model).
  - `Models/` containing the two vendored `.onnx` files plus
    `vision-models-manifest.json` recording each file's origin URL and
    SHA-256 (the ADR's origin/hash review); the engine verifies embedded
    bytes against the manifest hashes before creating a session. Models
    ship as `EmbeddedResource` in
    `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj`.
  - Raster decoding: add SkiaSharp (MIT; origin/hash/RID reviewed the
    same way) — the repo currently has no raster decoder (image
    dimensions come from the intake readers, not a bitmap library).
- Package changes: `Microsoft.ML.OnnxRuntime` and `SkiaSharp` in
  `Pegasus.Infrastructure.csproj`; regenerate every affected
  `packages.lock.json` (CI restores with `--locked-mode`).
- `.gitattributes`: add `*.onnx binary` (precedent: the vendored
  `provider-domains.v1.json` binary entry).
- Suggestion persistence: `ImageVrmSuggestionEntity` /
  `ImageVrmSuggestions` table in `PegasusDbContext.cs` (IntakeReceiptId
  + IntakeAssetId FKs restrict, StorageKey, ContentHash, EngineKey,
  EngineVersion, ModelHashes, Outcome, SuggestedVrm?, Confidence?,
  Actor, unique OperationKey, OccurredAtUtc, StaffDisposition —
  pending/confirmed/dismissed — kept separate from confirmed case data
  per `docs/requirements.md:449-452`); second migration
  `ImageVrmSuggestions` with web-role `SELECT, INSERT, UPDATE` grants.
- DI: `IVrmRecognitionEngine` singleton, `EfImageVrmSuggestionStore`
  scoped, `ISuggestVehicleRegistration` scoped, in
  `DependencyInjection.cs`.
- Web surfacing (`Pages/Intake/Details.cshtml(.cs)`): a per-image
  "Suggest registration" action invoking `ISuggestVehicleRegistration`;
  the result panel shows suggestion-vs-no-readable-vs-failure
  distinctly; a suggestion can prefill (never submit) the Step-3
  register form's VRM field; registering with a prefill marks the
  suggestion's staff disposition confirmed. No suggestion ever
  registers, links, or mutates anything on its own, and nothing runs
  automatically in the intake pipeline — `INT-17` stays non-blocking.

### Step 7 — Local corpus evaluation harness (open decision 1 remainder)

- New `tests/Pegasus.IntegrationTests/VrmRecognitionCorpusEvaluationTests.cs`
  marked `[Trait("Category", "Corpus")]`, modelled on
  `MultiFormatGenuineCorpusWebTests.cs` (resolves `corpus/` at the
  repository root, skips with an explicit reason when absent — `corpus/`
  is gitignored and absent in CI; CI already filters
  `Category!=Corpus`). It reads locally prepared, gitignored cohort and
  holdout manifests under `corpus/` (frozen file-hash lists with
  case-attributed VRM labels; labels never leave the machine), runs the
  engine over the labelled cohort, and writes
  `artifacts/vrm-recognition-eval/<run-id>/report.json` with coverage
  (suggestion rate), wrong-suggestion rate (primary), and abstention
  rate at the candidate thresholds. The holdout is not evaluated until
  the proposed bar is fixed from the cohort, then evaluated once.
- `docs/open-decisions.md` item 1: append the run's summary numbers,
  corpus sizes, commit hash, and the proposed provisional
  accuracy/abstention bar — a proposal for operator review, not an
  acceptance; the item stays open exactly as its text requires.

### Step 8 — Tests (CI, no fabricated domain imagery)

- `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeReferenceFormatTests.cs`:
  `AB12CDE` + 1 → `AB12CDE-01`, 9 → `-09`, 99 → `-99`, 100 → `-100`
  (expansion, no reuse), 0/negative rejected, blank VRM rejected.
- `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeLifecycleTests.cs`:
  register/link validation rules (VRM charset and length, SHA-256 shape,
  operation-key/reason bounds, lease token required, casework right
  required); `IsCaseEligibleForAssociation` truth table across all nine
  `CaseLifecycleState` values × report-sent evidence; use-case guards
  (link when already linked, unlink wrong case, register replay
  short-circuit) against a fake store — mirrors
  `tests/Pegasus.Core.Tests/Triage/TriageReplayTests.cs` in shape.
- `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs`
  (EF store, using the existing integration-test database fixtures):
  registration allocates `-01`; two concurrent registrations for the
  same VRM get distinct sequential references (serializable-transaction
  race, the same style as the Case-sequence usage); replay probe returns
  the identical committed result for the same key+fingerprint and
  throws `ImageIntakeOperationConflictException` for the same key with a
  different fingerprint; second registration for the same receipt with a
  different VRM conflicts; link enforces case lease, case version, image
  intake version, and rejects a `PostReport`/report-sent case with
  `ImageIntakeCaseNotEligibleException`; unlink then relink to another
  case allocates no new reference; history is append-only with correct
  before/after versions and `AfterLinkedCaseId`; a `CaseWorkflowEvents`
  row is written per link/unlink (mirror
  `QdosTriageCaseAssociationIntegrationTests.cs`).
- `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs`
  (`IntakeWebApplicationFactory` pattern): upload an image-bearing
  intake (existing `TinyJpegBase64`/`TinyPngBase64` fixtures), register
  an Image intake with a typed VRM from `Intake/Details`, assert the
  `Image intake registered` label on the intake list/detail, seed an
  eligible case, link (label becomes `Associated with Case`), find the
  intake by exact reference on `ImageIntake/Index` and via
  `Cases/Index`, unlink from the Case side, and assert the reference and
  history survive.
- `tests/Pegasus.IntegrationTests/VrmRecognitionEngineTests.cs` (CI-safe,
  repository fixtures only — no fabricated vehicle images, per the
  synthetic-imagery ban in `docs/requirements.md:457`): manifest hashes
  match the embedded model bytes; the engine loads and, on the tiny
  plate-free fixture images, returns `NoReadableResult`/abstention and
  never a suggestion; corrupt bytes return `TechnicalFailure`; the
  suggestion store persists and disposition-updates a record.
  Accuracy claims live exclusively in the Step-7 corpus run.

### Step 9 — PR mechanics (docs/engineering.md task workflow)

The PR into `dev` removes this task's claim line from `NOW.md` (merging
`origin/dev` and taking its `NOW.md` wholesale if conflicted). CI runs
the full build/test lanes (all changes are under `src/`, `tests/`, and
project/lock files, which match the build-path pattern in
`.github/workflows/ci.yml`). Before merge, an agent that did not
implement the task answers the two review questions against this plan.
After merge, one maintenance push deletes
`docs/temp-plans/image-led-intake.md`, then the worktree and branch are
removed.

## What does not change

- `IIntakeTriageMatcher` / `NoAcceptedIntakeTriageMatcher` and the whole
  Triage module: untouched.
- No automatic association ships: `INT-28`/`INT-32` remain unimplemented
  and separately gated; reading a plate never links, registers, creates,
  or identifies anything. Registration and link/unlink/relink are
  manual, reasoned staff actions.
- No external call, credential, or image egress of any kind; the engine
  is in-process only, per ADR-0018.
- `IntakeDecision` and the intake processing pipeline
  (`src/Pegasus.Core/Intake/ProcessIntake.cs`) keep their decision
  taxonomy; `Image intake registered` / `Associated with Case` are
  presentation labels derived from the Image-intake record.
- An Image intake is never allocated a Case/PO and never becomes a Case;
  `EfCaseAcceptanceStore` and Case identity allocation are untouched.
- `CaseSearchFilters`/`EfCaseQueryStore` schema of case search is
  unchanged; the reference search is an additive lookup.
- `corpus/` stays gitignored; no corpus content, label, or VRM enters
  the repository. Cohort/holdout manifests and evaluation reports stay
  local.
- `docs/requirements.md`, `docs/capabilities.md`, `CONTEXT.md`, and
  ADR-0018 already state the target behaviour; only
  `docs/open-decisions.md` item 1 receives appended evaluation evidence
  and the proposed provisional bar.
- Open decision 1's threshold acceptance is not closed by this task.

## Verification

Per step:

- Steps 1-2: `ImageIntakeReferenceFormatTests`,
  `ImageIntakeLifecycleTests`, `ImageIntakePersistenceTests` green;
  migration applies cleanly to a fresh database in the integration
  fixtures; `AzureSqlRuntimeRoleMigrationTests` (pinned to its
  historical migration point) still green.
- Steps 3-5: `ImageIntakeWebTests` green, including the exact
  `Image intake registered` / `Associated with Case` label strings and
  the reference search journeys from both `ImageIntake/Index` and
  `Cases/Index`; existing `QdosIntakeWebTests`, `CasesIndexWebTests`,
  `CaseDetailsWebTests`, and Triage web tests unbroken.
- Step 6: `VrmRecognitionEngineTests` green in CI without any corpus;
  model manifest hash verification passes; no new network access exists
  (the engine takes bytes in, returns a value out).
- Step 7 (local only, this machine): `dotnet test ./Pegasus.slnx
  --configuration Release --filter "Category=Corpus"` with `corpus/`
  present runs the evaluation and writes the report; the report numbers
  are transcribed into `docs/open-decisions.md` item 1;
  `./scripts/Test-DocumentationLinks.ps1` passes after the docs edit.

Final, before the PR:

- `dotnet restore ./Pegasus.slnx --locked-mode` (lock files regenerated
  for the new packages), `dotnet build ./Pegasus.slnx --configuration
  Release`, and `dotnet test ./Pegasus.slnx --configuration Release
  --filter "Category!=Corpus"` all green locally, matching the CI lanes.
- Green CI on the PR, then the independent two-question review.

## Risks and unknowns

- Model bytes (~10-30 MB total estimated) are committed as plain git
  binaries — the repository uses no LFS (`git lfs ls-files` is empty);
  growth is permanent. The exact fast-alpr/fast-plate-ocr release-asset
  file names, URLs, and hashes are pinned at implementation time into
  `vision-models-manifest.json`.
- `Microsoft.ML.OnnxRuntime` native binaries enlarge the publish output
  by tens of MB, and resident inference sessions add memory on the
  deployed App Service plan; the singleton/lazy design bounds this to
  one session per model, but plan-size headroom is unverified.
- CI restores with `--locked-mode`: forgetting to regenerate any
  `packages.lock.json` fails CI immediately.
- The repository holds no genuine plate-bearing test image, and
  fabricating one is prohibited; CI therefore proves loading, hash
  pinning, abstention, and contract behaviour only — accuracy evidence
  is exclusively the local corpus run.
- The corpus's case-level VRM attribution format is asserted by
  ADR-0018 but unverified until the local run; preparing the labelled
  cohort/holdout manifests may need a local mapping step.
- SkiaSharp is a new native dependency (MIT); origin/hash/RID review per
  ADR-0018 applies to it as well.
