# SIMPLI-013 post-implementation report — 2026-08-20

PR: https://github.com/collisionengineers/pegasus/pull/449 (`task/simpli-013-collisiondocnet-integration` → `dev`; commits c7457628 + d999277d). Implements this ticket and delivers capabilities INT-14 ([[TICK-039]]) and INT-15 ([[TICK-040]]).

## What was delivered vs the plan

- **Scope decision executed as planned**: CollisionDocNet scoped to `.doc`/`.msg`; PdfPig remains the only live PDF implementation (ADR-0001/ADR-0003 overlap resolved per ADR-0025's first option; FRD-05 sentence added).
- **Integration shape**: the MS-CFB, legacy Word, and Outlook-item readers folded into `Pegasus.Infrastructure/Intake/DocumentExtraction/{Cfb,Word,Msg}` — ADR-0025's fold-in option, so no new project, no `Pegasus.slnx` change, no architecture-test project-set change (the ticket body anticipated slnx/DependencyDirectionTests updates; none were needed and the imported code is pure BCL, so the existing forbidden-dependency assertions keep guarding). Left behind by design: Conversion/`DocumentExtractor`, Pdf, Email, Writer.OpenXml, Cli, Model, Core, `Storage/{Detection,Ole,Opc,Xml,Zip}`, `WordStructuredEvidenceParser` — reasons per item in the research document.
- **Phase A correctness fixes ride with the import** (feature-matrix defects; full disposition table in research): reserved FibBase fields no longer falsely reject genuine files; compressed text decodes CP1252 unconditionally (MS-DOC 2.9.73); `cbMac` bounds enforced on piece ranges; lone surrogates replaced (shared `TextSanitation`); the guard CP moved after the last specialised story (subdocument text no longer off by one); `\htmlrtf` suppression added to the RTF fallback. Dispositions recorded for the rest (detector nFib defect avoided — detector not imported; SPRM/formatting coverage irrelevant to plain-text intake; TNEF/recurrence/S-MIME fidelity honestly parked — protected items classify Encrypted and fall back).
- **Adapter**: `SourceFormat.Doc`/`Msg` replace `Deferred` in `MimeKitPdfPigOpenXmlIntakeSourceReader`; new partial implements `ReadDoc`/`ReadMsgAsync` in the `ReadPdf`/`ReadDocx` shape. `.msg` attachments re-enter `DispatchAsync` under the existing MIME byte budget; embedded messages map recursively; root `.msg` sender/subject become transport evidence with the existing sender-identity threading. Fail closed: unreadable/encrypted/oversized containers land the pre-existing `NeedsSorting` outcome with `FailureCode == null`; no macro/OLE/script/external content is ever opened. `ReaderKey` stable; `ReaderVersion` gains `collisiondocnet-doc-msg-0.1`.
- **Deviations from plan** (all recorded in the plan's simplification section): OperatorLabels change unnecessary (issue reasons are self-carried sentences, `pdf-engine` precedent); `CompoundFile` folder renamed `Cfb` (namespace/type collision); imported test names de-underscored for the repo's enforced CA1707.
- **Workspace retired**: `workspaces/document-extraction/` and `.github/workflows/workspaces.yml` deleted; `workspaces/README.md` register/provenance updated on the report-renderer precedent; runbook workspace section updated; `docs/capabilities.md` INT-14/INT-15 notes record "locally implemented and test-backed" (MAIL-01 convention — deployment and operator acceptance remain separate evidence).

## Evidence (exact counts)

- `tests/Pegasus.IntegrationTests/DocumentExtraction/`: 136/136 (converted CFB/Word/Msg/RTF suites + `MsgFileBuilder` roundtrip; includes new tests pinning cbMac bounds, CP1252 decode, lone-surrogate replacement, guard placement).
- Focused intake regression (`MultiFormatIntakeWebTests` + `InlineForwardedMailRouteTests` + `IntakeWebNegativeTests` + `UploadOutcomeQueriesTests` + DocumentExtraction): 202/202 in 5m59s — PDF/DOCX/EML paths untouched and green.
- End-to-end through the real Web pipeline: `.doc` upload extracts text (`doc-engine` evidence, `NeedsSorting`, no failure code); `.msg` upload reaches `CaseCreated` with QDOS fields extracted from its body and its PDF attachment processed by PdfPig (`pdf-engine` evidence); bare-CFB `.doc`/`.msg` fall back to `NeedsSorting` (updated legacy-container test).
- `Pegasus.ArchitectureTests` 97/97; `Pegasus.Core.Tests` 691/691; `dotnet build Pegasus.slnx -c Release` zero warnings (TreatWarningsAsErrors, no suppressions added).
- Fixtures are built programmatically (`WordBinaryFixture.CreateRawCfb`, new `MsgFileBuilder` raw-CFB builder) — no corpus material touched.

## Simplification pass

Run (author lens + independent `code-simplifier`), three behaviour-preserving fixes applied in commit d999277d, four findings declined with reasons — dated record in the plan.

## Open items for review

- The `.doc` embedded-picture gap is parked in open-questions (no decodable image bytes exist in the imported slice; EXT-DOC-009).
- Verification ticks in the ticket body are satisfiable on merge; proof is written on merged main per process.
