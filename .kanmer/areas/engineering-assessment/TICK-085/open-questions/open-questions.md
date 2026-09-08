# Open questions — TICK-085

## Resolved authority and scope

- [x] Root approved typed imported versus pending/unknown outcome, existing
  ImportRawEstimate as sole orchestrator, no HTTP wait or second dispatcher.
- [x] Root TICK-041 owns shared narrow anonymous Type3 font qualification and
  exact document OCR context. Public PdfPig Letter.Font is FontDetails, not
  IFont; resource selection uses public text operations and dictionaries.
- [x] Root approved current FRD-10 raw MCP import correction. Ordinary
  Automation SaveEstimate remains AiDraft/job-bound; a separate imported-
  document method on the existing store shares persistence, not a public
  trusted flag or user-chosen-provider permission bypass. Current acceptance
  remains Engineer-only.
- [x] No source-evidence schema: original retained artifact and row provenance
  preserve identity/rates/totals, while parsed source arithmetic is validated.
  Existing computed-total/line-origin JSON must not be repurposed.
- [x] LT72PYX PR is Partial repair in its actual page 6 legend, not a typo or
  unknown operation. PDF labour time is already net, unlike XML gross time.
- [x] Whole-page drop and removal of the old import dialog belong to ENG-033;
  TICK-085 repairs the canonical handler and provides retained-source
  completion. No second competing upload command is introduced.

## Before execution

- [x] TICK-041 merged d367219669ad26d5f2b727bd10b582330febc906 with ADR-0040. Exact API: IntakeOcrOperations.BeginDocumentAsync(IIntakeOcrOperationStore, CaseDocumentMetadata, IReadOnlyList<int>, CancellationToken); normalized pages and source Case/occurrence/version/hash bind its ID and persisted length. Completed IntakeOcrOperation.Result/PageResults are retained neutral evidence. PdfOcrQualification.HasUnusableTextMap(PdfDocument, Page) owns only the positive Type3 text-map fault, not scan policy. No parallel OCR request/worker.
- [x] ENG-041 merged baafa29e0f7002b8235aa43bf333f5d9bb172828; current integration includes its accepted interruption/replay/authority corrections.
- [x] CASE-049 merged 3a5ce645cfc0872d7a4324c6818497360c39cca4, passed exact merged acceptance and closed out with claim released; PLAT-072 is likewise verified Done/closed. Native handoff and current workspace access are accepted.
- [x] Root inspected EstimateImport, IRepairSpecificationStore, EfRepairSpecificationStore.Guard, CaseMutationGuard and explicit IAddCaseDocument. The plan now requires non-mutating persisted authority before OCR/replay and distinguishes one fresh staff custody mutation from version-neutral automatic confirmation and custody replay.
- [ ] ENG-041 PR691 correction must integrate and release its FRD-06 ownership; its independent PASS is awaiting final merge. TICK-035 must integrate and release Infrastructure DI/ProductionComposition ownership. Root coordinates execution after these actual overlapping claims release.

## Evidence work in the bounded checklist

Historical full-line oracles and recorded YL Azure evidence are absent locally;
this is not a credential lock or permission question. Build fresh reviewed
line oracles from the immutable five originals in the local evidence lane and
use the TICK-041/PLAT-065 captured provider result when supplied. Section-level
observations already exist in pegasus_pack/current/glass-pdf-research.json.
Do not call a synthetic/gibberish fixture a passing fifth-sample OCR oracle.
