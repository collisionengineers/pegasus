# CollisionSpike v2 remainder delivery

## Finish line

Deliver the first usable QDOS release described in [Remaining requirements](../remaining-requirements.md): genuine instructions and images enter through approved channels, one Core policy creates and manages a case with permanent action history, long-term files remain in Box, operators complete the workflow through report and post-report activity, and the release is independently verified and accepted.

This pack is a delivery map, not another product specification or status ledger. Requirements remain in the [operator notes](../../operator-notes/README.md), [project questionnaire](../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md), accepted [architecture decisions](../../architecture/decisions/), and [remaining-requirements baseline](../remaining-requirements.md).

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
| 2 | [Staff identity, authorisation and action history](identity-and-access/staff-identity-authorisation-and-action-history.md#authenticate-staff-and-enforce-role-boundaries) | Relational draft | Planned authenticated Web pages | Trusted actors and protected staff operations |
| 3 | [Principal and operational configuration](identity-and-access/staff-identity-authorisation-and-action-history.md#administer-principals-and-live-operational-configuration) | Staff identity/action history | Planned Administrator pages | Stable QDOS principal, read-only used codes, linked-successor cutover caller and completeness policy |
| 4 | [Durable source receipt and staging](integrations/source-custody-and-document-processing.md#durable-source-receipt-processing-and-custody-hand-off) plus [targeted scanned-PDF OCR](integrations/source-custody-and-document-processing.md#targeted-scanned-pdf-ocr) | Relational draft and actor contracts; OCR additionally requires staged page candidates and exact cloud approval | Planned Web receive operation stages manual/provider bytes; planned Worker receive/processing operations stage Graph bytes and alone call OCR | Production-safe original staging, targeted scanned-PDF completion and custody outbox |
| 5 | [Case identity and references](casework/case-identity-and-references.md), including [used-principal-code cutover](casework/case-identity-and-references.md#replace-a-used-principal-code-through-an-immutable-cutover) | Configured principal, trusted actor and durable definitive acceptance evidence | Planned acceptance/case-detail commands plus Administrator cutover caller | One active allocator; immutable case identity, linked wrong-principal case replacement and prospective linked-principal sequence continuity |
| 6 | [Definitive case acceptance](casework/intake-and-case-acceptance.md#accept-a-definitive-case-transaction) | Orders 1-5 | Planned automatic Worker/provider hand-off plus authenticated Web/manual resolution | One atomic case/reference/action-history/outbox transaction |
| 7 | [Box case files](integrations/box-case-files.md) | Custody outbox, accepted case and separately approved exact scope | Planned outbox handler through Core | Case folders, versions and file requests in an approved scope |
| 8 | [Exclusive case editing](casework/case-editing-concurrency.md) | Staff identity, accepted case, action history and first named mutation | Planned authenticated edit mode | One active editor with stale-write protection |
| 9 | [Lifecycle and work management](casework/lifecycle-and-work-management.md) | Case identity and edit guard | Planned guarded Web actions and Worker reminders | Settled review, terminal, reopen, chasing and manual-outcome workflow |
| 10 | [Operator workspace](casework/operator-workspace.md) | Case queries, permitted lifecycle, edit guard and document links | Planned authenticated Razor Pages | Searchable operational workspace and settled London activity tiles |
| 11 | [Outlook and background processing](integrations/outlook-and-background-processing.md) | Durable intake/custody and an accepted mailbox categorisation contract | Planned Functions trigger for `instructions@` | Continuous idempotent intake after exact mailbox approval |
| 12 | [Triage workflow](casework/triage-workflow.md) | Staff identity/action history, durable source, shared mailbox allowlist and accepted exact reply-chain matcher from the combined research | Planned authenticated Triage Web pages and Web-to-Core-to-Outlook evidence caller | Complete separate roadworthiness workflow with no case/reference creation |
| 13 | [Vehicle data and EVA export](integrations/vehicle-data-and-eva-export.md) | Confirmed case data and accepted vendor/export contracts | Planned guarded Web commands | Vehicle enrichment and manual EVA hand-off |
| 14 | [Provider submissions](integrations/provider-submissions.md) | Accepted versioned wire contract, intake policy and action-history actors | Planned provider endpoints | Principal-scoped machine intake |
| 15 | [Staff MCP](integrations/staff-mcp.md) | Existing staff Core use cases, auth and edit guard | Planned `/mcp` endpoint | Attributed staff automation without a second rule engine |
| 16 | [Azure, observability and release](platform/azure-observability-and-release.md) | Caller-backed application slices | Planned shared-development and production deployments | Managed runtime, recovery and release evidence |
| 17 | [Acceptance and cutover](platform/acceptance-and-cutover.md) | All required first-release areas | Actual Web/Worker/API/MCP journeys as delivered | Operator acceptance and controlled production cutover |

Provider API and staff MCP may branch after shared Core and authorisation contracts stabilise. Their edits to shared Web composition are integrated by one composition owner. The combined mailbox categorisation/all-automatic-email-matching research is the sole material open product decision and blocks only those automatic predicates; settled manual and non-email workflows remain planned deliverables.

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

The release proof starts with a genuine local QDOS shape through the actual intake caller, persists an operator-reviewable source and typed draft, accepts exactly one correctly referenced case, proves two staff sessions cannot edit that case concurrently, stores approved non-corpus proof material inside the permitted Box subtree, exercises staff lifecycle and workspace journeys, produces the EVA hand-off, and proves the Worker/API/MCP callers that are included in first-release scope. Shared-development deployment, recovery and integration smoke evidence remain separate from operator acceptance and production cutover.

## Plan maintenance

Before starting a task, re-read its authority and dependencies, inspect current callers and scoped working-tree changes, and record volatile facts in dated evidence. Update the owning task only when implementation or evidence changes. Reconcile source changes before editing instructions; never generate a board, duplicate acceptance criteria, or revive the retired monolithic plan.
