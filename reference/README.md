# Reference evidence

Supplied and raw material is evidence, not a requirement, implementation proof,
current directory, or authorization. Accepted behavior lives in
[requirements](../docs/prd/README.md), current callers in
[architecture](../docs/current-architecture.md), unresolved questions in
[open decisions](../docs/open-decisions.md), operator truth in
[operator notes](../docs/operator-notes.md), and UI rules in
[design](../docs/design/README.md).

Never infer currentness from a filename. Do not copy personal names, addresses,
or contact rows into canonical prose. Workbooks and samples are not import
authority.

## Retained sources

- `EVA/` — external EVA API schema evidence; no access or caller proof.
- `eva_information/` — reviewed EVA notes, example payloads, and screenshots;
  evidence only.
- `workproviders-and-repairers/` — raw historical provider, repairer, contact,
  and job spreadsheets; no automatic import. Its versioned
  `principal-identification-corpus.v1.json` is a reviewed, hash-bound crosswalk
  and criteria dossier over those sources, immutable local mail, and the
  read-only CollisionSpike evidence. The JSON is not loaded by runtime code;
  accepted behaviour remains explicit in versioned Core policies.
- `rendererref1/` — supplied report samples, design notes, logo, and signature
  sources. Its logo and three signature files are byte-identical to the four
  same-named governed assets under `docs/design/brand/`. Both placements remain:
  `reference/` preserves the supplied evidence grouping while `docs/design/` owns
  runtime use, so byte equality does not make either role or path redundant.

## Retained reviewed reports

These reports remain source-labelled evidence and accepted-decision provenance.
Their current product clauses are centralized in
[requirements](../docs/prd/README.md); retention does not make predecessor
implementation or raw source rows authoritative. Reports overtaken by current
documentation are removed; git history retains them.

- [Collision Engineers administrative workflow observations](reports/collision_engineers_admin_overview.md)
- [UI/UX interaction findings](reports/ui-ux.md)

## Handling rules

- Preserve retained raw bytes. Treat supplied schemas, screenshots, examples,
  workbooks, and contact exports as temporal evidence, not product policy or
  live integration proof.
- Preserve source evidence outside the tracked package as well: the package
  deduplicates originals by SHA-256 and records source locations, metadata,
  deterministic groups, and development/holdout assignment without rewriting
  an email, PDF, Office document, image, fixture, workbook, or CollisionSpike
  file.
- Any future import or directory use requires operator review, an accepted data
  contract, and separately authorized target operations.
