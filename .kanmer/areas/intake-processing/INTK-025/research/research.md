# Research — INTK-025

Grounding is [[INTK-024]]'s approved mapping; the additional premises checked:

- **Report recognition:** every report document in the corpus carries
  "report" in its retained file name (`Bodyshopreport…`, `EngineersReport…`,
  `Bodyshopsuppreport…`, `qdos26005-original-report.pdf`); the instruction
  letters never do (`…LtrtoEngineerIn`, `…LtrtoAuditEngin`,
  `Letter to Collision Engineers…`). Fragment source labels carry the file
  name, so report scoping keys off the fragment's source label.
- **Report `Vehicle:` line:** present in both bodyshop shapes; in the
  PdfPig-read 555017 text it runs together with the following columns
  ("…ECOBLUE 4Colour: Black Speedo: Miles"), so the synthesized value must
  cut at the report's own column labels (`Colour:`, `Speedo:`, `Reg No:`).
  The run-together raw value self-rejects today (the make/model charset
  excludes ':'), which is why nothing lands.
- **Mileage:** the labelled `Mileage: 28000 Miles` row already extracts via
  the existing global label. `Speedo:` carries **no numeric value anywhere in
  the corpus** ("Speedo: Miles") — a Speedo synthesis emits only when digits
  are present, and no corpus file can positively test it; recorded as
  approved-but-unevidenced, guarded to digits, revisit when a valued
  instance appears. The guide mileage ("…at 82500 Miles") is prose, not a
  labelled line, and never matches.
- **Circumstances:** the letters' page 2 carries the prompt line ending
  "following accident circumstances?" followed directly by the paragraph,
  terminated by `Damage Area`/`Pre-existing Damage`/`TP `/"If you need"
  lines — all four terminators verified across the letter corpus.
- **TP guard relocation:** the engine's two label regexes embed `TP `
  literally (INTK-023); a `GuardedPrefixes` parameter on `FieldDefinition`
  keeps the mechanism neutral while QDOS supplies the grammar.
