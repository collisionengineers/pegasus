# TICK-040 research — INT-15 automated MSG extraction

Implemented by [[SIMPLI-013]] (its research document carries the full analysis; this ticket records the capability view).

- Activation boundary resolved: operator direction 2026-08-20; CollisionDocNet scoped to `.doc`/`.msg` (ADR-0025 first option, recorded in FRD-05 and `docs/capabilities.md` INT-15).
- Core policy owner: `IIntakeSourceReader` — unchanged; no new Core surface.
- Real caller: `MimeKitPdfPigOpenXmlIntakeSourceReader` gains a `SourceFormat.Msg` dispatch branch delegating to the integrated `MsgReader` (`src/Pegasus.Infrastructure/Intake/DocumentExtraction/Msg/`): MAPI property bag, body policy plain → HTML(inert text) → compressed-RTF passive text (with the `\htmlrtf` encapsulated-HTML suppression fix), recipients, and by-value attachments. Root `.msg` sender/subject become transport evidence with the same sender-identity threading as `.eml`; attachments re-enter the existing per-format dispatch (a PDF inside a `.msg` goes through PdfPig — one PDF implementation); embedded messages map recursively as labelled fragments.
- Honest dispositions: S/MIME/rpmsg classify Encrypted and fall back to manual sorting; reference/OLE-only attachments stay passive with an explicit issue; TNEF/recurrence/calendar-fidelity gaps are irrelevant to body-plus-attachments intake (full table in SIMPLI-013 research).
- Evidence: `MsgReaderTests`/`RtfCompressionTests` (converted), `MsgFileBuilder` raw-CFB fixture, and `MultiFormatIntakeWebTests.OutlookMessageBodyAndPdfAttachmentDriveTheRealIntakePipeline` — a `.msg` upload reaches `CaseCreated` with QDOS fields extracted from its body and its PDF attachment processed by the PdfPig path.
