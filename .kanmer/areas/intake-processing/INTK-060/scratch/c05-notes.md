## C05 (Core slice) — assumptions recorded under M8

- [ ] ASSUMPTION 1 (implementer, attempt 1): the selector's `NotApplicable` outcome carries an
  explicit reason, and a source whose readable text is empty (the PDF is scan-only) resolves to
  `NotApplicable(TextUnavailableRequiresOcr)` rather than to a family or a negative document role
  — because two of the 29 originals (`JohnRBell1.pdf`, `TonBridgeAccidentRepair1.pdf`) carry no
  extractable text at all, so any family or role verdict for them would be inferred from the
  filename/folder, which the review and the dispatch forbid ("no folder/filename inference",
  "never fabricated"). The dispatch's three outcome kinds (Selected / NotApplicable / Ambiguous)
  are preserved; only the NotApplicable reason is new. Alternatives: (a) a fourth outcome kind —
  rejected, the dispatch fixes three; (b) classify the two by filename — rejected, forbidden;
  (c) leave them unclassified with no reason — rejected, the corpus assertion could then not tell
  "scan-only" from "unknown layout".
- [ ] ASSUMPTION 2 (implementer, attempt 1): where a family prints one combined vehicle
  description ("Vehicle: RENAULT CLIO ICONIC TCE" in the Connexus/Exclusive/EVA narrative
  layout), `vehicle.model` carries the printed text with disposition `Ambiguous` and
  `vehicle.make` stays `Missing` — because the source does not separate make from model, and the
  only existing two-word-make list lives in `QdosInstructionExtractionPolicy` (private, and that
  file is outside this slice's files map, so promoting or copying it would breach M5 and conduct
  rule 8). Families that label Make and Model separately (Laird, Montgomery, sPrint) extract both
  as `Usable`. Alternatives: (a) split on the first token — rejected, wrong for RANGE ROVER /
  MERCEDES-BENZ and it would be a fabricated fact; (b) copy the makes list into this slice —
  rejected by rule 8 and M5.

## C05 (Core slice) — attempt 2: assumptions 1 and 2 confirmed, three more recorded

ASSUMPTIONS 1 and 2 stand, and both are now evidenced rather than reasoned:

- ASSUMPTION 1: confirmed. `JohnRBell1.pdf` and `TonBridgeAccidentRepair1.pdf` are the only
  two of the 29 whose extracted text is empty (36 and 56 bytes, page markers only). Any
  family or negative-role verdict for either would have to come from the file name.
- ASSUMPTION 2: confirmed. The Connexus/Exclusive/EVA narrative prints one combined
  `Vehicle:` cell and no separate Make label; Laird, Montgomery and sPrint label Make and
  Model separately and both read as `Usable`.

- [ ] ASSUMPTION 3 (implementer, attempt 2): a printed value adjustment whose label is
  neither mileage nor condition is kept as a source row under the new field name
  `valuation.adjustment`, with the whole printed cell as its raw value, and the valuation
  reconciliation sums those rows alongside the two typed slots — because Montgomery prints
  "Urban edition adjustment 11,120" and the frozen `ThirdPartyReportValuation` types only
  `MileageAdjustment` and `ConditionAdjustment`. Without the row the £18,880 + £11,120 =
  £30,000 reconciliation the review names cannot be checked at all; with it filed under
  `ConditionAdjustment` the record would state a label the document does not print.
  REQUESTED CONTRACT CHANGE for A (not applied — the shape is frozen): add
  `IReadOnlyList<ThirdPartyReportFact<decimal?>> Adjustments` to
  `ThirdPartyReportValuation`, each carrying its printed label, so the typed projection can
  express an adjustment that is neither mileage nor condition. Until then the source row is
  the record and the finding reads it. Alternatives: (a) map it to `ConditionAdjustment` —
  rejected, it invents a label; (b) leave it unread — rejected, it loses a reconciliation
  the review requires; (c) use the `Deductions` list — rejected, it is an addition to the
  value, not a deduction.

- [ ] ASSUMPTION 4 (implementer, attempt 2): the production caller is `ProcessIntake`, which
  takes `IRetainedInstructionAnalysisStore` as an OPTIONAL dependency and records the
  reading at retention — because the only persistence for a `SourceFieldCandidate` is
  `IntakeSourceCandidateEntity`, whose FK requires a `RetainedInstructionAnalysis` row, and
  the natural caller (`AnalyzeRetainedInstruction`, which would call the reader when no
  instruction profile matches) is NOT in the C05 files map. `ProcessIntake` is, retention is
  where the bytes and their role are known, and this adds no entity, no table and no
  DependencyInjection.cs edit. It follows the optional-dependency pattern that file already
  uses three times, so until A registers the store the behaviour of intake is unchanged.
  A MUST REGISTER (C-F02), or the reading is recorded nowhere in production:
  `services.AddScoped<EfRetainedInstructionAnalysisStore>();`
  `services.AddScoped<IRetainedInstructionAnalysisStore>(p => p.GetRequiredService<EfRetainedInstructionAnalysisStore>());`
  `services.AddScoped<ISourceCandidateQueries>(p => p.GetRequiredService<EfRetainedInstructionAnalysisStore>());`
  `services.AddScoped<IGetLatestRetainedInstructionAnalysis, GetLatestRetainedInstructionAnalysis>();`
  Alternatives: (a) edit `AnalyzeRetainedInstruction` — rejected, outside the files map;
  (b) add a C-owned store and entity — rejected, the dispatch says stop and record instead;
  (c) record nothing until A lands — rejected, the slice would ship no production path.

- [ ] ASSUMPTION 5 (implementer, attempt 2): every bounded label rule is written to read the
  same value whether the PDF text engine preserves a printed column's padding or collapses
  it to a single space, and every prose phrase tolerates a line wrap — because the reference
  pack's extracted text was produced by PyMuPDF (`page.get_text(sort=True)`,
  `astra_output/tools/extract_pack.py`) while production reads through PdfPig
  (`ContentOrderTextExtractor`), and the drafts' rules depended on the padding the pack
  happens to carry. A free-text cell now ends at a run of two or more spaces, at the end of
  its line, or at the next printed label. All 24 classified originals read identically under
  both text shapes. Alternatives: (a) keep the padding-dependent rules — rejected, they read
  nothing under the production engine and the failure is silent; (b) normalize all whitespace
  before matching — rejected, it destroys the column boundary the existing instruction
  extractor also relies on to bound a flattened table cell.

## C05 (Core slice) — correction round 1: two more assumptions

- [ ] ASSUMPTION 6 (implementer, correction round 1): a reconciliation finding is persisted as
  its own `SourceFieldCandidate` row under a `finding.` field namespace — raw text is the
  finding's own statement including the printed values it compared, normalized value is the
  stable finding code, the locator is the one the compared rows carry, the disposition is
  `Conflicting` for a printed contradiction and `Ambiguous` for every other finding (never
  `Usable`, because a finding is not a value), and the policy version is
  `ThirdPartyReportValidation.PolicyVersion` rather than the extraction profile version —
  because `ThirdPartyReportExtractionResult.Findings` reached no store, query or screen at
  all (C05-R-1), `IntakeSourceCandidateEntity` has no findings column, and the frozen
  `ThirdPartyReportContracts.cs` may not change. The existing field keys are reused rather
  than renamed: the dispatch's illustrative `finding.labour-hours-times-rate` becomes
  `finding.labour-hours-rate-mismatch`, `finding.supplement-without-base` becomes
  `finding.supplement-without-proved-base` and `finding.net-derived-from-parts` becomes
  `finding.net-not-printed`, because `ThirdPartyFindingCodes` is already stated to be part
  of the contract with the Case UI and the corpus regression, and two spellings of one code
  would be worse than an imperfect one. Nothing writes a repaired number into any source
  candidate: the Montgomery rows still read 26.20, 90.00 and 1,582.20 beside the row that
  says they do not multiply out, and a corpus test asserts no `estimate.labour.amount` row
  holds 2358.00. ALTERNATIVE NOT TAKEN: a findings table or a findings column on the
  analysis row, added by A under C-F02, with a typed finding record and its own query. It is
  the better long-term shape — a finding would then carry its kind, its evidence row ids and
  its policy version as typed fields instead of as a namespaced field name — but it needs an
  A-owned entity, migration and DI change that C05 may not make, and taking it now would
  have left the findings unpersisted for another whole slice. The `finding.` namespace is
  forward-compatible with it: `ThirdPartyReportFields.IsFinding` is the one predicate a
  later migration reads to move these rows onto a typed table.
  Other alternatives: (a) leave the findings computed and discarded and only declare the gap
  in the report — rejected, the dispatch forbids accepting the major with a reason, and a
  reconciliation nothing can read is the same as one that never ran; (b) widen
  `SourceFieldCandidate` — rejected, it is a shared C-owned contract that C01 and the
  instruction reader also write, and a finding needs no new column to be recorded honestly.

- [ ] ASSUMPTION 7 (implementer, correction round 1): the persisted finding rows are shown on
  the Received screen by the markup that already renders every source candidate, and NO
  "Finding" chip or label was added — because the two Web files the dispatch names as C05's
  (`Pages/Shared/_Provenance.cshtml`, `Pages/Shared/_EvidenceViewer.cshtml`) do not render
  candidate rows at all: `_Provenance.cshtml` renders one provenance icon for a
  `CaseDataSource`, and `_EvidenceViewer.cshtml` is the image/PDF overlay. The only place a
  source candidate is rendered is `src/Pegasus.Web/Pages/Intake/Details.cshtml:627-641`,
  which the plan assigns to C04, and `OperatorLabels` is assigned to C08 — neither is in
  "### C05 files", so editing them would breach the ownership rule the dispatch states in
  the same paragraph. What ships instead is honest and visible: each finding renders as
  `finding.<code>: <the finding's statement, with both printed values>` with its
  operator-worded disposition ("Conflicting statements" / "Ambiguous") and its source label
  and page, and the web test asserts that exact text is in the served HTML. HANDOFF: C04
  should branch on `ThirdPartyReportFields.IsFinding(candidate.Field)` in the retained
  analysis list and render a chip; C08 should add the one label
  (`OperatorLabels.SourceCandidateKind` or similar) it reads from. Both are one-line
  additions and neither needs a CSS class that does not already exist.
  Alternatives: (a) edit `Details.cshtml` and `OperatorLabels.cs` anyway — rejected, they
  belong to two other slices whose reviews diff those files, and C05's ownership was the
  one thing this review graded PASS; (b) report the whole slice BLOCKED over the chip —
  rejected, the major's substance (findings reach storage and the provenance surface) is
  fully deliverable inside C05's files, and stopping would have left it undelivered too.

## C05 correction round 2 (head 868e7a5ea, from 7b632169b)

Two wave-19 failures, both from round 1's own code, plus the two majors the superseding
attestation added. Two commits, six files, all in "### C05 files"; frozen contract untouched;
build exit 0 / 0 warnings.

- **C05-R-10** (lane 3, `JohnRBell1.pdf` / `source-requires-ocr`): a finding with no evidence
  row of its own was filed against `selection.Issuer`, whose `sourceLabel` is
  `evidence?.SourceLabel ?? string.Empty` for a source that matched no signature — so the row
  persisted naming no part of its document. The locator is now the first compared row that
  names its source, then the issuer, then the first row of the document that does.
- **C05-R-6 open half** (lane 4): `IReevaluateIntake` only queues the work the Worker claims,
  so the second "pass" re-read nothing and emitted no outcome; the observed
  `["no_report_signature", "recorded"]` came from other collections' intakes on a
  process-global `ActivityListener`. The case now dispatches the queued work through the
  Worker's own processor and keys outcomes to `intake.receipt_id`. The operation key
  (`third-party-report:{sourceAsset.Id}`) is stable across a re-evaluation because
  `ReplaceEvaluationAsync` keeps the retained source asset row; what differs is
  `ExpectedReceiptVersion`, which is what makes the store raise the conflict that names
  `recorded_reading_stands`.
- **C05-R-11**: `IsRecordable` now asks the whole reading (a signature match, or a finding
  about the source itself) rather than the selection alone, so a scan-only source's page rows
  and OCR findings reach storage. A readable non-report that states nothing about itself is
  still left alone, and both halves now have a test — the gate had none, which is how it
  discarded them.
- **C05-R-12**: the finding's position in the raised order is part of the derived identifier
  key, so two findings stating the same sentence about the same page cannot collide and lose
  the whole analysis.

No new assumption was needed this round; no test was run here (controller wave loop).

## C05 correction round 3 (implementer, attempt 3) — dispositions

- [ ] ASSUMPTION 8 (implementer, attempt 3): the web case
  `ReprocessingTheSameRetainedBytesDoesNotWriteASecondSetOfCandidates` is
  replaced by `AQueuedReevaluationLeavesTheRecordedReadingExactlyAsItWas`,
  which asserts the observed outcome sequence `["recorded"]` rather than
  `["recorded", "recorded_reading_stands"]` — because a queued re-evaluation of
  a completed receipt cannot re-read anything at all. `ProcessQueuedIntake`
  deletes the staged copy once the evaluation is durably recorded
  (`TryDeleteCompletedStagingAsync`), and `IIntakeWorkStore.FindStagedReceiptIdForReceiptAsync`
  states the consequence in its own doc comment: a completed work item must
  never be made claimable again, because that "would force a re-claim through
  the artifact-reading path, whose staged copy is already deleted". The
  staff-facing `ReevaluateIntake` command does exactly that, so the re-claimed
  pass throws `IntakeArtifactIntegrityException`, fails terminally as
  `staged_artifact_integrity_failure`, and never reaches `ProcessIntake` — which
  is why round 2 saw one outcome where two were expected. Alternatives: (a) fix
  the re-evaluation path to read the durable artifact — `DurableIntake.cs` is
  not a C05 file and the change belongs to the durable-intake owner; (b) drive
  `ProcessIntake.ExecuteRetainedAsync` directly — it is `internal` and visible
  only to `Pegasus.Core.Tests`, and `ProcessIntakeTests.cs` is not a C05 file;
  (c) narrow the case to nothing and stay silent — rejected, the pass's stopping
  point is now asserted from the durable work item. The
  `recorded_reading_stands` mechanism is proved instead where it is reachable,
  against SQL Server: `RecordingTheSameReadingAgainReplaysItAndAMovedVersionIsRefused`
  records the identical request (replay, nothing written) and the same request
  at a moved receipt version (refused), which is the conflict `ProcessIntake`
  reports.
- HANDOFF (durable intake, not C05): a staff re-evaluation of any completed
  receipt fails as `staged_artifact_integrity_failure` rather than re-reading
  the retained source. Pre-existing, outside this slice's file map, discovered
  by C05's web case and now asserted by it.
- C05-R-16 (major) — fixed. `ThirdPartyReportSourceContext` gained an optional
  `SourceLabel` (the retained file's own name, a locator and never evidence);
  `ThirdPartyReportProfiles.Verdict` falls back to it when no signature evidence
  exists, so the scan-only issuer row names its document. Corpus case asserts the
  label, the `Missing` disposition and the absent value on both scan-only originals.
- C05-R-6 (major) — root-caused and closed by ASSUMPTION 8 above; the three
  untagged early returns in `RecordThirdPartyReportSourceAsync` are now tagged
  (`not_composed`, `source_not_readable`, `no_single_source_asset`), so no path
  through the report reader is silent.
- C05-R-17 (minor) — fixed. `AReadingWithNoPageAtAllStillNamesTheSourceOnEveryRowItRecords`
  builds the `RequiresOcr` reading with no scan-only page that no production
  reader emits today, and asserts every recorded row names the source.
- C05-R-18 (minor) — fixed. The conflict catch reads the stored row back and
  tags `recorded_reading_stands` only when it names this receipt and asset;
  otherwise `analysis_key_bound_elsewhere`, or `recorded_reading_unverified`
  when the probe itself fails.
- C05-R-13 (note) — already corrected in the round-2 report section; no further
  change.
- C05-R-14 (note) — not repeated: `git diff` for this round contains no BOM
  bytes; every edit preserved each file's existing encoding and CRLF endings.
- C05-R-19 (note) — fixed. The operation-key comment now says the conflict, not
  the replay, is the ordinary outcome of a second pass over one asset.
- C05-R-20 (note) — fixed. The `DeterministicId` comment records that a
  finding's ordinal is its position in the raised order, so inserting a rule
  renumbers later findings — the same version boundary a changed raw value
  crosses.

## ASSUMPTION 8 — CLOSED (retargeted), C integration round, 2026-09-06

ASSUMPTION 8 recorded that a queued re-evaluation could not re-read a completed
receipt's source: `ProcessQueuedIntake` read the staged copy, which is deleted once
the evaluation is durably recorded, so the re-claimed pass failed with
`staged_artifact_integrity_failure` before intake ran and only one pass ever tagged a
third-party outcome. C05 pinned that outcome as a tripwire rather than working around a
gap in the durable intake path.

Stream A closed the gap (INTK-027, A commit 9028aa12b, applied here as the bounded
caller patch at C 31e9857b8): `ProcessQueuedIntake` now takes a REQUIRED
`IReadLogicalDocumentVersion` and a re-evaluation re-reads the exact retained source
through it — by identity, as the system worker, against the recorded receipt/case/hash/
length — after the staged copy is gone.

So ASSUMPTION 8 is closed and its tripwire retargeted, not deleted. C-owned
`ThirdPartyReportProvenanceWebTests.AQueuedReevaluationLeavesTheRecordedReadingExactlyAsItWas`
(commit 78cb51c2c) now drives the queued re-evaluation through the real dispatcher and
asserts: the work item completes with no failure code; the third-party outcome sequence
is `recorded` then `recorded_reading_stands`; the port was asked exactly once, for the
receipt's own logical version with the recorded hash and length; and the candidate rows
are the same rows, value for value, with no second candidate set.
`RecordingTheSameReadingAgainReplaysItAndAMovedVersionIsRefused` is unchanged.

Qualification, in Stream A's words (PR 673 comments 5561171653 and 5561151076):
"C owns additional direct constructor adaptations and retargeting its C05 test. Any
isolated test double of the reader is qualified boundary proof; do not add a production
fallback or claim standalone C carries A04 adapters." — "A infrastructure/readers/tests
stay A-owned and are supplied by the combined host." — "Do not add stubs or assume C
standalone carries A04 concrete readers (it does not)." — "A positive and negative SQL
tests already prove the real local reader and exact confirmed Box/cache Worker path
separately."

Accordingly the retargeted test registers the C-owned
`tests/Pegasus.IntegrationTests/Support/RecordingLogicalDocumentVersionReader.cs` double
(armed after retention with the retained source's exact bytes for that logical version;
refusing anything else), and no production registration or fallback was added anywhere.

### Deviation recorded (constructor adaptation, beyond the two named call sites)

`git grep -n "new ProcessQueuedIntake(" -- tests src` named only
`CustodyOutboxIntegrationTests.cs:2467` (C-owned; a first pass over a staged source, so
it gets the double unarmed and refusing) and `QdosAllocationRecoveryTests.cs:602`
(A-owned, already adapted by A — left alone).

One further construction is not a `new` expression and so is not in that grep:
`IntakeWebDriver.CreateProcessor` in the C-owned `IntakeWebTestSupport.cs` builds the
processor with `ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(services)`, and
standalone C registers `IReadLogicalDocumentVersion` nowhere — so every C test that
drains queued work would have failed on an unresolved service rather than on anything it
was testing. It now prefers a reader the host composed (so the combined host's A04
adapter still wins) and falls back to the refusing double where none is registered, which
is the same rule the controller set for the direct constructions, applied to the one
shared indirect one. Still no production fallback: the fallback is test support only.

Build gate on 78cb51c2c: `dotnet build ./Pegasus.slnx --configuration Release
--no-restore` exit 0, 0 warnings, 0 errors (one MSB3027 file-lock retry after
`dotnet build-server shutdown`). Tests are the controller's wave loop; none run here.

## C05 seam for A — READY_FOR_TESTS (head 35cc17c66)

- `ThirdPartyReportExtraction.Reconstruct(IReadOnlyList<RetainedInstructionCandidate> rows, ThirdPartyReportSourceContext context)` returns `ThirdPartyReportCandidate?`. One public entry, reuses `Project`/`Lookup`/`Estimates`/`ObservedFields`; `Project` now takes the issuer row instead of the whole selection (all it ever read).
- Takes `RetainedInstructionCandidate`, not `SourceFieldCandidate`, because only that shape carries `PolicyKey` (needed for the null guard) and `Locator`. A already has `Map(IntakeSourceCandidateEntity)` producing it.
- Call once per retained analysis (receipt + asset + sha256). Identity: `new ThirdPartyReportSourceContext(receiptId, sha256, occurrence, IntakeAssetId: assetId)`.
- `null` = these rows record no report candidate: no row with policy key `third-party-report`, or the persisted issuer row is not `Usable` (ambiguous / non-report role / scan-only). Same answer `Extract` gave. Other policies' rows are ignored, not rejected.
- No bytes re-read, no store call, no signature re-run, no issuer inference, no arithmetic repair; Missing/Ambiguous/Conflicting come back as persisted.
- Persistence gap fixed inside the existing shape: `ToCandidates` was dropping the locator `Region` ("label"/"section"/"finding"). Now written into the `RetainedInstructionCandidate.Locator` envelope (version 1 → 2 for those rows). No column, no schema, no migration.
- Open for A: no persisted ordinal, so within-field row order (conflict first row, damage zones, deductions, photographs) follows the order A supplies. `OrderBy(Field).ThenBy(Occurrence)` is stable and adequate; exact declared-rule order would need an A-owned ordinal column.
- Builds: Core 0/0 exit 0, Pegasus.Core.Tests 0/0 exit 0. No `dotnet test` run here. Runner filter: `FullyQualifiedName~Pegasus.Core.Tests.Intake.ThirdPartyReports.ThirdPartyReportExtractionTests` (whole class), plus `ThirdPartyReportCorpusTests`/`ThirdPartyReportProvenanceWebTests` for the locator change.
- Report: `...\scratchpad\takeover\c05-seam-report.md`. Not pushed, no PR.

C05 OCR-replay research done (read-only, worktree v1-intake @ b386c9dd2). Full brief: scratchpad/takeover/c05-ocr-replay-research.md

- Gap confirmed: ProcessIntakeOcr.ExecuteAsync (IntakeOcr.cs:474-477) treats Completed and Failed as one terminal branch. ApplyAsync (IntakeOcr.cs:752-753) calls store.CompleteAsync then ReanalyzeAsync as two separate steps; a crash between them leaves a Completed row whose postprocess never ran, and every later replay returns at line 474 without retrying it.
- No report-analysis command exists yet: AnalyzeRetainedInstruction.cs only ever selects InstructionDocumentSignature.InstructionRole (line 379-381) and never touches ThirdPartyReportExtraction/ThirdPartyReportAnalysis. Today's only caller of ThirdPartyReportAnalysis is ProcessIntake.RecordThirdPartyReportSourceAsync (ProcessIntake.cs:312-431), inline, not a command object.
- A's IntakeOcrOperation.Result / EfIntakeOcrOperationStore.Map (confirmed present) is exactly what lets CompletedOcrEvidence be rebuilt on replay with no provider recall.
- Also found: IProcessIntakeOcr has zero references outside IntakeOcr.cs + its tests -- no Worker/DI route composes it yet anywhere in the repo. Flagging this as a possible separate prerequisite, not assumed in scope for C05's fix.
- Proposed: split the Completed/Failed branch; on Completed, always run a composite postprocessor (existing instruction-analysis call unchanged + new IAnalyzeThirdPartyReportSource command reusing ThirdPartyReportExtraction/AnalyzeRetainedInstruction.CreateOcrReadResult) under a distinct key `third-party-report:ocr:{operation.OperationKey}`, forced review-only via a new ToCandidates(forceReviewOnly) param. Idempotency comes from each RecordAsync's own key, not from detecting "did postprocess already run" -- so re-running on every Completed replay is safe by design, matching ReanalyzeAsync's existing doc comment.
- Existing IntakeOcrTests.AReplayOfATerminalOperationHasNoSecondSideEffect currently asserts the bug as correct and will need to change.
- No A-owned files identified as needing changes for the fix itself.
