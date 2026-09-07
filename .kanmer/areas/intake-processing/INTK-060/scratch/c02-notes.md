## C02 — assumptions and deviations (implementer, attempt 1)

- [ ] ASSUMPTION 1 (C02 implementer, attempt 1): a field with two readings the
  document itself supports is recorded `SourceCandidateDisposition.Ambiguous`,
  not `Conflicting` — because the C02 invariant says "multiple supported
  interpretations are Ambiguous" and reserves conflict for a candidate that
  contradicts a confirmed staff/Engineer fact; C01 had it as Conflicting and its
  one assertion in `AnalyzeRetainedInstructionTests` was corrected with a
  comment. Alternatives: leave C01's mapping and map Ambiguous only in the B
  projection (two owners disagreeing), or add a fifth disposition (widens a
  frozen contract).
- [ ] ASSUMPTION 2 (C02 implementer, attempt 1): the e-mail reader keeps the
  retained body whole as its first fragment (unchanged text, unchanged label,
  now carrying a `CurrentBody` locator whose region bounds the current message)
  and emits the quoted history as an ADDITIONAL fragment after it — because a
  true split would move the provider's original from the first fragment to the
  second and change which candidate wins document order in the existing QDOS
  corpus output, which the plan requires to stay unchanged. Alternatives: split
  the body in place (changes existing extraction ranks), or record only a
  boundary offset and emit no second fragment (fails the "separate fragments"
  expectation).
- [ ] ASSUMPTION 3 (C02 implementer, attempt 1): the durable OCR operation id IS
  the external-work item id, and the attempt count lives inside the request
  envelope stored in `QualifiedPagesJson` — because F's `IntakeOcrOperations`
  provides no work-item foreign key and no attempt column, and the vehicle
  lookup precedent already keys its request row by the work item id.
  Alternatives: a new column (would need C-F02 reopened), or a separate attempt
  table (a second aggregate the plan forbids).
- [ ] ASSUMPTION 4 (C02 implementer, attempt 1): an OCR operation scoped to a
  logical DOCUMENT VERSION fails closed with `ocr_source_unavailable` rather
  than guessing a content length — because A04's `ReadLogicalDocumentVersion`
  requires an expected content length and F's OCR storage records only the
  source SHA-256, while the pre-case INTAKE ASSET path reads its length from the
  receipt's own asset record and is fully supported. Alternatives: pass zero (a
  claim A04 would have to ignore), or query document custody from intake (a
  second owner of document identity).

**C-F02 status: no stop needed.** OCR operation/result persistence maps onto the
storage the foundation already froze — `IntakeOcrOperationEntity` /
`IntakeOcrOperations` in `V1FoundationEntities.cs` and migration
`20260906054658_V1PlatformFoundation`, with web/worker grants — plus the
existing external-work outbox for the queue row. No entity and no table was
invented. Structured candidate provenance likewise maps onto F's
`IntakeSourceCandidateEntity.LocatorJson`, widened here to a version 2 envelope.

**Deviation 1:** `src/Pegasus.Infrastructure/Persistence/EfIntakeOcrOperationStore.cs`
is a new file the C02 file map does not list, although C-F02 assigns "C store
methods" to C. The alternatives were folding EF code into the Azure adapter or
into the unrelated receipt store, both of which break ownership.

**Two follow-ups for A (C-F03), written out in full in the report:** register
`ExternalWorkKinds.IntakeOcr = "intake_ocr"`, route it to `IProcessIntakeOcr` in
`ProcessQueuedExternalWork`, and compose `IIntakeOcrProvider` /
`IIntakeOcrOperationStore` / `IProcessIntakeOcr` in DI and the Worker. C edited
none of those files.

## Replacement-controller completion — C02 correction round 1

Preserved the exhausted worker's dirty IntakeOcr.cs change and completed its coherent provider/store/test contract at commit e203c8100 on c02-provenance. First build failed with six CS0535 interface errors because the worker stopped mid-refactor; retained as failure evidence. After completing Azure accepted-operation callback, request-envelope submission timestamps, durable store transitions, and test doubles, dotnet build ./Pegasus.slnx --configuration Release --no-restore exited 0 with 0 warnings and 0 errors. Implementation role ran no tests. Exact head e203c8100 is READY_FOR_TESTS; independent C02 wave and exact-head re-review remain required before integration.

## C02 doc-Partial research (researcher, read-only pass)

Full report: `scratchpad/takeover/c02-doc-partial-research.md` (this session's temp dir; not in repo).

- `WordBinaryExtractor` decodes all 8 FIB stories fully (text incl. table cells, tab/para-projected); `Outcome=Partial` iff ANY issue accumulated — text is usually present even when Partial.
- 25/25 `.DOC` originals have `fib.IsComplex=false` (fComplex bit unset) AND ≥1 nonzero non-CLX `FibRgFcLcb97` range (style sheet/fonts/doc-props) → `doc-complex-flag-unset` + `doc-fib-ranges-unprocessed` fire on ALL 25 regardless of tables. These look like near-universal false positives on any real Word-97+ doc, not genuine content loss — a semantics decision, not a parse gap.
- Genuine content gaps beyond that floor: 20/25 have a nonzero Header story; 10/25 (ALS+BC) have real tables (`` CellOrRowMark, no cell/row structure emitted); 11/25 have an OLE embedded-object marker (by design, ADR-0025 passive); PCH 01 has field codes; MP Word 03 has a Textbox story.
- OAK's 5 "complete" `.DOC` originals are actually **RTF text saved as .DOC** (`{\rtf1...` signature) — routed to the separate `PassiveRtfText` branch, never entering `WordBinaryExtractor`/FIB/CLX at all. Not evidence the binary-Word branch handles real complexity.
- C03 policy unit tests (Pch/Rjs/Als/Bc/Mp) feed pre-extracted text, not real bytes: PCH tests use hand-transcribed C# literals; ALS/BC "Category=Corpus" tests read `astra_output/extractions/text/*.txt` (a third-party tool's ground truth, incl. table content), gated on `PEGASUS_REFERENCE_PACK_ROOT`. None exercise `WordBinaryExtractor`. Only `Top15InstructionCorpusTests.cs` runs genuine bytes end-to-end — it's the one that produced the 31 Inconclusive rows.
- Smallest reader extension: table/cell locators for the 10 ALS/BC files (no new package; extend `WordBinaryExtractor`/`WordBinaryModels`, mirror `AddRtfTableCells`'s `ForCell` pattern) — moderate size, ~150-250 LOC, but by itself does NOT reach `Complete` for any file since complex-flag/fib-ranges still fire; that gate needs a product decision first.
- 6 low-text MP PDFs (MP PDF 01-04, MP Weird 01-02) confirmed scan-only, zero embedded text, genuinely OCR-only per plan C02 item 2 — truthful Inconclusive, no in-repo fix.
- No A-stream dependency identified for the table-locator extension itself; the complex-flag/fib-ranges semantics question is a standalone open question, not blocked on another owner's work.
