# Proof — TICK-040 (INT-15)

Type: command-log. Delivered by the SIMPLI-013 parsers in **release 14** (`d91fd7d7…`, PR #449), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Verification lane at the cut: `.msg` detection and dispatch to `ReadMsgAsync` (`MsgReader` incl. compressed-RTF handling); sender/subject transport evidence; attachments re-enter `DispatchAsync` so nested PDFs reach PdfPig (one PDF owner); nesting capped at 8; reference/OLE-only attachments stay passive with an explicit issue; protected messages fail closed. End-to-end test `OutlookMessageBodyAndPdfAttachmentDriveTheRealIntakePipeline` plus MsgReader/RtfCompression/raw-CFB fixture suites.
- Live: production Upload accepts `.msg`; the deployed worker pipeline extracts it in-process.
- Full transcript: DELIV-013 scratch.
