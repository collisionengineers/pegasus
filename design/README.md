# Design authority

This file is the durable authority for Pegasus visual design, Web interaction contracts, approved assets, component and pattern boundaries, and source-to-runtime mappings. Product scope and business capability remain owned by [requirements](../docs/requirements.md) and [capabilities](../docs/capabilities.md); architecture, engineering, deployment and operational procedure remain with [architecture](../docs/architecture.md), [engineering](../docs/engineering.md), [operations](../docs/operations.md) and [operator notes](../docs/operator-notes.md).

## Evidence discipline

Intended, planned, implemented, caller-proved, deployed and accepted are distinct:

- **Planned `0.1.0-alpha.1`** describes the approved target contract. It does not prove an authenticated Web caller, deployment or operator acceptance.
- **Implemented** means code or an asset exists. Imported workspace code is not automatically a Pegasus caller.
- **Caller-proved** requires a real route or other named caller exercising the behavior.
- **Deployed** requires deployment evidence; none is inferred from implementation.
- **Accepted** requires the specified accessibility and operator review evidence.
- The three retained comparison rasters record the shell-selection comparison. Operations-first is the selected strategy; raster pixels and details are not design approval or runtime evidence.

The prior dated caller proof covered the now-retired Development-only `/Intake/Upload` thin slice. The implemented offline QDOS-alpha surface now assigns authenticated manual receipt/list/detail/source work to `/Intake`, `/Intake/{id}`, and `/Intake/{id}/Source`, keeps the non-persistent evaluator at `/Development/EmailEvaluation`, and exposes token-bound public request submission only at `/Uploads/{token}`. This cutover is not deployment, accessibility acceptance, or operator acceptance evidence.

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

Capabilities allocated beyond `0.1.0-alpha.1` have no alpha navigation, control, workflow or placeholder. Their exact first-introduction releases remain owned by the [capability inventory](../docs/capabilities.md#capabilities) and are mirrored ID by ID in the [traceability matrix](product/traceability-matrix.md). Every deferred UI capability must re-enter specification, alternatives, independent review, explicit approval, visual generation and manual visual review before implementation.

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

The retained comparison rasters are selection evidence only. The Operations-first shell strategy is approved; no raster is pixel-level authority, runtime payload or implementation proof.

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

Staff accounts, authentication, and authorisation remain planned until an authenticated Web caller exists. Planned accounts use Pegasus-managed usernames and passwords. Core owns the exact [staff role access matrix](../docs/requirements.md#staff-role-access-matrix), automated-actor boundary, and [case edit authority](../docs/requirements.md#case-edit-authority-and-recovery); this section owns only how those decisions appear in the planned UI.

| Actor | Planned UI boundary |
| --- | --- |
| Administrator | Staff shell plus Administration surfaces for accounts/access/roles, principals, configuration, and approved mailbox allowlist. |
| Engineer, User | Staff shell without Administration surfaces. Their ordinary Intake, Triage, Case, document, evidence, and lifecycle controls are identical. |
| Automated processing | No UI account or interactive control. |
| Provider API client ([API-01–API-04, `Next / 0.4.0`](../docs/capabilities.md#capabilities)) | No staff shell, Case workspace, or Administration surface. |
| External/customer | No application account or application surface. |

Every protected route and action must handle unauthenticated, disabled-session, stale-role, denied, loading, and successful outcomes. Hiding a route or control never replaces server authorisation. Administration has no generic rules editor, credential/cloud/release operation, bulk predecessor import, or bulk Case-edit tool. No surface permits permanent deletion or direct external/customer Case editing.

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
- Every metric shows its last-good time and one current refresh state: loading,
  current, stale, partial, unavailable, or failed.
- `0` is a current result, never a substitute for stale, partial, unavailable,
  failed, or not-yet-loaded data.
- Manual refresh reruns the same filter, gives start/completion feedback, keeps
  last-good data visible, and never claims an external action succeeded.
- Refresh remains telemetry; accepting, rejecting, linking, or changing an
  external fact during reconciliation is a permanent, attributable business
  event.
- Day boundaries use Europe/London midnight.
- Week boundaries begin Monday.
- At constrained desktop width or 200% zoom, the selected summary becomes an ordered, labelled section after the results without losing identity, state or action context.
- Receiving work, Queries and Other are `Next / 0.3.0` in the [capability inventory](../docs/capabilities.md#capabilities), with no `0.1.0-alpha.1` surface.
- There are no `0.1.0-alpha.1` saved views, bulk actions, inline mutation, calendar, personal assignments or general email queues.

The selection rationale is strongest shared-office awareness and truthful day/week visibility. Its risk is density and dependence on independent, accurate queries.

### Rejected alternatives retained as evidence

| Direction | Rationale and boundary |
| --- | --- |
| Worklist-first | Highest repeated case-queue throughput, initially focused on `Not ready`, with a selector limited to `Not ready`, `Review` and `Held`. It weakens whole-office day/week visibility. It must not become a generic cross-feature list; Intake and Triage remain dedicated, the summary is read-only, and consequential actions open focused flows. No bulk actions, saved personal queues, inline lifecycle mutation or speculative email work. |
| Case-first | Clearest auditability and deep case context, with Cases/search as the landing and Operations retained as a full named route. It makes shared queue scanning less immediate and cannot be the earliest implementation. No generic Close, notes substitute, percentage completeness, named Engineer assignment, inline external editing, estimator, valuation, finance, AI or mobile controls. |

The comparison rasters remain selection evidence: [Operations-first](references/mockups/candidate-a-operations-first.png), [Worklist-first](references/mockups/candidate-b-worklist-first.png), and [Case-first](references/mockups/candidate-c-case-first.png). Their styling and details are not automatically approved.

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
| Intake receipt and upload | Submit one bounded authenticated source through `ReceiveIntake`; list retained receipts; inspect provenance and decisions; download the retained source only as an authorised safe attachment | `src/Pegasus.Web/Pages/Intake/{Index,Details,Source}.cshtml(.cs)` |
| Triage queue/detail | List and filter triage records and execute the Core-owned detail commands without adding due/chaser controls | `src/Pegasus.Web/Pages/Triage/{Index,Details}.cshtml(.cs)` |

### Planned component contracts

| Component | Required contract |
| --- | --- |
| Shell/access | Sign-in; disabled, stale-role and denied outcomes; visibility derived from the [Core-owned role matrix](../docs/requirements.md#staff-role-access-matrix) plus server authorisation |
| Metric/queue | Label, value or unavailable state, last-good time, current refresh state and exact destination filter; `0`, loading, current, stale, partial, unavailable and failed remain distinct |
| Intake workbench | Immutable source occurrence and evidence beside the distinct editable candidate/accepted Case projection; source/dispatch identity; `All`/`Instructions`/`Images` filter; fact versus suggestion versus confirmed value; provenance, ambiguity/conflict, association history, acceptance path and no-case consequence |
| Request-scoped upload | Bound upload fields and immediate request-local result only; expired, revoked, limit, custody, replay and cross-request failures disclose no case/reference, request history or other material |
| State action | One current Case and one named Core action; prerequisites, consequence, reason where required, recovery and history link; never a generic Close, bulk edit or external edit |
| Identity header | Read-only Case/PO, principal, registration, type/secondary Audit identity, workflow state, `Due by`/overdue state and EVA proxy limitation |
| Due/chaser panel | Missing-material reason, next chase, most recent recorded channel/outcome, optional note and next permitted action together; preparation/copy is not sent or delivered |
| Inspection address | Explicit physical vehicle/repairer location or exact `Image Based Assessment`; physical address fields appear only for the first mode and never imply attendance |
| Engineering findings | Separate Roadworthiness and Assessment controls; accepted and superseded versions, reasoned correction, reopen requirement and no inferred fee/invoice mutation |
| Evidence/document panel | Original/source/version, logical removal and closed lock; Box/external state; issued report versions; exact Outlook evidence with separate discovery, link and sent times |
| Lease/conflict | One current Case; holder, expiry, renew/release/reacquire state and read-only alternative; current conflict and preserved proposed values; no forced Administrator takeover |
| History | Read-only presentation of the Core-owned [permanent action history](../docs/requirements.md#permanent-action-history), including actor/caller/time and one-Case scope without message bodies or telemetry noise |
| Reason dialog | Named requirement and consequence; labelled reason; confirmation/cancel; initial focus, focus containment, Escape where safe and focus return to the invoking control |

Opening source evidence or other supporting detail preserves the current list/detail position and every unsaved edit; returning never silently discards or replaces the operator’s proposed values.

## Planned workflow patterns

### Intake

The Intake workbench presents the immutable [source occurrence and durable dispatch identity](../docs/requirements.md#source-occurrence-and-dispatch-identity), provenance, original custody, attachments/images, facts, and derivations separately from an editable candidate or accepted Case projection. A source never becomes the Case record merely because a candidate is accepted.

The planned alpha surface includes:

- manual upload;
- automatic ingestion from `instructions@collisionengineers.co.uk`;
- correct treatment of staff-forwarded email as real intake;
- stable source-occurrence and dispatch identity, duplicate delivery, pending/retry state, and idempotent result;
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
- ambiguous/conflicting association review and reasoned manual link, unlink, reversal, or reassociation while preserving every prior relationship and original origin under the [Core association contract](../docs/requirements.md#matching-conflicts-and-reversible-association);
- missing, integrity, replay, retention, custody and persistence failures.

A staff-created in-house upload request is permitted only through a temporary
token bound to exactly one request, its allowed operation, and a server-enforced
expiry. Staff can revoke it, and the isolated unauthenticated surface exposes
only that request's upload fields and immediate structured result. It exposes no
case/reference, request state/history, other document, token-management function,
external account, or cross-request lookup. Success proves only request-local
custody, not case creation, Box custody, EVA handoff, report generation, or
external delivery. File type/count/size limits, expiry, revocation, idempotent
retry, abuse handling, cross-request isolation, durable custody, and
non-disclosing errors are acceptance gates.

Policy-specific email predicates and acceptance evidence remain open gates for only their named automatic paths. They do not weaken manual or shared fail-closed acceptance.

### Triage

Triage is a distinct inbox classification/label and separate pre-case record, never a case state. The UI implements the [Core-owned normal workflow and completion evidence](../docs/requirements.md#normal-workflow-and-completion-evidence) rather than defining another transition policy.

The detail workspace presents the normal sequence from registration-gated `Needs sorting`, through `Open`, missing-information correspondence, and an accepted finding, to exact reply-chain evidence and `Completed`. It must show acknowledgement, information request, or other ordinary correspondence as non-completing activity; display missing, ambiguous, unapproved, or technically failed reply evidence; and expose `Cancelled` as the separately named end without finding/reply.

Finding correction/replacement, new response, reasoned reopen, and optional later Case link/unlink/relink remain visible in permanent history. The Case link is reference-only: Triage findings do not alter Case/PO, reference, lifecycle, final outcome, Engineer report, or Audit identity. Assignee remains optional, with no due date or chaser UI.

### Case

The Case workspace visibly preserves the immutable [Case/PO and principal identity](../docs/requirements.md#principal-reference-organisation-and-case-party-identity), registration, [Inspection, standalone Audit, or Inspection + Audit type](../docs/requirements.md#case-types), secondary Audit identity where applicable, workflow state, `Due by`/overdue state, and EVA proxy limitation. It presents accepted case-party functions and the inspection-address snapshot for that Case without allowing later reusable organisation/repairer edits to rewrite historical case evidence.

A wrong-principal repair is presented as the Core-owned `Created in error` original and its linked replacement, never as an editable Case/PO or principal field. Both references remain visible and the original has no reopen control.

Case work includes:

- source, provenance, and typed case data;
- documents and images;
- suggestion-first ordinary-image VRM with source-image/confirmed/no-result
  distinction;
- DVLA/DVSA and MOT/mileage observations with source/version/age and
  supplied/external/estimated classification;
- explicit physical vehicle/repairer location or exact `Image Based Assessment`;
- separate Roadworthiness and Assessment findings plus correction history;
- tasks and reminders;
- `Due by`, missing-material reason, next chase, last channel/outcome, optional
  note, and next permitted action in one work area;
- seven-calendar-day missing-information chasers and `Held` behavior that
  preserves the interval;
- request-scoped upload-link creation and copyable manual chasers;
- manual WhatsApp material;
- successful deterministic EVA JSON/image/manifest generation as the
  once-per-case `First sent to Engineer` proxy, with later revisions distinct;
- issued report/addendum versions and exact report-Sent evidence;
- lease/conflict recovery; and
- permanent action history.

EVA owns actual named-Engineer assignment. Pegasus must not describe the export proxy as replacing EVA’s engineering workflow.

No-result, unknown, stale, partial, unavailable, and failed vehicle/external
states are distinct from a confirmed value. Refresh retains last-good data and
never overwrites a staff-confirmed value. The UI shows source/version, prior and
new value, actor, time, outcome, and reason when reconciliation changes business
truth.

Roadworthiness and Assessment are independent professional findings. Correction
retains the earlier accepted finding and displays the reasoned superseding
version; a closed Case must be reasonedly reopened before revision. A finding or
report correction never implies a fee/invoice change.

Report generation, PDF custody, Outlook Sent evidence, and external receipt are
separate. Report sent enters post-report work rather than closing the Case.
`CASE-23` query/dispute controls are `Next / 0.4.0` in the [capability inventory](../docs/capabilities.md#capabilities); the alpha UI invents no reply state machine.

Lifecycle actions use only the named [Core lifecycle and correspondence contract](../docs/requirements.md#lifecycle-closure-and-correspondence): Post-report completion, Provider cancellation, Collision Engineers rejection, and Created in error remain distinct from acknowledgements, information requests, report-Sent evidence, queries, and other correspondence. The interface never substitutes a generic Close action. A closed Case is read-only; only a permitted reasoned reopen to a valid nonterminal state restores mutation controls, and `Created in error` offers only its linked-replacement route.

Each Case has at most one authorised staff editor at a time through the [Core lease and mutation guard](../docs/requirements.md#case-edit-authority-and-recovery). Other authorised staff see the holder and that Case read-only. `Enter edit mode`, renewal, `Leave editing`, authoritative expiry, reload/compare, and reacquire are the only recovery interactions: lease loss or a stale version disables every mutation, preserves proposed values for comparison, and never overwrites the newer Case. There is no forced Administrator takeover, bulk Case edit, direct external edit, or collaborative merge control.

### Documents and external evidence

- Create the Box case folder using the immutable Case/PO name.
- Retain source emails, instruction documents, images, correspondence, and reports.
- Preserve document and issued report/addendum versions.
- Use logical removal; never physically delete files through the workflow.
- Closed-case documents are read-only until the Case is validly reopened.
- Show Box unavailable, pending, retry, and unknown states rather than implying success.
- Provide authorised staff upload, view, download, and export actions.
- Treat request-scoped public upload as request-local receipt only, not Case creation, Box custody, EVA handoff, report generation, or delivery.
- Private transient Worker staging is not a staff surface or downloadable area.
- Keep picture upload, report-with-PDF handoff, PDF generation/custody, and external delivery as distinct evidence states.
- Report evidence uses the exact Outlook Sent item and keeps discovery, link, and sent times distinct.
- Manual link, unlink, or relink requires a reason and deterministically recomputes dependent events/counts.
- Preserve the final accepted Sent association even if Outlook later moves or deletes the item.
- Ambiguous or absent evidence remains visible.
- Triage reply evidence and Case report-Sent evidence are separate contracts.
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

The History panel is a read-only presentation of the [Core-owned permanent action history](../docs/requirements.md#permanent-action-history). It shows the attributable staff or automated actor, caller, time, one affected Case or pre-case record, action/outcome, reason where required, and before/after or evidence reference needed to understand each business event. It does not render message bodies, routine views, refresh/polling, retries, lease heartbeats, or adapter/Worker mechanics; those remain telemetry or security evidence outside the operational UI.

## Complete UI state contract

| Scope | Required states |
| --- | --- |
| Queries | Loading; empty; current success; stale with last-good time; partial; unavailable; failed/retry; unauthenticated; disabled; stale-role; denied |
| Mutations | Validation; confirmation; success; denied; stale version; lease lost; dependency unavailable; idempotent/replayed result; conflict and recovery |
| Intake | Empty/oversize; replay; retention/custody failure; Draft ready; Needs sorting; Unsupported; missing/integrity asset; evidence missing/contradictory; reasoned Blocked intake/resolve/retry; every acceptance path; refusal with no case/reference; upload token expired/revoked/cross-request/limit/abuse result |
| Triage | Registration missing; unassigned/assigned; every named state; missing/ambiguous/unapproved/technical reply evidence; finding replacement/correction/new response; cancel/reopen/link/unlink/relink |
| Case | Not ready/chasing; Review; Held/preserved interval; due/overdue; chaser last-outcome/next-action; gate refusal; physical address/Image Based Assessment; VRM and vehicle/MOT suggestion/no-result/stale/unavailable/failure; independent finding correction; documents locked; Box/external-effect states; EVA proxy/revision limitation; report generated/custodied/sent/externally received distinction; report evidence absent/ambiguous/manual/exact; every terminal outcome; archive; reopened; Created-in-error nonreopenable; lease held/expired/lost/stale |

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

Exact horizon and first-introduction release remain owned by the [capability inventory](../docs/capabilities.md#capabilities). The [ID-by-ID design mapping](product/traceability-matrix.md) mirrors those allocations. No future allocation creates an alpha route, control, workflow, placeholder or dormant implementation.

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