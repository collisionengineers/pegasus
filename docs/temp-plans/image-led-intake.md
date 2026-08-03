# Task plan: image-led-intake

Branch `task/image-led-intake`, claimed in `NOW.md` on `origin/dev`.

Scope notes:

- The operator decided open decision 1's mechanism on 2026-08-03 (ADR-0018,
  in-process ONNX engine) and added full implementation of that engine to
  this task. Threshold acceptance (open decision 1's remainder) stays open:
  this task produces the first local evaluation evidence and the provisional
  bar, not an accepted threshold.
- A second operator direction round on 2026-08-03 (recorded here; doc edits
  in Step 6) settled the remaining seams:
  1. Registration is offered/performed only for image-only receipts (no
     instruction evidence), per `docs/requirements.md:118`.
  2. Registration writes a real intake decision: `IntakeDecision` gains
     `ImageIntakeRegistered`; registered receipts leave the `Needs sorting`
     queue and counts through the normal decision mechanics.
  3. `Associated with Case` is derived from the receipt's current case
     association, not a further decision record per link event.
  4. The recognition outcome taxonomy is `Suggested` / `NoReadableResult` /
     `TechnicalFailure` / `Unavailable` (engine dependency unusable),
     satisfying `docs/requirements.md:447-448` literally.
  5. Case association RIDES THE EXISTING intake-receipt manual association
     (`IntakeManualAssociationEntity`, `ILinkIntake`/`IReverseIntakeLink`,
     the lease choreography already live on `Pages/Intake/Details`). The
     Image intake record carries NO own `LinkedCaseId` and no link/unlink
     use cases — one link, one truth, no parallel mechanism (duplicate
     business implementation is a repository stop condition).
  6. A receipt already manually linked before registration yields an Image
     intake born `Associated with Case`; eligibility governs new
     associations, not past ones.
  7. Scanning is AUTOMATIC: no staff "suggest" button. The intake pipeline
     scans image-only material as part of processing, in whatever host runs
     `ProcessIntake` (Web for manual upload today).
  8. FULL AUTO at the provisional bar: a confident unambiguous read
     auto-registers the Image intake (allocates `{VRM}-NN`), and when
     exactly one eligible pre-report instructed Case carries that confirmed
     VRM with no contradictory identity evidence, auto-associates it
     (`INT-28`/`INT-32` activate in this task). Staff review and reasoned
     reversal remain. The provisional bar is active from the first corpus
     evaluation; operator review of those numbers later confirms or adjusts
     it (open decision 1 stays open).
  9. Both flows are covered: no matching Case → the registered Image intake
     awaits instruction (the operational "image-initiated case", technically
     pre-Case per `docs/operator-notes.md:82`); matching Case → the images
     arrive as evidence on the existing Case via auto-association.
  10. Manual paths remain for everything below the bar: staff-typed VRM
      registration (prefilled from the best suggestion), reasoned
      link/unlink with typed reference + same-VRM candidate list, and
      explicit suggestion dismissal with a reason.

## What changes

### Already done on this branch

- Claim commit and initial plan (`e9a74ad`, `5a6832f`).
- `e84097d` — decision-1 candidate evidence appended to
  `docs/open-decisions.md` item 1.
- `b165860` — `docs/adr/0018-in-process-onnx-vrm-recognition.md` accepted,
  plus the `docs/open-decisions.md` item-1 rewrite and the
  `docs/capabilities.md` `INT-17` row update.
- `4ed2c67` — `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` and
  `ImageIntakeLifecycle.cs`. Step 1 reworks this module to the
  ride-the-receipt architecture; fixed points that survive:
  `ImageIntakeReferenceFormat.Create` (`{VRM}-{NN}`, two-digit minimum,
  expands past `-99`, sequences start at 1, never reused), the register
  replay probe semantics, `ImageIntakeLifecycleRules.ValidateNormalizedRegistration`,
  `ValidateOrigin`, and `IsCaseEligibleForAssociation`
  (`NotReady`/`Held`/`Review`/`ReportPreparation`, no report-sent evidence).

### Step 1 — Core ImageIntake contract rework (ride the receipt)

`src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` and
`ImageIntakeLifecycle.cs`:

- Remove `ImageIntakeCaseLinkRequest`, `ILinkImageIntakeCase`,
  `IUnlinkImageIntakeCase`, `LinkImageIntakeCase`, `UnlinkImageIntakeCase`,
  `ImageIntakeVersionConflictException`, `ImageIntakeHistoryEntry`, and the
  `Version`/`LinkedCaseId` members: an `ImageIntakes` row is immutable after
  creation and association lives on the origin receipt. Keep
  `ImageIntakeCaseNotEligibleException` and `IsCaseEligibleForAssociation` —
  they now guard the receipt link path and the auto-associate predicate.
- `ImageIntakeRecord(Id, Origin, NormalizedVehicleRegistration,
  ImageIntakeReference)`; `ImageIntakeSummary`/`ImageIntakeDetail` expose
  `AssociatedCaseId`/case reference DERIVED from the origin receipt's
  current association (`IntakeReceipt.CurrentCaseId` semantics) at query
  time.
- `RegisterImageIntakeRequest` keeps `(Origin, NormalizedVehicleRegistration,
  Actor, OperationKey, Reason)`; `ValidateRegister` accepts a staff actor
  with `PerformCasework` OR the pipeline's `ActorKind.SystemWorker`
  (automatic registration), mirroring `StaffAuthorization`'s
  `ExecuteSystemWork` boundary.
- `IImageIntakeQueries`: `ListAsync(bool? associated)` (associated derived
  via receipt association), `GetAsync`, `GetByReferenceAsync`,
  `GetByOriginReceiptAsync`, `ListByOriginReceiptsAsync(receiptIds)`,
  `ListForCaseAsync(caseId)` (image intakes whose origin receipt is
  currently associated with the case), and `SearchByRegistrationAsync(vrm)`
  for the UI-07 VRM search.
- `IImageIntakeOriginResolver.ResolveOriginAsync(intakeReceiptId, ...)`
  unchanged from the prior plan (the web/pipeline-facing receipt lacks the
  evaluation revision `ImageIntakeOrigin` requires).
- `IImageIntakeStore : IImageIntakeQueries` keeps only
  `ProbeRegisterReplayAsync` + `RegisterAsync`.

### Step 2 — Core: engine port and the automatic scan-register-associate flow

New `src/Pegasus.Core/ImageIntake/VrmRecognition.cs` (Core owns the port
per ADR-0018; peer precedent `IVehicleLookupAdapter`):

- `IVrmRecognitionEngine`: image bytes + retained source-image identity in;
  `VrmRecognitionOutcome` out — closed taxonomy `Suggested` (candidates
  with plate text, supplied confidence, plate bounds), `NoReadableResult`,
  `TechnicalFailure`, `Unavailable` (model bytes fail hash verification /
  session cannot initialise), plus engine key, engine version, pinned model
  hashes. The engine never mutates anything.
- `IVrmSuggestionStore`: persist each asset's outcome bound to its source
  image (receipt id, asset id, storage key, content hash), with staff/system
  disposition updates.
- Provisional-bar constants (named, documented as provisional pending open
  decision 1): per-candidate confidence floor for a "confident read" and
  the requirement of exactly ONE distinct normalised VRM across the
  receipt's confident reads.

Automatic flow, integrated where image-only receipts currently fall to
`NeedsSorting` (`src/Pegasus.Core/Intake/ProcessIntake.cs:262-283`):

- For a receipt whose retained assets are images only (no instruction
  evidence): scan each retained image via the engine, record every outcome
  through `IVrmSuggestionStore`.
- Exactly one distinct confident VRM and no contradictory identity
  evidence → decision `ImageIntakeRegistered` with an atomic registration
  (`RegisterImageIntake`, `SystemWorker` actor, operation key derived from
  the receipt id so reprocessing replays instead of duplicating).
- Then the auto-associate predicate (`INT-28`): candidate Cases whose
  confirmed vehicle registration equals the read VRM, not archived,
  eligible per `IsCaseEligibleForAssociation`. Exactly one candidate →
  associate through the SAME receipt-association write path as the manual
  link (system actor, generated reason naming the match evidence);
  zero or multiple → leave unlinked/awaiting staff.
- Any non-confident, ambiguous, multi-VRM, `NoReadableResult`,
  `TechnicalFailure`, or `Unavailable` situation → `NeedsSorting` exactly
  as today, suggestions recorded, staff paths take over. `INT-17` stays
  non-blocking: no engine state ever blocks or fails intake itself.
- `IntakeDecision` gains `ImageIntakeRegistered`; `IntakeQueueCounts` and
  the queue pages treat it as its own outcome (registered receipts leave
  `Needs sorting` naturally).

Manual registration (below the bar): a staff use case that mutates the
receipt decision `NeedsSorting → ImageIntakeRegistered` through the
`EfIntakeMutationStore.ExecuteAsync` pattern (serializable transaction,
operation-key replay, `intake_mutation` history event
`image_intake_registered`) and creates the `ImageIntakes` row atomically.

### Step 3 — Infrastructure: persistence, receipt-link eligibility, migration, DI

`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`:

- `ImageIntakeEntity` (`ImageIntakes`): Id, OriginReceiptId, SourceChannel,
  ExternalReceiptToken, SourceHash (64 fixed), EvaluationRevisionId,
  NormalizedVehicleRegistration (max 20), ImageIntakeReference (max 30),
  CreatedAtUtc, CreatedByActorKind/SubjectId, CreationOperationKey
  (unique), RequestFingerprint (64 fixed); unique indexes on
  OriginReceiptId, (SourceChannel, ExternalReceiptToken), and
  ImageIntakeReference; FK OriginReceiptId → `IntakeReceipts` (restrict).
  No LinkedCaseId, no Version — the row is immutable after creation and
  association is the receipt's. (The prior plan's `ImageIntakeHistory`
  table is dropped: the registration event lives in the receipt's mutation
  history; replay resolves via `CreationOperationKey` + fingerprint.)
- `ImageIntakeSequenceEntity` (`ImageIntakeSequences`): key
  NormalizedVehicleRegistration (max 20), LastAllocatedSequence, check
  `>= 0` only — deliberately no ceiling (the reference format expands past
  `-99` instead of exhausting, unlike `CK_CaseSequences_...<= 999`).
- `ImageVrmSuggestionEntity` (`ImageVrmSuggestions`): IntakeReceiptId +
  IntakeAssetId (FKs restrict), StorageKey, ContentHash, EngineKey,
  EngineVersion, ModelHashes, Outcome, SuggestedVrm?, Confidence?,
  unique OperationKey, OccurredAtUtc, Disposition
  (pending / confirmed / dismissed) with disposing actor kind+id and typed
  reason — kept separate from confirmed case data per
  `docs/requirements.md:449-452`; automatic registration marks the used
  suggestion confirmed with the system actor, staff registration with the
  staff actor.

`src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` (new):

- `RegisterAsync`: serializable transaction; replay probe by trimmed
  `CreationOperationKey` (`EnsureReplay` compares SHA-256 request
  fingerprint, throws `ImageIntakeOperationConflictException` on
  mismatch); verify the origin against the persisted `IntakeReceipts` row
  (exists; channel/token/hash match; `IntakeEvaluations` holds the
  revision — both checks copied from `EfTriageStore.CreateAsync:44-88`);
  verify the receipt is image-only and not already registered (one Image
  intake per origin receipt: identical origin+VRM replays, different
  conflicts); allocate the per-VRM sequence inside the transaction with the
  read-increment-save pattern of `EfCaseAcceptanceStore.cs:177-197` (create
  the row on first use, no exhaustion branch); build the reference with
  `ImageIntakeReferenceFormat.Create`; write the receipt decision change +
  mutation-history event in the same transaction for the manual path (the
  automatic path persists the decision with the receipt itself).
- Queries incl. the Step-1 additions; association fields derived by joining
  the origin receipt's `ManualAssociation`/accepted case (the
  `IntakeReceipt.CurrentCaseId` rule).

`src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs`:

- `LinkAsync` gains the eligibility guard: when the receipt has a
  registered Image intake, load the target case workflow state +
  report-sent evidence inside the transaction and enforce
  `ImageIntakeLifecycleRules.IsCaseEligibleForAssociation`, throwing
  `ImageIntakeCaseNotEligibleException`. `ReverseLinkAsync` unchanged
  (reasoned reversal stays available; requirements 204 permits reversal
  before report delivery — the same guard applies on re-link, not on
  reversal).
- A store entry point for the automatic association (system actor, no
  staff lease — the pipeline owns the receipt at that moment; same
  serializable transaction + operation-key replay + history event
  `intake_case_linked`).

Migration `ImageIntakeRegistration` (`dotnet ef migrations add`, producing
`.cs`, `.Designer.cs`, and the snapshot update under
`src/Pegasus.Infrastructure/Persistence/Migrations/`): the three tables,
indexes/FKs/checks, plus `migrationBuilder.Sql` runtime-role grants
following `20260729199000_RuntimeRoleReconciliation.cs`: web and worker
roles `SELECT, INSERT, UPDATE` on `ImageIntakes` (UPDATE not needed —
grant `SELECT, INSERT` only), `SELECT, INSERT, UPDATE` on
`ImageIntakeSequences` and `ImageVrmSuggestions`; no DELETE anywhere.

`src/Pegasus.Infrastructure/DependencyInjection.cs` (next to the Triage
registrations at ~line 58-77): `EfImageIntakeStore` scoped with
`IImageIntakeStore`/`IImageIntakeQueries` forwards;
`IImageIntakeOriginResolver`; `IRegisterImageIntake → RegisterImageIntake`;
`IVrmRecognitionEngine` singleton (lazy sessions);
`EfImageVrmSuggestionStore` scoped. Worker DI mirrors what the durable
intake path needs.

### Step 4 — Infrastructure: ONNX engine (ADR-0018)

New `src/Pegasus.Infrastructure/Vision/`:

- `OnnxVrmRecognitionEngine.cs` (+ `PlateDetector.cs`,
  `PlateRecognizer.cs`): YOLOv9 plate detection (letterboxed input,
  confidence threshold) then global CCT recognition per crop;
  below-threshold or empty → `NoReadableResult`; decode/inference error →
  `TechnicalFailure`; model-hash mismatch or session-initialisation
  failure → `Unavailable` (and the pipeline records it without blocking
  intake). Sessions are lazy singletons, one `InferenceSession` per model.
- `Models/` with the two vendored `.onnx` files plus
  `vision-models-manifest.json` (origin URL + SHA-256 per file, per the
  ADR's origin/hash review); bytes ship as `EmbeddedResource` in
  `Pegasus.Infrastructure.csproj` and are verified against the manifest
  before session creation.
- Raster decoding via SkiaSharp (MIT; origin/hash/RID reviewed the same
  way) — the repo currently has no raster decoder.
- Package changes: `Microsoft.ML.OnnxRuntime`, `SkiaSharp`; regenerate
  every affected `packages.lock.json` (CI restores `--locked-mode`).
- `.gitattributes`: `*.onnx binary` (precedent: the vendored
  `provider-domains.v1.json` entry).

### Step 5 — Web surfaces

`src/Pegasus.Web/Pages/Intake/Details.cshtml(.cs)`:

- Per-image suggestion panel (auto-recorded outcomes) distinguishing all
  four outcomes; never renders an empty value as success
  (`docs/requirements.md:447-452`). Explicit per-suggestion dismiss action
  with a typed reason.
- For an image-only receipt without a registration (below-bar cases): the
  staff register form — VRM prefilled from the best pending suggestion,
  staff-typed otherwise, required reason, fresh operation key, handler
  calling `IRegisterImageIntake` with the `IsExpected(exception)` message
  pattern of `Pages/Triage/Details.cshtml.cs:465-466`. Instruction-bearing
  receipts never offer it.
- When registered: reference, registered time, outcome label — exactly
  `Image intake registered` when the receipt is unassociated and
  `Associated with Case` when associated (`docs/requirements.md:944`) —
  and a link to the Image intake details page. The EXISTING link/reverse
  choreography stays the association surface; its target picker gains a
  same-VRM eligible-case candidate list beside the typed reference (still
  fully manual).

`src/Pegasus.Web/Pages/Intake/Index.cshtml(.cs)`: `ImageIntakeRegistered`
flows through `DecisionLabel` as its own decision; rows for associated
receipts show `Associated with Case` derived from the association; queue
counts follow the decision (registered receipts have left `Needs sorting`).

New `src/Pegasus.Web/Pages/ImageIntake/` (Triage-page authorization
convention `[Authorize(Roles = Administrator + Engineer + User)]`):

- `Index.cshtml(.cs)`: list with associated/unassociated filter, exact
  Image Intake Reference search, and VRM search
  (`SearchByRegistrationAsync`); rows are full-row focusable links with
  reference, VRM, outcome label, registered date/time per
  `docs/requirements.md:944`.
- `Details.cshtml(.cs)`: record, preserved origin (receipt link, source
  channel/token, source hash, evaluation revision — `INT-30`), the
  suggestions that led to it, current association, and navigation to the
  origin receipt's link/unlink actions (association acts on the receipt —
  one mechanism, surfaced from here for convenience).

`src/Pegasus.Web/Pages/Cases/Details.cshtml(.cs)`: an "Image intakes"
section (`ListForCaseAsync`) with reference links and the reasoned unlink
(existing reverse choreography); a link action accepting an Image Intake
Reference (resolved via `GetByReferenceAsync`, rejecting an
already-associated intake, acting on the origin receipt).

`src/Pegasus.Web/Pages/Cases/Index.cshtml(.cs)`: search unification per
the operator's direction — a bare VRM search returns matching Cases AND
Image intakes; a record-type filter with exactly `All`, `Instructions`,
`Images` (the exact option set `docs/requirements.md:928-929` mandates);
an uppercase-trimmed input exactly matching an Image Intake Reference
renders that intake's row. Case rows for cases with linked image intakes
show an images-arrived indicator (`INT-32` staff visibility).
`CaseSearchFilters`/`EfCaseQueryStore` case-search schema unchanged — the
image-intake results are an additive lookup beside it.

### Step 6 — Documentation edits (operator-directed, same task)

- `docs/requirements.md:118`: define "usable normalised VRM" as staff-typed
  or an engine read meeting the current accepted (initially provisional)
  bar.
- `docs/requirements.md:443`: scoped amendment — automatic registration of
  a pre-Case Image intake (allocating its Image Intake Reference) from a
  confident engine read at the current bar is permitted; everything else in
  the suggestion-first boundary stands (no Case creation/identity, no
  confirmed-registration overwrite, no readiness/workflow mutation, no EVA
  selection).
- `docs/capabilities.md`: `INT-28`/`INT-32` rows move from `Next / 0.2.0`
  to current scope with the activation note (automatic register+associate
  at the provisional bar, operator-directed 2026-08-03); `INT-17` row note
  gains "automatic in-pipeline scan".
- `docs/adr/README.md` ADR-0018 index row: dated status note that the
  operator exercised the separate `INT-28`/`INT-32` gate on 2026-08-03
  (automatic register+associate at the provisional bar; body unchanged).
- `docs/open-decisions.md` item 1: record that automatic actions run at the
  provisional bar from the first evaluation; operator acceptance of the
  reviewed numbers still closes the item.
- `CONTEXT.md`: no meaning change needed (Image intake stays pre-Case; the
  association derivation is implementation).

### Step 7 — Local corpus evaluation harness (open decision 1 remainder)

- New `tests/Pegasus.IntegrationTests/VrmRecognitionCorpusEvaluationTests.cs`
  marked `[Trait("Category", "Corpus")]`, modelled on
  `MultiFormatGenuineCorpusWebTests.cs` (resolves `corpus/` at the repo
  root, skips with reason when absent; CI filters `Category!=Corpus`).
  Reads locally prepared, gitignored cohort/holdout manifests under
  `corpus/` (frozen file-hash lists with case-attributed VRM labels; labels
  never leave the machine), runs the engine over the cohort, writes
  `artifacts/vrm-recognition-eval/<run-id>/report.json` with suggestion
  rate, wrong-suggestion rate (primary), and abstention rate at candidate
  thresholds. The holdout is evaluated once, only after the bar is fixed
  from the cohort.
- The run's numbers set the provisional bar constants (Step 2) and are
  transcribed with corpus sizes + commit hash into `docs/open-decisions.md`
  item 1 for operator review.

### Step 8 — Tests (CI, no fabricated domain imagery)

- `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeReferenceFormatTests.cs`:
  `AB12CDE`+1 → `AB12CDE-01`, 9 → `-09`, 99 → `-99`, 100 → `-100`
  (expansion, no reuse), 0/negative/blank rejected.
- `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeLifecycleTests.cs`:
  validation rules (VRM charset/length, SHA-256 shape, operation-key/reason
  bounds, staff-or-SystemWorker actor); `IsCaseEligibleForAssociation`
  truth table across all `CaseLifecycleState` values × report-sent
  evidence; register replay short-circuit against a fake store.
- `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs`:
  the pipeline predicate against a FAKE engine (the port makes this
  CI-safe without plate imagery): one confident VRM → registered; two
  distinct VRMs → `NeedsSorting` + suggestions recorded; below-bar →
  `NeedsSorting`; engine `Unavailable`/`TechnicalFailure` → non-blocking
  `NeedsSorting`; exactly one eligible same-VRM case → auto-associated
  with system actor; zero/multiple candidates or ineligible case → no
  association; reprocessing replays (no duplicate registration).
- `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs`
  (existing integration-test database fixtures): registration allocates
  `-01`; concurrent same-VRM registrations get distinct sequential
  references; replay probe returns the identical committed result for the
  same key+fingerprint and throws `ImageIntakeOperationConflictException`
  for a different fingerprint; a second registration for the same receipt
  conflicts; receipt link with a registered Image intake enforces
  eligibility (`ImageIntakeCaseNotEligibleException` on a post-report
  case) while reversal stays allowed; association derivation follows the
  receipt (`Image intake registered` ↔ `Associated with Case`); the
  migration applies cleanly; `AzureSqlRuntimeRoleMigrationTests` stays
  green.
- `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs`
  (`IntakeWebApplicationFactory`, existing `TinyJpegBase64`/`TinyPngBase64`
  fixtures — plate-free, so the real engine abstains and the below-bar
  staff path is exercised end-to-end): upload image-only intake → staff
  register with typed VRM → `Image intake registered` label on
  list/detail → seed an eligible case → link via the receipt choreography
  → `Associated with Case` → find by exact reference and by VRM on
  `ImageIntake/Index` and `Cases/Index` (including the
  `All`/`Instructions`/`Images` filter) → unlink from the Case side →
  reference and record survive. A fake-engine web test covers the full-auto
  journey (upload → auto-registered → auto-associated → staff reversal).
- `tests/Pegasus.IntegrationTests/VrmRecognitionEngineTests.cs` (real
  engine, CI-safe): manifest hashes match embedded bytes; plate-free
  fixture images → abstention, never a suggestion; corrupt bytes →
  `TechnicalFailure`; tampered model bytes → `Unavailable`; the suggestion
  store persists outcomes and disposition updates. Accuracy claims live
  exclusively in the Step-7 corpus run.

### Step 9 — PR mechanics (docs/engineering.md task workflow)

The PR into `dev` removes this task's claim line from `NOW.md` (merging
`origin/dev` and taking its `NOW.md` wholesale if conflicted). CI runs the
full build/test lanes. Before merge, an agent that did not implement the
task answers the two review questions against this plan. After merge, one
maintenance push deletes `docs/temp-plans/image-led-intake.md`, then the
worktree and branch are removed.

## What does not change

- `IIntakeTriageMatcher` / `NoAcceptedIntakeTriageMatcher` and the whole
  Triage module: untouched.
- The receipt manual-association mechanism keeps its contract; it gains
  only the Image-intake eligibility guard and a system-actor entry point.
- No external call, credential, or image egress of any kind; the engine is
  in-process only, per ADR-0018.
- An Image intake is never allocated a Case/PO and never becomes a Case;
  `EfCaseAcceptanceStore` and Case identity allocation are untouched.
- `CaseSearchFilters`/`EfCaseQueryStore` case-search schema is unchanged;
  image-intake search results are an additive lookup.
- `corpus/` stays gitignored; no corpus content, label, or VRM enters the
  repository; cohort/holdout manifests and evaluation reports stay local.
- Open decision 1's threshold acceptance is not closed by this task; the
  provisional bar is active but explicitly pending operator review.

## Verification

Per step:

- Steps 1-3: `ImageIntakeReferenceFormatTests`, `ImageIntakeLifecycleTests`,
  `AutomaticImageIntakeTests`, `ImageIntakePersistenceTests` green;
  migration applies cleanly to a fresh database;
  `AzureSqlRuntimeRoleMigrationTests` (pinned) green.
- Step 5: `ImageIntakeWebTests` green, including the exact
  `Image intake registered` / `Associated with Case` strings, the
  `All`/`Instructions`/`Images` filter, and both search journeys; existing
  `QdosIntakeWebTests`, `CasesIndexWebTests`, `CaseDetailsWebTests`, and
  Triage web tests unbroken.
- Step 4/8: `VrmRecognitionEngineTests` green in CI without any corpus; no
  new network access exists (bytes in, value out).
- Step 6: `./scripts/Test-DocumentationLinks.ps1` passes after the docs
  edits.
- Step 7 (local only): `dotnet test ./Pegasus.slnx --configuration Release
  --filter "Category=Corpus"` with `corpus/` present writes the report;
  numbers transcribed into `docs/open-decisions.md` item 1 and into the
  provisional-bar constants.

Final, before the PR: `dotnet restore ./Pegasus.slnx --locked-mode` (lock
files regenerated), `dotnet build ./Pegasus.slnx --configuration Release`,
`dotnet test ./Pegasus.slnx --configuration Release --filter
"Category!=Corpus"` all green locally; green CI; independent two-question
review.

## Risks and unknowns

- Model bytes (~10-30 MB estimated) are committed as plain git binaries —
  no LFS; growth is permanent. Exact release-asset names/URLs/hashes are
  pinned at implementation time into `vision-models-manifest.json`.
- `Microsoft.ML.OnnxRuntime` native binaries enlarge the publish output by
  tens of MB and resident sessions add memory on the Container Apps plan;
  the lazy-singleton design bounds this, but headroom is unverified and
  scale-to-zero cold starts pay the model load inside intake processing.
- Automatic registration/association runs at a bar the operator has NOT
  yet reviewed (first corpus run sets it): a wrong plate before review can
  auto-register a wrong-VRM reference (permanent, never reused) or link to
  the wrong Case (reversible, but permanent history). Wrong-suggestion
  rate is the primary evaluation measure for exactly this reason.
- The repository holds no genuine plate-bearing image and fabricating one
  is banned: CI proves loading, hash pinning, abstention, contract, and
  (via the fake engine) the full auto pipeline — accuracy evidence is
  exclusively the local corpus run.
- The corpus's case-level VRM attribution format is asserted by ADR-0018
  but unverified until the local run; cohort/holdout preparation may need
  a local mapping step.
- `--locked-mode` restore fails CI instantly if any `packages.lock.json`
  regeneration is missed. SkiaSharp is a new native dependency subject to
  the same origin/hash/RID review.
