# Pre-Azure-Workflow plan archive

Status: **Historical evidence — not an active plan route**

This archive preserves the complete pre-onboarding `docs/plans/` evidence after
Azure Workflow converted living ownership to product, roadmap, design,
operations/runbooks, decisions, and change records. Files here cannot allocate
a capability, authorize implementation, settle a decision, or claim current
behavior. Any future work must revalidate the relevant evidence and create one
current change record.

## Canonical destinations

| Former plan concern | Current owner |
| --- | --- |
| 213 stable identities and allocation | [capability inventory](../../product/capabilities.md) |
| outcome horizons and dependency intent | [roadmap](../../roadmap.md) |
| V1 gap | [V1 gap](../../product/v1-gap.md) |
| permanent/conditional boundaries | [product boundaries](../../product/boundaries.md) |
| unresolved product questions | [open decisions](../../product/open-decisions.md) |
| identity/access | [product area](../../product/areas/identity-and-access.md) |
| intake/casework/mail | [product area](../../product/areas/intake-and-casework.md) |
| documents/external systems | [product area](../../product/areas/documents-and-integrations.md) |
| APIs/MCP/AI | [product area](../../product/areas/interfaces-and-automation.md) |
| platform/operator experience | [product area](../../product/areas/platform-and-operator-experience.md) |
| UI requirements/specification/traceability | [design product authority](../../../design/product/requirements.md) |
| UI direction candidates | [design references](../../../design/references/README.md) |
| local testing/evidence profiles | [testing runbook](../../runbooks/testing/README.md) |
| one activated change | [change records](../../changes/README.md) |

## Retained evidence packs

- [Feature maturity map](feature-maturity-map.md): exact normalized parity with
  the retained worksheet; no longer an active allocation owner.
- [Delivery roadmap](delivery-roadmap.md): pre-conversion dependency analysis;
  the root roadmap now owns current intent.
- [Mailbox categorisation and matching](mailbox-categorisation-and-email-matching/README.md):
  research evidence routed by current open decisions.
- [Remainder delivery](remainder-delivery/README.md): verbose V1 planning
  evidence, including the dated PR #1 review snapshot.
- [Later delivery](later-delivery/README.md): V1.x/V2/V3/V3+ activation evidence.
- [Deferred-capability architecture](deferred-capability-architecture/README.md):
  reconciliation evidence; accepted ADRs and current product owners prevail.
- [UI/UX history](ui-ux/README.md): superseded concepts, generation prompts,
  and their historical rasters.

## Source mapping and recovery

The default mapping is exact: every former `docs/plans/<path>` not listed below
moved to `docs/history/plans/<path>`.

| Former source | Current destination |
| --- | --- |
| `open-decisions.md` | `docs/product/open-decisions.md` |
| `remaining-requirements.md` | `docs/product/v1-gap.md` |
| `permanent-and-conditional-boundaries.md` | `docs/product/boundaries.md` |
| `ui-ux/requirements.md` | `design/product/requirements.md` |
| `ui-ux/ui-spec.md` | `design/product/ui-spec.md` |
| `ui-ux/traceability-matrix.md` | `design/product/traceability-matrix.md` |
| `ui-ux/directions/*.md` | `design/references/directions/*.md` |
| `ui-ux/mockups/candidate-*.png` | `design/references/mockups/candidate-*.png` |
| `long-term-local-testing/README.md` | `docs/runbooks/testing/README.md` |
| `long-term-local-testing/platform/local-testing.md` | `docs/runbooks/testing/local-testing.md` |

The complete original tree remains recoverable from commit `4bbe176` and its
ancestors. Historical concept Markdown/rasters and generation prompts remain in
this archive because they are evidence, not current design authority.
