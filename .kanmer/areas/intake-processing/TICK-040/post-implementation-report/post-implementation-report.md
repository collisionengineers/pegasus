# TICK-040 post-implementation report — 2026-08-20

INT-15 (automated MSG extraction) is implemented by [[SIMPLI-013]]'s PR https://github.com/collisionengineers/pegasus/pull/449 (branch `task/simpli-013-collisiondocnet-integration`, commits c7457628 + d999277d). SIMPLI-013's post-implementation report is the full record; the `.msg` capability facts:

- `.msg` sources yield body text (plain → inert-HTML text → passive RTF with the `\htmlrtf` encapsulated-HTML fix), sender/subject transport evidence with the existing sender-identity threading, and by-value attachments through the existing `IIntakeSourceReader` dispatch. Attachments re-enter the per-format pipeline (a PDF inside a `.msg` is processed by PdfPig — one PDF implementation); embedded messages map recursively as labelled fragments.
- Fail closed: unreadable, protected (S/MIME/rpmsg), or oversized items keep the pre-existing `NeedsSorting` outcome (`FailureCode == null`); reference/OLE-only attachments stay passive with an explicit issue; nothing is ever executed or decrypted.
- Evidence on the branch: `MsgReaderTests`/`RtfCompressionTests` plus the raw-CFB `MsgFileBuilder` roundtrip (within 136/136 DocumentExtraction tests); end-to-end `OutlookMessageBodyAndPdfAttachmentDriveTheRealIntakePipeline` reaches `CaseCreated` with QDOS fields extracted from the `.msg` body and `pdf-engine` evidence from its attachment; PDF/DOCX/EML regression 202/202 unchanged; Release build zero warnings.
- `docs/capabilities.md` INT-15 note records "locally implemented and test-backed"; deployment and operator acceptance remain separate evidence.

This ticket's remaining step is the review/merge of PR #449; proof is written after merge per process.
