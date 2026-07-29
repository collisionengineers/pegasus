# Product requirements

## Authority and evidence

This document is the sole owner of Pegasus intended product requirements. The [capability inventory](capabilities.md) owns stable capability IDs, allocations and activation boundaries; it does not prove implementation.

The [operator notes](operator-notes.md) are the binding source for Collision Engineers’ business process and current-system knowledge. [Architecture](architecture.md) owns what is currently implemented and called. [Operations](operations.md) owns procedures and evidence profiles. [Open decisions](open-decisions.md) owns unresolved material questions. [Design](../design/README.md) owns the durable UI contract.

Evidence states remain distinct:

1. allocated to `Now` or a version;
2. implemented in source;
3. exercised through the real caller;
4. deployed;
5. live-verified;
6. accepted by an authorised operator or management.

A lower state never implies a higher one.

## Ordered release sequence

`Target release` is the first intended release containing a capability. The
sequence is dependency-qualified, not a calendar schedule: no release has an
invented date, and allocation never activates or proves a caller, route, schema,
credential, external operation, deployment, or acceptance. The
[capability inventory](capabilities.md) remains the sole ID-to-target owner.

| Order | Target release | Stage and dependency intent | Count |
| ---: | --- | --- | ---: |
| 01 | `0.1.0-alpha.1` | Existing QDOS-alpha scope; allocation unchanged, not a completion claim | 128 |
| 02 | `0.2.0` | Provider expansion and intake fidelity after QDOS acceptance | 8 |
| 03 | `0.3.0` | Four-mailbox classification, association, folder actions, email workspace and email MCP | 19 |
| 04 | `0.4.0` | Principal-scoped provider API and post-report query/dispute casework | 5 |
| 05 | `0.5.0` | Extended case types and staff/outbound communication channels | 5 |
| 06 | `0.6.0` | Individually approved operator AI assistance | 5 |
| 07 | `0.7.0` | Optional direct EVA API coexistence before replacement | 1 |
| 08 | `1.0.0` | Pegasus-owned engineering record/workbench and transfer of EVA assignment, estimating, valuation and report-preparation authority | 12 |
| 09 | `1.1.0` | Deterministic report and fee-note rendering | 6 |
| 10 | `1.2.0` | Targeted report distribution, accounts/invoicing and management information | 5 |
| 11 | `1.3.0` | Vendor-neutral AI work requests, Engineer-reviewed query proposals and staff-selected AI Assessor | 3 |
| 12 | `1.4.0` | Conditional capture and domain outcomes after direct promotion decisions | 3 |

The 200 planned capabilities use these twelve targets; 29 permanent boundaries
remain `Not planned / unallocated`.

Sequence constraints:

- accepted `0.1.0-alpha.1` evidence precedes activation of later releases;
- `INT-04` precedes `INT-05`, `INT-06`, and `INT-07`;
- `INT-28` precedes `INT-32` within `0.2.0`;
- accepted `CASE-31`, `ENG-01`, and `ENG-02` data/workflow precede
  `EXT-08` and `RPT-01`–`RPT-05` rendering;
- accepted report events/rendering precede `MAIL-17` and the `MI-*`
  consumption path;
- within `1.3.0`, `AI-09` transport, lease, and recovery are proved before any
  AI proposal caller, and `AI-07` remains blocked on assignment authority;
- `AI-02`–`AI-04` and `AI-06` remain blocked until evidence shows deterministic
  rules are insufficient;
- `0.7.0` / `EXT-04` is optional and non-blocking, not a prerequisite for
  `1.0.0`;
- `EXT-16`, `EXT-17`, and `EXT-19` remain non-blocking Triage allocations and
  prohibited from implementation until their direct promotion decisions.

All mailbox, WhatsApp, EVA, Box, provider, AI, and other source-specific
approval gates remain mandatory. A target never authorises an external read or
write, credential, vendor contract, or product caller.

## Purpose, users, and outcomes

Pegasus is Collision Engineers’ clean-room case-management and reporting application. It must replace fragmented intake, case tracking, document custody, correspondence, engineering workflow, and reporting with one auditable system while preserving operator authority and human approval.

Primary users are authorised Collision Engineers staff. The alpha is an Operations-first staff service focused on a QDOS intake route; that focused caller is the first exercised slice, not the limit of the intended mailbox, provider, casework, or reporting model.

Required outcomes:

- make receiving work, incomplete intake, Triage, active cases, due work, queries, and completed work visible without reconstructing state from multiple systems;
- retain source identity, chronology, custody, decisions, corrections, and action history;
- fail closed before case creation or reference allocation when identity, mandatory evidence, limits, processing, or standalone Audit evidence is incomplete or ambiguous;
- keep business decisions in `Pegasus.Core`, with infrastructure, UI, Worker, MCP, imported workspaces, skills, prompts, and models subordinate to Core policy and human approval;
- support deterministic, repeatable local verification and separately authorised live verification;
- preserve deferred capability seams and data identities without building dormant capability.

## Product invariants

### Principal, case, and reference identity

- Principal and internal reference are immutable after allocation.
- Reference allocation occurs only after the principal, case type, mandatory fields, and acceptance gates are settled.
- A wrong-principal case closes as `Created in error`, with a reason and a linked replacement. Neither reference is reused; the original never reopens.
- A case is never deleted.
- Reopening requires a reason and the normal destination gates.
- Source messages, files, visible placements, attachments, images, and subsequent correspondence retain stable source identities and provenance.
- Hashes may correlate equal bytes, but never replace visible placement or occurrence identity.
- Historical correspondence is not reconstructed into synthetic historical cases. New correspondence about historical work may be handled under the current process with explicit provenance.

### Terminology and outcomes

`Audit`, `Triage`, `Needs sorting`, and `Blocked intake` have distinct meanings. `Triage` is the only current term for the operator workflow described below.

- `Audit` is standalone reviewed work with its own evidence and acceptance boundary; it is not a synonym for Triage or generic sorting.
- `Triage` is a staff workflow for a recorded matter requiring a finding and, where applicable, exact reply-chain Sent evidence.
- `Needs sorting` is a receiving/intake outcome when evidence can be persisted safely but cannot yet be routed.
- `Blocked intake` is a pre-case failure boundary where required processing, identity, limits, custody, or evidence is incomplete or unsafe.

## Intake and source identity

### Ways intake starts

Intake may begin through staff-forwarded email, a request-scoped upload, provider material, manually supplied files, images, correspondence, or a future approved API route. Receipt is not case creation.

Image-led material remains pre-case until principal, type, mandatory fields, and acceptance gates pass. No case or reference is allocated merely because images arrived.

Every intake path must:

- preserve original source bytes and message/file identity before deriving text or classifications;
- retain sender, recipients, subject, message identifiers, timestamps, attachment names, content types, byte lengths, hashes, and parent/placement relationships where available;
- be idempotent for the same source occurrence without collapsing distinct visible placements;
- surface unsupported, incomplete, corrupt, encrypted, oversized, ambiguous, or technically failed input as an explicit decision rather than silently dropping or accepting it;
- record the actor, time, caller, source, policy version, and reason for every transition;
- prevent untrusted content from becoming instructions, policy, identity, or authority.

### Mandatory pre-case gates

Before creating a case or allocating a reference, Pegasus must establish:

- authenticated principal identity and the staff actor where the route requires staff;
- provider/intermediary route identity and enabled policy where relevant;
- case type and principal association;
- the mandatory fields for that case type and route;
- successful source persistence and required extraction/classification receipts;
- processing and size/format limits;
- required standalone Audit evidence;
- absence of unresolved wrong-principal, duplicate-occurrence, or custody ambiguity.

If the route cannot establish these facts, it must persist only what is safe and route to the corresponding pre-case outcome. It must not allocate a reusable identity as a convenience.

### Matching and association

Matching uses explainable evidence. Message identifiers, provider/domain policy, route identity, accepted reference tokens, VRM, party identity, and operator confirmation may contribute. A weak or ambiguous signal never silently associates material with a case.

VRM correlation is a suggestion until confirmed by accepted evidence or an authorised operator. Source deduplication is occurrence-aware: exact bytes and transport identifiers support correlation, while each visible placement and chronology entry remains auditable.

## Triage

Triage records have the states `Open`, `Awaiting information`, `Finding recorded`, `Completed`, and `Cancelled`.

A recorded finding has two independently optional dimensions:

- Roadworthiness: `Roadworthy` or `Unroadworthy`;
- Assessment: `Repairable` or `Total loss`.

At least one dimension is required for each recorded finding. A later correction creates a reasoned superseding finding; it does not overwrite history.

Completion requires the exact reply-chain Sent-item evidence when a reply is required. Drafting, queuing, or staff assertion is not delivery evidence. Triage may be linked to a case later; unlink and relink actions retain reasoned history.

Cancellation and reopening require reasons. Reopening returns through the normal destination gates and never erases the prior finding, reply, actor, or chronology.

## Case identity and lifecycle

### Case types

The first release supports the allocated alpha case types and preserves stable seams for later types. Diminution and Commercial work remain deferred unless their capability rows and activation evidence say otherwise.

A case owns immutable identity, principal, internal reference, type, accepted source links, parties, vehicle identity, work state, due work, documents, correspondence, findings, decisions, action history, and closure history.

### Lifecycle

The lifecycle must support:

- pre-case receiving and acceptance;
- active work, awaiting information, review, and due-work visibility;
- manual chasing with explicit channel, cadence, actor, time, and result;
- inspection/report preparation appropriate to desktop assessment;
- report approval and delivery evidence;
- post-report queries, corrections, addenda, disputes, and reasoned closure where allocated;
- terminal outcomes including normal completion and `Created in error`;
- reasoned reopen through normal gates.

State changes are explicit Core transitions. UI labels, Worker handlers, APIs, and MCP tools call the same use cases; they do not implement parallel policy.

### Editing and concurrency

Accepted-case edits require concurrency protection. The observable contract must prevent silent lost updates, expose the current version/lease conflict, preserve the attempted actor and time where appropriate, and require the user to reload or deliberately reconcile. No UI or background process may bypass the Core concurrency decision.

### Chasing and action history

Manual chasing remains a staff action in the alpha unless an allocated capability and accepted integration explicitly authorise automation. The history records what was attempted, by whom, through which channel, against which party/address, when, and with what evidence. A recorded action is not proof of external delivery.

## Parties, principals, organisations, and access

Pegasus distinguishes principals, organisations, staff accounts, roles, and case-party roles. A repairer, broker, agent, client, legal representative, provider, vehicle keeper, or other contact may occupy different roles on different cases; raw provider/contact workbooks are evidence, not an import-authority model.

Requirements:

- staff authenticate through the approved staff identity route;
- authorization is enforced in Core use cases and at caller boundaries;
- least privilege separates ordinary casework, administration, configuration, mailbox/integration operation, and development-only evaluation;
- role, principal, organisation, mailbox, configuration, and staff-account changes are audited with actor, time, before/after value, and reason where required;
- access failure is fail-closed and does not reveal case or source data;
- immutable principal/reference rules apply regardless of administrative privilege;
- development routes and data never confer production access.

No current authentication design, app registration, scope declaration, or role table is evidence that the live caller exists or is accepted.

## Documents, extraction, and custody

### Supported source boundary

The intended intake boundary covers PDF, DOC, DOCX, EML, and MSG source material plus attached images and route metadata. Current support is proved only by the actual application caller and current architecture/evidence, not by an imported workspace or plan.

Pegasus must:

- preserve source bytes before deriving content;
- isolate parsing and enforce depth, count, size, decompression, relationship, and cancellation limits;
- return structured text/images/provenance and explicit partial/unsupported/technical-failure outcomes;
- retain extraction engine/package/version and policy provenance;
- never execute macros, active content, external relationships, or embedded instructions;
- distinguish scan-like material from corrupt, blank, unsupported, or encrypted material.

Alpha does not include dormant OCR. Scan-like OCR is a deferred capability and requires a separately accepted slice, provider, failure/recovery contract, caller proof, and evaluation.

### Staging and custody

Receipt/staging and accepted case custody are different states.

- Network, local, or Azure staging is temporary processing storage and is never custody proof.
- Box is the intended long-term accepted case-file custody system for the alpha, subject to an implemented adapter, approved test subtree/live target, identity, failure/recovery behavior, and caller proof.
- Local alpha work must not mutate any Outlook mailbox or Box location. Box testing is permitted only in a separately approved disposable test subtree; Outlook tests use immutable local copies or an explicitly approved test mailbox and operation.
- A custody transition records source identity, content hash, target identity/version, actor/caller, time, and failure/retry state without deleting the source proof prematurely.

## Vehicle and engineering evidence

Vehicle identity, registration, location, valuation, repair evidence, roadworthiness, total-loss, and salvage information remain source-labelled and reviewable.

For the report, record the vehicle location—client address or garage/repairer location—when explicitly supplied or operator-confirmed. Otherwise record `Image Based Assessment`. Collision Engineers performs desktop assessments only. Provider configuration may suggest a default but cannot overwrite explicit source evidence or operator confirmation.

Automated or AI-assisted extraction may propose candidate facts, confidence, damage observations, repair operations, costs, flags, valuation comparables, roadworthiness, total-loss, or salvage evidence only where an allocated capability and accepted evaluation permit it. `Pegasus.Core` and an authorised human own accepted facts, economics, findings, outcome, legal use, and approval.

A skill, prompt, model, workspace, external schema, or imported reference never becomes current OEM instruction, repair policy, valuation authority, legal advice, engineer approval, or product policy merely by existing.

## EVA and external engineering handoff

For the focused alpha, EVA remains the authoritative external engineering/report workflow. Pegasus supports the allocated manual handoff: produce the agreed JSON/image package with stable source identity and validation, then record the human-mediated handoff evidence. A supplied EVA schema, example payload, screenshot, or API guide is reference evidence only; it does not prove credentials, support, network access, a Pegasus adapter, or accepted activation.

EVA API integration and EVA replacement remain deferred. Activation requires vendor access, an accepted mapping, identity/authorization, idempotency, failure/recovery, current-version handling, caller proof, and operator acceptance.

Audatex remains a separate estimating-system role unless an accepted capability and integration contract establish otherwise. Guided-capture providers are candidates/evidence, not active routes.

## Email, mailbox, and background processing

The target product covers the approved mailbox estate and full source messages; the focused alpha mailbox is only the first caller. Mailbox inventory and current-system roles remain in [operator notes](operator-notes.md).

An Outlook/Graph route must, before activation:

- use an approved test/live mailbox and exact operation;
- preserve message, conversation, folder, attachment, sender/recipient, and received/sent identity;
- maintain a durable cursor/checkpoint and idempotent occurrence processing;
- separate read/intake scopes from draft/send and administrative scopes;
- queue only stable work identifiers, never full source payloads;
- record poison/retry/dead-letter and operator recovery behavior;
- prove the real Worker timer/queue caller;
- obtain exact Sent-item/reply-chain evidence when delivery is part of a completion gate.

The local alpha must not mutate a mailbox. A Worker project, queue registration, or timer configuration is not caller proof.

## Provider and intermediary routes

Provider identity, intermediary identity, route identity, and provider/domain-suffix association are separate facts. The versioned provider/domain package is evidence and configuration input; package presence does not activate a route.

Direct-provider and intermediary policies may differ but both call the same Core intake contract and fail closed when route identity, enabled policy, principal, or mandatory evidence is missing. Future provider APIs require distinct credentials/scopes, idempotency, source custody, status/recovery, rate-limit behavior, and acceptance. They remain unallocated or deferred according to the capability inventory.

## Staff MCP and automation

Staff MCP is an authenticated staff caller over the same Core use cases as the Web UI. It must expose only the approved capability/tool inventory, enforce OAuth scopes and Core authorization, return stable resource identities, preserve actor/history, and avoid administration, credential, mailbox-configuration, destructive, or live-send behavior unless separately allocated and accepted.

MCP registration, a tool schema, or an endpoint file is not proof. Each tool requires an exercised real caller, expected success result, authorization failure, validation failure, and audit-history proof.

Background automation follows the same rule. Queues and timers transport stable work identities; Core owns transitions and idempotency. Poison work remains recoverable and observable. No AI proposal or workspace service can mutate case state directly.

## Reports and correspondence

Reports are produced from accepted case facts and source-labelled evidence through the approved renderer boundary. Renderer source workspaces remain independent source imports until an accepted integration contract and real application caller exist.

Requirements:

- deterministic template and payload versioning;
- preserved document/source provenance;
- authorised human review and approval before issue;
- immutable issued artifact identity and hash;
- correction/addendum rather than silent overwrite;
- exact delivery evidence where the workflow requires it;
- accessible staff presentation of status, validation, and failure without implying an unproved external delivery.

Signatures embedded in governed renderer documents are provenance-sensitive document assets, not Web decorative imagery.

## Operator experience

The selected alpha direction is Operations-first. The UI must provide:

- an authenticated office-wide dashboard with day/week/due visibility;
- actionable receiving, requests, Triage, case, query, and exception queues;
- clear counts that link to their exact filtered work and do not render stale zero placeholders;
- list/detail journeys for intake, source evidence, Triage, cases, documents, history, and exports;
- administration for authorised accounts, roles, access, organisations, principals, configuration, and mailboxes;
- exact state labels mapped to Core decisions;
- loading, empty, validation, conflict, unavailable, partial, and access-denied states;
- keyboard, pointer, screen-reader, 200% zoom, forced-colour, and reduced-motion support;
- responsive use without hiding required evidence or actions.

The UI never infers state from colour alone, never uses decorative glyphs as unlabeled controls, and never presents draft, queued, attempted, allocated, or configured work as completed, delivered, deployed, or accepted.

The durable interaction, visual, component, and source/runtime rules are owned by [design](../design/README.md).

## Quality, capacity, security, and evidence

Pegasus is designed for the observed office workload of roughly 1,000–1,200 matters per month and a 2,000-per-month capacity target. These are observed workload and design capacity, not throughput proof.

Required qualities:

- deterministic, bounded, cancellable processing;
- least privilege and fail-closed authorization;
- encrypted transport and protected storage appropriate to the data boundary;
- no secrets in source, logs, proof artifacts, URLs, or client-rendered configuration;
- immutable source and action provenance;
- structured diagnostics without source-content leakage;
- reasoned recovery and replay without duplicate case/reference allocation;
- Windows-native local development and supported browser accessibility proof;
- independently buildable source workspaces with no application reference, dynamic load, dependency hoist, or deployment inclusion;
- explicit test/evidence scope and limits rather than evergreen counts.

## Permanent boundaries

The `Not planned` capability rows are boundaries, not backlog. They receive no activation issue or release target. They include permanently excluded or intentionally unsupported behaviors identified in the capability inventory. In particular:

- no case deletion or reference reuse;
- no silent principal/reference mutation;
- no dormant provider, OCR, AI, external-system, migration, or automation scaffolding;
- no workspace as a Pegasus runtime, deployment unit, or business-policy owner;
- no synthetic historical-case reconstruction;
- no local-alpha Outlook or Box mutation outside an exact separately approved target and operation;
- no model, skill, prompt, or external source issuing an accepted case, engineering, economic, legal, or report outcome;
- no broad production-data import from raw reference workbooks or evidence.

## Deferred capabilities and preserved seams

Deferred capabilities remain named in [capabilities](capabilities.md). Preserving a seam means retaining the stable identity/data/port needed to add the capability later without implementing dormant behavior.

| Deferred area | Preserved seam or data identity | Excluded until activation | Activation evidence |
| --- | --- | --- | --- |
| additional mailboxes and classification | mailbox/source/message/occurrence identity; provider/domain route identity | live Graph caller, automated taxonomy, mailbox mutation | accepted taxonomy/holdout, exact scopes, test mailbox, Worker caller, recovery and operator acceptance |
| scanned-document OCR | source hash, scan-like decision, page/image provenance | OCR service, flag, route, fallback | accepted OCR slice, provider/licensing/security decision, genuine cohort evaluation, caller and recovery proof |
| provider APIs | intake command, source/correlation/idempotency identity | endpoint, credentials, retry client, activation | provider contract, credential/scopes, failure/recovery, real caller and acceptance |
| EVA API/replacement | manual handoff identity and payload version | network adapter or replacement workflow | vendor access, mapping, auth, idempotency, current-version handling, caller and acceptance |
| guided capture and vehicle data | request/source/vehicle fact provenance | live vendor route, OCR lookup, auto-acceptance | vendor contract, confidence/human confirmation rule, data-age/source policy, failure/recovery and evaluation |
| automated correspondence/chasing | action, channel, party, draft and delivery-evidence identities | autonomous send or completion | allocation, approved channel policy, exact send scopes, pre-send approval and delivery proof |
| AI assistance | typed evidence/proposal/review identity | direct mutation, approval, business policy | accepted Core proposal port, representative evaluation, abstention/challenge gates, human approval and caller proof |
| Diminution, Commercial, post-report dispute and finance | stable case/work/document/action identities | dormant case types, calculations, invoicing/accounting routes | allocated release, accepted Core contract, source/provider decisions, UI/caller and operator acceptance |
| production deployment and migration | versioned schema/release/evidence identities | provisioning, deployment, predecessor deletion or data migration | exact target approval, validated IaC, migration/rollback plan, deployed caller proof and acceptance |

No irreversible choice is made merely to reserve a seam. New top-level projects, stores, runtimes, migration streams, or deployment units require an accepted ADR proving the existing boundary cannot carry the work.

## Acceptance model

A feature is accepted only when its owning requirement and capability are linked to:

- one Core policy/use-case owner;
- the actual Web, Worker, API, or MCP caller;
- infrastructure/persistence behavior where applicable;
- observable success, boundary, authorization, conflict, and recovery tests;
- current design/operations documentation;
- exact-head review;
- separately authorised live proof and operator/management acceptance where the feature depends on an external system or deployment.

Allocation, a file, registration, a green structural check, a source pull request, deployment, and operator acceptance are separate evidence states.