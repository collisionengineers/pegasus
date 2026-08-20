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

## Simplification pass — 2026-08-20

Run over the branch's own diff with two lenses: author self-review plus an independent `code-simplifier` agent (reuse, simplification, efficiency, altitude), scoped to the new application/test-support code (imported parser sources reviewed but not restructured — they carry the deliberate Phase A fixes only). Verified after fixes: Infrastructure and IntegrationTests builds zero warnings; DocumentExtraction suites 136/136.

Applied (commit d999277d):
1. `MimeKitPdfPigOpenXmlIntakeSourceReader.DocMsg.cs` — `SanitizeText(senderAddress)` computed twice; hoisted to a local and reshaped the guard to match the sibling `subject` block's `TryGetValue && !IsNullOrWhiteSpace` convention.
2. Same file — replaced the one fully-qualified `System.Globalization.CultureInfo` with a using directive (dominant repo convention, incl. the sibling `MsgReader.cs`).
3. `TextSanitation.ReplaceLoneSurrogates` — collapsed the duplicated detect-then-replace two-pass scan into one traversal with a lazily materialised `char[]`; same allocation profile (no array when clean), pair-skipping rule now stated once. Edge cases (lone high/low, trailing surrogate, `\uD800𐀀`) re-checked; covered by `ExtractLoneSurrogateInUnicodePieceIsReplacedAndVisible`.

Considered, not applied (with reasons):
4. Duplicated unreadable-container message strings in `ReadDoc`/`ReadMsgAsync` (catch + switch default) — hoisting would allocate the interpolated string on every successful read; a shared helper across the two methods would be an abstraction without a third caller.
5. Unifying the two outcome switches — only the control-flow shape is shared; outcome enums, codes, and operator wording all differ. A delegate/parameter-object mapper would read worse (no-abstraction rail).
6. `out bool replaced` on `ReplaceLoneSurrogates` looked dead from the adapter (`out _`) but has a genuine second caller (`WordBinaryExtractor` raising `doc-lone-surrogate-replaced`). Kept.
7. `MsgFileBuilder` — left as is; layout logic each stated once; remaining nits are fixture-only micro-allocations not worth churn.

Plan deviations noted during execution: step 4 (OperatorLabels) turned out to be unnecessary — issue reasons are self-carried sentences, matching the `pdf-engine`/`openxml-engine` precedent, and the fail-closed design introduces no new failure codes; the `CompoundFile` folder was renamed `Cfb` because the namespace segment collided with the `CompoundFile` type; imported test method names were de-underscored to satisfy the repo's enforced CA1707 convention.
