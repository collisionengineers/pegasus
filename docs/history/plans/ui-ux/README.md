# UI/UX planning

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Planned, direction-neutral V1 route. Three comparison rasters were generated with explicit user authorisation on 2026-07-26; no candidate is approved.**

This directory records the operator-facing UI route, not an implemented staff application. The only currently called UI is the Development-only pre-case intake/receipt path: `/Intake/Upload` calls `ProcessIntake` and includes the retained-asset handler. It is not authenticated staff UI, creates no case or reference, and proves no V1 staff caller. All V1 staff surfaces below are **Planned**.

## Current route

- [Requirements](../../../../design/product/requirements.md) are the direction-neutral V1 boundary and retained historical-content map.
- [UI specification](../../../../design/product/ui-spec.md) defines the common shell, focused flows, state/accessibility contracts, and acceptance evidence.
- [Feature traceability matrix](../../../../design/product/traceability-matrix.md) maps the canonical allocation to a V1 destination or explicit non-UI boundary.
- The three unapproved shell candidates are [operations-first](../../../../design/references/directions/operations-first.md), [worklist-first](../../../../design/references/directions/worklist-first.md), and [case-first](../../../../design/references/directions/case-first.md). They share the same complete Intake, Triage, Case, and Administration flows; they differ only in landing and shell strategy.

The user explicitly authorised an equally finished raster for each reviewed candidate so the direction can be selected visually. Direction approval remains a future explicit user decision. Selecting a V1 shell would approve only its landing/hierarchy direction; it would not prove implementation, approve every raster detail, or approve later-horizon UI. Any V2, V3, V3+, or conditional UI change re-enters the whole UI route: inventory, specification, alternatives, independent review, approval, concept and manual review.

## Current visual comparison

| Candidate | Comparison raster | Primary emphasis | Selection state |
| --- | --- | --- | --- |
| A — Operations-first | [Open raster](../../../../design/references/mockups/candidate-a-operations-first.png) | Shared-office queues, due work and day/week awareness | Unapproved |
| B — Worklist-first | [Open raster](../../../../design/references/mockups/candidate-b-worklist-first.png) | Repeated work through one named case queue | Unapproved |
| C — Case-first | [Open raster](../../../../design/references/mockups/candidate-c-case-first.png) | Case search, identity, evidence and business history | Unapproved |

The three rasters use blank values rather than fabricated operational records. Their empty logo slot deliberately avoids redrawing or altering the approved Collision Engineers logo; the exact packaged asset is reserved for a selected implementation/final-design step. These are layout-selection aids, not requirements, test fixtures, caller evidence or operator acceptance.

## Historical concepts and rasters

`concept-01-operations-cockpit.md`, `concept-02-intake-workbench.md`, `concept-03-case-workspace.md`, and `generation-prompts.md` are retained historical, unapproved material. Their unique content is retained as evidence and is superseded as an active candidate by the direction-neutral route above. The associated PNGs are historical visual filler, not requirements, test fixtures, V1 scope, or an approved visual direction; sample names, dates, counts, navigation items, countries, and extracted values must not be implemented as product rules.

The public Collision Engineers website kit does not define the internal application. Any later approved implementation applies the contained internal-app style boundary: warm off-white ground, white panels, warm-charcoal navigation, near-black text, CE-red accents, border-first depth, restrained system-sans operational text and line icons. Those visual primitives do not select one candidate or authorise a raster concept.

## Boundaries

Mobile staff UI is **Never**. At constrained desktop widths and 200% zoom, essential work reflows without losing identity, labels, focus, or actions; that does not create a mobile or read-only product. Operator review and evaluation use approved genuine local immutable material only; do not fabricate operational emails, images, instructions, staff, or cases.

## Deferred-capability impact

The V1 route preserves stable case, Triage, source, document and external-evidence identity plus named Core actions. It does not add dormant navigation, controls, forms, roles, endpoints, flags, or placeholders for V2 email management/image AI, V3 WhatsApp/chaser automation and later case types, V3+ EVA replacement/report sending, conditional guided capture, or Never capabilities. Activation needs the owning accepted decision, Core contract, real caller, and operator-reviewed accessible workflow.
