# Reference evidence

Supplied and raw material is evidence, not a requirement, implementation proof,
current directory, or authorization. Accepted behavior lives in
[requirements](../docs/requirements.md), current callers in
[architecture](../docs/architecture.md), unresolved questions in
[open decisions](../docs/open-decisions.md), operator truth in
[operator notes](../docs/operator-notes.md), and UI rules in
[design](../docs/design.md).

Never infer currentness from a filename. Do not copy personal names, addresses,
or contact rows into canonical prose. Workbooks and samples are not import
authority.

## Retained sources

- `EVA/` — external EVA API schema evidence; no access or caller proof.
- `eva_information/` — reviewed EVA notes, example payloads, and screenshots;
  evidence only.
- `workproviders-and-repairers/` — raw historical provider, repairer, contact,
  and job spreadsheets; no automatic import.
- `rendererref1/` — supplied report samples, design notes, logo, and signature
  sources. Its logo/signature bytes intentionally differ from the governed
  runtime assets under `design/brand/`: reference preserves evidence while
  design owns runtime use, so neither copy replaces the other.

## Retained reviewed reports

These reports remain source-labelled evidence and accepted-decision provenance.
Their current product clauses are centralized in
[requirements](../docs/requirements.md); retention does not make predecessor
implementation or raw source rows authoritative. Reports overtaken by current
documentation are removed; git history retains them.

- [Collision Engineers administrative workflow observations](reports/collision_engineers_admin_overview.md)
- [UI/UX interaction findings](reports/ui-ux.md)

## Handling rules

- Preserve retained raw bytes. Treat supplied schemas, screenshots, examples,
  workbooks, and contact exports as temporal evidence, not product policy or
  live integration proof.
- Any future import or directory use requires operator review, an accepted data
  contract, and separately authorized target operations.
