# TICK-039 research — INT-14 automated legacy DOC extraction

Implemented by [[SIMPLI-013]] (its research document carries the full analysis; this ticket records the capability view).

- Activation boundary resolved: the operator directed the work proceed on 2026-08-20, scoping CollisionDocNet to `.doc`/`.msg` with PdfPig remaining the PDF path (ADR-0025's first option; recorded in FRD-05 and `docs/capabilities.md` INT-14).
- Core policy owner: `IIntakeSourceReader` (`src/Pegasus.Core/Intake/IntakeContracts.cs`) — unchanged; no new Core surface.
- Real caller: `MimeKitPdfPigOpenXmlIntakeSourceReader` gains a `SourceFormat.Doc` dispatch branch delegating to the integrated `WordBinaryExtractor` (`src/Pegasus.Infrastructure/Intake/DocumentExtraction/Word/`), imported from `workspaces/document-extraction` with its correctness defects fixed (reserved-FibBase-field false rejection, unconditional CP1252 compressed decode, cbMac piece bounds, lone-surrogate replacement, guard-CP placement) — dispositions per defect in SIMPLI-013's research.
- Failure behaviour: fail closed — an unreadable/encrypted/oversized `.doc` keeps the pre-existing honest manual-sorting outcome (`NeedsSorting`, no failure code) instead of failing intake.
- Evidence: converted parser unit suites plus end-to-end web tests (`MultiFormatIntakeWebTests.DirectLegacyDocTextIsExtractedThroughWebCaller`, `UnreadableLegacyContainersFallBackIntoNeedsSortingWithoutReference`).
