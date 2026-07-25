# Operator workspace

## Purpose

Provide the staff-facing intake dashboard, queues, case search and detail workspace that expose real Core decisions rather than a second workflow implementation. It makes case/inbox work observable and actionable while keeping planned callers honestly labelled.

## Authority and current boundary

- **Authority:** [source order](../../../agent-guidance/source-of-truth.md), [questionnaire §§4–7](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md), and [remaining requirements §§4–5](../../remaining-requirements.md).
- **Policy owner:** Core case/intake query and action use cases; Web owns request/view translation only.
- **Current implementation:** an unauthenticated development-only `/Intake/Upload` page plus provider-neutral receipt queue/review and persisted counts. Its models are not the first-MVP operational workspace.
- **Real callers:** `/Intake/Upload` is the only current real intake caller. Authenticated dashboard, inbox queues, case detail/search and manual refresh are **planned**.
- **Persistence/adapters:** read models must query the authoritative intake/case/lifecycle records; no dashboard counter store. Box/EVA/document presentation depends on their own adapters.
- **Dependencies:** staff identity, [intake acceptance](intake-and-case-acceptance.md), [Triage workflow](triage-workflow.md), [identity](case-identity-and-references.md), [lifecycle/work](lifecycle-and-work-management.md) and [exclusive case editing](case-editing-concurrency.md).
- **Replaces/consolidates:** replace local receipt count/queue semantics with shared query contracts and delete any view-local state/calculation after migration.

## Shared failure and observability rules

Every count opens the exact filtered query it represents. Counts, last-updated time and manual refresh must be truthful about the data boundary. `Triage` has a separate business workflow and is never an inbox label; `Needs sorting` is uncertain material, while `Blocked intake` is a manually selected pre-case filter with reason, warning and retry. Authorisation is enforced before data query/action; failed/empty/partial views are visible and content-safe telemetry is correlated to the underlying use case.

## Deliver operational queues and dashboard

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §5 dashboard requirements and remaining requirements §5.
- **Confirmed facts:** required work tiles are `Not ready`, `Review`, `Held`, `Receiving work`, `Queries`, `Other`, `Needs sorting`, and `Blocked intake`. `In today` counts cases created since Europe/London midnight. Replace ambiguous submitted/cleared activity with paired `Sent to Engineer` and `Reports sent` today/week totals; weeks are Monday-to-Monday in Europe/London. `Sent to Engineer` counts each case once from the stable first event, whose first-MVP proxy is successful EVA JSON/image export generation. `Reports sent` counts every successfully sent report from exact Outlook Sent-item evidence in the shared approved-mailbox allowlist.
- **Decision required before implementation:** none; queue contents defer to lifecycle/chase decision gates where they apply.

### Owner and dependencies

- **Policy/implementation owner:** Core `OperationalWorkQuery`; Web dashboard is its caller/presenter.
- **Independent evaluator:** UI/accessibility reviewer plus independent query/integration tests.
- **Prerequisites:** identity authorisation, authoritative intake/case state, lifecycle data and due-work model.
- **Consumers/unlocks:** staff daily work and later MCP parity.

### Caller, contract and change boundary

- **Real or intended caller:** planned authenticated dashboard and queue pages; current `/Intake/Upload` must not be described as this caller.
- **Input/output:** signed-in role, one named queue/filter and Europe/London date/week boundary return a count, ordered items, last-updated time and page/action links; refresh re-queries authoritative records. Separate Triage navigation queries the Triage owner and never adds a generic inbox filter.
- **Ordered decisions and failure behavior:** authorise; validate filter and London boundary; use Core query; render state/reason/age/due details; surface failed read without fabricated zero count. `Blocked intake` retry calls the intake Core policy. A failed EVA export or missing/unapproved Sent item contributes no activity count.
- **Persistence/migration:** projection/indexes only where needed by the authoritative model; never persist independently editable dashboard counts.
- **Adapters/side effects:** manual refresh has no external side effect; no background polling or sender is implied.
- **Operator surface and observability:** business labels, accessible count/link relationship, empty/failure state and content-free query timing/outcome telemetry.
- **Documentation affected:** testable UI guidance only after the live caller exists.
- **Replaces/consolidates:** retire the current `IntakeQueueCounts` and receipt-only queue pages after parity, rather than maintaining a second dashboard.

### Scope

- **Included:** required work and paired activity tiles, exact filters, London calendar boundaries, manual refresh, last-updated, intake retry entry point and a separate link/count into the planned Triage list.
- **Excluded:** analytics dashboard, outbound automation, generic `Triage` inbox, unauthorised data views and direct external integration calls.

### Implementation checklist

- [ ] Define one Core query contract for all named queues/counts and map dashboard links to exact query filters.
- [ ] Build authenticated Web dashboard/queue presentation with manual refresh, clear empty/failure states and business vocabulary.
- [ ] Add separate Triage navigation/count backed by its named query; do not include it in Receiving work/Queries/Other/Needs sorting filters.
- [ ] Migrate/reconcile existing receipt queue/count pages and remove duplicate calculation after user-visible parity.
- [ ] Derive `In today` from case creation, `Sent to Engineer` once per case from the stable first event, and `Reports sent` once per exact report event; do not persist editable counter totals.

### Validation checklist

- [ ] Seed each named case/inbox state and prove tile count equals destination query count; test zero, pagination and stale/failed read display.
- [ ] Test `Blocked intake` reason/warning/retry and `Needs sorting` uncertainty remain distinct; test `Triage` cannot be used as generic inbox filter.
- [ ] Test the separate Triage link/count agrees with its own list, exposes no due/chaser semantics and cannot create a case/reference.
- [ ] Test London midnight, Monday week and daylight-saving boundaries; successful first EVA export counts a case once, retries do not, each exact report-sent event counts, and failures/unapproved evidence count zero.
- [ ] Test role denial and manually refresh the actual dashboard caller; review keyboard/accessibility semantics.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1`, reporting scoped outcome separately from concurrent changes.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Tile with work | Count and linked filtered list agree | browser/integration evidence | Operational accuracy after deployment |
| Blocked intake | Reason/warning and retry shown; no case/reference | Core-to-Web test | Staff resolution judgement |
| Manual refresh/read error | Fresh timestamp/result or clear failure, never invented zero | UI/test evidence | Azure availability |
| Activity at London boundary | case creation, first sent-to-Engineer event and every report event appear in the correct today/week totals | injected-clock query and browser test | EVA/Engineer receipt or report delivery |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** no external mutation; staff usability acceptance is required before replacing local development queue views.
- **Rollout/activation:** deploy behind authenticated access, prove each tile against seeded/QDOS data, then remove obsolete local views in the same slice.
- **Rollback/recovery:** restore prior application artifact while authoritative records remain unchanged; no dashboard data migration is destructive.
- **Irreversible risk:** none beyond removal of duplicate display code after parity proof.

### Deferred-capability impact

- **Named capabilities:** MCP, external/customer accounts, full mailbox scope, WhatsApp and AI/vision assistance.
- **Stable seam retained:** role-aware Core queries and named business filters can be consumed by later staff MCP without exposing administration.
- **Future migration/replacement:** external views/channels need their own authorisation and content rules.
- **Activation boundary:** accepted caller/authorisation evidence for each new surface.
- **Deliberately absent:** no analytics lake, public portal, MCP route, AI assistant or mailbox poller.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Queue/query ownership and acceptance boundaries | Implemented UI, deployment or operator acceptance |

## Deliver case search and workspace actions

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §§4–6; remaining requirements §§4–6.
- **Confirmed facts:** search/filter includes Case/PO, registration, claimant, claim number, principal, stage/status, Engineer, received/instruction dates, range and intake origin; original source remains after match/merge.
- **Decision required before implementation:** none for reopen, chase cadence, identity immutability or report evidence. Automatic Sent-item matching remains deferred; this UI must require explicit exact-item association and never guess.

### Owner and dependencies

- **Policy/implementation owner:** Core `CaseWorkspaceQuery` plus existing named lifecycle/intake actions.
- **Independent evaluator:** test engineer for filter/provenance and UI reviewer for operator flow.
- **Prerequisites:** typed case/identity/lifecycle, document custody links and authorisation.
- **Consumers/unlocks:** staff case work, manual EVA export and later role-constrained MCP.

### Caller, contract and change boundary

- **Real or intended caller:** planned authenticated case-list/detail pages; no current caller.
- **Input/output:** authorised structured filters return matching cases; case detail presents identity, origin/source, fields/provenance, history, documents/links, state/gates and permitted Core actions.
- **Ordered decisions and failure behavior:** validate filter/date range; authorise; query the owner; acquire the [exclusive case-edit lease](case-editing-concurrency.md#acquire-renew-and-release-one-case-edit-lease) before exposing mutations; delegate changes to intake/lifecycle/identity use cases with lease token and case version. Unknown/no-match, active editor and stale lease/version are visible; UI never alters reference/state directly.
- **Persistence/migration:** searchable/indexed authoritative fields and origin/association history; no copied Elastic/index service in first MVP.
- **Adapters/side effects:** render persisted Box/EVA links only through approved adapters; export and document actions remain separate plans.
- **Operator surface and observability:** exact labelled filters, readable origin and permanent action history; content-safe search/action telemetry. Principal/reference are read-only immediately after allocation; wrong-principal handling shows the terminal `Created in error` original and linked replacement, not an alias or edit.
- **Documentation affected:** preserve operator terminology and source links; no operator-note edits.
- **Replaces/consolidates:** no existing case workspace; do not grow the receipt review page into a parallel case engine.

### Scope

- **Included:** required filters/search, read-only case detail, explicit edit-mode entry, origin/provenance and links to authorised lease-guarded Core actions.
- **Excluded:** financial editing, EVA API, automatic Sent-item matching, public/external views and permanent deletion.

### Implementation checklist

- [ ] Add one authorised Core query/read model covering the stated filters, durable source-origin history and linked `Created in error` replacements; do not add reference aliases.
- [ ] Build list/detail pages that delegate every mutation to the named owner, require the exclusive lease/version contract, keep identity read-only, and expose only the settled reasoned reopen/replacement/report-evidence actions.
- [ ] Add necessary database indexes with migration evidence; do not introduce an independent search store.

### Validation checklist

- [ ] Test every emitted filter singly and in combination, date boundary and case origin after merge/reversal; test immutable identity, reciprocal replacement links and absence of aliases.
- [ ] Verify user cannot retrieve/change unauthorised administration data, bypass Core action policy through page parameters or submit a case mutation without the current lease token and case version.
- [ ] Exercise actual planned list/detail callers with seeded data and genuine-shaped QDOS case record; perform accessibility review.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1` and record the exact scoped outcome.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Origin/date/claim filter | Only matching authoritative cases shown | integration test | Production query performance |
| Decision-gated action | Hidden/refused with a clear state, no direct mutation | authorisation/UI test | Settled future policy |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** no external write; operator accepts search/workspace evidence before staff enablement.
- **Rollout/activation:** migrate indexes, deploy authenticated pages, run non-sensitive case smoke journey and monitor failed queries/actions.
- **Rollback/recovery:** return to previous artifact; preserve data/index migration and use forward migration if correction is necessary.
- **Irreversible risk:** none; case/source/action history remains retained.

### Deferred-capability impact

- **Named capabilities:** EVA replacement/API, estimates/valuation/invoices, external accounts, MCP and guided capture/AI.
- **Stable seam retained:** structured case read model, origin/provenance and named Core actions accommodate later fields/surfaces without duplicate workflow policy.
- **Future migration/replacement:** financial/AI/external data requires new source authority, permissions and migration decisions.
- **Activation boundary:** product authority and independent evidence for each later capability.
- **Deliberately absent:** no public endpoint, generic search platform, finance UI, AI suggestions or external-account role.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Workspace ownership, caller and gates are defined | Code, caller, deployment or acceptance |
