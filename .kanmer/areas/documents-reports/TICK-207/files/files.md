# Files — TICK-207

## Where the change lands

No repository implementation file is currently authorised to change. The following is the future surface after representative Audit evidence is supplied and explicitly approved.

| Path | Why |
|---|---|
| `reference/<approved-audit-evidence>/**` | Immutable supplied representative Audit report/template, schema/field notes and sample inputs; exact placement follows the existing reference index and must not be fabricated or edited. |
| `reference/README.md` | Index the supplied evidence and its provenance without promoting it to normative behaviour. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Restate approved Audit inputs, sections, wording, conditional/fail-closed behaviour and acceptance evidence. |
| `docs/design/assets/report-renderer/templates/<audit>.scriban` | Governed fixed Audit presentation after approval; no placeholder or assessment clone before then. |
| `docs/design/assets/report-renderer/templates/report.css` | Only approved Audit-specific layout additions, reusing the existing design rather than creating a second stylesheet. |
| `src/Pegasus.Core/Reports/**` | Future accepted Audit render payload/readiness contract, binding exact conservative/maximised specification versions and Core-derived uplift. |
| `src/Pegasus.Infrastructure/Reports/**` | Future fixed template descriptor/model mapping and renderer adapter implementation after CollisionRenderer integration. |
| `tests/Pegasus.Core.Tests/Reports/**` | Required/missing/ambiguous input and exact-version binding tests. |
| `tests/Pegasus.IntegrationTests/Reports/**` | Template/resource registration, deterministic rendering, wording and representative PDF/visual baseline tests. |
| `artifacts/**` | Generated comparison/visual-regression outputs; never write generated results into `reference/`. |

## Context files

| Path | What it tells the implementer |
|---|---|
| SIMPLI-014 `open-questions` | Binding operator decision: defer Audit rendering; assessment samples cannot invent Audit wording. |
| EPIC-004 `context.md` | Renderer integration, Core policy ownership, immutable report identity and evidence/reference authority boundary. |
| TICK-205 `research` | Accepted dual immutable conservative/maximised data model and computed uplift; presentation remains missing. |
| [[TICK-098]] | RPT-03 capability boundary and dependencies on structured case/engineering data, repair specification and Engineer outcomes. |
| `docs/operator-notes.md` | Audit's business meaning and secondary Audit custody, but not template wording/layout. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Existing cross-report finality, provenance, review, identity/hash and correction rules. |
| `reference/rendererref1/DESIGN_SPEC.md` and `report_data_schema.json` | Assessment-only evidence; the exact source that must not be stretched into Audit authority. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/TemplateCatalog.cs` | Current catalogue has no Audit template. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/Models/Documents.cs` | Current models have no Audit pair/uplift contract. |
| `workspaces/report-renderer/src/CollisionRenderer.Core/AuthoringCatalog.cs` | Generic/fixed authoring entries are not accepted Audit behaviour and should not be used as a loophole. |
| `docs/design/README.md` | Governs approved UI/report design assets and prevents a competing source. |
| `src/Pegasus.Core/Cases/CaseContracts.cs` | Existing Audit identity/outcome is separate from report-template content. |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | Current assessment estimate data is not the accepted dual-specification RPT-03 input. |

## Ripple effects

- [[TICK-098]] cannot reach implemented/accepted RPT-03 until this evidence gate is satisfied.
- [[SIMPLI-014]] and DOCS-001 must expose Audit as unavailable while allowing independently accepted assessment/fee-note rendering.
- [[TICK-205]] owns the domain pair and uplift; its work is necessary but not sufficient for an Audit report.
- Template-capability mapping must show no active Audit descriptor until this ticket is reactivated.
- Staff UI/API/MCP surfaces, readiness responses, telemetry and documentation must not imply Audit render availability.

## Out of scope

- No guessed Audit wording, layout, legal text, statement of truth, comparison table, signatures or fee treatment.
- No reuse of assessment samples as Audit acceptance evidence.
- No placeholder/dormant template or feature flag claimed as delivery.
- No renderer integration, Core domain implementation, Azure deployment, Box/mailbox write or generated reference artifact.
- No decision about percentage uplift; its denominator/rounding remains outside accepted evidence.
