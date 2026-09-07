## C01 — assumptions and deviations (implementer, attempt 1)

- [ ] ASSUMPTION 1 (C01 implementer, attempt 1): the Received page takes the new
  `AnalyzeRetainedInstruction` / `IGetLatestRetainedInstructionAnalysis` as OPTIONAL
  constructor parameters and renders "not available in this environment" when they are
  absent, rather than as required parameters — because `DependencyInjection.cs` is A-owned
  and required parameters would make `/Received/{id}` unresolvable in every A- and C-owned
  suite that exercises it, breaking work outside C01's scope, which the stop condition
  forbids. Nothing is swallowed: the absence is stated on the page and the exact
  registrations A must add are listed in the C01 report. Alternatives: required parameters
  (breaks the suite until A's patch); a feature flag (a second switch nobody owns).

- [ ] ASSUMPTION 2 (C01 implementer, attempt 1): the "Automation actor with the connector
  scope" on the intake stream boundary is expressed as `StaffAuthorization.Require(actor,
  StaffAccessRight.PerformCasework)` — because that right is the one ADR-0011 grants the
  Automation Actor (the ordinary operational casework surface) and no narrower per-actor
  scope concept exists in `Pegasus.Core.Identity`. Alternatives: inventing a connector
  scope enum (a new identity concept C01 does not own).

- [ ] ASSUMPTION 3 (C01 implementer, attempt 1): the "Core test asserting `EfUnidentifiedStore`
  declares the three members" is placed in `tests/Pegasus.IntegrationTests/UnidentifiedPersistenceTests.cs`,
  not in Core.Tests — because `Pegasus.Core.Tests` references only `Pegasus.Core` and cannot
  see `Pegasus.Infrastructure`. Core.Tests instead asserts the interface DEFAULTS
  (empty page / NotSupportedException) in `UnidentifiedContractsTests`.

### Deviations from the file list
- `src/Pegasus.Core/Intake/DownloadIntakeSource.cs` is NOT in C01's list, but the
  `IDownloadIntakeSource` implementation lives there, so A05 need (a)'s
  streaming-through-`IReadLogicalDocumentVersion` change could only be made for the ASSET
  path (`InstructionEvidenceImages.cs`, in scope). The source path keeps its existing
  hash-verified artifact read. Recorded, not coded around.
- `tests/Pegasus.IntegrationTests/LocalIntakeAccessTests.cs` withdrawn by controller
  message mid-run (Stream A02 owns hunks there); the streaming-authorization tests went to
  a new C-owned `IntakeSourceAccessTests.cs` instead.
- Preservation-table statement 15 (opening a Triage for an already-linked receipt through
  `POST /Received/{id}?handler=OpenTriage`, receipt Version provably unchanged) has no
  real-SQL test: the property it proves — four distinct operation keys across two
  corrections at an unmoving receipt version — is asserted at Core level (statement 8).
  Open risk, listed in the report.

### Known consequence
`PrincipalIdentificationCorpusTests.TrackedPegasusSourceHashesHaveNotDrifted` WILL fail:
`QdosInstructionExtractionPolicy.cs` is a tracked snapshot and C01 added its document
signature to it. The corpus rebuild is Stream A's
(`scripts/Build-PrincipalIdentificationCorpus.ps1`). The corpus JSON was not edited and
the test was not weakened.

## C01 all-15 retained-analysis proof — READY_FOR_TESTS (2026-09-07)

- Branch `c01-retained-analysis`, head `d505d6078` (one commit on `aa5e669d7`). Tests only; nothing under `src/`.
- `EveryGenuineOriginalReachesRetainedAnalysisWithoutAllocating`: all 81 originals staged by manual upload, analysed via `IAnalyzeRetainedInstruction` resolved from the host; asserts Analyzed, labeller's profile, review-only principal candidate, per-row source SHA-256 + occurrence, replay writes no duplicate, zero Cases/CaseIntakeLinks/IntakeManualAssociations. Writes `artifacts/evaluation/v1-intake/retained-analysis-corpus.md`.
- `NoGenuineNonQdosOriginalIsAllocatedAutomaticallyThroughNormalIntake`: 14 non-QDOS originals through the real upload + Worker drain; not `case_created`, no allocation, held Open in Unidentified. QDOS positive control cited (`QdosIntakeWebTests.StaffForwardedEmailStrongContentBeatsSenderAndRendersPersistedDraft`), not duplicated.
- Expectations shared, not copied: `Top15InstructionCorpusTests.Expectations` and 7 helpers widened `private`→`internal` (8 lines). The 81 rows are untouched.
- `WithAnalysis` now registers ONLY `IReadLogicalDocumentVersion`; the rest comes from A's `AddPegasusInfrastructure` (`136b30a2d`), so the command under test is the host's.
- Compile check `dotnet build ./tests/Pegasus.IntegrationTests/... -c Release --no-restore`: 0 warnings, 1 error — only the A-owned `CS0246 EfCaseArtifactCustody` at `DocumentCustodyDurabilityTests.cs(462,35)`. That file was not edited or excluded. An earlier run found CA1828 in the new code; fixed before commit.
- Not executed: `dotnet test` is the runner's, and `PEGASUS_REFERENCE_PACK_ROOT` is unset here, so both tests would skip. Runner must set it; filter `FullyQualifiedName~RetainedInstructionAnalysisTests`. Expect a long run (81 uploads + 162 analyses on LocalDB).
- Open: (1) no production `IReadLogicalDocumentVersion` in any host — A04 still owes it, C stand-in remains; (2) the plan's "multiple profiles return Ambiguous" bullet conflicts with treating Ambiguous as a failure for the 81 labelled originals — read as being about no-route samples; a real expected-Ambiguous row would need an outcome field on the expectation record.
- Report: `C:\Users\PGUSER\AppData\Local\Temp\claude\C--Users-PGUSER-documents-github-pegasus\5adc2fb3-f15d-4145-84ed-948eb9fde4e4\scratchpad\takeover\c01-all15-report.md`

## C01 round 1 — READY_FOR_TESTS, head `d57383b2b` (2026-09-07)

- PCH cause: NOT a retained-vs-direct divergence. The Top15 artifact from the same wave-38 run lists PCH 01-05.DOC under `## Inconclusive` ("the reader returned incomplete content") — the direct suite reads the same bytes just as incompletely and SKIPS them (`Top15InstructionCorpusTests.cs:641`). Chain: `DocMsg.cs:81-88` sets `IsIncomplete` for a Partial legacy .doc → `AnalyzeRetainedInstruction.cs:363-373` guarded Status only → `:417` calls Extract outside the try/catch → `PchInstructionExtractionPolicy.cs:312-317` throws. Owner: production, C01's own file.
- BIGGER FINDING, needs an owner decision: **31 of 81 originals cannot reach extraction at HEAD** — 25 legacy `.DOC` (PCH, RJS, ALS, BC, MP Word, all 5/5) + 6 low-text PDFs (MP PDF/Weird). **5 of 15 profiles have zero deliverable samples.** The plan's "all 15 profiles reach extraction" is unachievable until the reader/OCR gap is closed; that reader is not C-owned. Honest ceiling today: 10/15 profiles, ~50/81 originals.
- Fix (F-001): `IsIncomplete` → `SourceUnavailable` before selection, reason built from the reader's own issues, nothing persisted. No policy guard touched. Core test `AnIncompletelyReadSourceIsUnavailableAndRecordsNothing`.
- Robustness (F-002): typed catch (`ArgumentException or InvalidOperationException or IntakeArtifactIntegrityException`, no CA1031 blanket), matrix written in a `finally`, INCONCLUSIVE bucket mirroring Top15 (corroborated by the receipt's own OcrRequired / ScannedPdfPages / `insufficient-embedded-text`), per-profile coverage table, `analysed > 0` gate.
- Minors: F-003 `TryAddScoped` so the combined host uses A's real `LocalLogicalDocumentVersionReader` and the C stand-in fills the port only in the standalone tree; F-004 report corrected (the drain DOES call `AttemptAutomaticAsync`; zero Cases holds because manual upload establishes no principal); F-005 QDOS comment restated with the channel limitation; F-006 principal disposition tightened to `Usable`; F-008/F-009/F-010 stated in comments (F-009 cites `AnalyzeRetainedInstructionTests.cs:148`). F-007 deliberately not taken.
- Gates: Core.Tests build 0W/0E exit 0; Integration build 0W, 1E — only the A-owned `CS0246 EfCaseArtifactCustody`. First round-1 build found `CA1859` in the new code; fixed before commit.
- Next run should PASS with ~50 Analysed / 31 Inconclusive / 0 Failed. Read the coverage table, not the exit code — green does not mean all 81.
