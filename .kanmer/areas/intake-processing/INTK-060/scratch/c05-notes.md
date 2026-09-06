## C05 (Core slice) — assumptions recorded under M8

- [ ] ASSUMPTION 1 (implementer, attempt 1): the selector's `NotApplicable` outcome carries an
  explicit reason, and a source whose readable text is empty (the PDF is scan-only) resolves to
  `NotApplicable(TextUnavailableRequiresOcr)` rather than to a family or a negative document role
  — because two of the 29 originals (`JohnRBell1.pdf`, `TonBridgeAccidentRepair1.pdf`) carry no
  extractable text at all, so any family or role verdict for them would be inferred from the
  filename/folder, which the review and the dispatch forbid ("no folder/filename inference",
  "never fabricated"). The dispatch's three outcome kinds (Selected / NotApplicable / Ambiguous)
  are preserved; only the NotApplicable reason is new. Alternatives: (a) a fourth outcome kind —
  rejected, the dispatch fixes three; (b) classify the two by filename — rejected, forbidden;
  (c) leave them unclassified with no reason — rejected, the corpus assertion could then not tell
  "scan-only" from "unknown layout".
- [ ] ASSUMPTION 2 (implementer, attempt 1): where a family prints one combined vehicle
  description ("Vehicle: RENAULT CLIO ICONIC TCE" in the Connexus/Exclusive/EVA narrative
  layout), `vehicle.model` carries the printed text with disposition `Ambiguous` and
  `vehicle.make` stays `Missing` — because the source does not separate make from model, and the
  only existing two-word-make list lives in `QdosInstructionExtractionPolicy` (private, and that
  file is outside this slice's files map, so promoting or copying it would breach M5 and conduct
  rule 8). Families that label Make and Model separately (Laird, Montgomery, sPrint) extract both
  as `Usable`. Alternatives: (a) split on the first token — rejected, wrong for RANGE ROVER /
  MERCEDES-BENZ and it would be a fabricated fact; (b) copy the makes list into this slice —
  rejected by rule 8 and M5.
