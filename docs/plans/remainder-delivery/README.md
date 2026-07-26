# CollisionSpike v2 remainder delivery

Status: **Ready V1 delivery spine — later work is routed separately**

## Finish line

Deliver the V1 live QDOS alpha described in [Remaining requirements](../remaining-requirements.md): every active QDOS case type enters through approved channels, one Core policy creates and manages the case, long-term files remain in Box, operators reach successful EVA export and the V1 terminal/reopen outcomes, and the release is independently verified and accepted. [The maturity map](../feature-maturity-map.md) owns allocation.

This pack is the V1 delivery spine, not another product specification or status ledger. Requirements remain in the [operator notes](../../operator-notes/README.md), [project questionnaire](../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md), accepted [architecture decisions](../../architecture/decisions/), and [remaining-requirements baseline](../remaining-requirements.md). Route later-horizon sequencing through the [delivery roadmap](../delivery-roadmap.md) and the [later-delivery index](../later-delivery/README.md); do not extend this V1 spine into a second roadmap.

## Authority and boundaries

- Apply the [source-of-truth order](../../agent-guidance/source-of-truth.md). Record material ambiguity in the canonical [open-decision register](../open-decisions.md); do not infer it from predecessor code or corpus evidence.
- Keep `docs/operator-notes/` and `corpus/` read-only. Corpus evidence remains local and cannot be uploaded to Box, Azure, a model provider, or any other external service.
- Preserve the four approved projects, one Core owner for each business rule, one `CollisionSpikeDbContext`, one migration stream, and thin Web, Worker, API and MCP entry points.
- Treat every external call, credential operation, billed run, Azure change, deployment and predecessor action as separately approval-gated. This plan is not authority to perform one.
- No Box subtree is authorised by this plan. Any development proof requires a direct user decision naming the acting identity, exact root ID/type/name, descendant targets and permitted operations; everything else remains out of scope.
- Start v2 without importing predecessor cases, users, action-history records or application state. Predecessor retirement remains a separate exact-target decision.

## Stable invariants

- Manual upload, Outlook, provider API and MCP consume named Core use cases; transports and adapters never decide matching, acceptance, numbering, workflow or permissions.
- A source has a channel-specific immutable identity. Content hashes prove integrity and possible duplication but are not a global business identity.
- Case acceptance writes the state change, reference allocation, case, action-history event and outbox atomically. Sequence numbers never wrap, widen or return to the pool.
- One server-owned expiring edit lease permits one active staff editor per case; other staff remain read-only, and every mutation also checks the lease token and current case version.
- Relational typed case data is authoritative. Immutable source bytes, extraction candidates and provenance remain review evidence rather than a second editable authority.
- Cases are archived, never deleted. Business terminal outcomes remain distinct from integration and technical failures.
- `Planned`, `Implemented`, `Called`, `Locally verified`, `Deployed`, `Live verified` and `Accepted` are separate evidence states.

## Delivery order

| Order | Area | Requires | Real or intended caller | Unlocks |
|---|---|---|---|---|
| 1 | [Relational intake draft](casework/intake-and-case-acceptance.md#review-and-resolve-an-intake-draft) | Current local slice | Existing Development-only `POST /Intake/Upload` and `GET /Intake/Review` | Provider-neutral source-identity receipts and typed read-only drafts; no case/reference |
| 2 | [Staff identity and role enforcement](identity-and-access/staff-identity-authorisation-and-action-history.md#authenticate-staff-and-enforce-role-boundaries) | Relational draft | Planned authenticated Web pages | Trusted actors and protected staff operations |
| 3 | [Permanent action history](identity-and-access/staff-identity-authorisation-and-action-history.md#attribute-permanent-action-history-and-automation) | Trusted actors | Planned Web/Worker business actions | Durable attribution for later acceptance and case mutations, separate from security logs/telemetry |
| 4 | [Principal/configuration administration](identity-and-access/staff-identity-authorisation-and-action-history.md#administer-principals-and-live-operational-configuration) plus [reviewed provider reference data](casework/intake-and-case-acceptance.md#prepare-reviewed-provider-reference-data) | Administrator identity and one-time reviewed preparation | Planned Administrator pages and an authorised offline preparation command | Stable principal/code/configuration inputs; no runtime spreadsheet importer |
| 5 | [Durable source receipt and staging](integrations/source-custody-and-document-processing.md#durable-source-receipt-processing-and-custody-hand-off) | Stable source identity and actor contracts | Planned Web stages manual bytes; later triggered Worker stages Graph bytes | Production-safe source processing, provenance, staging and custody outbox; targeted scanned-PDF OCR is V2 |
| 6 | [Ordinary-image registration](casework/intake-and-case-acceptance.md#read-vehicle-registration-from-ordinary-images) plus [provisional image identity](casework/intake-and-case-acceptance.md#establish-provisional-image-identity-before-acceptance) | Durable image occurrence/provenance | Planned intake-review caller through the shared Core policy | Readable reviewed VRM for image-led work; uncertainty remains pre-case |
| 7 | [Inspection-address reference preparation](integrations/vehicle-data-and-eva-export.md#prepare-reviewed-inspection-address-reference-data) | Supplied spreadsheets and authorised one-time reviewer | Authorised offline preparation command | Versioned local reference output/provenance; no runtime upload, job or sync |
| 8 | [Case identity and references](casework/case-identity-and-references.md), including [used-principal-code cutover](casework/case-identity-and-references.md#replace-a-used-principal-code-through-an-immutable-cutover) | Orders 1-7 provide trusted actor, configured principal and definitive pre-case identity evidence | Planned `AcceptCaseDraft` allocator call plus Administrator cutover caller | One allocator contract with immutable case identity and no accepted-case prerequisite cycle |
| 9 | [Definitive case acceptance](casework/intake-and-case-acceptance.md#accept-a-definitive-case-transaction) | Orders 1-8 | Planned automatic Worker hand-off plus authenticated Web/manual and image-led resolution | One atomic case/reference/action-history/outbox transaction; automatic instruction intake begins in `Not ready` |
| 10 | [Box case files](integrations/box-case-files.md) | Custody outbox, accepted case and separately approved exact Box scope | Planned outbox handler through Core | Case folders, versions and file requests in an approved scope |
| 11 | [Exclusive case editing](casework/case-editing-concurrency.md#acquire-renew-and-release-one-case-edit-lease) | Staff identity, accepted case, action history and first named mutation | Planned authenticated edit mode | One active editor with stale-write protection |
| 12 | [Lifecycle and work management](casework/lifecycle-and-work-management.md) | Case identity and edit guard | Planned guarded Web actions and Worker reminders | Settled review, terminal, reopen, chasing and manual-outcome workflow |
| 13 | [Reviewed V1 UI route](../ui-ux/README.md) and [operator workspace](casework/operator-workspace.md) | Stable query/command owners and explicit shell-direction approval | Planned authenticated Razor Pages | Operations, Intake, Triage, Case and Administration surfaces |
| 14 | [Outlook and background processing](integrations/outlook-and-background-processing.md#scoped-inbound-outlook-receipt-and-processing) | Accepted relevant mailbox contract/allowlist, durable custody and a real Function trigger | Planned Worker trigger for `instructions@` | Continuous idempotent staff-forwarded intake through the same Core owner |
| 15 | [Triage workflow](casework/triage-workflow.md) | Trusted actor/history, durable source, registration and accepted exact reply-chain matcher | Planned authenticated Triage pages and Web-to-Core-to-Outlook evidence caller | Separate roadworthiness workflow with no due date, chasers or case/reference creation |
| 16 | [Inspection-address resolution, vehicle/MOT and EVA export](integrations/vehicle-data-and-eva-export.md) | Confirmed case data and separately accepted vendor/export contracts | Planned guarded Web commands | Reviewed address, vehicle enrichment and successful manual EVA hand-off proxy |
| 17 | [Staff MCP](integrations/staff-mcp.md) | Existing staff Core use cases, OAuth/roles and edit guard | Planned `/mcp` endpoint | V1 case/document/intake actions; classified-email tools are V2 |
| 18 | [Azure, observability and release](platform/azure-observability-and-release.md) | Caller-backed slices plus ADR-0009 package, migration, identity and provenance foundation | Authorised terminal, then deployed Web/Worker callers | Managed runtime, explicit migration, recovery and immutable release evidence |
| 19 | [Acceptance and cutover](platform/acceptance-and-cutover.md#complete-operator-acceptance-and-production-cutover) | Every required V1 slice through its actual caller | Actual Web/Worker/MCP journeys as delivered | Operator acceptance and controlled production cutover; provider API remains V2 |

The V2 provider API and V1 staff MCP branch only after shared Core and authorisation contracts stabilise; one composition owner integrates shared Web edits. The mailbox research blocks the V0 instruction classifier and allocated V1 exact matchers until their predicates are accepted, then owns the V2 expansion. It does not block settled manual or non-email workflows.

## Ownership and merge hotspots

| Boundary | Single owner | Consumers | Coordination rule |
|---|---|---|---|
| Intake, acceptance, references and lifecycle policy | Corresponding Core feature | Web, Worker, API, MCP | Extend the current owner; never add transport-specific policy |
| Triage state, finding and case-link policy | Core `TriageWorkflow` | Authenticated Web and Outlook evidence adapter | Keep separate from inbox categories/case lifecycle; Outlook supplies evidence only |
| Principal-code replacement and sequence continuity | Core case-identity/reference owner | Administrator principal page and allocator | Used code is read-only; one atomic linked-successor cutover, never a second counter |
| `CollisionSpikeDbContext` and migrations | Persistence owner | Every feature | One ordered migration stream; no concurrent migration or model-snapshot edits |
| Web composition and authentication | Web composition owner | Razor Pages, provider API, OAuth/MCP | Merge shared registration once after feature contracts stabilise |
| Case edit lease and case row version | CaseEditing/persistence owner | Web case actions and staff MCP | One SQL lease/version contract; no page-local, in-memory or per-action lock |
| Worker composition and triggers | Worker composition owner | Graph polling, queues and background work | Register only handlers reached by a real Function trigger |
| External adapters | One adapter per boundary | Named Core use cases | Persist external identities and fail visibly; no global search or workflow decisions |
| Outlook allowlist and exact Sent evidence | Administrator configuration plus one Infrastructure adapter | Report and Triage Web/Core actions | One allowlist; manual report link is settled, automatic email matching remains under the combined research |
| Material tests and final verdict | Test author, then different reviewer | Every task | Implementation author, material test author and final evaluator remain distinct |

## Approval boundaries

| Action | Exact scope required | Approval and evidence required |
|---|---|---|
| Box read or mutation | Exact acting identity, root ID/type/name, descendant IDs and operation | A direct user decision must name the scope; verify it and prove local out-of-scope denial before any client call |
| Outlook or Exchange access | Per-environment service principal, shared approved-mailbox allowlist and exact folder/action scopes | Exchange Application RBAC is the sole `Application Mail.Read` grant, no unscoped Entra Graph application mail permission remains, and every Outlook reader enforces the shared allowlist before a Graph call. Production automatic intake is `instructions@collisionengineers.co.uk` Inbox; development needs a separately approved non-production scope or stays live-disabled |
| Corpus evaluation | Frozen local cohort/holdout, hashes and maximum record/page count | Local-only execution; no external transfer and no mutation of source files |
| Azure preview or write | Subscription, tenant, UK South region, environment, resource group, SKUs and hard spending cap | Fresh inventory, current Microsoft guidance, policy/quota checks and separate preview/provision/deploy approval |
| Provider or MCP live enablement | Principal clients, OAuth client, scopes, callback, data flow and target environment | Credential/data-processing approval plus negative isolation and revocation evidence |
| Production release or predecessor change | Exact artifact, migration, target environment, mailbox poller and predecessor resource | Management acceptance, technical release approval, smoke/rollback evidence and separate destructive authority |

## Evidence language

Record evidence in the owning task, never as a central roll-up:

| State | Meaning |
|---|---|
| Planned | Reviewed sequence, boundaries and acceptance criteria exist |
| Implemented | Code or configuration exists in the working tree |
| Called | The intended production entry point reaches the behavior |
| Locally verified | Stated local checks pass on stated inputs |
| Deployed | The named environment accepted the deployment |
| Live verified | Fresh production-like traffic reached the expected path and result |
| Accepted | An authorised operator or stakeholder accepted the observed result |

## Integrated acceptance journey

The V1 proof takes every active QDOS case type through actual intake, reviewable source/draft, exactly one correct reference, exclusive editing, approved Box custody, workflow, exact Sent evidence, staff MCP, and successful EVA export. The Worker and MCP callers must be real; provider API, V2 OCR/matching/email work, shared-development evidence, operator acceptance, and production cutover remain distinct.

## Plan maintenance

Before starting a task, re-read its authority and dependencies, inspect current callers and scoped working-tree changes, and record volatile facts in dated evidence. Update the owning task only when implementation or evidence changes. Reconcile source changes before editing instructions; never generate a board, duplicate acceptance criteria, or revive the retired monolithic plan.
