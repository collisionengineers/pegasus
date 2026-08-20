# Proof — SIMPLI-013

Type: command-log + visual. Released in **release 14** (`d91fd7d7…`, PR #449), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Verification lane at the cut: bounded parsers live under `src/Pegasus.Infrastructure/Intake/DocumentExtraction/{Cfb,Word,Msg}` behind the partial `MimeKitPdfPigOpenXmlIntakeSourceReader.DocMsg.cs` (`ReaderVersion` gains `collisiondocnet-doc-msg-0.1`); `workspaces/document-extraction` deleted with the register marked "Integrated and retired (SIMPLI-013, ADR-0025)"; zero workspace references in `Pegasus.slnx`, no dynamic loads; FRD-05 one-engine-per-format sentence holds.
- Live: the production Upload page advertises `.doc` and `.msg` as accepted formats; the deployed reader carries the parsers (release-14 image).
- Docs: current-architecture refreshed in-release to record live DOC/MSG extraction with the reader bounds (PR #475).
- Full transcript: DELIV-013 scratch.
