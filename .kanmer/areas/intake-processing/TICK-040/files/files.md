# TICK-040 files

Delivered by [[SIMPLI-013]]'s branch (`task/simpli-013-collisiondocnet-integration`); the `.msg` slice touches:

- `src/Pegasus.Infrastructure/Intake/DocumentExtraction/Cfb/` — bounded MS-CFB reader (shared with INT-14).
- `src/Pegasus.Infrastructure/Intake/DocumentExtraction/Msg/` — `MsgReader`, `MapiPropertyReader`, `RtfCompression`/`PassiveRtfText`.
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs` / `.DocMsg.cs` — `Msg` dispatch branch, transport evidence, attachment re-dispatch, fail-closed fallback.
- `tests/Pegasus.IntegrationTests/DocumentExtraction/` — `MsgReaderTests`, `RtfCompressionTests`, `MsgFileBuilder(+Tests)`.
- `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` — `.msg` end-to-end and unreadable-container tests.
- `docs/frd/frd-05-documents-extraction-and-custody.md`, `docs/capabilities.md` (INT-15 note), `workspaces/README.md`, `docs/runbook.md`.
