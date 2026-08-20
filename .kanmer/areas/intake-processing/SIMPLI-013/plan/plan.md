# SIMPLI-013 plan

Direction fixed by ADR-0025 (integrate, not package) and the 2026-08-20 operator scope direction (CollisionDocNet scoped to `.doc`/`.msg`; PdfPig remains the PDF path — this resolves the ADR-0001/ADR-0003 overlap the ticket names; FRD-05 records it). Design and defect dispositions are in this ticket's research document.

## Steps

1. **Import the minimal closure** into `src/Pegasus.Infrastructure/Intake/DocumentExtraction/` (CompoundFile 11 files, Word 5 files, Msg 4 files), renamespaced `Pegasus.Infrastructure.Intake.DocumentExtraction.*`, all `internal`. Reuses: the existing `Pegasus.Infrastructure` project (ADR-0025's fold-in option — no new project, no slnx/arch-test change), existing `InternalsVisibleTo("Pegasus.IntegrationTests")`. Trim the Word surface to the text slice (drop `WordStructuredEvidenceParser`/`WordStructuredModels` and the result fields they fed).
2. **Apply the Phase A fixes inside the imported source** (each mapped to a matrix defect in research): reserved-FibBase-field false-corrupt removal; unconditional CP1252 compressed decode; `cbMac` piece bounds; lone-surrogate replacement (`TextSanitation`, shared); guard-CP moved after the last specialized part (+ `StoryLengths[3]` in the specialized check); `\htmlrtf` suppress toggle in `PassiveRtfText`. Fix any analyzer warnings properly (no blanket suppressions).
3. **Adapter**: extend `MimeKitPdfPigOpenXmlIntakeSourceReader` — `SourceFormat.Doc`/`Msg` replace `Deferred`; new partial file implements `ReadDoc`/`ReadMsgAsync` following the `ReadPdf`/`ReadDocx` shape (fragments, assets, issues, `IntakeExceptionPolicy.IsRecoverable` catch). `.msg` by-value attachments re-enter `DispatchAsync` under the existing `MimeLimitState` budget (one pipeline, one PDF implementation); embedded messages map recursively as labelled fragments; root `.msg` sender/subject become `IntakeTransportEvidence` with the existing `IntakeSenderIdentityKind` threading. Fail closed: any non-Complete/Partial reader outcome → issue + `Readable` → the existing `NeedsSorting` manual-sorting lane (no macro execution anywhere — extraction only). Update the `:118` failure message and `ReaderVersion`; `ReaderKey` stays stable (persisted provenance).
4. **Operator surface**: add `OperatorLabels` entries for the new issue codes (design/README.md style — one-sentence consequence, no jargon, no identifiers).
5. **Tests**: convert the production-facing workspace suites to xunit under `tests/Pegasus.IntegrationTests/DocumentExtraction/` (expectations updated where a fix intentionally changes behaviour — each noted in the test diff); add `.doc` and `.msg` end-to-end web tests to `MultiFormatIntakeWebTests` using programmatic fixture bytes (`WordBinaryFixture.CreateRawCfb` port + a minimal raw-CFB `.msg` builder — no corpus material); update the Deferred-containers test to the new fallback issue code; leave every existing PDF/DOCX/EML test untouched.
6. **Retire the workspace**: delete `workspaces/document-extraction/` and `.github/workflows/workspaces.yml`; update `workspaces/README.md` register + provenance (report-renderer retirement precedent); one FRD-05 sentence.
7. **Verify**: `dotnet restore`, `dotnet build -c Release` (zero warnings), focused `dotnet test` — new DocumentExtraction suites, `MultiFormatIntakeWebTests`, `InlineForwardedMailRouteTests`, `UploadOutcomeQueriesTests`, plus `Pegasus.ArchitectureTests` and `Pegasus.Core.Tests` in full.

## Test framework note (recorded reason)

The imported suites arrive as MSTest; the repo convention is xunit. They are converted rather than importing a second framework — mechanical assert/attribute mapping, reviewed against the originals.

## Acceptance

- A real `.doc` and a real `.msg` upload produce receipts with extracted text through the actual Web pipeline; a `.msg` PDF attachment routes through PdfPig.
- An unparseable/encrypted `.doc`/`.msg` still lands `NeedsSorting` with `FailureCode == null` (fail-closed parity with today).
- PDF/DOCX/EML behaviour unchanged (existing test classes green, no source changes on those paths).
- Zero build warnings; workspace and its CI lane removed; register updated.

## Simplification pass

(to be recorded before the PR)
