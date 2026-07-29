---
id: 2026-07-27-qdos-alpha-reference-corpora
type: feature
status: in_progress
risk: high
created: 2026-07-27
updated: 2026-07-29
issue: https://github.com/collisionengineers/pegasus/issues/3
pull_request: pending
baseline: b2f40a2b68b5b1a906ff2e736fa43653006dba61
target_release: 0.1.0-alpha.1
roadmap_horizon: Now
mode: development
supersedes: none
superseded_by: none
---

# Change: Deliver provider-aware email interpretation and QDOS alpha

## Outcome

Deliver the two coupled `Now` outcomes:

1. an immutable provider-domain evidence foundation that grows through cumulative snapshots; and
2. the first complete QDOS alpha through intake, acceptance, immutable identity, custody, work, review, EVA handoff, report evidence, lifecycle, recovery, deployment, and operator acceptance.

The provider-domain Step 2 implementation and local verification are complete. The complete offline application, accepted genuine route evidence supplied by the separate evaluator, live adapters, Azure reconciliation, deployment, operator acceptance, management acceptance, and release remain unproved. Code present, caller-proved, deployed, and accepted are distinct states.

Issue #3 remains open until both mandatory delivery stages complete:

1. **Offline development acceptance:** the complete application runs locally through real Web, Worker, Functions, SQL, storage, authentication, MCP, and operator callers without cloud credentials or external-service calls.
2. **Approved live integration and release:** live adapters, Azure changes, deployment, recovery proof, operator acceptance, management approval, and production cutover occur only after the offline gate and their own exact-target approvals.

Offline acceptance is neither a reduced alpha nor a substitute for live evidence.

Historical drafts that named Box File Request are superseded for product behavior by the Core-owned [request-scoped in-house upload-link contract](../requirements.md#request-scoped-upload-links). Box remains long-term custody; it is not the chaser upload-request mechanism.

## Authority and evidence boundaries

Current requirements, capability allocation, architecture, operations, design, decisions, and contributor workflow remain owned by:

- [documentation index](../index.md)
- [requirements](../requirements.md)
- [capabilities](../capabilities.md)
- [open decisions](../open-decisions.md)
- [architecture](../architecture.md)
- [operations](../operations.md)
- [engineering](../engineering.md)
- [operator notes](../operator-notes.md)
- [decision index](../decisions/README.md)
- [change-record index](README.md)
- [design index](../../design/README.md)
- [reference-data index](../reference/README.md)
- [Azure index](../azure/README.md)
- [workspace index](../../workspaces/README.md)

Operator truth outranks historical plans and predecessor behavior. Accepted decisions outrank historical architecture. Genuine corpora, the predecessor, source inspection, and local evaluations are evidence, not requirements or acceptance by themselves. Material unresolved choices remain blockers only for their named slices.

The four-project modular monolith remains: Core owns policy and ports; Infrastructure owns persistence and adapters; Web and Worker are composition roots. This change adds no project, top-level store, runtime, migration stream, deployment unit, generic rules engine, or second classifier.

### Checkpoint 1 activation

[ADR-0014](../decisions/ADR-0014-qdos-alpha-implementation-contract.md) activates the clause-specific QDOS implementation and Razor/Worker/MCP caller contract without changing accepted decision bodies or capability allocation. Checkpoint 1 starts from merged `main` at `46b0328b149d7da887fa899c8aa39e01fcf159dc`, whose parents are the pull request 18 documentation merge `536f5fc470a541281f86ebc711564d49432ed73f` and capability child/source head `f77e1492b25abdd5a14725f4c15129333482b743`.

Issue #3 and this record remain the sole implementation, evidence, review, and delivery identity. The addendum creates no second status ledger and promotes no implementation, caller, deployment, live-verification, or acceptance claim. The Development/local evaluator remains a separately delivered evidence harness under `DOC-CON-052`; it is not a QDOS caller or checkpoint. Repository-policy verification remains disabled until post-alpha; current direct and repository-language invocations are successful no-ops recorded only as **skipped/deferred**, never **passed**.

## Scope

### Included

- Publish the immutable cumulative `provider-domains-v1` snapshot from `docs/reference/workproviders-and-repairers/initial.xlsx`, preserving exact source and package provenance.
- Retain 11 stable provider codes and 16 provider/domain-suffix associations, comprising 16 distinct suffixes.
- Treat source columns A and E as the complete approved authoring contract:
  - column A is the provider code;
  - column E contains semicolon-separated email observations;
  - only the lowercase suffix after the final `@` is retained.
- Keep columns B–D and later columns opaque inside the immutable workbook and source hash. They have no authoring or runtime meaning.
- Publish additions only through new immutable cumulative workbook, package, and migration versions. Earlier versions remain queryable and are never updated or deleted.
- Keep provider-domain evidence distinct from direct-provider and intermediary route identities. A stored suffix is candidate evidence only; it neither activates a route nor resolves a provider by itself.
- Add explicit, stable, code-versioned Core policies for direct-provider and intermediary routes, mirrored by tests and accepted genuine-input evidence supplied by separately owned evaluation.
- Model an organization once and assign `WorkProvider`, `InstructionIntermediary`, or both roles. A route result separately records route owner, route kind, and resolved work provider; route owner and provider may be the same organization.
- Reconstruct a proved original sender for approved Collision Engineers staff-forward shapes while retaining the outer message as transport provenance.
- Activate only genuinely evidenced routes. The only currently accepted QDOS direct identity is the exact suffix `@qdosassist.co.uk`.
- Implement the sole live alpha mailbox caller for `instructions@collisionengineers.co.uk`.
- Complete QDOS Inspection, standalone Audit, and Inspection + Audit through durable intake, fail-closed acceptance, immutable identity, Box custody, work and review, EVA JSON/image handoff, exact report evidence, lifecycle, observability, recovery, deployment, and acceptance.
- Implement the selected Operations-first `0.1.0-alpha.1` staff UI, authenticated staff MCP, and all workflow, error, stale, denied, retry, and accessibility states required by the active flow.
- Support authenticated manual intake and bounded request-scoped unauthenticated uploads through the same Core use cases.

### Excluded or deferred

- Case creation or workflow activation for a non-QDOS provider without its own genuine corpus, executable policy evidence, approval, and activation decision.
- Generic rules engines, expression languages, database-authored predicates, admin rule editors, universal case-association ordering, empty policy scaffolds, placeholder policies, historical-frequency `always` rules, or a mailbox-specific second classifier.
- `Next`/`unallocated` all-mailbox management, folder moves, general correspondence matching, general email/image association, provider API activation, DOC/MSG extraction, scan-like PDF OCR, post-report query/dispute work, broader image/damage AI, and vision assistance.
- `Later`/`unallocated` Diminution, Commercial, automated WhatsApp, automated chasers, in-app assistance, conditional AI suggestions, direct EVA API or EVA replacement, estimating, valuation, invoices, report generation, automatic report sending, staff-selected AI Assessor, guided capture, Tractable/Ravin, and custom domain.
- `Not planned` external/customer accounts, malware scanning, SMS, Teams, portal, predecessor application reuse/import after cutover, separate QA/UAT/staging/demo/training environments, GitHub Actions/OIDC deployment, slots/S1, private networking, zone or multi-region resilience, and quarterly recovery exercises.
- Live-service client construction, Azure/IaC mutation, cloud reads or writes, deployment, predecessor retirement, or production cutover before complete offline acceptance and separate exact-target authorization.
- Dormant services, speculative projects, duplicate engines, or deferred features treated as current release gates.
- The Development/local email evaluator, `/Development/EmailEvaluation`, its `unchecked`/`checked` workspaces, reviewer workflow, evaluator command, report campaign, and UI/checkpoint acceptance mechanics. These are separately owned prerequisites under `DOC-CON-052`, not QDOS delivery.

Clean seams for deferred work are permitted; dormant implementations are not.

## Fixed decisions and invariants

1. **Tool-neutral governance.** Repository-native authority, proportional validation, exact-head review, exact-target cloud approval, and no-agent-merge remain mandatory. Azure Skills, MCP tools, Microsoft Learn, language servers, and other tools aid execution but provide neither repository authority nor authorization.
2. **Offline first.** Before offline acceptance, do not add or enable live Graph, Box, DVLA/DVSA, VRM-service, or Azure clients; modify Bicep or `azure.yaml`; inspect or mutate Azure; deploy; or touch the predecessor estate. Development uses explicit local implementations. Production must never silently fall back to a local adapter.
3. **One Core email owner.** Transport adapters normalize evidence only. One versioned Core policy owns route selection, provider/type/case evidence, received/sent classification, and approved report/Triage matching. The existing QDOS extractor remains an inner typed extractor, not a competing orchestrator.
4. **Independent route identities.** Direct-provider and intermediary policies are separate. A provider may be reached through both. An individual message matching both is ambiguous and fails closed.
5. **Evidence-gated Triage.** Triage is a pre-case business record, not a mailbox folder, category, case state, or fallback. No sender-only, subject-only, registration-only, or universal rule may create or complete it.
6. **One active inspection lifecycle.** Active case states are `Not ready`, `Review`, `Report preparation`, and `Post report`, with a reasoned `Held` overlay. Inspection work and report preparation are typed activities within `Report preparation`; `Inspection` is not a separate lifecycle state. Terminal states are `Post-report complete`, `Provider cancelled`, `Collision Engineers rejected`, and `Created in error`.
7. **Immutable case identity and external effects.** Intake is split into durable receive, process, resolve, and accept operations. Queue messages carry IDs only. SQL outbox operations have stable identities. Principal and reference allocate once in the acceptance transaction and are never reused or rewritten.
8. **The predecessor is stale evidence, not a target.** The 2026-07-23 Azure inventory is a dated snapshot. Owner direction says the predecessor is not in active use and needs no restart window, but that is not live telemetry proof. Any deletion requires refreshed exact-resource evidence and separate approval.
9. **No guessed evidence.** Missing genuine evidence blocks the corresponding policy, matcher, adapter, or release slice. It does not justify fabricated fixtures, placeholder rules, silent local fallback, or reduced release scope.
10. **Separate evaluator ownership (`DOC-CON-052`).** The Development/local email evaluation harness and review/report mechanics are delivered separately. QDOS alpha has no evaluator route, command, workspace UI, report campaign, or checkpoint acceptance owner. Accepted evidence from that delivery may satisfy genuine-evidence prerequisites, but the shared Core mail policy, production intake, Graph replay/live adapters, and their caller and activation proof remain QDOS scope. The inventory allocations for `OPS-22`, `EVAL-01` through `EVAL-05`, and `MAIL-20` remain unchanged pending an authorised replacement target and must not be read as QDOS implementation commitments.

## Provider-domain reference contract

### Source and package identity

| Property | Contract |
| --- | --- |
| Source | `docs/reference/workproviders-and-repairers/initial.xlsx` |
| Source SHA-256 | `e4bf89b0aeef3f1106bf34ed50f74dffc44c5ed748e0ad0811b66ee099b6cd29` |
| Worksheet | `Sheet1` |
| Source rows | 11 headerless rows |
| Provider source | Column A |
| Email observations | Semicolon-separated values in column E |
| Opaque source | Columns B–D and all later columns |
| Package identity | `provider-domains-v1` |
| Package path | `src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json` |
| Package encoding | Exact canonical UTF-8 bytes with a final newline |
| Package SHA-256 | `f6b5ad8ecdd428db4316b23e16aa7e0ffc93562aec33374c03ea68cd4f0370a3` |
| Package counts | 11 provider codes, 16 provider/suffix associations, 16 distinct suffixes |
| Runtime source | SQL imported through the existing EF migration stream; no runtime workbook reader |

The package may contain only provider codes, source-row provenance, normalized domain suffixes, and source/package identity. It must contain no email local part, full email address, Case ID, inspection location, default, or opaque source value.

### Validation and lookup

- Validation binds exact bytes, schema, version, source provenance, and SHA-256.
- Unknown JSON members, malformed identity, invalid suffixes, duplicate provider rows, duplicate per-provider suffixes, source/package mismatch, removal from a later cumulative package, or replacement of an existing different output fail closed.
- Core owns generic byte/hash/schema/version/source/provider/suffix validation, transient suffix extraction, candidate semantics, and the exact-version catalog port.
- The authoring pipeline owns the exact source path, worksheet, headerless A/E contract, suffix-only reduction, source lock checks, monotonic cumulative growth, staging, and atomic publication.
- Infrastructure owns immutable package/provider/evidence tables, embedded resources, migrations, and exact-version SQL lookup.
- Source-specific global count constants do not belong in Core.
- Lookup always supplies the exact schema/version/package-hash tuple. There is no implicit `current` or `latest`.
- Candidate provider codes are returned in ordinal order using one bounded query:
  - zero matches: `Unknown`;
  - one match: `Found`;
  - more than one match: `Ambiguous`;
  - invalid or mismatched package identity: fail closed with no candidates.

### Publication procedure

The source workbook must be closed. The wrapper rejects its exact sibling Office lock marker and an exclusive-read failure as `source-locked` before Python discovery, source hashing/parsing, staging, or output work.

```powershell
pwsh ./scripts/Build-ProviderReferenceData.ps1
pwsh ./scripts/Build-ProviderReferenceData.ps1 -Verify
```

The wrapper invokes the Python 3.11+ standard-library helper at `scripts/reference_data/build_provider_reference_data.py`. It uses `zipfile` and `xml.etree.ElementTree`, stages only beneath ignored `artifacts/reference-data-staging/`, and makes no network, cloud, or vendor call.

There is no recursive workbook discovery, pip dependency, authoring virtual environment, dependency lock, package cache, second manifest, or runtime workbook reader.

The first package is the only bootstrap without a previous package. A later version requires:

- a new immutable cumulative workbook;
- a new package version and output path;
- validation of the previous package;
- retention of every previous provider/suffix pair;
- a new explicit migration;
- continued queryability of every old snapshot.

Corrections or removals require new accepted authority and a new explicit contract. Published snapshots are irreversibly append-only.

## Route and intake acceptance

- Direct-provider and intermediary route identities are distinct from provider identity.
- Tests must prove that one provider can be resolved through its own direct policy and independently through an intermediary policy without sharing message-specific predicates.
- A direct route identifies its provider from the normalized source sender:
  - use the proved original sender for an approved Collision Engineers staff-forward shape;
  - otherwise use the direct/root sender.
- A direct route then uses extracted attachment, body, subject, and document evidence to determine instruction type and case association.
- An intermediary route first identifies the intermediary, then applies only that intermediary’s policy to determine provider, instruction type, and case association.
- Non-CE mail uses the root sender. A CE staff forward is authoritative only when an observed approved forwarded-message shape contains one consistent external sender. Zero, conflicting, malformed, or ambiguous forwarded senders produce `Needs sorting`.
- Arbitrary quoted `From:` text is never sender authority.
- The outer message remains transport provenance when an original sender is proved.
- An intermediary message is never evaluated as direct-provider mail.
- A source sender matching direct-provider and intermediary traits produces `multiple routes`, `Needs sorting`, and no case/reference.
- Case-association precedence belongs to the applicable route policy. There is no global ordering. A CE Case/PO is not preferred and may be used only as a route-approved lowest fallback.
- The first successful evaluation stores route kind, stable route-policy key/version, route owner, resolved provider, classification/case outcome, and evidence. Ordinary retries and replays reuse that revision.
- Explicit staff re-evaluation appends actor, reason, policy version, and a new revision. It cannot bypass route activation, QDOS-only activation, or standalone-Audit gates.
- Zero or multiple applicable routes, contradictory provider/type/case evidence, extraction incompleteness, unknown policy version, or dependency ambiguity remain visible before case creation and allocate nothing.
- Spreadsheet presence creates reference identity only. No route exists without genuine positive, negative, ambiguous, forward/intermediary, retry, and untouched holdout evidence plus explicit approval.
- Only the separately accepted QDOS direct trait `@qdosassist.co.uk` may support current route work. `@qdosassists.co.uk`, `@qdoslaw.co.uk`, and all other imported suffixes remain inactive evidence.
- Non-QDOS policies may be exercised by separately owned evaluation, but the alpha activation gate prevents them from creating a case or reference.
- Triage and automatic report predicates require their own genuine positive, negative, ambiguous, forwarded, reply-chain, and untouched holdout evidence.
- No mailbox destination, folder, category, provider name, sender alone, subject keyword alone, or historical frequency is business classification authority.

### Separately owned evaluation prerequisite

The Development/local email evaluator is not implemented or accepted by this
QDOS change. Its owner supplies any required reviewed genuine-input evidence;
QDOS defines no `/Development/EmailEvaluation` route, `unchecked`/`checked`
workspace workflow, evaluator command, reviewer report campaign, Administrator
approval, or evaluator UI acceptance checkpoint.

Route, Triage, and automatic-report policies still require genuine positive,
negative, ambiguous, forwarded, reply-chain, and untouched holdout evidence
before activation. Consuming accepted source-labelled results does not make the
separate harness or its review mechanics a QDOS caller. The shared Core policy,
production intake, local Graph replay contract, live Graph adapter, and their
real-caller evidence remain in this change.

The provider/intermediary route-disposition inventory remains outside Git. Only
aggregate counts, hashes, policy versions, and evidence limits may be committed.

## Durable intake and caller acceptance

- Refactor intake into `ReceiveIntake`, `ProcessIntake`, `ResolveIntake`, and `AcceptIntake`.
- Persist source identity, staging state, processing state, immutable evaluation revisions, current revision, manual blocking, retries, and SQL outbox work.
- Source identity is channel plus immutable external token. Same identity with a different content hash fails closed.
- Web or mailbox receipt stages bytes to run-scoped storage, then atomically commits receipt plus processing outbox.
- Worker dispatches receipt IDs only. Queue messages, logs, and telemetry contain no source bytes.
- The actual isolated queue trigger loads retained bytes and invokes the same Core process use case as manual intake.
- `instructions@collisionengineers.co.uk` is the sole live alpha mailbox caller.
- Worker never creates a case/reference directly; it calls Core acceptance.
- Only definitive, authorized QDOS instructions may create cases.
- Manual staff resolution and Worker processing share one acceptance path.
- Unreadable, encrypted, corrupt, oversized, unsupported DOC/MSG, bounded-out, incomplete, unknown-route, multiple-route, contradictory, non-QDOS, dependency-unavailable, or manually blocked inputs remain visible in `Needs sorting` or reasoned `Blocked intake` with no case/reference.
- DOC and MSG remain retained with provenance but automated extraction is deferred.
- PDF, DOCX, bounded nested EML/freehand instructions, and image-led intake are in scope.
- Embedded PDF text and images remain reviewable; targeted OCR for scan-like PDF pages remains deferred.
- Ordinary vehicle images require a separately selected and evidenced VRM mechanism; the implementation technology is not assumed.
- Outbox claims are renewable and operation keys are unique.
- Retry delays are five bounded attempts at 30 seconds, 2 minutes, 10 minutes, 30 minutes, and 2 hours, honoring a longer approved `Retry-After`. Exhaustion is terminal and visible.
- Duplicate delivery, identity/hash conflict, storage-success/SQL-failure, queue outage/replay, poison exhaustion, crash/restart, corrupt/oversized content, and one-durable-outcome behavior require caller-backed proof.

## Identity, authorization, leases, and history

- Use ASP.NET Core Identity and OpenIddict in the existing DbContext and migration stream.
- Roles are `Administrator`, `Engineer`, and `User`; there is no public registration, MFA, or external/customer account in this release.
- All active staff roles may perform case, intake, document, transition, and pre-Engineer review work.
- Administrators alone manage accounts, roles, principals, workflow configuration, approved mailboxes, and OAuth clients.
- Passwords use secure non-reversible hashes, require at least eight characters, and impose no digit, uppercase, lowercase, or non-alphanumeric composition requirement.
- Persistent account lockout is disabled.
- Login throttling uses:
  - a fixed-window partition of 10 attempts per trusted client IP per minute;
  - zero queue;
  - generic failure or `429` with `Retry-After`;
  - a separate global partition of 100 attempts per minute.
- Production accepts forwarded client IP only from configured trusted proxies.
- Browser sessions use a two-hour sliding idle cookie and an immutable original-issue claim enforcing an eight-hour absolute maximum.
- Security-stamp and account-enabled state are revalidated. Account disable or role change revokes later browser and MCP use.
- Use secure cookies, antiforgery, forced first-password change, and no password, token, or secret telemetry/history.
- `bootstrap-admin` is a one-shot command.
- `register-mcp-client` permits only pre-approved public S256 PKCE clients with exact callback, scopes, and resource. No client secret, wildcard callback, or Dynamic Client Registration is allowed.
- Claims map to an explicit Core `StaffActor`.
- Permanent `ActionHistory` records business mutations, downloads/exports, material denied or failed business actions, automated business results, and external information actually accepted, linked, or used. It stores structured before/after values, actor, time, required reason, and outcome without secrets or file/message bodies.
- Sign-ins remain security events. Routine views, searches, refreshes, polling, retries, leases, and heartbeats remain content-safe telemetry.
- Edit protection uses one five-minute hashed server lease, a 60-second heartbeat, displayed holder, and optimistic version. There is no Administrator override.
- Every save presents the lease token and current case version. An expired or stale editor cannot overwrite newer data.
- Routine lease and heartbeat activity does not enter permanent history.

## Case, reference, Triage, and lifecycle acceptance

### Case and reference identity

- One transaction allocates acceptance, case/intake link, immutable history, and custody/external outbox work.
- Use one atomic principal-lineage and Europe/London year counter shared across all QDOS types.
- Base reference is `{principalCode}{yy}{nnn}`.
- Standalone Audit displays `a.` or `ap.` only from retained Repairable or Total-loss evidence in the original Engineer report, never from Triage.
- Inspection + Audit starts with the inspection reference and adds the later Audit display reference in the same case and folder after Collision Engineers’ assigned Engineer records the applicable finding. It consumes no second sequence number.
- At sequence 999, allocate nothing.
- A principal code becomes immutable after first allocation.
- A legitimate successor is created in one Administrator transaction: close the predecessor for new work, share sequence lineage, preserve both identities, prohibit overlap, aliasing, in-place rename, or reference rewrite, and continue the predecessor’s next sequence in the cutover year. Later years begin at `001`.
- A wrong-principal allocation closes the original as `Created in error`, requires a reason, allocates a normal corrected replacement, links both, preserves both folders and references, and never reopens or reuses either identity.
- No case or reference is deleted or reused.

### Case creation and workflow

- Support Inspection, standalone Audit, and Inspection + Audit. Diminution and Commercial remain deferred.
- Definitive intake creates exactly one QDOS case idempotently through shared fail-closed acceptance.
- The accepted case enters `Review` only when instruction and image completeness both pass or staff explicitly confirm both; otherwise that same case enters `Not ready`. This follows the canonical [intake transition contract](../requirements.md#matching-conflicts-and-reversible-association) and does not add a universal manual-creation gate.
- Completeness and review gates are configurable backend gates and do not encode a hard-coded provider field matrix.
- Standalone Audit allocates nothing until the original Engineer report and a staff-confirmed Repairable/Total-loss assessment are retained.
- Inspection + Audit derives its Audit identity only after the later Engineer finding exists.
- Retain available provider, claimant, claim, vehicle, accident, instruction date, inspection address/mode, association, source, and provenance data as typed, editable, operator-reviewable values.
- Validate registrations, dates, mileage, and claim references without silent truncation or guessing.
- A real vehicle/repairer address or the exact value `Image Based Assessment` is valid.
- Current provider-domain data contains no inspection address, mode, location, or default. The future precedence seam is explicit accepted address/mode, then an independently accepted versioned provider default, then ambiguity. No current default may be inferred from `provider-domains-v1`.
- `Review` precedes Engineer assignment.
- `Report preparation` owns inspection and report activities; no current alias for an `Inspection` lifecycle state may remain.
- There is no pre-send report review gate.
- `Held` stores the previous state and remaining chase interval.
- Terminal commands are explicit. Cancellation and rejection require reasons.
- Reopen requires a reason and an otherwise valid nonterminal destination. It may not reopen directly into `Held` and may never reopen `Created in error`.
- Archive is reversible read-only state after closure, not deletion.

### Chasers and due work

- Extract the inspection date or equivalent instruction deadline as `Due by`.
- The first chase is scheduled at the same Europe/London local clock time seven calendar days after entering `Not ready`, then every seven calendar days while information remains missing.
- Entering `Held` preserves the prior state and remaining interval.
- Releasing to `Not ready` resumes the remaining interval; releasing to `Review` ends chasing.
- Material arrival or terminal closure ends future chasing.
- Generate clickable, copyable chaser text and an in-house request-scoped upload link created by authenticated staff.
- Preparing or copying text sends nothing. Recording a manual send is an actor assertion, not delivery evidence.
- Automated outbound chasers remain deferred.

### Triage

- Triage is a distinct inbox classification and a separate pre-case record; it is never a case state.
- An active Triage requires a normalized vehicle registration. Otherwise the source remains in `Needs sorting`.
- States are `Open`, `Awaiting information`, `Finding recorded`, `Completed`, and `Cancelled`.
- Findings are independently optional:
  - Roadworthiness: `Roadworthy` or `Unroadworthy`;
  - Assessment: `Repairable` or `Total loss`.
- At least one finding is required before `Finding recorded` or `Completed`. `Cancelled` is the only terminal state without a finding.
- Triage findings are reference-only and have no bearing on Case/PO/reference, case workflow, final outcome, Engineer report, Audit suffix/allocation, or any other case decision.
- Triage has an optional assignee, no due date, and no chasers.
- Completion requires one immutable exact reply-chain Outlook Sent item from an approved mailbox. There is no subject, registration, arbitrary message, or manual-message fallback.
- Before send, finding correction or replacement requires a reason.
- After send, correction stores a superseding finding, requires a new response, and preserves full history.
- Reopen always returns to `Open`.
- Each Triage remains separate if linked to a later case.
- Automatic linking requires separately accepted definitive shared-match evidence; otherwise staff confirm.
- A Triage links to at most one case; a case may link multiple Triage records.
- Any staff role may unlink or relink with a reason. The link remains reference-only.

### Report evidence and terminal history

- Report evidence is one immutable exact Sent item from an approved mailbox with Outlook `sentDateTime` as authoritative.
- Discovery and linking times remain separate.
- Evidence may be selected by an accepted automatic matcher or by a reasoned staff link.
- It moves the case to `Post report`, but does not prove recipient receipt or close the case automatically.
- Any staff role may unlink or relink with a reason; dependent events and counts are recomputed through Core.
- Confirmed evidence remains final after an Outlook move or deletion.
- Revisions and unlinks preserve immutable history.
- Automatic report sending remains deferred.

## Custody and external-boundary acceptance

### Documents and Box

- Original inbound emails, instructions, attachments, images, correspondence, and reports require case-file custody; hashes and extracted metadata alone are insufficient.
- Maintain a QDOS case folder named from Case/PO.
- Preserve stable folder, file, version, ancestry, hash, etag, semantic-role, and operation identities.
- Prior versions are retained. Writes create versions; removal is logical and reasoned.
- Closed cases are application-read-only until a reasoned reopen.
- Staff may add relevant material received through manual WhatsApp coexistence without adding a WhatsApp integration.
- In-house upload requests are temporary, revocable, request-scoped, and bounded to the approved request/case relationship; Box receives accepted custody material only through the normal custody boundary.
- Every custody operation proves the approved root and descendant relationship before access.
- Box failures block progression but never roll back an allocated identity.

### Request-scoped unauthenticated upload

Authenticated staff may create temporary, revocable, request-scoped links for clients, bodyshops, or storage yards.

The unauthenticated caller may see only the bounded upload form and immediate result, never case/request state or upload history. Release remains blocked until token identity, limits, custody, idempotent retry, revocation, abuse handling, expiry, authenticated creator attribution, and cross-request isolation are implemented and proved through a real caller.

### VRM and vehicle data

- Benchmark candidate VRM engines against approved genuine ordinary vehicle photographs.
- Selection requires exact-read, false-positive, uncertainty, latency, licence, security, and operator evidence.
- No engine is registered before acceptance; generated images are not evidence.
- Persist suggestion provenance. Staff acceptance creates provisional identity.
- DVLA/DVSA behavior requires an accepted provider/API, licence, fields, credentials, rate/error behavior, target, and mileage rule.
- Vehicle and MOT results are suggestions and never overwrite confirmed values.
- Missing local replay evidence returns `Unavailable`; it never invents a success.
- No valuation behavior is included.

### EVA

- Approve a focused `0.1.0-alpha.1` mapping, readiness contract, image order, names, and recovery behavior against genuine cases.
- Generate deterministic JSON, images, and a SHA-256 manifest.
- First successful generation records `First sent to Engineer` exactly once.
- Regeneration does not duplicate that event and does not claim EVA receipt or named Engineer assignment.
- EVA remains authoritative for Engineer assignment, estimating, valuation, and report generation until an accepted replacement exists.
- The source-labelled saved-email/image/valuation/instruction readiness set, Experian boundary, and image eligibility/order/duplication/video-screenshot observations remain pinned through the canonical [focused handoff contract](../requirements.md#focused-eva-manual-handoff) and its retained administration-overview evidence; their presence does not prove an EVA caller or acceptance.

## Operations-first Web, MCP, and UI acceptance

### Web

Required routes are:

- `/` for Operations;
- `/Intake` and `/Intake/{id}`, including authenticated upload;
- `/Triage` and `/Triage/{id}`;
- `/Cases` and `/Cases/{id}`;
- Administrator account, principal, configuration, and mailbox pages;
- sign-in, forced password change, sign-out, and access denied;
- OAuth metadata and authorization endpoints;
- `/mcp`;

The shell uses the approved adapted Collision Engineers logo at `design/brand/logos/logo_no_margin.png`; the scaffold `CE` CSS logo, Privacy page, and scaffold navigation are removed.

Header order is:

`Operations | Intake | Triage | Cases | Administration | Search | User`

Operations presents exact, clickable filtered queues for:

- `Not ready`;
- `Review`;
- `Held`;
- `Needs sorting`;
- `Blocked intake`;
- Triage;
- `Due today`;
- `In today`;
- `Sent to Engineer` today and week;
- `Reports sent` today and week.

Days use Europe/London midnight boundaries; weeks are Monday-to-Monday. `In today` counts created cases. `Sent to Engineer` counts the first successful EVA bundle generation as an explicit proxy that does not prove EVA receipt. `Reports sent` counts every successfully sent report.

Each count exposes last-updated time and distinct zero, loading, stale, partial, unavailable, and failure states. `Receiving work`, `Queries`, `Other`, saved views, bulk actions, calendars, mobile design, and `Next`/`unallocated` mailbox management remain excluded.

Search and filtering cover Case/PO, registration, claimant, claim number, principal, state/status, Engineer, received/instruction dates, date range, and image- versus instruction-led origin. Linking or merging never changes the original intake origin.

Case pages expose typed data and provenance, parties, documents/images, vehicle/MOT suggestions, address/mode, tasks, chasers, request-scoped upload links, EVA export, report evidence, `Report preparation` work, lifecycle/history, immutable identity, lease/conflict/retry, and reasoned actions. There is no permanent case or file deletion.

### MCP

- Use OpenIddict authorization code flow with S256 PKCE, exact audience/resource, short access tokens, rotating refresh tokens, protected-resource metadata, and one Streamable HTTP `/mcp` endpoint.
- Local development uses explicit local keys; production refuses them.
- Tools invoke only Core-owned case, intake, Triage, document, EVA, and report actions also available through Web.
- Exclude accounts, roles, principals, configuration, OAuth client administration, cloud operations, arbitrary custody IDs, generic email, credential management, and deletion.
- Prove browser/MCP parity, real HTTP caller behavior, staff attribution, stale/lease handling, and immediate account-disable/role-change enforcement.
- Direct service invocation does not count as MCP caller proof.
- Claude-hosted callback behavior and production key custody remain live gates.

### Accessibility and interaction

The UI must distinguish unknown, conflicting, retrying, stale, denied, empty, loading, partial, and dependency-unavailable states and provide a valid recovery path.

Acceptance covers:

- keyboard-only use and visible focus;
- semantic controls and errors;
- forced colours;
- reduced motion;
- constrained desktop;
- 1024px and 200% zoom;
- 1280px and wider desktop;
- multi-session lease/conflict behavior;
- role boundaries.

Internal parser and policy names are not operator language. The selected adapted CE colour, type, geometry, icon, and logo rules remain; upstream marketing, mobile, imagery, document, and animation excess is not reintroduced.

## Capability evidence index

This index accounts for all 128 `Now` capability IDs after explicit deferral of `DATA-02`. It assigns QDOS implementation/evidence ownership except where `DOC-CON-052` marks a separately owned evaluator prerequisite; it does not claim pending evidence has passed or change canonical allocation.

| Capability IDs | Delivery steps | Required evidence |
| --- | --- | --- |
| `OPS-10` | 12, 13 | approved isolated Azure Development deployment and direct-terminal release evidence |
| `OPS-22` | separate delivery | separately owned genuine-input harness evidence; no QDOS route, caller, report campaign, or acceptance checkpoint |
| `OPS-01`, `OPS-02`, `OPS-03`, `OPS-04`, `OPS-05`, `OPS-06`, `OPS-07`, `OPS-08`, `OPS-09`, `OPS-11`, `OPS-13`, `OPS-14`, `OPS-20`, `OPS-24` | 1, 5, 8, 10, 12, 13 | offline platform/caller/concurrency proof followed by approved Azure, resilience, capacity, deployment, and recovery proof |
| `OPS-23`, `OPS-25` | 13 | operator journey and Collision Engineers management release approval |
| `EVAL-01`, `EVAL-02`, `EVAL-03`, `EVAL-04`, `EVAL-05` | separate delivery | separately owned reviewer workflow and evidence; no QDOS implementation or acceptance checkpoint |
| `MAIL-20` | separate delivery | separately owned local evaluator caller; no QDOS implementation or acceptance checkpoint |
| `MAIL-21`, `MAIL-22` | 4, 10, 11 | shared Core taxonomy/route evidence and production-intake caller proof, then approved Graph replay/live parity |
| `MAIL-14`, `MAIL-15`, `MAIL-16` | 6–8, 10, 11 | exact local Sent evidence/linking and approved automatic matcher holdout, then Graph parity |
| `MAIL-18` | 6, 9, 10 | Core chaser policy and authenticated copyable Web output |
| `ACC-01`, `ACC-02`, `ACC-03`, `ACC-04`, `ACC-05`, `ACC-06`, `ACC-07`, `ACC-08`, `ACC-09`, `ACC-10`, `ACC-11` | 3, 9, 10 | Identity/OpenIddict, authorization, history, authenticated browser/MCP |
| `INT-01`, `INT-02`, `INT-03`, `INT-08`, `INT-09`, `INT-10`, `INT-11`, `INT-12`, `INT-13`, `INT-17`, `INT-18`, `INT-19`, `INT-20`, `INT-21`, `INT-22`, `INT-23`, `INT-24`, `INT-25`, `INT-26`, `INT-27`, `INT-29`, `INT-30` | 4–10 | shared Core classification/extraction, separately supplied accepted evidence where required, durable receipt/outbox/Worker, acceptance and negative recovery smoke |
| `INT-31` | 7, 9, 10 | request-scoped upload token/limit/custody/retry/revocation/abuse contract, real authenticated staff creator, bounded unauthenticated upload caller, negative isolation proof, and operator acceptance; release remains blocked until implemented |
| `TRI-01`, `TRI-02`, `TRI-03`, `TRI-04`, `TRI-05`, `TRI-06`, `TRI-07`, `TRI-08`, `TRI-09` | 4, 6, 8–10 | approved matcher evidence, Core transitions, Worker Sent evidence, UI/MCP |
| `CASE-01`, `CASE-02`, `CASE-03`, `CASE-04`, `CASE-07`, `CASE-08`, `CASE-09`, `CASE-10`, `CASE-11`, `CASE-12`, `CASE-13`, `CASE-14`, `CASE-15`, `CASE-16`, `CASE-17`, `CASE-18`, `CASE-19`, `CASE-20`, `CASE-21`, `CASE-24`, `CASE-25`, `CASE-26`, `CASE-27`, `CASE-28`, `CASE-29`, `CASE-30` | 6–10 | Core/persistence contract, local adapters, Worker, UI/MCP, lifecycle smoke |
| `UI-01`, `UI-02`, `UI-03`, `UI-04`, `UI-05`, `UI-06`, `UI-07`, `UI-08`, `UI-09`, `UI-11`, `UI-13` | 9, 10 | authenticated Razor Pages caller and Playwright/accessibility acceptance |
| `DOC-01`, `DOC-02`, `DOC-03`, `DOC-04`, `DOC-05`, `DOC-06`, `DOC-07`, `DOC-08` | 6, 7, 9–11 | Core custody contract, local adapter/UI smoke, then Box parity/live proof |
| `EXT-01`, `EXT-02`, `EXT-03`, `EXT-14`, `EXT-18` | 7, 10, 11 | local replay/export contract and operator smoke, then approved live parity |
| `MCP-01`, `MCP-02`, `MCP-03`, `MCP-04` | 3, 9, 10, 13 | OpenIddict actor enforcement and real Streamable HTTP caller |
| `DATA-01` | 2, 10 | deterministic cumulative provider-domain package/migration and exact count/hash/suffix-only proof |

**Count assertion: 128 distinct allocated IDs; no duplicate or omission. `DOC-CON-052` separately owns seven of those IDs and creates no QDOS delivery step.**

## Dependency-ordered delivery sequence

### 0. Activate delivery and tool-neutral governance

- Keep issue #3 and this single change record as delivery owners; do not create another status ledger.
- Preserve repository-native authority, exact-head review, proportional validation, exact-target cloud approval, and no-agent-merge while removing active plugin-specific routing and ownership claims.
- Historical onboarding and decision material remains historical rather than being rewritten.
- Active guidance must reject reintroduction of plugin route tokens while permitting quoted historical evidence.
- Reconfirm the 128-capability allocation and the `DOC-CON-052` non-QDOS evaluator boundary without cloud or vendor access.

Implementation was approved on 2026-07-27. Obsolete workflow-specific repository, doctor, and documentation wrappers were removed by direct owner instruction; current completed verification uses owning executables. The tool-neutral local entry points below remain delivery requirements unless explicitly superseded by another owner decision.

### 1. Establish the reproducible offline platform

Required default tooling:

| Dependency | Contract |
| --- | --- |
| Windows / PowerShell | Windows 11; PowerShell `7.6.3`; commands remain PowerShell-first |
| Git | repository/path guards and exact working-copy checks |
| .NET SDK | `global.json` pin `10.0.302`; restore/build all four projects |
| Node/npm | Node 24 and npm 11; `npm ci` uses repository pins |
| Python | `3.11+`; standard library only for provider authoring; no Python application runtime |
| Azurite | npm package `3.36.0`; run-scoped loopback Blob and Queue state |
| Functions Core Tools | v4, pinned/checked at `4.12.1`; actual isolated Worker host |
| SQL | SQL Server Express LocalDB using committed production migrations |
| SqlServer module | `22.4.5.1`, CurrentUser |
| HTTPS | trusted `dotnet dev-certs` certificate |
| Browser evidence | pinned Microsoft Playwright for .NET browsers and repository accessibility dependency |

The future `Cloud` profile adds Azure CLI `2.88`, Azure Developer CLI `1.28.0`, Bicep `0.45.15`, GitHub CLI `2.88`, Infisical `0.43.104`, Box CLI `4.9.2`, SqlServer `22.4.5.1`, and ExchangeOnlineManagement `3.10.0`, all at the approved scope. The application uses the .NET Graph SDK; `Microsoft.Graph.Authentication` is not a required PowerShell module.

Azure CLI, `azd`, Bicep, GitHub CLI, Infisical, Box CLI, cloud login, live credentials, and Docker are not offline prerequisites. Initial package/browser restore may use package feeds; normal local start and smoke must not contact cloud or vendor services.

Required entry points:

```powershell
pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline
pwsh ./scripts/Initialize-LocalDevelopment.ps1
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Status
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Smoke
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Reset
```

Required behavior:

- `Invoke-Doctor -Profile Offline` checks only the local tool contract and prints exact install/repair commands.
- `-Profile Cloud` adds pinned cloud/vendor checks but neither logs in nor mutates an external service.
- `Initialize-LocalDevelopment` runs `npm ci`, installs pinned Playwright browsers, verifies/trusts Development HTTPS, validates LocalDB/Functions/Azurite, and creates ignored local state only. It does not silently install global/system packages or retrieve secrets.
- `Invoke-LocalDevelopment` allocates a run ID, loopback ports, GUID-named LocalDB database, Azurite state, local mailbox, local case-file root, logs, and ownership manifest under ignored `artifacts/local-development/<run-id>/`.
- Dependencies start in order and wait for real readiness. A Development-only explicit command applies migrations; Web and Worker never migrate on normal startup.
- First start securely prompts for an Administrator password of at least eight characters, passes it only as a child-process environment value to the one-shot bootstrap command, does not echo or persist it, and forces change at first sign-in. Later starts refuse bootstrap if an Administrator exists.
- `Stop` and `Reset` affect only resources whose run ID and ownership manifest match.
- `Reset` refuses ambiguous database, process, or path ownership and never touches tracked files, corpus, another run, Azure, or predecessor resources.
- Failures retain content-safe diagnostics.
- `DevelopmentOffline` registrations use loopback-only endpoints and fail startup outside Development. Production registration fails closed when required configuration is absent and never substitutes local storage, fixtures, keys, mailbox, or endpoints.

### 2. Publish the cumulative provider-domain snapshot

Execute the source-lock, authoring, canonical-package, migration, exact-version lookup, repeat-byte, append-only, and suffix-only contract defined above.

Application runtime reads SQL only. Provider-domain evidence does not activate a route. `DATA-02` remains deferred.

### 3. Add staff identity, authorization, leases, and history

Implement the Identity/OpenIddict, roles, session limits, login throttles, bootstrap, OAuth-client registration, `StaffActor`, account revocation, business history, edit lease, and optimistic-version contracts above in the existing DbContext and migration stream.

### 4. Build the shared classification policy and consume genuine evidence

- Extend neutral evidence to transport sender, normalized source sender, proved forwarded sender, recipients, subject, body, document/attachment fragments, occurrence labels, reply/thread identifiers, completeness, and source references.
- Update the existing Infrastructure MIME reader to expose nested-forward sender evidence without losing provenance or relaxing limits.
- Replace the one-policy assumption with a stable direct/intermediary policy catalog.
- Persist route kind, route owner, stable key/version, provider, classification/case outcome, and evidence on versioned evaluations.
- Remove the superseded selector path after all production callers move; retain no compatibility path in development mode.
- Consume accepted genuine route, Triage, and report cohorts and untouched holdouts supplied by the separately owned evaluator. Do not build its route, command, workspace review flow, report campaign, or UI acceptance checkpoint here.
- Do not infer routes from provider names or generate operational email fixtures.

### 5. Make intake durable and idempotent

Implement staged receipt, immutable revisions, manual resolution, SQL outbox, ID-only queue messages, actual queue-trigger processing, bounded retries, and all negative/recovery paths. Prove LocalDB transaction, contention, duplicate, replay, crash, and poison behavior rather than relying only on SQLite tests.

### 6. Implement case, reference, Triage, work, and lifecycle policy

Add principal/successor and sequence-lineage models, typed case/provenance, parties, intake links, documents/versions, tasks/chasers, completeness, lifecycle, Triage/findings, external evidence, outbox attempts, and immutable action history.

Implement every allocation, Audit, lifecycle, Held/chaser, report, Triage, replacement, reopen, archive, and concurrency invariant above. SQL constraints cover source acceptance, sequence output, external identities, operation keys, first-Sent-to-Engineer, report/Triage evidence, and replacement relations.

### 7. Complete each external boundary locally

| Production boundary | Offline implementation | Limit that remains unproved |
| --- | --- | --- |
| Azure SQL | GUID-named LocalDB database using the production migration stream | Entra, Azure throttling, PITR, and managed identity |
| Blob/Queue/Functions | Azure SDK against run-scoped Azurite plus the actual Functions host | Azure RBAC, scale, durability, and platform poison behavior |
| Outlook/Graph | durable local mailbox with genuine working-copy `.eml`, immutable IDs, Inbox/Sent folders, delta cursor, `sentDateTime`, and reply metadata | Graph permission, delta, throttling, and mailbox-policy behavior |
| Box | guarded local case-file store through the same Core custody port, including root/descendant proof, identities, hashes, versions, and logical removal | Box identity, scopes, SDK, retention, and recipient delivery |
| In-house upload link | isolated loopback upload/result route using temporary, revocable, request-scoped tokens and the normal custody port | public-edge abuse controls, recipient delivery, and authorised live acceptance |
| DVLA/DVSA | Development replay adapter accepting only owner-approved ignored responses and typed failures | live contract, licence, limits, mileage rule, and availability |
| VRM | selected local engine after a genuine labelled benchmark | no engine is registered until accepted |
| EVA | deterministic local JSON/image/manifest bundle | EVA import, receipt, or named assignment |
| OAuth/MCP | local HTTPS OpenIddict, registered public PKCE client, and actual Streamable HTTP calls | Claude-hosted callback and production key custody |
| Telemetry | structured console/test exporter and optional local OTLP collector | Azure Monitor ingestion, alerts, retention, and cost |

Run one Core port contract suite against every local implementation. A future live adapter must pass the same suite; a contract change returns the work through the offline gate.

### 8. Add real Worker callers

In the existing Worker, add:

- local-mail Inbox poll timer;
- SQL outbox dispatch timer;
- `intake-work` queue trigger;
- external-work recovery;
- due-work sweep;
- Sent-evidence poll.

Each trigger claims persisted work, invokes one Core use case, and acknowledges only after a durable outcome. Use a SQL lease/cursor for one mailbox poller. Align Functions poison handling with the five application attempts. No trigger creates a case/reference, changes lifecycle, carries bytes, or calls a vendor outside Core.

`DevelopmentOffline` Worker startup must prove that no external client was constructed.

### 9. Complete the authenticated Web and local MCP

Build the Operations-first routes, queues, intake, Triage, case, document, administration, authentication, OAuth, MCP, error, stale, retry, lease, and accessibility behavior above.

PageModels bind, authorize, and translate only. Business decisions remain in Core. Prove browser/MCP parity through real HTTP endpoints.

### 10. Pass the complete offline acceptance gate

Run from a fresh setup:

1. Offline doctor, initialization, start/status/smoke/stop/reset, a second parallel run, induced startup failure, recovery, and proof that no cloud credential, login, client, hostname, or stale Azure resource was used.
2. Provider generation from the pinned source: exact hash and A/E contract, 11 providers, 16 associations, suffix-only bytes, repeat-byte equality, exact package/migration equality, fresh SQLite and LocalDB migration idempotency, tuple/suffix fail-closed behavior, and monotonic synthetic later-version growth.
3. Automated Core-policy proof against accepted genuine route evidence for every activated route, including QDOS, direct/intermediary collision, malformed forwards, Triage positives/negatives/ambiguities/replies, report predicates, determinism, and source/corpus immutability. The separately owned evaluator supplies reviewed cohorts and holdouts; this gate creates no QDOS evaluator UI, report campaign, or acceptance checkpoint.
4. Actual Web, Functions host, Azurite, LocalDB, local mailbox, and local custody smoke for QDOS Inspection, standalone Audit repairable/total loss, and Inspection + Audit. Duplicate, retry, and crash must still produce one immutable case/reference/evaluation/custody/outbox result.
5. Negative and recovery smoke for unsupported, corrupt, oversized, incomplete, unknown/non-QDOS, route overlap, missing Audit assessment, unavailable dependency, identity/hash conflict, sequence 999, poison exhaustion, stale lease/version, unauthorized actor, and terminal external failure.
6. Full local lifecycle through `Not ready`, chasing, `Held`, `Review`, `Report preparation`, custody, selected VRM/address/vehicle suggestions, one-time EVA event, exact local Sent evidence, `Post report`, terminal outcomes, valid reopen, created-in-error replacement, archive/read-only, and Triage exact-reply completion/correction.
7. Real HTTP Identity and OAuth/MCP:
   - reject a seven-character password;
   - accept an eight-character composition-free password;
   - repeated failures never persist lockout;
   - per-IP and global partitions return generic `429` and `Retry-After`;
   - clock-test two-hour idle and eight-hour absolute expiry;
   - reject disabled or role-changed users;
   - complete actual MCP calls with the public PKCE client.
8. Playwright interaction evidence for Operations, intake, Triage, case, administration, authentication, and MCP-visible effects at 1280+, constrained desktop, 200% zoom, keyboard-only, focus/error handling, forced colours, reduced motion, and multi-session conflicts.
9. Active repository checks that perform validation, exact-head CI, independent implementation review, clean-operator runbook execution, and proof that every QDOS-owned locally exercisable `Now` capability has real caller evidence. `scripts/Test-RepositoryPolicy.ps1` and its repository-language caller are excluded as described below. The seven `DOC-CON-052` evaluator allocations are separately owned prerequisites and have no QDOS checkpoint.

Repository-policy enforcement is temporarily disabled and deferred until after
`0.1.0-alpha.1`. `scripts/Test-RepositoryPolicy.ps1`, whether invoked directly
or through `scripts/Test-RepositoryLanguage.ps1`, is a successful no-op. Record
that outcome as **skipped/deferred**, not **passed**: it proves no repository-
policy property, cannot be cited as green evidence, and is not an alpha-required
gate. Post-alpha activation requires a reviewed re-enable change, reproducible
proof inputs, a clean-checkout pass, and independent review.

Do not tag, release, deploy, or call the alpha accepted when this gate passes. Every live-only capability remains pending live evidence.

### 11. Acquire approvals and add live adapters

No live client is constructed before exact evidence, target, scope, and approval exist.

#### Graph and Exchange

- Cloud doctor must find ExchangeOnlineManagement `3.10.0`.
- Register the app service-principal pointer in Exchange Online and assign only scoped `Application Mail.Read` through Application RBAC for `instructions@collisionengineers.co.uk`.
- Do not also grant unscoped Entra `Mail.Read` application permission; authorization sources are additive.
- `Test-ServicePrincipalAuthorization` must prove the approved mailbox in scope and one approved control mailbox out of scope after propagation.
- Exchange scope is mailbox-level; the .NET adapter additionally enforces Inbox ingestion and Sent-items evidence allowlists.
- Permit MIME and attachment reads only: no move, delete, mark, category, or send.
- Use immutable IDs, durable delta cursor, and bounded throttle/retry behavior.
- Pass the local mailbox contract suite plus approved live permitted/denied mailbox and folder fixtures.

The approval-gated preflight procedure must connect as the approved operator and perform the positive and negative authorization tests without widening scope.

#### Box

Require the exact enterprise, user, root descriptor, identity, and operations. Guard root and descendant scope before every SDK call. Persist remote identities, hashes, and versions. Allow no destructive delete or arbitrary ID. Pass local contract parity plus one approved permitted and one denied live fixture.

#### DVLA/DVSA, VRM, and secrets

- Require the accepted provider/API, licence, fields, credentials, limits, errors, target, and mileage rule.
- Activate only the already selected VRM engine. External image egress requires separate security and cost approval and rerunning the same cohort/holdout.
- Store third-party secrets only in the approved secret boundary. Never expose them in source, local settings, deployment output, prompts, telemetry, or business history.
- Enable one live dependency at a time in an approved Development deployment. Failure must not route to local adapters or silently become success.

### 12. Reconcile Azure, retire the predecessor, and deploy an isolated target

- Obtain exact subscription/resource-group read approval, then refresh inventory by resource ID.
- Classify every resource and dependency as predecessor-only, shared, data-bearing/undecided, or Pegasus/current-target. Names and tags are not ownership proof.
- Open one separate linked teardown change for destructive execution; do not create a second repository status ledger.
- Record a reviewed exact-resource manifest, never a wildcard or computed deletion list.
- Establish retained traffic evidence for at least 30 days where telemetry exists across public endpoints, Functions, queues, schedules, DNS, and downstream callers.
- Check locks, policies, backups, managed-resource ownership, role assignments, and dependencies.
- Missing telemetry is an explicit risk, not inferred zero use.
- Record rebuild provenance before deletion: source/IaC/package location and revision, deployment history/template, retrievable package hashes, non-secret configuration names, domains/certificates, identity/RBAC shape, and required secret names/issuers.
- Recovery is a fresh deployment from recorded provenance. There is no restart/cooldown promise and no commitment to restore predecessor application state.
- Assign every data-bearing or potentially shared asset an explicit `delete`, `retain in place`, or `move/replace then delete` disposition.
- Starting Pegasus fresh permits deletion of approved predecessor PostgreSQL case/queue test state but does not silently authorize deletion of capture/evidence storage, Foundry, shared ACR/ValuationBot images, default workspace, Visual Studio accounts, or any undecided asset.
- After exact write approval, fence ingress and schedules, stop callers, disposition queued work, revoke credentials and role assignments, and delete dependency-ordered leaf batches:
  1. event subscriptions, webhooks, and callers;
  2. app endpoints, compute, and plans;
  3. approved data stores;
  4. app monitoring and alerts;
  5. private/network attachments;
  6. identities and residual role assignments.
- Verify absence and business/platform health after each batch.
- Delete managed children only through their owning service.
- Delete a resource group only as a final separately approved action after proving no retained, shared, or undecided resource remains. Never start with resource-group deletion.
- Recheck Resource Graph, role assignments, DNS/endpoints, Key Vault secret names and expiry, identities/service principals, schedules/events, orphan network/storage/monitoring resources, and cost views.
- Record deleted IDs, retained IDs with owners, failures/retries, irrecoverable state, and rebuild procedure in the teardown change.

For the isolated Pegasus target:

- update the deployment plan, existing Bicep, parameters, and `azure.yaml` only after refreshed inventory and teardown disposition;
- require Bicep what-if to show no mutation of retained predecessor/shared assets or unapproved resources;
- remove Document Intelligence and its roles/configuration from the new output;
- configure Azure SQL Entra-only with distinct deployment, migrator, Web, and Worker identities;
- grant no DDL to Web or Worker, no standing app-data role to deployment, only `intake-temporary` Blob access to Web, and only justified host/business-storage roles to Worker;
- build immutable Web, Worker, and Linux-x64 migration bundles once;
- create a machine-readable release manifest with source revision, package/tool provenance, paths, and SHA-256;
- deploy those exact bytes without rebuild;
- have an authorized migrator apply schema before application packages;
- run policy/quota checks, Bicep validation/what-if, health and dependency probes, scope-denial smokes, alerts, restore, and compatible package rollback;
- prove Azure SQL 15-minute RPO and four-hour RTO in an approved temporary target.

Production remains a separate exact-target approval.

### 13. Complete live acceptance and release

- Run approved live Development smokes.
- Have Alex and relevant staff perform the genuine QDOS operator journey.
- Obtain Collision Engineers management approval.
- Record separately what local evidence, live-adapter evidence, deployment evidence, operator evidence, and management approval prove and do not prove.
- Run full canonical checks, green exact-head CI, and independent exact-head implementation review with no unresolved blocker or required finding.
- Only then:
  - mark this change accepted;
  - close issue #3;
  - tag `0.1.0-alpha.1`;
  - perform production migration, deployment, and cutover under separate exact-target approval.

The agent workflow never merges or self-certifies its own head.

## Data, failure, and recovery

### Data ownership

- Provider-domain reference data is database-owned and immutable by package version.
- Activation remains separate from reference presence.
- Sender traits, intermediary identities, route predicates, precedence, and policy activation remain code-owned stable keys and predicates rather than database-authored rules or mapping tables.
- A direct route resolves one provider.
- An intermediary may resolve multiple providers.
- A provider may be reached through multiple route policies.
- Persist route kind, route owner, policy key/version, provider, classification/case outcome, and supporting evidence on immutable evaluation revisions.
- Provider/location/reference and later case/workflow schema remain in the existing Infrastructure migration stream.
- Provider codes become immutable after first case use.

### Failure behavior

All source rows must be accounted for by imported, special, unmapped, or review-needed counts. Hash or count mismatch aborts preparation and release; it never partially guesses.

Before allocation, unreadable or incomplete extraction, unknown or competing route, uncertain provider/type/case, policy mismatch, custody failure, dependency ambiguity, or unsupported behavior remains visible and allocates nothing.

After allocation, idempotent retries preserve principal, reference, policy version, evaluation identity, and external operation identities.

No external failure may silently downgrade to success or local fallback.

### Recovery

- Correct reference generation or policy code forward and rerun verification.
- Roll back the application to a prior immutable package only when schema compatibility permits.
- Schema recovery is a tested forward fix or database restore, never an automatic down-migration.
- Preserve source, case, reference, route revision, external attempts, and action history.
- Never delete or reuse a case or reference.

## Current source and caller evidence

At the source/caller baseline inspected on 2026-07-26, the real product caller was Development-only `POST /Intake/Upload`; the Worker was telemetry-only and had no trigger. That source inspection was not fresh deployment or operator evidence.

The existing local thin slice can, under its explicit Development feature boundary:

- accept manually selected `.eml`, `.pdf`, `.docx`, `.doc`, `.msg`, `.jpg`, `.jpeg`, or `.png` files up to 10 MB;
- read email bodies, bounded nested EML, every PDF page and discrete PDF image through MimeKit/PdfPig, and DOCX text/internal images through Open XML;
- apply one aggregate per-intake expansion budget, with PDF processing all-pages-or-incomplete rather than silently page-truncated;
- retain each local source, attachment, inline image, DOCX image, and discrete PDF image as a separate review occurrence in ignored content-addressed storage, while SQL stores metadata only;
- retain DOC and MSG as `Needs sorting` with explicit deferred-format reasons and no case/reference;
- mark only low-text, dominant-raster PDF pages as OCR candidates; ordinary image evidence is not OCR input;
- fail closed for incomplete bounded EML, DOCX package-limit failure, or aggregate PDF expansion failure even when earlier content appears confirming;
- verify content-addressed bytes before reuse or review and refuse a hash mismatch;
- invoke the contained QDOS extraction policy only after complete readability;
- let strong QDOS instruction content outrank the outer sender of a staff-forwarded message without using QDOS as the default principal;
- record evidence, missing fields, conflicts, and review candidates for ten initial instruction fields;
- default a missing instruction date from the injected clock;
- produce `Draft ready`, `Needs sorting`, `OCR required`, `Unsupported`, and retryable technical-failure outcomes;
- treat `Draft ready` as a reviewable extraction draft, not definitive mailbox classification, case acceptance, or reference allocation;
- identify manual upload by a stable channel occurrence token while retaining SHA-256 as integrity and duplicate evidence;
- persist provider-neutral receipts, read-only relational drafts, assets, evidence, candidates, and extraction policy key/version without creating a case or reference;
- initialize and migrate the default `PegasusDevelopment` SQL Server LocalDB database;
- refuse an old or mismatched SQLite migration/schema baseline before mutation when SQLite is explicitly selected for isolated testing;
- render persisted dashboard counts, queues, and review pages;
- return the existing receipt for replay of the same occurrence while retaining equal bytes under different occurrence identities as separate review evidence;
- deny every `/Intake` route outside Development or when the feature is disabled;
- keep retired `/Intake/Qdos` unavailable.

The then-current MIME reader recorded the root sender but suppressed nested-message sender evidence. The QDOS-specific extraction policy created a draft only, not a case or reference.

None of this proves Graph delivery, Worker calling, provider routing, case creation, Box custody, deployed behavior, field accuracy, operator acceptance, or release acceptance.

## Corpus and benchmark evidence

`corpus/` is ignored, immutable, and untrusted test input. No corpus item enters Git or a pull request. Tests use only sanitized hash-derived names. Generated output belongs beneath ignored `artifacts/`.

### Multi-format corpus identity

Historical local evaluation dated 2026-07-23 used the retired Development-only `/Intake/Qdos` caller. ADR-0006 later moved the caller to `/Intake/Upload` and contained QDOS extraction behind provider-neutral `ProcessIntake`; the old result remains historical QDOS-policy evidence only.

- Inventory: 1,192 files; 1,098,669,618 bytes.
- Redacted manifest SHA-256: `312795590A2FED329125E1374B9C554EF13034FBD67E28439ED9E4728731197E`.
- Ignored inventory artifact: `artifacts/evaluation/multiformat-corpus-inventory.json`.
- Azure, mailbox, Box, external model, and billed OCR calls: zero.

| Format | Files | Bytes |
| --- | ---: | ---: |
| EML | 286 | 485,546,394 |
| PDF | 387 | 444,059,046 |
| DOC | 43 | 3,199,051 |
| DOCX | 19 | 22,329,915 |
| MSG | 23 | 85,953,536 |
| JPG | 9 | 1,651,773 |
| PNG | 45 | 2,888,894 |

These counts establish available format shapes, not expected correctness.

Before that implementation, nine synthetic multi-format cases failed at the real upload boundary. After independent-review fixes, 22 synthetic Web integration cases proved:

- deterministic DOCX text reached QDOS extraction;
- corrupt DOCX produced visible terminal `Unsupported`;
- DOC and MSG remained visible in `Needs sorting` without a reference;
- direct JPEG/PNG remained review evidence without OCR;
- bounded nested EML retained supported attachments, inline images, and nested messages with provenance;
- duplicate image occurrences retained distinct IDs with the same content hash;
- PDF image objects remained separately downloadable without collage segmentation;
- a low-text full-page-raster PDF produced one scanned-page OCR candidate;
- a low-text PDF without a dominant raster remained `Needs sorting` without an OCR candidate;
- MIME trees beyond 128 entities, attached-message nesting beyond eight levels, or repeated decoded nested payloads beyond 25 MB stopped visibly and allocated no reference;
- DOCX entry-count and expansion breaches produced `docx_limit_exceeded`;
- tampered content-addressed artifacts returned a generic integrity conflict and were not served.

Five pinned genuine samples at or below the 10 MB Web limit passed through that caller:

| Format | Observed result | Claim supported |
| --- | --- | --- |
| DOC | `Needs sorting`; no reference/OCR | deferred container retained visibly |
| MSG | `Needs sorting`; no reference/OCR | deferred container retained visibly |
| JPEG | `Needs sorting`; no reference/OCR | ordinary image retained, not OCR input |
| PNG | `Needs sorting`; no reference/OCR | ordinary image retained, not OCR input |
| DOCX | `Needs sorting`; `openxml-engine` evidence | package readability only; no field-accuracy claim |

The genuine category had 11 passing Web tests. A historically low-text PDF correctly remained `Needs sorting` because it had no dominant page raster.

The recorded historical gate passed 11/11 Core tests, 57/57 non-corpus integration tests, 29/29 architecture tests, and 11/11 corpus tests, with no failures or skips. It also completed Release build, repository guards, Bicep compilation, and then-current project validation. This was repository evidence, not deployment evidence.

### Embedded-PDF benchmark identity

Historical run `2026-07-23T07:06:15Z` used immutable local `corpus/qdos-email-corpus/`.

- External calls: 0.
- External processing cost: 0.
- Unique PDFs: 74.
- Pages reported by each runnable engine: 567.
- Direct-PDF origins: 15.
- Email-attachment origins: 68.
- No filenames, document text, claimant data, registrations, claim references, addresses, or source hashes were recorded.
- Detailed disposable output remained ignored under `artifacts/`.

| Engine | Opened | Insufficient embedded text | Extracted characters | Time | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| PdfPig 0.1.15 | 74/74 | 12 | 303,030 | 6.50 s | selected for the first slice |
| iText 9.7.0 | 74/74 | 12 | 296,294 | 5.15 s | equivalent measured marker coverage |
| Aspose.PDF 26.7.0 evaluation | 74/74 | 12 | 308,991 | 11.29 s | lower claim/mileage marker coverage; evaluation-limited |
| Apryse 12.0.0 | 0/74 | not run | not run | not run | valid key required at initialization |

PdfPig and iText had identical document counts for ten redacted markers: QDOS, claim, claimant, registration, vehicle, mileage, accident, instruction, inspection, and address. Aspose detected `claim` in 49 rather than 55 documents and `mileage` in 22 rather than 34.

This establishes that PdfPig decoded the sampled embedded-text cohort without exception, agreed with the other runnable engines on insufficient-text classification, did not lose measured marker coverage, and required no external per-page service. It does not prove literal field accuracy, OCR accuracy for 12 insufficient-text documents, encrypted/damaged/revised/future layouts, Linux App Service behavior, production throughput, or operator acceptance.

## Current implementation evidence

### Proved locally

- Implementation was activated on 2026-07-27.
- Provider-domain Step 2 authoring, Core contracts, persistence, migration, and exact-version catalog behavior are locally implemented and caller-tested.
- The source and package contract produced exactly 11 provider codes and 16 associations.
- Build and `-Verify` emitted byte-identical `provider-domains-v1` bytes with package SHA-256 `f6b5ad8ecdd428db4316b23e16aa7e0ffc93562aec33374c03ea68cd4f0370a3`.
- Four synthetic opacity, growth, immutability, and lock-order tests passed.
- Focused provider-domain Core tests passed 34/34, including strict JSON/schema/version/hash checks and deterministic found/unknown/ambiguous/invalid outcomes.
- Embedded package, source, migration, and seeded rows matched exactly.
- Provider persistence and baseline tests passed.
- Direct catalog smoke observed:
  - `Found` with `QDOS`;
  - `Unknown` with no candidates;
  - `PackageRejected` with no candidates.
- Direct Release restore/build and repository tests passed:
  - Architecture: 33/33;
  - Core: 62/62;
  - Integration: 98/98;
  - no failures or skips.
- Local platform source/caller smoke observed:
  - `npm ci`;
  - LocalDB migration applied twice idempotently;
  - Web HTTPS live, readiness, and intake returned HTTP 200;
  - Azurite Blob and Queue listeners;
  - Functions Core Tools 4.12.1 host lock;
  - no Worker trigger at that checkpoint, which was the expected current limitation.
- The development HTTPS certificate and HTTPS host were observed. Clean-operator Windows trust confirmation remains required and is not claimed complete on the current workstation.
- Historical documentation validation passed against 155 Markdown files, 1,115 local links, 213 feature triples, 41 archived artifacts, and 21 assertions.
- Historical exact-head GitHub validation runs `30236008712` and `30236209099` passed.
- Candidate review at exact head `ce0135ede23101af320846a135d97c1ee05c7146` found one required documentation issue, corrected in the next head.
- Review at exact head `9a8ffe7cb992c024bb2ba1655368a2fdbe3db6fb` confirmed that correction and found one further record-state wording issue.
- Independent Core review returned `SAFE_TO_FREEZE` with 0.98 confidence after correcting authoring ownership leakage. A second independent review of the typed `sourceContracts` seam also returned `SAFE_TO_FREEZE` with no findings.
- Final exact-head review must be external to the commit under review and repeated after every tracked change; this record does not self-certify its own head.

### Explicitly not proved

The following remain absent or unaccepted:

- complete human-reviewed QDOS field-accuracy cohorts and untouched holdouts;
- genuine route predicates and activation evidence beyond the accepted QDOS suffix trait;
- genuine malformed/encrypted/nested format cohorts and broad PDF image-encoding coverage;
- Worker triggers and real Worker caller behavior;
- durable receive/process/resolve/accept intake;
- staff identity, roles, sessions, leases, history, OAuth, or MCP callers;
- case/reference allocation and all three QDOS case paths;
- request-scoped unauthenticated upload;
- Triage and automatic report predicates;
- ordinary-image VRM selection;
- accepted DVLA/DVSA and mileage behavior;
- focused EVA mapping, import, receipt, or assignment;
- Graph permission, delta, mailbox, throttling, and folder behavior;
- Box identity, scope, SDK, per-flow retention rules, in-house upload custody handoff, or production custody;
- Azure identity, RBAC, SQL behavior, platform queues, scale, durability, telemetry, alerts, restore, RPO/RTO, capacity, or cost;
- deployment, predecessor retirement, production cutover, operator acceptance, management approval, or release acceptance.

The ignored local artifact store is development evidence only and must never be described as production custody.

## Deferred-capability seams

### `DATA-02`

`DATA-02` moves to `Next`/`unallocated`.

The preserved join seam is stable provider code plus package/source-version provenance. Excluded until separately accepted are:

- inspection locations and location history;
- provider defaults;
- inspection-mode defaults;
- Case-ID mappings;
- repairer reference data;
- authoring shapes for those data.

Activation requires separate provider-location evidence, authority, schema/package, migration, policy, and real caller proof. Published provider-domain snapshots remain append-only and do not gain those meanings retrospectively.

### Provider API

The principal-scoped provider API remains `Next`/`unallocated`. Its preserved contract is separately issued client IDs and opaque secrets, secret hashes only, rotation/revocation, idempotent submission, and access only to the caller’s own receipt/status/result. It is not an alpha release gate.

### Future email and workflow work

The shared Core classification policy and policy catalog are the seam for future providers, four-mailbox management, detailed categories, queues, folder actions, and general email/image association. Deferred work must reuse that owner rather than add a parallel classifier or rules engine. The separately owned evaluator may exercise the policy but does not own it.

## Blockers and unresolved evidence choices

No unresolved architecture or product decision remains for the provider-domain slice. The following evidence-dependent choices remain hard release holds:

- executable route predicates and dispositions for every provider/intermediary route selected for activation;
- genuine Triage and report matcher cohorts and untouched holdouts;
- selected VRM engine with representative accuracy and false-positive evidence;
- accepted DVLA/DVSA provider/API, licence, target, fields, error behavior, limits, and mileage rule;
- accepted focused `0.1.0-alpha.1` EVA mapping, readiness, image, naming, and recovery contract;
- exact Graph tenant, mailbox, Inbox/Sent allowlist, Application RBAC scope, approved operator, and denied control mailbox;
- exact Box enterprise, identity, root, scopes, and operations;
- refreshed Azure inventory and exact predecessor-resource dispositions;
- approved isolated Pegasus target, spending boundary, identity/RBAC, deployment, restore, rollback, and recovery evidence;
- clean-operator offline acceptance;
- operator acceptance;
- Collision Engineers management approval;
- production migration, deployment, and cutover approval.

The stale Office lock marker that previously blocked provider authoring was removed with exact owner approval; its absence was observed and Step 2 authoring completed. It is no longer an active blocker.

If any hold cannot be satisfied, keep the corresponding caller absent or disabled and keep the release blocked. Do not infer a rule, fabricate data, silently fall back, expose the predecessor, treat a `DOC-CON-052` evaluator allocation as QDOS implementation, or silently reduce the remaining QDOS contract.

## Verification commands and evidence gates

### Provider package

```powershell
pwsh ./scripts/Build-ProviderReferenceData.ps1
pwsh ./scripts/Build-ProviderReferenceData.ps1 -Verify
```

These commands prove authoring bytes only. They do not prove route activation, migration deployment, caller behavior, release, or alpha acceptance.

### Intended offline platform

```powershell
pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline
pwsh ./scripts/Initialize-LocalDevelopment.ps1
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Status
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Smoke
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Reset
```

### Repository and behavioral evidence

- Restore and Release-build the complete solution.
- Run all Architecture, Core, Integration, Web, provider-package, LocalDB concurrency, Azurite/Functions, browser, accessibility, adapter-contract, negative, retry, and recovery tests. Separately owned evaluator tests and report campaigns are not QDOS gates.
- Treat `scripts/Test-RepositoryPolicy.ps1` and its `scripts/Test-RepositoryLanguage.ps1` caller as deferred no-ops, not alpha-required repository evidence; do not cite their successful exit as green evidence.
- Exercise actual Web, Functions, SQL, storage, OAuth, MCP, and Worker callers. Direct service invocation is insufficient.
- Record exact-head CI and obtain independent exact-head review after the final tracked change.
- Preserve source and corpus immutability and keep generated evidence beneath ignored `artifacts/`.

### Live preflight and deployment

After offline acceptance and exact approvals:

- run the Cloud tool profile;
- import ExchangeOnlineManagement `3.10.0`;
- connect with the approved operator;
- run positive authorization against `instructions@collisionengineers.co.uk`;
- run the approved denied-control-mailbox test;
- execute every live adapter’s local-contract parity and permitted/denied live fixtures;
- validate Bicep and inspect exact-target what-if;
- apply migration through the authorized migrator, never application startup;
- deploy immutable manifest-bound packages without rebuild;
- run health, scope-denial, dependency, restore, rollback, RPO/RTO, and operator smokes.

Local parity never substitutes for live scope, delivery, or platform evidence.

## Documentation impact

Implementation must keep the canonical owners above synchronized for:

- operator intake, work, lifecycle, Triage, report, and recovery behavior;
- requirements and all 128 capability allocations;
- open evidence decisions and release blockers;
- route-policy, organization-role, lifecycle, persistence, caller, and local/live architecture;
- local setup, testing, migration, deployment, backup/restore, rollback, live preflight, and predecessor teardown operations;
- Operations-first design source/runtime mapping and approved assets;
- exact change outcome and independently reviewed evidence.

No agent-mistake entry is currently required. The intermediary-model correction was caught during planning before publication or implementation.

## Acceptance outcome placeholders

- **Offline development acceptance:** pending complete real-caller and operator-runbook evidence.
- **Live adapter acceptance:** pending separately approved Graph, Box, vehicle, VRM where applicable, and Azure evidence.
- **Deployment and recovery acceptance:** pending immutable deployment, health, restore, RPO/RTO, and rollback proof.
- **Operator acceptance:** pending genuine QDOS journey by Alex and relevant staff.
- **Management approval:** pending Collision Engineers release approval.
- **Release outcome:** pending exact-head checks and review, issue closure, `0.1.0-alpha.1` tag, and separately authorized production cutover.