# TICK-039 files

Delivered by [[SIMPLI-013]]'s branch (`task/simpli-013-collisiondocnet-integration`); the `.doc` slice touches:

- `src/Pegasus.Infrastructure/Intake/DocumentExtraction/Cfb/` — bounded MS-CFB reader (shared with INT-15).
- `src/Pegasus.Infrastructure/Intake/DocumentExtraction/Word/` — FIB/CLX/piece-table text extractor.
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` / `.DocMsg.cs` — `Doc` dispatch branch and fail-closed fallback.
- `tests/Pegasus.IntegrationTests/DocumentExtraction/` — `WordBinaryExtractorTests`, `WordBinaryFixture`, CFB suites.
- `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` — `.doc` end-to-end and unreadable-container tests.
- `docs/frd/frd-05-documents-extraction-and-custody.md`, `docs/capabilities.md` (INT-14 note), `workspaces/README.md`, `docs/runbook.md`.
