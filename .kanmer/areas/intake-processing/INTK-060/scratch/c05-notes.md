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
