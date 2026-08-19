# Research — TICK-206: renderer template-to-capability activation map

## Question

Which CollisionRenderer templates have an authorised Pegasus capability and real activation scope, and what disposition should the remaining workspace catalogue receive when the renderer is integrated?

## Findings

- The operator decision for EPIC-004 is explicit: **activate only the `rendererref1` assessment-report family and its fee-note family; every other workspace catalogue entry remains inactive**. “Inactive” means unavailable through the Pegasus Core workflow and its Web/Worker composition, not silently mapped to a convenient capability.
- The integration boundary is already accepted: CollisionRenderer becomes an Infrastructure adapter behind a Core-owned render contract and a real application caller; it is not a separate API, MCP host, package, repository, or deployment. Source: `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` and `EPIC-004/context.md`.
- The only directly authorised initial capability mapping is:
  - shared deterministic renderer/design/validation/computation mechanics → `RPT-01`;
  - four assessment outcomes, the bundled/associated fee note, and itemised repair specification → `RPT-02`;
  - activation from accepted Core-owned data → `EXT-08`;
  - upstream source and professional findings → `CASE-31` and `ENG-02`.
  Source: `docs/capabilities.md`.
- `reference/rendererref1/` contains exactly the evidence needed for that active family: a schema and JSON/PDF samples for `total_loss`, `repairable`, `cash_in_lieu`, and `contract_repair`; every sample PDF includes the assessment bundle, repair-specification lists, statement/signature, and fee-note page. Source: `reference/rendererref1/DESIGN_SPEC.md`, `report_data_schema.json`, `sample_job_*.json`, and `Sample - *.pdf`.
- The workspace render catalogue currently exposes 12 IDs: `market-valuation-evidence`, `advert-evidence-pack`, `fee-note`, `expert-report`, `blank-letterhead`, `repairable-contract-repair-report`, `total-loss-report`, `addendum-report`, `diminution-rebuttal`, `roadworthy-criminal-report`, `part-35-response`, and `response-letter`. Source: `workspaces/report-renderer/src/CollisionRenderer.Core/TemplateCatalog.cs` and `README.md`.
- The workspace catalogue does not line up one-to-one with the approved active family:
  - `fee-note` is reusable engine evidence for the active fee-note page/family, but its current standalone payload and wording are not automatically the authoritative `rendererref1` fee-note contract.
  - `repairable-contract-repair-report` combines two outcomes that `rendererref1` distinguishes.
  - `total-loss-report` represents only one of four outcomes.
  - no distinct `cash-in-lieu` entry exists.
  - `expert-report` is a generic free-form block document, not the typed `rendererref1` assessment schema.
  Therefore activation cannot safely be implemented by exposing the current IDs wholesale or by treating their names as the capability map.
- Proposed active logical family:
  - one closed assessment-report operation with the four Core-owned outcome values defined under TICK-204, rendered through the `rendererref1` structure;
  - one fee-note artifact/family generated from accepted billing data and, where required, attached to the assessment bundle.
  Whether these become two internal template descriptors or one bundle composer plus a reusable fee-note component is a planning/implementation choice; the capability map stays `RPT-01` + `RPT-02`.
- Proposed inactive catalogue disposition:
  - `market-valuation-evidence`, `advert-evidence-pack`, `expert-report`, `blank-letterhead`, `addendum-report`, `diminution-rebuttal`, `roadworthy-criminal-report`, `part-35-response`, and `response-letter` are **not exposed or callable** in the integrated Pegasus application.
  - current `repairable-contract-repair-report` and `total-loss-report` are workspace evidence, not the final public IDs/contracts; they are replaced/superseded internally by the approved four-outcome assessment family rather than exposed alongside it.
  - `fee-note` mechanics may be reused, but only behind the accepted fee-note contract.
- Some inactive entries resemble allocated later capabilities (`RPT-04` diminution and `RPT-05` addenda; `RPT-03` audit has no faithful current template). Similarity is not activation: those capabilities require their own accepted inputs, wording, caller, approval, recovery, and evidence. The generic presets cannot satisfy those contracts.
- Market valuation and advert evidence have adjacent upstream capabilities (`EXT-07`, `EXT-10`, `EXT-13`) but none authorises those two renderer documents as active Pegasus outputs. Their current workspace tests prove mechanics, not product authority.
- Tests currently encode “all 12 are built in,” require every authoring entry to map to a render entry, and attempt every catalogue entry in the Chromium integration theory. This is incompatible with an application allow-list unless tests distinguish the retained engine/source catalogue from the much smaller application-activated catalogue. Source: `workspaces/report-renderer/tests/CollisionRenderer.Core.Tests/CoreTests.cs`, `PreviewAndStarterTests.cs`, and `IntegrationTests.cs`.
- The current generic `ExpertReportDocument` validator requires only a title and one section; it cannot prove report readiness, accepted data, variant-specific required fields, computed-once figures, or the closed active family. Source: `workspaces/report-renderer/src/CollisionRenderer.Core/Models/Documents.cs` and `Validators.cs`.
- FRD-11 governs immutable version/hash, provenance, approval, correction, and delivery but currently does not record the initial active template-family allow-list. The durable mapping belongs there; `docs/capabilities.md` remains a registry/schedule and should not become a second behavior table.

## Implications

- Integration needs an explicit Core-owned allow-list or closed operation vocabulary for the assessment and fee-note families. Arbitrary caller-supplied workspace template IDs must be rejected before Infrastructure rendering.
- “Inactive” is the correct disposition for unsupported catalogue entries. They need not be deleted merely to prevent activation, but they must not appear in Pegasus UI/API/MCP discovery, validation, render endpoints, dependency injection, or runtime dispatch.
- If unused workspace presets are copied into production code, they increase policy and test surface without a caller. The simplest compliant integration is to bring across only the rendering mechanisms/assets required by the approved family and leave unsupported presets behind or remove them during workspace retirement.
- The application mapping should be documented once in FRD-11 and tested at the actual caller: the two approved families succeed when ready; every legacy/unknown template ID fails closed.
- Later activation of `RPT-03`, `RPT-04`, `RPT-05`, valuation evidence, Part 35, letters, or other outputs requires a separate governing behavior/caller change. No dormant switch or discoverable placeholder should be added now.
- TICK-204 supplies the four outcome vocabulary/behavior; TICK-206 supplies the capability/activation boundary; SIMPLI-014 implements that combined contract.

## Open questions

None. The operator’s activation decision resolves the product choice: only the `rendererref1` assessment and fee-note families are active; the remaining workspace catalogue is inactive.
