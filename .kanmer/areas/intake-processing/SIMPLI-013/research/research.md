# SIMPLI-013 research — CollisionDocNet behind IIntakeSourceReader for .doc/.msg

Date: 2026-08-20. All premises below were verified by read-only checks of the tree at `origin/dev` (8812b278) unless marked *assumed*.

## Scope decision (resolves the ADR-0001/ADR-0003 overlap)

CollisionDocNet is scoped to `.doc` and `.msg` only. PdfPig remains the one live PDF implementation (ADR-0001/ADR-0003); DOCX stays on OpenXml; EML stays on MimeKit. This is the first option ADR-0025 names, chosen per the 2026-08-20 operator scope direction. FRD-05 gets one sentence recording the engine boundary in the same PR.

## Current state (verified)

- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` is the single `IIntakeSourceReader`. `.doc`/`.msg` map to `SourceFormat.Deferred` (`DetectFormat`) → issue `deferred_file_type`, "retained for manual sorting" (`:108-113`, failure text `:118`). Composition: `DependencyInjection.cs:359` (`composesDocumentSurface` branch). Upload page already accepts `.doc`/`.msg` (`Upload.cshtml:36`).
- End-to-end behaviour today: `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs:63-82` — a `.doc`/`.msg` upload lands `IntakeDecision.NeedsSorting`, `FailureCode == null`, source asset retained. This is the honest manual-sorting outcome the fail-closed fallback must preserve.
- `workspaces/document-extraction/` (CollisionDocNet): 9 src projects, MSTest suites, own `CollisionDocNet.slnx`, CI lane `.github/workflows/workspaces.yml` (document-extraction is the only live workspace; report-renderer already retired).
- The `.doc` reader is `CollisionDocNet.Writer` (`WordBinaryExtractor`/`WordFibParser`/`WordPieceTableParser`/`WordStructuredEvidenceParser`), the `.msg` reader is `CollisionDocNet.Outlook` (`MsgReader`/`MapiPropertyReader`/`RtfCompression`). **Both depend only on `CollisionDocNet.Storage.CompoundFile`** (verified by `using` scan; the csproj references to Core/Model are unused). The public façade `DocumentExtractor.ExtractAsync` (Conversion) additionally pulls Pdf, Email, Writer.OpenXml — a second PDF/EML/DOCX implementation Pegasus must not carry.

## Integration shape decided

**Fold the needed source into `Pegasus.Infrastructure`** under `src/Pegasus.Infrastructure/Intake/DocumentExtraction/{CompoundFile,Word,Msg}` — no new production project. ADR-0025's consequence clause says the activating change "either folds the source into an existing production project or reconciles the four-project boundary in its own ADR"; folding needs no new ADR and Infrastructure already owns the extraction adapter. `Pegasus.slnx` and `DependencyDirectionTests` therefore need no project-set change (the imported code is pure BCL — no new package references; the forbidden-prefix assertions keep passing and keep guarding).

**Use the narrow reader APIs, not `DocumentExtractor.ExtractAsync`.** The adapter is two new dispatch branches (`SourceFormat.Doc`, `SourceFormat.Msg`) inside `MimeKitPdfPigOpenXmlIntakeSourceReader.DispatchAsync`, implemented in a partial-class file, mapping directly to `IntakeContentFragment`/`IntakeAssetCandidate`/`IntakeSourceIssue` the way `ReadPdf`/`ReadDocx` do. This keeps ONE reader pipeline: a `.doc` attached to an `.eml` already flows through `DispatchAsync`, and a PDF attached to a `.msg` is re-dispatched into the existing PdfPig path (single PDF implementation) under the existing `MimeLimitState` decoded-bytes budget.

### Minimal closure imported (and what is left behind, and why)

Imported (≈4,000 lines, renamespaced `Pegasus.Infrastructure.Intake.DocumentExtraction.*`, kept `internal` — `InternalsVisibleTo("Pegasus.IntegrationTests")` already exists):
- `Storage/CompoundFile/*` (11 files, 1,300 lines) — bounded MS-CFB reader both formats stand on.
- `Writer/` → `Word/`: `WordBinaryExtractor`, `WordFibParser`, `WordPieceTableParser`, `WordBinaryModels`, `WordBinaryExtractionLimits` — trimmed to the text slice (see EXT-DOC-005 disposition).
- `Outlook/` → `Msg/`: `MsgReader`, `MapiPropertyReader`, `MsgModels`, `RtfCompression`.

Left behind (workspace deleted; git history retains):
- `Cli`, `Conversion` (`DocumentExtractor`), `Pdf`, `Email`, `Writer.OpenXml`, `Model`, `Core` — would create second PDF/EML/DOCX implementations or carry an evidence model intake does not consume; `Conversion`'s mapping job is done directly against the Core intake contracts.
- `Storage/Detection` (`FileFormatDetector`), `Storage/{Ole,Opc,Xml,Zip}` — routing stays extension/media-type based (the reader's existing convention); nothing imported references them.
- `WordStructuredEvidenceParser` + `WordStructuredModels` (867 lines) — produce property runs/structure records/passive assets/OLEPS metadata. `IntakeSourceReadResult` has no metadata surface, EXT-DOC-009 says no `.doc` image is actually decoded (descriptors only), and formatting semantics are not intake text. `WordBinaryExtractor`'s call into it is removed with the field surface.
- DocR03/R04 executable-specification tests — self-contained oracles that by their own header "do not call WordFibParser, WordPieceTableParser, or WordBinaryExtractor"; they encode the reviewed contract, which the fixes below implement. Production-facing tests are imported instead.

## Phase A defect assessment (feature-matrix rows vs. what plain-text + attachment intake needs)

The DocR03 oracle inside the workspace tests (`DocR03ExecutableSpecificationTests`) independently encodes the correct semantics for cbMac bounds, guard-CP placement, and lone-surrogate replacement — the fixes below implement exactly what the oracle already specifies.

| Row | Defect (matrix wording) | Verdict for intake | Action |
| --- | --- | --- | --- |
| EXT-DOC-001 | Broad `nFib` range misclassifies unrelated containers (detector) | **Avoided** — `FileFormatDetector` is not imported. Routing is by extension/media type; `WordBinaryExtractor` itself enforces `wIdent == 0xA5EC` and the exact `nFib` set {0xC1, 0xD9, 0x101, 0x10C, 0x112} (`WordFibParser:11,32,149`), so a mislabeled container fails closed to manual sorting. | none (recorded) |
| EXT-DOC-003 | Misreads FIB state | **Fix** — `WordFibParser:47-53` rejects files as Corrupt when FibBase bytes 24/28 (reserved5/reserved6, "MUST be ignored" per MS-DOC 2.5.2) look inconsistent → false rejection of genuine files. Remove the validation. | fixed in import |
| EXT-DOC-003 | False CP1252 route | **Fix** — `WordBinaryExtractor.DecodeCodeUnit:340-355` gates high-byte decoding on FibBase byte 20 (reserved `chse`, also "MUST be ignored"): any nonzero value turns ALL bytes ≥0x80 into U+FFFD, destroying accented/€/quote text. MS-DOC 2.9.73 (FcCompressed) defines the mapping as fixed CP1252 + the 0x80–0x9F remap table unconditionally. Decode unconditionally. | fixed in import |
| EXT-DOC-003 | Omits `cbMac` enforcement | **Fix** — `cbMac` (FibRgLw97[0], the declared meaningful byte count of WordDocument) is never read; pieces are bounded only by stream length. Enforce piece FC ranges against `min(cbMac, streamLength)` (oracle `ValidatePieceBounds`). Bounds/safety. | fixed in import |
| EXT-DOC-003 | Omits surrogate enforcement | **Fix** — Unicode pieces emit raw UTF-16 units; lone surrogates flow into fragments and can break downstream JSON/DB encoding. Replace unpaired surrogates with U+FFFD (oracle `DecodeUnicodeUnits`); same sanitation applied to `.msg` strings in the adapter. | fixed in import |
| EXT-DOC-004 | Misplaced outside guard | **Fix** — `WordBinaryExtractor.BuildStories:210-213` inserts the single guard CP between Main and Footnote; MS-DOC places it after the LAST subdocument (the oracle test name is literally "…PlaceOneGuardAfterTheLastSpecializedPart"). Today every subdocument story (headers, footnotes, textboxes) decodes shifted by one character. Move the guard to the end; include `StoryLengths[3]` in the specialized-story check so the extent equation stays exact. | fixed in import |
| EXT-DOC-004 | Exposes reserved3 as Macro; loses structured provenance | **Accept** — the `Macro` story-kind name for FibRgLw97 reserved3 is internal, never operator-facing; with the guard fix its length still totals correctly (spec says it MUST be zero in 97+ files anyway). Structured provenance is the evidence model intake does not consume. | disposed |
| EXT-DOC-005 | Only eleven SPRM meanings | **Irrelevant** — SPRMs are formatting properties; plain-text intake consumes none of them. The whole property engine leaves with `WordStructuredEvidenceParser`. | disposed |
| EXT-DOC-002/others | 183-descriptor atlas, secondary FIB, quick-save branch conformance | **Accept** — unsupported branches already surface as explicit Partial issues (`AddUnsupportedBranchIssues`), which intake reports honestly; encrypted/obfuscated files classify as Encrypted and fail closed. | disposed |
| Pre-97 family | Not implemented | **Accept** — classified explicitly (`doc-pre97-unsupported`) → manual sorting. | disposed |

### MSG (intake needs body text + attachments)

| Row | State | Verdict | Action |
| --- | --- | --- | --- |
| EXT-MSG-001/002 | CFB + MAPI property substrate | Sound: bounded CFB reader, contextual 32/24/8-byte property headers, cumulative limits. | import as-is |
| EXT-MSG-005 | Body policy plain → HTML(inert text) → RTF(passive text) | Correct preference order for intake; HTML decoded per PidTagInternetCodepage with deterministic Latin-1 fallback + issue. | import as-is |
| EXT-MSG-006 | Compressed RTF / encapsulated HTML | LZFu decompression is complete (CRC-checked, dictionary-bounded). **Fix (small)** — `PassiveRtfText` ignores `\htmlrtf` toggles, so when RTF is the only body (encapsulated HTML), RTF-renderer-only runs leak into the text. Add the `\htmlrtf` suppress toggle (≈10 lines); `{\*\htmltag …}` groups are already skipped via `\*`, leaving exactly the HTML text content. Full MS-OXRTFEX fidelity is not needed: RTF is the last-resort body source. | fixed in import |
| EXT-MSG-007/008 | Attachments, embedded messages | By-value bytes surface with filename/media-type/content-id; embedded messages (method 5) parse recursively as `MsgDocument`. The adapter re-dispatches by-value attachment bytes through `DispatchAsync` (PDF→PdfPig, DOCX→OpenXml, EML→MimeKit, images, nested doc/msg) and maps embedded messages' bodies as labelled fragments. Reference/OLE-only attachments stay passive with an explicit issue — honest. | adapter maps |
| EXT-MSG-009 | S/MIME, rpmsg | Classified `Encrypted` without decryption → fail-closed manual sorting. TNEF decoding not needed at this layer (TNEF lives inside EML, MimeKit's path). | disposed |
| EXT-MSG-010–012 | Calendar/contact/task fidelity | Item classes still expose subject/body/attachments via the generic bag; recurrence/time-zone semantics are not intake data. | disposed |
| PT_UNICODE strings | Raw `Encoding.Unicode` decode | Lone surrogates possible → sanitized by the same helper as `.doc` text at the adapter boundary. | fixed |

## Verified integration seams

- Contracts: `IntakeContentFragment(Source, SourceLabel, Text)`, `IntakeAssetCandidate`, `IntakeSourceIssue`, `IntakeTransportEvidence` — `src/Pegasus.Core/Intake/IntakeContracts.cs:216-303`. No Core change needed (reuses `IIntakeSourceReader` exactly as the ticket requires).
- Root `.msg` sender/subject map to `IntakeTransportEvidence` (`Sender`/`Subject`) with the same `IntakeSenderIdentityKind` threading the email path uses (`ReadMessageAsync:609-635`); a `.msg` attached to a staff email at depth 0 gets `AttachedOriginal`, mirroring `ReadMimeEntityAsync`.
- Fail-closed: reader outcomes other than Complete/Partial add an issue and return `ReadOutcome.Readable` → decision `NeedsSorting` (same lane the Deferred branch produces today; `MultiFormatIntakeWebTests:63` keeps passing with the issue code updated). `OperationCanceledException` propagates as today.
- Test framework: repo is xunit; workspace tests are MSTest. Imported production-facing suites (`CompoundFileHeaderReaderTests`, `CompoundFileReaderTests` + fixture, `WordBinaryExtractorTests` + fixture, `MsgReaderTests`, `RtfCompressionTests`) are converted mechanically to xunit and live in `tests/Pegasus.IntegrationTests/DocumentExtraction/` (the one test project referencing Infrastructure; internals already visible).
- Fixtures for end-to-end tests are built programmatically (no corpus/ material): `WordBinaryFixture.CreateRawCfb` already emits genuine `.doc` bytes; a small raw-CFB builder modeled on it produces a minimal genuine `.msg` (root `__properties_version1.0` + `__substg1.0_001A001F`/`0037001F`/`1000001F` streams).
- TFM: everything is `net10.0`, matching the workspace. Imported code adds zero package references, so `packages.lock.json` files are unchanged and `--locked-mode` restore is unaffected.
- `ReaderKey` (`mimekit_pdfpig_openxml`) is persisted provenance — kept stable; `ReaderVersion` gains a `collisiondocnet-doc-msg/0.1` component.
- Operator-facing issue text goes through `src/Pegasus.Web/Presentation/OperatorLabels.cs` (e.g. `deferred_file_type:262`); new issue codes get entries there, no GUIDs, one-sentence consequence style.

## Assumed (not verified by execution yet)

- The imported code compiles warning-free under `AnalysisLevel latest-recommended` + `TreatWarningsAsErrors` (the workspace builds warnings-as-errors too, but with default analysis level); reconciliation is planned as fix-not-suppress.
- The CompoundFileReader's strict MS-CFB 2.6.4 directory validation accepts the hand-built minimal `.msg` fixture; the fixture builder will be iterated against the parser (unit level) before the web test uses it.
