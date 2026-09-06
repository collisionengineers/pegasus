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
