# Reference evidence

Supplied and raw material is evidence, not a requirement, implementation proof,
current directory, or authorization. Accepted behavior lives in
[requirements](../requirements.md), current callers in
[architecture](../architecture.md), unresolved questions in
[open decisions](../open-decisions.md), operator truth in
[operator notes](../operator-notes.md), and UI rules in
[design](../../design/README.md).

Never infer currentness from a filename. Do not copy personal names, addresses,
or contact rows into canonical prose. Workbooks and samples are not import
authority.

## Retained sources

- `EVA/` — external EVA API schema evidence; no access or caller proof.
- `eva_information/` — reviewed EVA notes, example payloads, and screenshots;
  evidence only.
- `workproviders-and-repairers/` — raw historical provider, repairer, contact,
  and job spreadsheets; no automatic import.
- `rendererref1/` — report-renderer reference material.

## Retained reviewed reports

These reports remain source-labelled evidence and accepted-decision provenance.
Their current product clauses are centralized in
[requirements](../requirements.md); retention does not make predecessor
implementation or raw source rows authoritative. Six reports whose unqualified
present-tense claims had been overtaken by current documentation were removed
on 2026-08-02 (EVA API preference, historical correspondence boundary, manual
chaser history, parser boundary, repository data authority, and UI required
fields); they remain in git history.

- [Case/PO decision](reports/case-po-info.md)
- [Repairer identity and case-party roles](reports/repairer-identity-and-case-party-roles.md)
- [Collision Engineers administrative workflow observations](reports/collision_engineers_admin_overview.md)
- [Provider API intake finding](reports/provider-api-intake-already-covered.md)
- [Report delivery and post-report lifecycle](reports/report-delivery-and-post-report-lifecycle.md)
- [Suggestion-first image analysis and VRM recognition](reports/suggestion-first-image-analysis-and-vrm-recognition.md)
- [UI/UX interaction findings](reports/ui-ux.md)
- [VRM correlation and source deduplication](reports/vrm-correlation-and-source-deduplication.md)

## Handling rules

- Preserve retained raw bytes. Treat supplied schemas, screenshots, examples,
  workbooks, and contact exports as temporal evidence, not product policy or
  live integration proof.
- Any future import or directory use requires operator review, an accepted data
  contract, and separately authorized target operations.
