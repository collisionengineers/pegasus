# Design authority

This file is the durable authority for Pegasus visual design, Web interaction contracts, approved assets, component and pattern boundaries, and source-to-runtime mappings. Product scope and business capability remain owned by [requirements](../docs/requirements.md) and [capabilities](../docs/capabilities.md); architecture, engineering, deployment and operational procedure remain with [architecture](../docs/architecture.md), [engineering](../docs/engineering.md), [operations](../docs/operations.md) and [operator notes](../docs/operator-notes.md).

## Evidence discipline

Intended, planned, implemented, caller-proved, deployed and accepted are distinct:

- **Planned `0.1.0-alpha.1`** describes the approved target contract. It does not prove an authenticated Web caller, deployment or operator acceptance.
- **Implemented** means code or an asset exists. Imported workspace code is not automatically a Pegasus caller.
- **Caller-proved** requires a real route or other named caller exercising the behavior.
- **Deployed** requires deployment evidence; none is inferred from implementation.
- **Accepted** requires the specified accessibility and operator review evidence.
- Candidate rasters, historical concepts and the current Development proof are evidence, not design approval.

The only currently called Pegasus UI is the Development-only Razor Pages dashboard and `/Intake/Upload` path through Core `ProcessIntake`, including retained-asset download. It is unauthenticated, creates no case or reference, and is not the planned staff UI. The Operations, Intake, Triage, Cases and Administration surfaces remain planned. The current proof has no accepted complete accessibility evidence.

Detailed durable product-design owners are the
[operator-experience requirements](product/requirements.md),
[capability traceability matrix](product/traceability-matrix.md), and
[UI specification](product/ui-spec.md).


## Product direction

The application is an operational, restrained, desktop-first internal case-management tool for a small office of approximately eight users. It is not a marketing site, document system, mobile product or general-purpose command centre.

**Operations-first was selected on 2026-07-27 for the planned `0.1.0-alpha.1` shell and landing strategy.** This approves the route hierarchy and operating model, not pixel-for-pixel reproduction of a comparison raster and not a partial implementation.

The planned authenticated routes are:

1. Operations
2. Intake
3. Triage
4. Cases
5. Administration, visible only to authorised Administrators
6. Search and authenticated user/sign-out controls

The common hierarchy is:

1. authenticated identity, role, navigation and sign out;
2. surface title, exact queue or filter, freshness and safe primary action;
3. operational table, workbench or record;
4. named workflow, evidence, lease or exception state and consequential action;
5. provenance, external identity, permanent business history and limitations.

Planned capabilities outside `0.1.0-alpha.1` have no alpha navigation, control, workflow or placeholder; [the capability inventory](../docs/capabilities.md) owns their exact targets. Every deferred UI capability must re-enter inventory, specification, alternatives, independent review, explicit approval, visual generation and manual visual review.

## Design principles

- Operational, restrained and border-led rather than decorative.
- White or light-neutral ground, white panels, warm-charcoal navigation and near-black text.
- Collision red is sparse: primary actions, active navigation, visible focus and urgent emphasis.
- Product states are distinct: amber for incomplete/pending, restrained navy for **Review**, and green only for confirmed completion.
- State is never conveyed by colour alone.
- Use 2px corners, 1px hairline borders, rare soft shadows and a 4px spacing rhythm.
- Use system UI text and Lucide line icons only.
- Controls communicate purpose without narrating obvious actions.
- Do not expose Azure, OCR, AI, queue mechanics, extraction engines, deployment or adapter terminology in operator copy.

Settled terms retain their exact meanings and casing, including `Audit`, `Triage`, `Needs sorting`, `Blocked intake`, `Not ready`, `Review` and `Held`. Never substitute a generic **Close** action for a named lifecycle outcome.

## Tokens

The upstream token source was `styles/colors_and_type.css` in the provided `collision-engineers-design-dev` bundle. That source pack is not retained. The values below are the adapted repository-owned authority; no website stylesheet or generated token file is copied.

### Colour

| Role | Approved value or rule |
| --- | --- |
| Collision red | `#DB0816` |
| Pressed/dark red | `#8F1422` |
| Red tint | `rgba(219,8,22,.07)` |
| Warm charcoal | `#2C2A27` |
| Near-black ink | `#16191D` |
| White | `#FFFFFF` |
| Light neutral | `#F5F4F2` |
| Border | `#E6E4E1` |
| Muted text | `#6B6B6B` |
| Confirmed-success green | `#16833B` |
| Incomplete/pending amber | Semantic role accepted; target value remains open |
| Review navy | Semantic role accepted; target value remains open |

Amber incomplete/pending and navy **Review** are approved Pegasus semantics, but their final token values are unresolved. The current `site.css` values `#B87A00` and `#173B5F` are runtime-divergence evidence only and must not be promoted to approved targets without a reviewed reconciliation. Green must not represent progress, availability or a generic positive action; it is reserved for confirmed completion.

Excluded marketing tokens include WhatsApp green/pills, large display scales, CTA shadows, document red and brand-font declarations.

### Typography

Use this system stack for all application text:

```css
ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif
```

Rules:

- Body text is 14–16px.
- Use semantic heading hierarchy.
- Compact uppercase eyebrows may be used where useful.
- Queue and metric values may use stronger weight.
- Tw Cen MT and Futura are marketing, logo and document faces, not application body or UI fonts.
- Do not copy or load an application brand-font bundle.

The shorter fallback stack currently used by `src/Pegasus.Web/wwwroot/css/site.css` is compatible but is not a separate authority.

### Shape, borders and focus

| Token | Approved value |
| --- | --- |
| Primary radius | `2px` |
| Borders | `1px` |
| Keyboard focus ring | `3px rgba(219,8,22,.38)` |
| Depth | Border-first; rare soft shadows |

The current Development CSS uses 3px geometry in places. That is divergence evidence, not approval of a second radius.

### Spacing and layout

Approved spacing steps are:

```text
4, 8, 12, 14, 18, 24, 32, 40, 64px
```

Use only steps exercised by the selected UI. Primary gutters are 24px.

At 1280px and wider, use dense desktop multi-pane layouts. At 1024–1279px and at 200% zoom, reorder secondary content into labelled tabs, drawers or ordered sections without losing identity, state, labels, focus or actions. The upstream marketing 1200px/96px section rhythm is not imported.

Mobile staff UI is **Not planned**. CSS reflow does not create a mobile product, and a supported-device notice is only for genuinely unsupported devices, never a substitute for responsive desktop behavior.

### Motion

There is no product-wide motion system and no approved duration or easing tokens.

A basic, non-essential refresh or loading animation is permitted if:

- the feedback remains understandable without motion;
- reduced-motion preferences receive a static equivalent; and
- the behavior is verified through the real approved route.

Marketing scroll reveals, staggered entrances, hover scaling and CTA lift are excluded. Do not invent duration or easing tokens during implementation.

## Assets

### Logo

The approved master is:

```text
design/brand/logos/logo_no_margin.png
```

It is the red gear-C Collision Engineers lockup, copied exactly from `assets/logo_no_margin.png` in the provided `collision-engineers-design-dev` source bundle.

```text
SHA-256: E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2
```

Current consumers:

- embedded by `workspaces/report-renderer/src/CollisionRenderer.Core`;
- linked by `workspaces/report-renderer/src/CollisionRenderer.Gui`;
- approved as the source for the selected future Web shell, but not yet adopted by the current Development layout.

Rules:

- Never redraw the gear.
- Never extract it from a screenshot.
- Never recolour the master or invent another mark.
- Copy or optimise it for a runtime only through a reviewed source-to-runtime mapping with checksum proof.
- The current HTML/CSS `.brand-mark` spelling `CE` is runtime divergence, not an approved logo variant.

The upstream source directory may be absent from a clean checkout. The checksum-pinned repository copy is the durable source.

### Icons

Lucide is the only approved Web/UI icon system:

- 24×24 viewBox;
- 2px stroke;
- round caps and joins;
- rendered at 16–24px;
- `currentColor`.

Do not use emoji, Unicode dingbats, hand-drawn icons or infrastructure symbols.

No Lucide package or copied SVG set is currently exercised. A selected implementation must choose a repository-owned delivery path, map every used glyph and provide accessible labels whenever an icon is not decorative. `src/Pegasus.Web/wwwroot/favicon.ico` has unrecorded provenance and is not icon-system authority.

### Imagery and evidence

No brand or decorative imagery is needed for the internal Web application. Upstream marketing photography is excluded.

Genuine case images, emails and documents are operational evidence, not decorative assets. Use only authorised repository-provided evidence through its owning workflow. Never generate placeholder cases, damage images, emails, documents or people.

Candidate shell rasters are authorised comparison aids only. Historical concepts and matching rasters are historical, unapproved visual evidence. Neither has a runtime consumer or becomes authority merely by being retained.

### Web and renderer boundary

| Asset class | Approved consumer and boundary |
| --- | --- |
| Master logo | Renderer Core and temporary renderer GUI today; approved source for a reviewed future Web copy |
| Report templates and document stylesheet | Embedded by `workspaces/report-renderer/src/CollisionRenderer.Core`; not Web shell assets |
| Supplied engineer signatures | Embedded renderer evidence only; never Web decorative imagery |
| Temporary renderer GUI package assets | Linked by `workspaces/report-renderer/src/CollisionRenderer.Gui`; remove when that GUI is decommissioned during Pegasus integration |
| Imported renderer, prompt, model, skill and AI material | Source evidence only unless a separate accepted contract provides a real Pegasus caller |

The imported renderer can exercise its own assets without proving the planned Pegasus report capability. Imported workspace material does not become UI, report or design authority by existing in the repository. See the [workspace boundary](../workspaces/README.md).

## Voice, labels and necessary copy

Use concise, settled Collision Engineers language. Guidance is appropriate only when an operator must understand a consequence.

Approved necessary copy includes:

> Blocked intake — no case has been created. A reason is required.

> No case or reference was created; review the missing or conflicting evidence.

> Created in error cannot be reopened. Create and link the replacement case.

Permanent consequences must be visible without hover or colour alone. Illustrative text must not fabricate operational input.

## Access and permissions

Staff accounts, authentication and authorisation remain planned until an authenticated Web caller exists. Planned accounts use Pegasus-managed usernames and passwords.

| Actor | May manage or perform | Must not access or perform |
| --- | --- | --- |
| Administrator | Staff accounts, creation/disable/access review/roles; principals and successor cutover; configuration; approved mailbox allowlist; all ordinary staff Intake, Triage, Case and document work | Credentials, cloud or release administration through the UI; permanent deletion; a generic mailbox-rule editor before policy is resolved |
| Engineer, User | Authorised Intake, Triage, Case, document, lookup, chaser, evidence and lifecycle work | Account, role or access review; principals; configuration; mailbox allowlist; credentials or cloud administration; permanent deletion |
| Automated processing | Named Core intake and evidence actions under its durable identity | A UI account, guessed matching or independent business policy |
| Provider client, deferred planned caller | Principal-scoped submission receipt, status and result API only | Staff shell, general case workflow or Administration |
| External/customer | No application account | Every application surface; external/customer accounts are not planned |

Every protected route and action must handle unauthenticated, disabled-session, stale-role, denied, loading and successful outcomes. Hiding a route or control never replaces server authorisation.

Administration is limited to account administration, principal successor cutover, configuration and mailbox allowlist. It has no generic rules editor and no credential, cloud or release operation.

## Operations-first shell

Operations is the landing route.

```text
CE logo | Operations | Intake | Triage | Cases | Administration | Search | User
Operations
Not ready | Review | Held | Needs sorting | Blocked intake | Triage | Due today
In today | Sent to Engineer: today / week | Reports sent: today / week
Last updated | Refresh
Exact filtered queue list | selected summary / next safe action
```

Rules:

- Every metric is an exact query link to its corresponding filtered queue.
- `Blocked intake` is exact wording and remains pre-case.
- Zero is distinct from stale, partial, unavailable or failed.
- Last-updated time and manual refresh are visible.
- Day boundaries use Europe/London midnight.
- Week boundaries begin Monday.
- At constrained desktop width or 200% zoom, the selected summary becomes an ordered, labelled section after the results without losing identity, state or action context.
- Receiving work, Queries and Other have no `0.1.0-alpha.1` surface; the capability inventory owns their exact targets.
- There are no `0.1.0-alpha.1` saved views, bulk actions, inline mutation, calendar, personal assignments or general email queues.

The selection rationale is strongest shared-office awareness and truthful day/week visibility. Its risk is density and dependence on independent, accurate queries.

### Rejected alternatives retained as evidence

| Direction | Rationale and boundary |
| --- | --- |
| Worklist-first | Highest repeated case-queue throughput, initially focused on `Not ready`, with a selector limited to `Not ready`, `Review` and `Held`. It weakens whole-office day/week visibility. It must not become a generic cross-feature list; Intake and Triage remain dedicated, the summary is read-only, and consequential actions open focused flows. No bulk actions, saved personal queues, inline lifecycle mutation or speculative email work. |
| Case-first | Clearest auditability and deep case context, with Cases/search as the landing and Operations retained as a full named route. It makes shared queue scanning less immediate and cannot be the earliest implementation. No generic Close, notes substitute, percentage completeness, named Engineer assignment, inline external editing, estimator, valuation, finance, AI or mobile controls. |

The comparison rasters remain selection evidence. Their styling and details are not automatically approved.

## Current Development caller

The exercised Development journey is:

```text
upload one supported local source
→ Core ProcessIntake, fail closed
→ persisted receipt/outcome
→ queue
→ receipt review
→ retained source/evidence/draft/assets
→ authorised retained-asset download
```

It does not authenticate staff, create a case, allocate a reference or prove the planned shell.

### Core outcome to operator label and persistence

The current caller exposes these exact outcome labels. The supplied evidence does not establish different public enum names, so implementations must not invent aliases.

| Core result exposed to the UI | Exact operator label | Receipt persisted | Case/reference persisted |
| --- | --- | --- | --- |
| `Draft ready` | Draft ready | Yes | No |
| `Needs sorting` | Needs sorting | Yes | No |
| `OCR required` | OCR required | Yes | No |
| `Unsupported` | Unsupported | Yes | No |
| `Retryable failure` | Retryable failure | Yes | No |

`OCR required` records a fail-closed outcome; it does not prove that deferred OCR capability is implemented.

Validation or refusal before an accepted intake receipt must not be described as case creation. The current Development path never creates a case/reference, regardless of its receipt outcome.

### Planned case-creation mapping

| Intake decision | Operator state | Persisted case/reference consequence |
| --- | --- | --- |
| Definitive authorised instruction with instruction and image completeness satisfied | `Review` | Create exactly one case/reference through shared fail-closed acceptance |
| Definitive authorised instruction without both completeness requirements | `Not ready` | Create exactly one incomplete case/reference |
| Staff-resolved acceptance with explicit confirmation of both completeness requirements | `Review` | Create exactly one case/reference |
| Staff-resolved acceptance without explicit confirmation of both requirements | `Not ready` | Create exactly one incomplete case/reference |
| Explicit confirmation of both requirements on an existing `Not ready` case | `Review` | Transition the existing case; do not create another case/reference |
| `Blocked intake` | Blocked intake with required reason | Persist pre-case intake work only; no case/reference |
| `Needs sorting`, unsupported/incomplete source, ambiguity, custody/integrity/replay/occurrence conflict or missing evidence | Needs sorting or named pre-case failure | No case/reference |
| Resolve/retry of blocked or failed intake | Re-enter ordinary fail-closed intake | Create exactly one case/reference only if the ordinary gates then pass |

## Component map

Only the first table describes exercised components. Planned contracts do not create a speculative component library.

### Exercised components

| Component | Purpose and states | Runtime owner |
| --- | --- | --- |
| Development shell/navigation | Identify the current proof and reach Development routes; normal, hover and focus; local-intake link is conditional | `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` |
| Queue/metric card | Show persisted Development intake counts and open the exact list; empty/value links are exercised; stale/unavailable is planned but unimplemented | `src/Pegasus.Web/Pages/Index.cshtml`, `src/Pegasus.Web/wwwroot/css/site.css` |
| Upload form | Submit one supported local source through the real caller; validation, refusal and success | `src/Pegasus.Web/Pages/Intake/Upload.cshtml` |
| Intake queue/review | List persisted receipts and inspect source, evidence, draft and assets; filters, empty state, failure detail and retained-asset download | `src/Pegasus.Web/Pages/Intake/` |

### Planned component contracts

| Component | Required contract |
| --- | --- |
| Shell/access | Sign-in; disabled, stale-role and denied outcomes; permitted-route visibility plus server authorisation |
| Metric/queue | Label, value or unavailable state, freshness, exact destination filter; zero differs from stale or failed |
| Intake workbench | Persistent source identity; evidence/candidate; fact versus suggestion versus confirmed value; provenance, missing/conflict; acceptance path and no-case failure consequence |
| State action | Permitted transition, prerequisites, consequence, required reason, recovery and history link; never generic Close |
| Identity header | Read-only Case/PO, principal, registration, type/secondary Audit identity, workflow state, due date and EVA proxy limitation |
| Evidence/document panel | Original/source/version, logical removal and closed lock; Box/external state; exact Outlook evidence with separate discovery, link and sent times |
| Lease/conflict | Holder, expiry and recovery; read-only alternative; current conflict and preserved proposed values |
| History | Business mutation, accepted evidence, export and material denied/failed business action only |
| Reason dialog | Named requirement and consequence; labelled reason; confirmation/cancel; initial focus, focus containment, Escape where safe and focus return to the invoking control |

## Planned workflow patterns

### Intake

Intake retains source identity and provenance, original custody, attachments/images, facts, suggestions, confirmed values, validation, conflicts and origin.

The planned alpha surface includes:

- manual upload;
- automatic ingestion from `instructions@collisionengineers.co.uk`;
- correct treatment of staff-forwarded email as real intake;
- stable source identity, duplicate delivery and idempotent retry;
- EML and freehand email-body extraction;
- PDF embedded text and embedded images;
- DOCX text and every visible image placement without deduplicating repeated appearances;
- JPEG and PNG image-led intake;
- reviewed vehicle-registration suggestions from ordinary vehicle images;
- bounded, fail-closed handling for unreadable, oversized or incomplete sources;
- typed, editable, operator-reviewable drafts;
- field provenance, validation, missing values and contradictions;
- principal/provider identification;
- `Needs sorting` and reasoned `Blocked intake`;
- definitive and staff-resolved acceptance through the same business rules;
- registration-based provisional identity for image-led work;
- manual linking and reasoned reversal while preserving original origin;
- missing, integrity, replay, retention, custody and persistence failures.

A temporary external upload request is permitted only when an authenticated staff member creates a temporary, revocable, request-scoped unauthenticated link. It creates no external account, exposes no case/request state, is not permanent, and does not imply acceptance before token, custody and abuse contracts pass. The public surface is an isolated upload form with an immediate result only.

Policy-specific email predicates and acceptance evidence remain open gates for only their named automatic paths. They do not weaken manual or shared fail-closed acceptance.

### Triage

Triage is a distinct inbox classification/label plus a separate pre-case reference record. It is never a case state.

Rules:

- Registration is required; otherwise the source remains `Needs sorting`.
- States are Open, Awaiting information, Finding recorded, Completed and Cancelled.
- Findings are independently optional, but at least one is required before Finding recorded or Completed:
  - Roadworthiness: Roadworthy or Unroadworthy.
  - Assessment: Repairable or Total loss.
- A case’s `has Triage` value is Boolean/reference-only.
- Findings do not affect Case/PO/reference, workflow, final outcome, Engineer report, Audit suffix/allocation or any other decision.
- Completion requires exact approved-mailbox reply-chain evidence.
- Missing, ambiguous, unapproved or technically failed reply evidence remains visible.
- Correction, replacement, new response, cancel, reopen, link, unlink and relink are reasoned and permanently recorded.
- Assignee is optional.
- There is no due date and no chaser UI.

### Case

Case identity keeps the following visible and immutable where specified:

- Case/PO;
- allocated principal;
- registration;
- Inspection, standalone Audit, or Inspection + Audit type;
- secondary Audit identity where applicable;
- workflow state;
- due date;
- EVA handoff/proxy limitation.

Allocated principal and reference never change. A wrong principal closes the original as **Created in error** with a reason and linked replacement. Neither reference is reused and the original never reopens.

Case work includes:

- source, provenance and typed case data;
- documents and images;
- vehicle, DVLA/DVSA and MOT/mileage information;
- inspection address or exact `Image Based Assessment`;
- tasks and reminders;
- seven-calendar-day missing-information chasers;
- `Held` behavior that preserves the chase interval;
- Box file request and copyable manual chasers;
- manual WhatsApp material;
- successful EVA JSON/image export as the `Sent to Engineer` proxy;
- exact report-Sent evidence;
- lease/conflict recovery;
- permanent action history.

EVA owns actual named-Engineer assignment. Pegasus must not describe the export proxy as replacing EVA’s engineering workflow.

Lifecycle states include `Not ready`, `Review`, `Held`, due/overdue and these exact terminal outcomes:

1. Post-report completion
2. Provider cancellation
3. Collision Engineers rejection
4. Created in error

Archive never deletes. Reopening requires a reason and a valid nonterminal destination; `Held` is not a reopen destination, and `Created in error` never reopens.

Cases are read-only until an explicit edit lease is held. Stale writes, expired/lost leases and conflicts must preserve proposed values and offer safe recovery or a read-only alternative.

### Documents and external evidence

- Create the Box case folder using the Case/PO name.
- Retain source emails, instruction documents, images, correspondence and reports.
- Preserve document versions.
- Use logical removal; never physically delete files through the workflow.
- Closed-case documents are read-only until the case is validly reopened.
- Show Box unavailable, pending, retry and unknown states rather than implying success.
- Provide authorised upload, view, download and export actions.
- Private transient Worker staging is not a staff surface or downloadable area.
- Report evidence uses the exact Outlook Sent item and keeps discovery, link and sent times distinct.
- Manual link, unlink or relink requires a reason.
- Ambiguous or absent evidence remains visible.
- Triage reply evidence and case report-Sent evidence are separate contracts.
- Chasers are copyable for manual sending; automated outbound messages are deferred.

### Search and filters

The exact UI-07 fields are:

- Case/PO;
- registration;
- claimant;
- claim number;
- principal;
- state;
- Engineer;
- received date;
- instruction date;
- date range;
- origin.

### Permanent history

Permanent action history records:

- business mutations;
- accepted external evidence;
- exports;
- material denied or failed business actions;
- actor, time, outcome, reason and before/after values.

It excludes routine views, refresh, polling, retries, leases, heartbeats and adapter or Worker mechanics. Those belong in telemetry or security evidence outside the operational UI.

## Complete UI state contract

| Scope | Required states |
| --- | --- |
| Queries | Loading; empty; success; stale/partial with last-good time; transient error/retry; unauthenticated; disabled; stale-role; denied |
| Mutations | Validation; confirmation; success; denied; stale version; lease lost; dependency unavailable; idempotent/replayed result; conflict and recovery |
| Intake | Empty/oversize; replay; retention/custody failure; Draft ready; Needs sorting; Unsupported; missing/integrity asset; evidence missing/contradictory; reasoned Blocked intake/resolve/retry; every acceptance path; refusal with no case/reference |
| Triage | Registration missing; unassigned/assigned; every named state; missing/ambiguous/unapproved/technical reply evidence; finding replacement/correction/new response; cancel/reopen/link/unlink/relink |
| Case | Not ready/chasing; Review; Held/preserved interval; due/overdue; gate refusal; documents locked; Box/external-effect states; EVA proxy limitation; report evidence absent/ambiguous/manual/exact; every terminal outcome; archive; reopened; Created-in-error nonreopenable; lease held/expired/lost/stale |

## Accessibility

The planned UI supports keyboard and pointer operation, screen readers, 200% zoom, forced colours and reduced motion on supported desktop layouts.

Required behavior:

- skip link;
- semantic landmarks and headings;
- labelled navigation;
- semantic tables with captions, headers and sort state;
- keyboard-operable queue selection;
- explicit pane and tab relationships;
- associated field errors and error summaries;
- visible focus;
- practical 44px targets;
- restrained live announcements;
- non-colour state cues;
- safe modal focus handling;
- permanent consequences visible without hover;
- server authorisation regardless of route visibility.

When a planned surface has a real caller, record:

1. keyboard-only traversal;
2. screen-reader and semantic inspection;
3. focus and error behavior;
4. 1280px-and-wider desktop review;
5. 1024–1279px constrained-desktop review;
6. 200% zoom review;
7. forced-colours review;
8. reduced-motion review;
9. contrast review;
10. automated accessibility scanning through the real caller.

Each visible capability/state also needs authenticated Web-caller and named Core-owner evidence. Generated imagery or synthetic operational material cannot prove acceptance. Operator review uses approved, genuine, local immutable material only.

## Deferred and absent UI seams

The complete allocation is owned by [capabilities](../docs/capabilities.md). These boundaries are design invariants:

### Deferred integration and intake surfaces

There is no alpha control, route or placeholder for:

- additional provider activation beyond the alpha source policy;
- `desk@`, `engineers@` or `info@` automatic ingestion;
- legacy DOC, MSG or scan-like PDF OCR extraction;
- automatic image-led/instruction-led matching;
- broader mailbox identity, taxonomy mapping, folder recommendation/move, suggested actions, case association or mailbox browsing;
- Receiving work, Queries, Other or a full email-management workspace;
- post-report query/dispute work;
- provider submission/status/result APIs;
- broader classified-email MCP actions;
- AI/vision assistance for vehicle images or damage evidence;
- separate-age instruction/image pairing and readiness notification;
- spreadsheet preparation of future inspection-address/repairer reference data.

Provider APIs and MCP are non-browser boundaries and do not create staff-shell destinations.

### Deferred casework and advanced surfaces

There is no alpha control, route or placeholder for:

- automatic chaser or report sending;
- authenticated compose/reply/forward/send in Pegasus;
- Diminution or Commercial case workflows;
- automated WhatsApp ingestion;
- an in-app AI assistant or AI-assisted identification, action, extraction or address suggestion;
- replacing EVA assignment, estimating, valuation, report preparation or engineering workflow;
- direct EVA, Audatex, valuation, finance or invoicing integrations;
- guided mobile image capture or third-party guided-capture integration;
- a custom application domain;
- a canonical Engineer workbench, repair specification, valuation, salvage or deterministic report-output workflow;
- AI-generated query-response proposals or durable `Send to AI` work;
- management information for Engineer throughput, query rates, Audit uplift, principal report/invoice measures or turnaround;
- `AI Assessor`.

Deferred AI may propose but must not mutate, accept or send autonomously. Future deterministic outputs must use one accepted structured case/engineering record, validate accepted data, calculate once and avoid duplicate truth owners or output-specific source forks.

### Not planned

The following are permanent absences, not backlog placeholders:

- external/customer accounts;
- public registration;
- staff multi-factor authentication;
- mobile/responsive staff product;
- automated malware scanning;
- document redaction;
- digital signatures;
- automated retention/deletion;
- legal hold;
- subject-access/correction/export/erasure workflow;
- dedicated DPIA/compliance workflow;
- GitHub Actions deployment with scoped OIDC;
- separate staging, QA, UAT, training or demo environments;
- deployment slots/Standard S1;
- private networking, zone redundancy or multi-region failover;
- quarterly restore exercises;
- predecessor data import, predecessor availability after cutover or predecessor code reuse;
- SMS or Microsoft Teams integration;
- customer/claimant portal;
- independent Engineer accounts;
- solicitor, insurer, repairer or vehicle-owner accounts.

A supported desktop reflow does not alter the permanent mobile-product boundary.

## Source and runtime map

| Concern | Durable owner or source | Runtime consumer or evidence |
| --- | --- | --- |
| Product capability and horizon | [Requirements](../docs/requirements.md), [capabilities](../docs/capabilities.md) | Planned staff routes; current caller is narrower |
| Open policy and token questions | [Open decisions](../docs/open-decisions.md) | No implementation inference until resolved |
| Architecture and caller boundaries | [Architecture](../docs/architecture.md) | Core, Web, Worker, MCP and external adapters |
| Operations and deployment | [Operations](../docs/operations.md), [Azure](../docs/azure/README.md) | No deployment claim from design or source presence |
| Engineering procedure | [Engineering](../docs/engineering.md) | Reviewed implementation and verification |
| Design authority | This file | Approved Web tokens, assets, components and patterns |
| Current Web shell | This file’s approved direction; current code is evidence only | `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` |
| Current Web tokens/layout | This file | `src/Pegasus.Web/wwwroot/css/site.css`, currently divergent |
| Current dashboard | Current exercised component map | `src/Pegasus.Web/Pages/Index.cshtml` |
| Current intake caller | Current Development pattern | `src/Pegasus.Web/Pages/Intake/` → Core `ProcessIntake` |
| Master logo | `design/brand/logos/logo_no_margin.png`, checksum above | Renderer Core and temporary renderer GUI; future reviewed Web copy |
| Renderer templates/style | Repository renderer asset sources | `workspaces/report-renderer/src/CollisionRenderer.Core` |
| Engineer signatures | Repository renderer signature sources | Renderer Core only; excluded from Web decorative imagery |
| Temporary renderer GUI assets | Repository renderer GUI asset sources | `workspaces/report-renderer/src/CollisionRenderer.Gui`; remove with GUI |
| Imported renderer/skills/AI source | [Workspaces](../workspaces/README.md) | Non-caller evidence unless separately integrated and accepted |
| Decision rationale | [Decision records](../docs/decisions/README.md) | Does not itself prove implementation |
| Change evidence | [Change records](../docs/changes/README.md) | Does not replace caller, deployment or acceptance evidence |
| External reference qualification | [Reference index](../docs/reference/README.md) | Reference presence never creates authority |

The original `collision-engineers-design-dev` bundle supplied the shared logo, colour, type and icon foundation but explicitly did not define this internal command-centre application. The repository imports only approved shared essentials and renderer assets. Marketing layouts, imagery, fonts, WhatsApp styling, scroll reveals and mobile navigation are excluded. The source bundle is not retained as a second design system.

## Change and verification rule

Change approved design authority, source/runtime mapping and affected implementation in one reviewed change.

A conforming change must:

1. identify whether it is planned, implemented, caller-proved, deployed or accepted;
2. preserve exact business labels, consequences and authorisation boundaries;
3. use approved tokens and assets or explicitly record a reviewed divergence;
4. verify the real caller rather than imported or unused source;
5. update accessibility evidence for affected states and routes;
6. use genuine authorised material for operator review;
7. preserve checksum proof for copied or optimised logo assets;
8. avoid synthetic brand assets, operational examples, copy or duplicated generated output;
9. avoid a parallel runtime token file until one selected implementation can make a single source directly consumable; and
10. return every `Next` or `Later` UI capability to complete design approval before adding any route, control, workflow or placeholder.