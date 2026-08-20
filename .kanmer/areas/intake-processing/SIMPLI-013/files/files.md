# SIMPLI-013 file map

## New — imported extraction source (from `workspaces/document-extraction`, renamespaced, internal)

- `src/Pegasus.Infrastructure/Intake/DocumentExtraction/CompoundFile/` — 11 files from `CollisionDocNet.Storage/CompoundFile/*` (reader unchanged).
- `src/Pegasus.Infrastructure/Intake/DocumentExtraction/Word/` — `WordBinaryExtractor.cs` (guard-CP + CP1252 + surrogate fixes; structured-evidence call removed), `WordFibParser.cs` (reserved-field fix, cbMac read), `WordPieceTableParser.cs` (cbMac bound), `WordBinaryModels.cs` (trimmed), `WordBinaryExtractionLimits.cs` (trimmed).
- `src/Pegasus.Infrastructure/Intake/DocumentExtraction/Msg/` — `MsgReader.cs`, `MapiPropertyReader.cs`, `MsgModels.cs`, `RtfCompression.cs` (`\htmlrtf` toggle fix).
- `src/Pegasus.Infrastructure/Intake/DocumentExtraction/TextSanitation.cs` — lone-surrogate replacement helper (shared by Word decode and the msg adapter).

## Modified — application

- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` — `SourceFormat.Deferred` → `Doc`/`Msg`; dispatch branches; failure message `:118`; `ReaderVersion`.
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.DocMsg.cs` — new partial: `ReadDoc`, `ReadMsgAsync` mapping to fragments/assets/transport/issues; attachment re-dispatch; fail-closed fallback.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — labels for the new issue codes.
- `docs/frd/frd-05-documents-extraction-and-custody.md` — engine-boundary sentence (PdfPig stays the PDF path; CollisionDocNet-derived readers scoped to .doc/.msg).
- `workspaces/README.md` — register/provenance updated to integrated-and-retired (report-renderer precedent).
- `.github/workflows/workspaces.yml` — deleted (no live workspace remains).
- `workspaces/document-extraction/` — deleted (superseded by the import).
- `workspaces/AGENTS.md` — check/adjust if it names document-extraction.

## Tests

- `tests/Pegasus.IntegrationTests/DocumentExtraction/` — xunit conversions of `CompoundFileHeaderReaderTests`, `CompoundFileReaderTests`, `CompoundFileFixture`, `WordBinaryExtractorTests`, `WordBinaryFixture`, `MsgReaderTests`, `RtfCompressionTests`; expectations updated where a Phase A fix changes behaviour.
- `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` — `.doc`/`.msg` end-to-end extraction tests (real bytes via fixture builders); Deferred test updated to the new unreadable-container fallback; existing PDF/DOCX/EML tests unchanged.

## Unchanged on purpose

- `Pegasus.slnx`, `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` (no project-set change; no new external dependency), `src/Pegasus.Infrastructure/DependencyInjection.cs` (same reader class registered), `src/Pegasus.Core/**` (port reused as-is), `Upload.cshtml` (already accepts .doc/.msg).
