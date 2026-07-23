# Design and dated reviews

These documents preserve old UI thinking and operator feedback. They may reveal useful language or workflow questions, but the old folder's claim that reviews are “binding” does not carry into v2.

## Design documents

| File | Brief contents | Current v2 comparison |
| --- | --- | --- |
| [`docs/design/README.md`](../docs/design/README.md) | Index and design principles for the old application. | **Review only.** Current v2 UI planning lives under `docs/ui-ux/`. |
| [`docs/design/ui-ux.md`](../docs/design/ui-ux.md) | Old navigation, forms, tables, error handling, accessibility and operator-language rules. | **Some principles overlap.** No predecessor component or layout is adopted automatically. |
| [`docs/design/THEME-MAPPING.md`](../docs/design/THEME-MAPPING.md) | Old theme tokens and mapping to application surfaces. | **Predecessor-specific.** Current design system and Razor UI differ. |
| [`docs/design/product-demo/README.md`](../docs/design/product-demo/README.md) | Demo narrative and source list. | **Historical presentation material.** |
| [`docs/design/product-demo/presentation-notes.md`](../docs/design/product-demo/presentation-notes.md) | Talking points and evidence claims for the old product demo. | **Not current product evidence.** |

## Review 19 June 2026 — feature and UI review

| File | Brief contents | Current v2 comparison |
| --- | --- | --- |
| [`overview.md`](../docs/reviews/190626/overview.md) | Navigation for a broad review of dashboard, intake, queues, case view, provider settings and EVA creation. | **Concept discovery only.** |
| [`process.md`](../docs/reviews/190626/process.md) | Old method for converting findings into tickets and verification. | **Predecessor delivery process.** |
| [`checklist.md`](../docs/reviews/190626/checklist.md) | Old action/verification status. | **Not a v2 checklist.** |
| [`broad-review/review.md`](../docs/reviews/190626/broad-review/review.md) | Vehicle enrichment, mileage and broader feature requests. | **Vehicle enrichment planned; related automation needs current review.** |
| [`corpus-admin/review.md`](../docs/reviews/190626/corpus-admin/review.md) | Provider settings and corpus administration feedback. | **Principal administration planned; old fields/rules not adopted.** |
| [`dashboard/review.md`](../docs/reviews/190626/dashboard/review.md) | Dashboard layout, counts and operator actions. | **Dashboard planned; three activity meanings remain open.** |
| [`evacreation/evacreation.md`](../docs/reviews/190626/evacreation/evacreation.md) | Manual EVA-creation blockers and required case data. | **Useful handoff questions.** EVA export is planned, not implemented. |
| [`nav-bar/review.md`](../docs/reviews/190626/nav-bar/review.md) | Navigation-bar observations without a Markdown heading. | **Review only.** |
| [`new-case/review.md.md`](../docs/reviews/190626/new-case/review.md.md) | New-case form observations without a Markdown heading. | **Review only.** Current case creation is not implemented. |
| [`queues-cases/queues/review.md`](../docs/reviews/190626/queues-cases/queues/review.md) | Queue behavior and presentation observations. | **Partly overlaps planned inbox/case queues.** |
| [`queues-cases/caseview/review.md`](../docs/reviews/190626/queues-cases/caseview/review.md) | Case workspace layout and actions. | **Planned concept, no current case workspace caller.** |

## Reviews 1–2 July 2026 — UI and inbox decisions

| File | Brief contents | Current v2 comparison |
| --- | --- | --- |
| [`010726/overview.md`](../docs/reviews/010726/overview.md) | Old UI/UX reforge scope and navigation. | **Review only.** |
| [`010726/decisions.md`](../docs/reviews/010726/decisions.md) | Decisions on colour, dashboards, tables, bulk actions, quick peek, empty states and accessibility. | **Potential design input.** Must be reconciled with current v2 flows. |
| [`020726/decisions.md`](../docs/reviews/020726/decisions.md) | Inbox simplification, case links, references, mailbox naming and suggested filing. | **Some concepts overlap; mailbox categorisation remains open.** |

## Review 15 July 2026 — repository reset review

All files in this group review old PR #100 and PLAN-006. They are **predecessor-specific engineering evidence**, not product scope.

| File | Original purpose |
| --- | --- |
| [`overview.md`](../docs/reviews/150726/overview.md) | Review scope and lane map. |
| [`process.md`](../docs/reviews/150726/process.md) | Review method and responsibilities. |
| [`checklist.md`](../docs/reviews/150726/checklist.md) | Reconciliation and release checklist. |
| [`final-review.md`](../docs/reviews/150726/final-review.md) | Final remediation/release opinion. |
| [`release-validation.md`](../docs/reviews/150726/release-validation.md) | Old release validation record. |
| [`agents-ci/review.md`](../docs/reviews/150726/agents-ci/review.md) | Agent generation and CI review. |
| [`docs-integrity/review.md`](../docs/reviews/150726/docs-integrity/review.md) | Documentation and governance review. |
| [`evidence/gate-battery.md`](../docs/reviews/150726/evidence/gate-battery.md) | Historical offline check results. |
| [`purge-outputs/review.md`](../docs/reviews/150726/purge-outputs/review.md) | Retired-platform and generated-output purge review. |
| [`python-vendor/review.md`](../docs/reviews/150726/python-vendor/review.md) | Python services and parser-vendor review. |
| [`reconciliation/review.md`](../docs/reviews/150726/reconciliation/review.md) | “Nothing lost” reconciliation review. |
| [`runtime-surface/review.md`](../docs/reviews/150726/runtime-surface/review.md) | Old HTTP/DTO surface invariance review. |
| [`spa-database/review.md`](../docs/reviews/150726/spa-database/review.md) | SPA and database-move review. |
| [`tickets-board/review.md`](../docs/reviews/150726/tickets-board/review.md) | Old ticket/board consistency review. |

## Review 16 July 2026 — predecessor ADR rewrite

| File | Brief contents | Current v2 comparison |
| --- | --- | --- |
| [`overview.md`](../docs/reviews/160726/overview.md) | Scope of the old ADR consistency review. | **Historical architecture review.** |
| [`review.md`](../docs/reviews/160726/review.md) | Operator comments against the old ADR set. | **Useful only as questions for renewed operator review.** |
| [`decisions.md`](../docs/reviews/160726/decisions.md) | Old rulings on contradictions and rewrites. | **Does not supersede current v2 decisions.** |
| [`checklist.md`](../docs/reviews/160726/checklist.md) | Old reconciliation status. | **Not current evidence.** |

## Review index

| File | Brief contents | Current v2 comparison |
| --- | --- | --- |
| [`docs/reviews/README.md`](../docs/reviews/README.md) | Old precedence rules and review catalogue. | **Conflicts with the v2 source-of-truth order if treated as current.** |

## Safe reuse

UI text, table needs and workflow pain points can be brought to an operator as discussion prompts. Old screenshots, completion marks and visual decisions cannot prove that the same behavior should exist in v2.
