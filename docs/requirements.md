# Product requirements

## Authority and evidence

This document is the sole owner of Pegasus intended product requirements. The [capability inventory](capabilities.md) owns stable capability IDs, allocations and activation boundaries; it does not prove implementation.

The [operator notes](operator-notes.md) are the binding source for Collision Engineers’ business process and current-system knowledge. [Architecture](architecture.md) owns what is currently implemented and called. [Operations](operations.md) owns procedures and evidence profiles. [Open decisions](open-decisions.md) owns unresolved material questions. [Design](../design/README.md) owns the durable UI contract.

The accepted [QDOS alpha implementation contract](adr/0013-qdos-alpha-implementation-contract.md) fixes checkpoint 1's clause-specific implementation and Razor/Worker/MCP caller boundary. It does not change capability allocation or promotes an intended caller to implementation, caller, deployment, or acceptance evidence.

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
| 01 | `0.1.0-alpha.1` | Existing QDOS-alpha scope; allocation unchanged, not a completion claim | 127 |
| 02 | `0.2.0` | Provider expansion and intake fidelity after QDOS acceptance | 7 |
| 03 | `0.3.0` | Four-mailbox classification, association, folder actions, email workspace and email MCP | 19 |
| 04 | `0.4.0` | Principal-scoped provider API and post-report query/dispute casework | 5 |
| 05 | `0.5.0` | Extended case types and staff/outbound communication channels | 5 |
| 06 | `0.6.0` | Individually approved operator AI assistance | 5 |
| 07 | `0.7.0` | Optional direct EVA API coexistence before replacement | 1 |
| 08 | `1.0.0` | Pegasus-owned engineering record/workbench and transfer of EVA assignment, estimating, valuation and report-preparation authority | 13 |
| 09 | `1.1.0` | Deterministic report and fee-note rendering | 6 |
| 10 | `1.2.0` | Targeted report distribution, accounts/invoicing and management information | 5 |
| 11 | `1.3.0` | Vendor-neutral AI work requests, Engineer-reviewed query proposals and staff-selected AI Assessor | 3 |
| 12 | `1.4.0` | Conditional capture and domain outcomes after direct promotion decisions | 3 |

The 199 planned capabilities use these twelve targets; 29 permanent boundaries
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
- fail closed before source receipt or reference allocation when safe persistence, identity-critical route facts, limits, processing, or standalone Audit evidence is incomplete or ambiguous; once safe processing establishes Principal and Case type, allocate the Case/PO and retain incomplete ordinary detail, images, or checks as `Not ready`;
- keep business decisions in `Pegasus.Core`, with infrastructure, UI, Worker, MCP, imported workspaces, skills, prompts, and models subordinate to Core policy and human approval;
- support deterministic, repeatable local verification and separately authorised live verification;
- preserve deferred capability seams and data identities without building dormant capability.

## Product invariants

### Principal, reference, organisation, and case-party identity

- Principal and internal reference are immutable after allocation.
- Reference allocation occurs once safe source processing establishes an unambiguous Principal and Case type and all identity-critical gates pass. Incomplete ordinary business detail, images, or required external checks create or retain the Case as `Not ready`; they do not leave a valid instruction pre-Case.
- The normal Case/PO is `{principal code}{YY}{shared sequence}` with a three-digit minimum: `001` through `999`, then `1000` through `9999`. Inspection, standalone Audit, and Inspection + Audit consume one principal/year sequence. Exhaustion at `9999` is visible and blocks allocation; references and sequence values never wrap or return to use.
- A standalone Audit derives lowercase `a.` or `ap.` only from an unambiguous repairable or total-loss assessment in the original Engineer report. Missing or ambiguous evidence blocks case creation and allocation.
- Inspection + Audit begins with the normal Inspection reference. After Collision Engineers’ Engineer records the assessment, the applicable lowercase Audit reference is created inside that case; it does not consume another sequence value.
- A used principal code is replaced by one linked successor in an atomic Core transaction: deactivate the predecessor, continue its next unused sequence in the Europe/London cutover year, and begin later years at `001`. Both identities and the reason remain permanent.
- A wrong-principal case closes as `Created in error`, with a reason and a linked replacement. Neither reference is reused; the original never reopens.
- A case is never deleted. Reopening requires a reason and the normal destination gates.
- Principal is the instructing and paying party. An Intermediary supplies a route without thereby becoming Principal. Repairer identifies the vehicle holder or repair organisation; Image Source identifies the actual supplier of images. One organisation may hold several case roles, but an ambiguous sender never establishes Principal.
- Every case snapshots the inspection address, organisation identities, and party roles accepted for that case. Later reusable-directory corrections never rewrite historical case evidence.
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

Intake may begin through staff-forwarded email, a staff-created request-scoped upload link, provider material, manually supplied files, images, correspondence, or a future approved API route. Receipt is not case creation.

Image-only material with a usable normalised VRM creates a pre-Case Image intake with an Image Intake Reference; it is not `Needs sorting` merely because it lacks a formal instruction or accepted Principal. Image material without a usable normalised VRM remains `Needs sorting`. An Image intake is never allocated a Case/PO or promoted into a Case merely because images arrived.

Every intake path must:

- preserve original source bytes and message/file identity before deriving text or classifications;
- retain sender, recipients, subject, message identifiers, timestamps, attachment names, content types, byte lengths, hashes, and parent/placement relationships where available;
- be idempotent for the same source occurrence without collapsing distinct visible placements;
- surface unsupported, incomplete, corrupt, encrypted, oversized, ambiguous, or technically failed input as an explicit decision rather than silently dropping or accepting it;

- record the actor, time, caller, source, policy version, and reason for every transition;
- prevent untrusted content from becoming instructions, policy, identity, or authority.

When a retained source remains `Needs sorting` because no category can be determined, the UI explains the missing, ambiguous, or contradictory predicates rather than presenting the positive rationale for an unrelated category.

### Request-scoped upload links

**Accepted source boundary:** only authenticated staff may create a link. The token has a stable identity and
is bound to exactly one upload request, its allowed operation, and a
server-enforced expiry. It is security-sensitive and is never written to
permanent business history, message content, or content-bearing telemetry.
Token generation and at-rest representation remain implementation choices;
acceptance must prove expiry, revocation, and cross-request isolation through
the real caller. Revocation invalidates every later request, and an
unauthenticated caller cannot extend expiry.

The public page exposes only the bound request's upload fields and its immediate
structured success or failure. It exposes no case or reference identity,
request/history state, other document, token-management function, external
account, or cross-request lookup. An accepted upload result means only that the
request-local custody boundary succeeded; it is not case creation, Box custody,
EVA handoff, report generation, or external delivery.

File type/count/size limits, authentication of the staff creator, token expiry
and revocation, idempotent retry, abuse handling, durable custody, cross-request
isolation, and non-disclosing error behavior are acceptance gates.
Every attempt returns the same bounded result classes without revealing whether
another request, case, reference, or file exists. This in-house route supersedes
Box File Request behavior.

### Source occurrence and dispatch identity

A source occurrence is the channel-scoped receipt identity for one visible receipt or placement. It is distinct from its content hash, extracted evidence, processing dispatch, and any accepted Case projection.

- Replaying the same occurrence with the same bytes returns the existing receipt.
- Reusing an occurrence identity for different bytes is a visible identity conflict; it creates no new receipt, association, case, or reference.
- Equal bytes received under different permitted occurrence identities remain separate evidence with separate provenance.

Pegasus acknowledges receipt only after the original bytes, source receipt, and one durable processing-dispatch record commit. Each dispatch has its own stable idempotency identity tied to the source occurrence; a queue carries only the stable source/work identifier, never the payload. This acknowledgement means “durably received for processing,” not classified, associated, accepted as a case, completed, or closed.

### Mandatory pre-case gates

Before creating a case or allocating a reference, Pegasus must establish:

- successful source persistence and required extraction/classification receipts;
- authenticated Principal identity and the staff actor where the route requires staff;
- provider/intermediary route identity and enabled policy where relevant;
- unambiguous case type and Principal association;
- processing and size/format limits;
- required standalone Audit evidence; and
- absence of unresolved wrong-Principal, duplicate-occurrence, receipt-integrity, or source-custody ambiguity.

Once those identity-critical facts are established, Pegasus creates the Case/PO
and allocates its permanent reference. Incomplete ordinary business detail,
images, or mandatory external checks retain that Case as `Not ready`; they do
not form another pre-Case acceptance gate. If the route cannot establish an
identity-critical fact, it persists only what is safe and enters the
corresponding pre-Case outcome. `Blocked intake` records a reason and visible
warning, offers reasoned resolve and retry actions, and retains the resolution
evidence and each retry result. It never allocates a reusable identity as a
convenience.

Box case-file custody is a required day-one alpha capability, but it follows Case/PO allocation: Pegasus uses the newly allocated immutable reference to create the Box case folder and stores the retained source material there. Blob staging remains temporary hot processing storage, not accepted Case custody. A Box folder or filing failure retains the allocated Case as `Not ready`, records the exact failure and staff-initiated retry/recovery evidence, and prevents progression that requires accepted Case custody; it never rolls back, reuses, or reallocates the immutable Case/PO reference. No background or automatic business retry is permitted.

### Matching conflicts and reversible association

Matching uses explainable evidence. Message identifiers, provider/domain policy, route identity, accepted reference tokens, VRM, party identity, and operator confirmation may contribute. A weak, ambiguous, or contradictory signal never silently associates material with a case; competing candidate cases and unresolved source-identity conflicts remain visible in `Needs sorting`.

VRM correlation is a suggestion until confirmed by accepted evidence or an authorised operator. Source deduplication is occurrence-aware: exact bytes and transport identifiers support correlation, while each visible placement and chronology entry remains auditable.

Arrival-time proximity never associates or consolidates material. A mismatch
between accepted incident dates may eliminate a candidate; a matching incident
date proves nothing alone and requires corroborating accepted evidence before
association or consolidation.

The immutable source occurrence and its evidence remain distinct from the accepted, editable Case projection. Linking creates a versioned source-to-case relationship; it never converts the source into the case, rewrites source facts, or changes the original intake origin.

An Image intake remains pre-Case until its retained evidence can associate with exactly one eligible pre-report instructed Case. Automatic association requires an unambiguous normalised VRM match and no explicit contradictory identity evidence; otherwise an authorised staff member makes the reasoned decision. A Case after report delivery is not eligible. Association retains both permanent identities and source histories: the instructed Case/PO remains the sole Case identity and the Image Intake Reference remains linked history. Before report delivery, authorised staff may reasonedly reverse or correct the association; the intake returns to awaiting instruction, the instructed Case recomputes readiness, and neither identity, source fact, or relationship event is reused, rewritten, or deleted.

Each direct Case datum retains its current field provenance: staff entry,
extraction, AI prefill or proposal, provider API, or another external
vehicle/estimate source with its applicable identity, version, and time.
Operator UI shows that provenance without treating it as confirmation. A
derived value identifies its accepted inputs and calculation rather than
claiming a separate raw source; provenance and value status remain distinct.

### Global vehicle and value checks

Every Case must satisfy globally required vehicle identity/specification,
vehicle-history/risk, and market-valuation checks, unless an explicit,
documented exception applies. All three results or their recorded exceptions
are required before staff may accept Case review and expose the Case in the
Engineers queue. The authorised staff reviewer may record an exception as a
named, reasoned Case action in permanent history. Provider and route policy
select the provider, required result, acceptable provenance, and
unavailable/failure behavior for each check; no provider is inferred by this
requirement.

Vehicle details are extracted from the instruction where available, otherwise
obtained from the applicable DVLA/MOT source. Mileage evidence ranks as:

1. an accepted staff-entered value;
2. directly extracted instruction text;
3. Document Intelligence extraction from a scanned instruction or future
   odometer-vision evidence; and
4. a DVSA-derived estimate.

DVSA is run for every Case. Where no higher-tier mileage value is available, it
supplies the source-labelled estimate. A difference between DVSA mileage and
any accepted staff-entered, instruction-extracted, Document Intelligence, or
odometer value is a visible Case discrepancy. The later odometer-vision
capability does not imply an activated AI caller before its own accepted
evaluation and integration contract.

The DVSA estimate follows [ADR-0012](adr/0012-conservative-mot-mileage-estimation.md):
it preserves raw observations, validates units, groups fail/retest episodes,
segments corroborated odometer drops, and excludes implausible or
low-information intervals without deleting them. It uses a recency- and
quality-weighted median of clean rates, with a versioned cohort prior only for
eligible sparse histories; interpolation and forecasting remain bounded. An
estimate without eligible chronological holdouts is a wider, explicitly
non-probabilistic range and never defaults into the Case.

Definitive authorised intake creates exactly one instructed Case idempotently. A definitive match to an existing instructed Case allocates no duplicate. A new instructed Case enters `Not ready` until its ordinary business detail, required source images, and applicable progression requirements are satisfied; the route may move it to `Review` only when its explicit policy permits that transition. The allocation decision adds no universal manual acceptance gate.

One source occurrence has at most one current Case association. Every automatic or manual association records the exact source and Case identities, evidence, actor, time, policy/version, and reason where required. Any authorised staff member may reasonedly unlink or reassociate a mistaken match; the prior relationship and both source origins remain permanent, and dependent facts and counts recompute without deleting history.

## Triage

### Normal workflow and completion evidence

Triage begins when the exact accepted route policy classifies a provider request as an assessment request or an authorised staff member manually classifies safely retained, attributable material as Triage. Manual classification records the source, available route evidence, actor, time, reason, and policy version; it neither invents Principal identity nor creates a Case. Material whose route or category remains unaccepted stays `Needs sorting` and never becomes Triage or a Case by fallback. A Triage request stays separate pre-Case work: without a VRM it remains `Needs sorting`; with a VRM it opens as `Open`, may move to `Awaiting information`, records an accepted finding as `Finding recorded`, and reaches `Completed` only after the required response evidence is confirmed. An acknowledgement, request for information, Draft, queue action, or other correspondence may be retained but is not itself a finding or completion evidence.

Triage records have the states `Open`, `Awaiting information`, `Finding recorded`, `Completed`, and `Cancelled`.

A recorded finding has two independently optional dimensions:

- Roadworthiness: `Roadworthy` or `Unroadworthy`;
- Assessment: `Repairable` or `Total loss`.

At least one dimension is required. A later correction creates a reasoned superseding finding; it never overwrites history. A pre-send correction replaces the current finding with a reason. A post-send correction creates a superseding finding, returns the Triage to `Finding recorded`, and requires a new response.

Every `Completed` Triage has one exact reply-chain Sent item from an approved mailbox. Subject, VRM, a manual “sent” assertion, a Draft, a queue result, an acknowledgement, or an unrelated Sent item is not completion evidence. `Cancelled` is the only terminal Triage outcome without a finding and reply; `Completed` and `Cancelled` close only that Triage workflow and never make its finding definitive for a later Case.

Triage may have an optional assignee but no due date or chase schedule. It may link to at most one current case; a case may have many Triages. The [staff role access matrix](#staff-role-access-matrix) permits every staff role to reasonedly unlink or relink; the exact prior/current Case identities, actor, time, reason, and evidence remain in permanent history.

Cancellation and reopen require reasons. Reopen always returns to `Open` and never erases the prior finding, reply, actor, or chronology.

## Case identity and lifecycle

### Case types

The active alpha types are:

- **Inspection:** Collision Engineers prepares accepted work for its Engineer’s desktop assessment and returns that Engineer’s report to the provider.
- **Standalone Audit:** another engineering firm has already inspected the vehicle; Collision Engineers accepts that firm’s original Engineer report and audits or double-checks the work.
- **Inspection + Audit:** Collision Engineers completes an Inspection report and then immediately performs a distinct Audit of that report in the same Case; the Audit retains its own identity, evidence, and acceptance boundary.

Diminution and Commercial remain deferred unless their capability rows and activation evidence say otherwise. They are not active alpha aliases or generic case types.

A case owns immutable identity, principal, internal reference, type, accepted source links, snapshotted parties/addresses, vehicle identity, work state, due work, documents, correspondence, findings, decisions, action history, and closure history.

### Lifecycle closure and correspondence

The lifecycle must support:

- pre-case receiving and acceptance;
- active work, `Not ready`, `Held`, `Review`, due-work visibility, and separate mandatory instruction-completeness, image-completeness, and staff-review gates before Engineers-queue eligibility; provider policy may define accepted gate evidence but may not remove a gate, and named-Engineer assignment remains EVA-owned through `0.1.0-alpha.1`;

- manual chasing with the exact schedule below;
- inspection/report preparation appropriate to desktop assessment;
- report approval and delivery evidence without adding a separate pre-send case-review gate;
- post-report queries, corrections, addenda, disputes, and reasoned closure where allocated;
- four distinct instructed-Case terminal outcomes: `Post-report complete`, `Provider cancelled`, `Collision Engineers rejected`, and `Created in error`; confirmed Image-intake association is a separate pre-Case source outcome, not a fifth Case closure state;
- reasoned reopen through normal destination gates, excluding `Created in error` and `Held` as a reopen destination.

Each unmet progression requirement is an individual actionable blocker. The UI identifies its exact field or material, source/provenance, reason, and permitted resolution; an opaque aggregate such as “no unresolved field reviews” is prohibited. An action is enabled exactly when its current explicit prerequisites are satisfied. Saving unchanged or unrelated data must neither unlock it nor reset lifecycle, readiness, or advisory state.

Durable receipt acknowledgement, retained correspondence, prepared or copied text, the `First sent to Engineer` export proxy, and a `Report sent` event are not terminal case outcomes. Report-sent evidence enters post-report work; post-report completion is a separate named closure action.

The named Core workflow records the policy key and version used for every configured readiness gate. It permits Engineer assignment only when the configured instruction-completeness, image-completeness, instruction-review, and image-review gates each pass; no caller, assignment, prepared artifact, or later workflow event supplies a missing gate by implication. A Report approval identifies one immutable artifact and its approving staff actor. `Report sent` requires one retained exact approved-mailbox Sent item with its mailbox/Sent-folder scope, immutable item, conversation/reply-chain identities, authoritative Sent time, and separate link time; an assertion, draft, queue result, generated file, or export proxy fails closed.

Every closure selects exactly one named terminal outcome, records the authenticated actor, time, reason and prior/new state in permanent history, and leaves the Case, Case/PO, source relationships, and closure chronology intact. A closed case and its files remain application-level read-only until an authorised, reasoned reopen passes the normal destination gates. `Created in error` never reopens.

Every Image-intake association, reversal, or correction records the same attributable relationship evidence without closing or creating a Case. The Case, Case/PO, Image Intake Reference, source relationships, and chronology remain intact.

State changes are explicit Core transitions. UI labels, Worker handlers, APIs, and MCP tools call the same use cases; they do not implement parallel policy.

When a Case passes its staff-review gate, it becomes visible in the Engineers
queue. Through `0.1.0-alpha.1`, named-Engineer assignment and reassignment remain
authoritative in EVA; Pegasus neither assigns nor mirrors a named Engineer.
That authority transfers only with the accepted `1.0.0` Engineer-workbench
capabilities and caller evidence.

Incoming cancellation classification or association never changes a Case automatically. In the focused alpha, mailbox processing covers incoming instructions only; a separately retained and reasonedly associated cancellation message may support an authorised staff action to place a pre-report Case in `Held pending staff decision`, confirm `Provider cancelled`, or release it. Release requires the message to be reasonedly recategorised, unlinked, or reassociated first. Every original and corrected classification/association, actor, time, reason, and evidence remains permanent history.

### Case edit authority and recovery

Every staff case mutation targets one identified case through a named Core action and requires the role permitted by the [staff role access matrix](#staff-role-access-matrix). Entering edit mode acquires the case’s one server-owned expiring lease. Other authorised staff remain read-only and can see the holder and recovery state. Every save, transition, assignment, association, evidence change, and other staff mutation presents both the lease token and the Case version loaded by that editor.

The holder may leave editing; an abandoned lease expires by server time and may then be reacquired. Core refuses a missing, expired, wrong-holder, or stale-version mutation without overwriting newer work. The rejected editor keeps proposed values for comparison and must reload and reacquire rather than merge or force the save. There is no Administrator bypass, forced takeover, collaborative merge, bulk case mutation, queue-inline lifecycle edit, provider case-edit route, or direct external-system or adapter edit.

Web and MCP Automation Actor callers use the same guard. Background append-only receipt, dispatch, and document-processing records remain separate from editable Case state and cannot bypass Case versions to alter it. A deliberate recovery or material denial/failure is attributable permanent history; routine renewal, expiry, heartbeat, polling, and adapter mechanics remain telemetry.

### Due work, chasing, and action history

`Due by` comes from the inspection date or accepted equivalent deadline. For a case entering `Not ready`, the first chase occurs at the same Europe/London local time seven calendar days later and repeats every seven calendar days. `Held` preserves the remaining interval; release to `Not ready` resumes it. `Review`, accepted material arrival, or terminal closure stops the schedule.

Manual chasing remains a staff action in the alpha unless an allocated capability and accepted integration explicitly authorize automation. The history records what was attempted, by whom, through which channel, against which party/address, when, and with what evidence. A recorded action is not proof of external delivery.

Each chaser retains its recipient, channel, prepared draft or draft reference,
staff disposition, and attributable timestamps. Free-text notes may accompany a
structured chaser without implying that it was sent or answered.

For each item awaiting material, the current work projection keeps the
missing-material reason, `Due by`, next chase, most recent recorded
channel/outcome, optional note, and next permitted action together. Prepared or
copied text remains visibly distinct from sent, delivered, answered, or
completed work.

## Parties, principals, organisations, accounts, and access

Pegasus distinguishes principals, reusable organisations, staff accounts, roles, and case-party roles. A repairer, broker, agent, client, legal representative, provider, vehicle keeper, or other contact may occupy different roles on different cases. Reusable repairer-directory identity is separate from the inspection address and role snapshot retained by each historical case; raw provider/contact workbooks are evidence, not import authority.

A Repairer directory records its name, full address, and contacts. A Repairer
may relate to multiple Principals, and a Principal may relate to multiple
Repairers; these reusable relationships do not rewrite the accepted address or
party-role snapshot on an existing Case.

### Staff role access matrix

Staff accounts use Pegasus-managed usernames and passwords with non-reversible password hashes until a separately accepted identity change supersedes that route.

| Staff role | May view | May create or change | Must not access or perform |
| --- | --- | --- | --- |
| `Administrator` | All authorised application data and settings | Every ordinary Intake, Triage, Case, document, evidence, task, transition, and pre-assignment review action; staff account creation/disable/access review/role assignment; principals and successor cutover; workflow configuration; approved-mailbox allowlist; accepted OAuth-client registration/revocation | Credential-secret, cloud, or release administration through the staff UI; permanent deletion; a generic mailbox-rule editor before its policy is accepted |
| `Engineer` | Cases, inbox items, documents, evidence, and details | Every authorised Intake, Triage, Case, document, evidence, task, transition, and pre-assignment review action | Accounts, roles, access review, principals, successor cutover, workflow configuration, mailbox allowlist, authentication-client administration, credentials, cloud/release administration, or permanent deletion |
| `User` | Cases, inbox items, documents, evidence, and details | Every authorised Intake, Triage, Case, document, evidence, task, transition, and pre-assignment review action | Accounts, roles, access review, principals, successor cutover, workflow configuration, mailbox allowlist, authentication-client administration, credentials, cloud/release administration, or permanent deletion |

Andrew and Alex are the initial `Administrator` assignments held in application data/configuration. No person, name, email address, or bypass is hard-coded into authorization. Automated processing uses a distinct durable machine identity and only named Core actions; it is not a staff account or an independent policy owner.

Authorization is enforced in Core use cases and at every caller boundary. It fails closed without revealing case or source data. Immutable principal/reference, source, association, history, and closed-case rules apply regardless of administrative privilege. Development routes and data never confer production access.

### Permanent action history

Permanent business history records every business mutation; download/export; material denial or failure; automated result; and accepted, linked, or used external fact with the exact affected Case when case-bound, source/evidence identity, trusted staff or automated actor, caller, time, policy/version, structured before/after values, outcome, and reason where applicable. A history write is part of the mutable business transaction; a failed write cannot leave an unrecorded successful mutation. History is append-only: correction and reassociation add events rather than rewrite prior facts.

Sign-ins and authentication failures remain in the security log. Routine views, searches, refreshes, polling, retries, lease renewal/expiry/heartbeat, and adapter mechanics remain content-safe telemetry.

No identity design, app registration, scope declaration, role table, file, or registration proves that a live caller exists or is accepted.

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

- Network, local, or Azure staging is temporary processing storage and is never accepted Case custody proof.
- Box is the required accepted case-file custody system for the day-one alpha. Every allocated Case/PO uses its immutable reference for its Box case folder, then retains its source emails, instruction documents, images, correspondence, and reports there.
- A Box failure after Case/PO allocation retains the Case as `Not ready` with explicit failure and staff-initiated retry/recovery evidence. It does not roll back, reuse, or reallocate the reference, and no background or automatic business retry is permitted.
- Staff may add manually received WhatsApp evidence with its source/channel provenance; this does not activate a WhatsApp integration.
- A closed case and its files are application-level read-only. A new version, revision, logical removal, move, copy, share, or other mutation requires a reasoned reopen first; no Box operation bypasses that gate, and the alpha infers no general move/copy/share/delete authority.
- Default local alpha work must not mutate any Outlook mailbox or Box location. The separately approved Box integration-test profile and explicitly approved non-production test deployments may create and update controlled non-corpus artifacts only in the approved disposable test subtree recorded in [operations](operations.md#approved-box-integration-test-target); they must not delete, move, copy, or share Box content. Outlook tests use immutable local copies or an explicitly approved test mailbox and operation.
- A custody transition records source identity, content hash, target identity/version, actor/caller, time, and failure/retry state without deleting the source proof prematurely.
## Vehicle and engineering evidence

Vehicle identity, registration, location, valuation, repair evidence,
roadworthiness, total-loss, and salvage information remain source-labelled and
reviewable.

### Inspection address

**Settled operator truth:** the report records either the physical vehicle/repairer location, when that
location is explicitly supplied or operator-confirmed, or the exact value
`Image Based Assessment`. Collision Engineers performs desktop assessments
only. For an always-image-based Principal, `Image Based Assessment` is
autofilled at Case creation even when a physical location appears in the
instruction; authorised staff may override it to the explicitly supplied or
confirmed location. For other Principals a provider setting may suggest a mode
but cannot overwrite explicit evidence or operator confirmation. The current
provider-domain reference package contains no address or address-mode default,
so none may be inferred from a provider or domain match.

A manual selection of `Image Based Assessment` requires an attributed staff
reason in permanent Case history; the always-image-based autofill records its
route-policy provenance. Neither is inferred from a corpus row or domain match.

When `DATA-02` activates, its separately approved reference-data pipeline
accepts only reviewed full addresses, retaining each complete display address
with a normalized postcode. It preserves operator-maintained confirmed rows
across refresh and is deterministic and auditable. Frequency, recency,
proximity, accepted Principal, Repairer, Image Source, and normalized search
text may rank suggestions but never select an address. This activates no
spreadsheet import, route, or caller before its separate acceptance evidence.

### Ordinary-image VRM and image analysis

**Accepted source boundary:** automatic registration reading from an ordinary vehicle image is
suggestion-first. Every result remains attached to one retained source-image
occurrence; staff confirmation creates the provisional vehicle identity. Before
confirmation, a suggestion must not create or identify a case, allocate a
reference, overwrite a confirmed registration, select an EVA image, satisfy a
readiness gate, or mutate workflow.

The operator surface distinguishes a suggestion from no readable result or an
unknown result, an unavailable dependency, and a technical failure. It never
renders an empty value as success. Record the source occurrence, task,
engine/provider and version where applicable, time, output, supplied
confidence, failure or unknown outcome, and later staff disposition separately
from confirmed case data.

The implementation mechanism is not inferred: ordinary-image VRM reading,
Document Intelligence extraction from scanned PDFs, and broader image/damage AI
or vision assistance are different capabilities.
Generated or synthetic vehicle imagery is not acceptance evidence, and no recogniser, model, or adapter acts autonomously.

Pegasus retains every source image. An automated VRM or colour result may only suggest that an image depicts another vehicle; it does not exclude the image from Case-vehicle, EVA-export, or future report-selection pools. An authorised staff member must confirm the different-vehicle finding before the retained source is categorised and excluded as third-party vehicle evidence. Without that confirmation it remains visible as unmatched-vehicle evidence. Neither outcome deletes source evidence or turns an automated assessment into accepted Case fact.

When activated, an AI-assisted image readiness assessment runs automatically whenever current Case images are added, replaced, or removed. It returns a source- and version-labelled advisory on whether the set contains a registration overview, at least one damage close-up, and a reflected image. An accepted always-image-based Principal route policy waives only the reflection advisory.

The assessment may run before market valuation and neither creates nor returns an AI Proposal. Its result does not affect Case/PO allocation, Case state, Review, Engineers-queue eligibility, due work, chasing, or staff discretion. Source images remain retained, and report-image selection continues to exclude images showing a person's reflection.

Image-readiness advice never selects, excludes, orders, or otherwise decides report images. Report-image selection is a human Engineering decision in the report-generation section, not an opposing-toggle control on the Case evidence surface.

This allocation creates no AI caller. Its activation still requires accepted model/transport, data, cost, evaluation, failure/recovery, real-caller, and approval evidence. Broader image or damage analysis and AI-generated repair specifications remain separate capabilities.

### Vehicle data and MOT enrichment

Vehicle identity/specification is a global Case requirement. Where instruction
evidence omits vehicle facts, an accepted DVLA/DVSA caller supplies
registration-linked make, model, manufacture year, engine capacity, fuel type,
available MOT history, and mileage observations. At activation, DVSA runs for
every Case; until then, approved local replay returns its preserved result and
absent replay evidence returns source-labelled `Unavailable`.

The mileage tiers and discrepancy rule are defined in
[Global vehicle and value checks](#global-vehicle-and-value-checks). Every
lookup or refresh preserves provider/source, retrieval time, applicable
effective date, source age, response/version identity, and a typed current,
stale, unavailable, partial, or failed outcome. A refresh creates a new
observation; it never silently overwrites a last-good observation, confirmed
value, or higher-tier mileage. Acceptance, rejection, or linking of an
external fact enters permanent business history. Routine calls, retries, and
polling remain content-safe telemetry.

**Source limitation:** no allowed source selects the live DVLA/DVSA provider,
API, licence, exact response fields, credentials, rate/limit behavior, error
contract, target, or caller proof. Those items remain activation gates.
Vehicle enrichment does not activate valuation behavior.

### Professional engineering findings and correction

**Settled operator truth:** the Collision Engineers Engineer report is definitive for the case.
Roadworthiness (`Roadworthy` or `Unroadworthy`) and Assessment (`Repairable` or
`Total loss`) are separate professional findings: neither is derived from the
other, and Triage findings never populate or change either one.

A correction never edits an earlier accepted or issued finding in place. It
creates a reasoned superseding report/finding or addendum with actor, time,
source, structured before/after values, and the prior artifact/version retained.
If the case is closed, an authorised reasoned reopen through the ordinary
destination gates must occur before the correction; `Created in error` remains
non-reopenable. Current views may recompute from the superseding version, but
historical reports, events, and counts keep their original provenance.

Triage findings and their corrections have no case, report, Audit-reference,
fee, or invoice effect. Invoicing is separately deferred: a professional
finding correction must not silently create, alter, credit, or void an invoice.
Any later financial consequence requires the separately accepted,
versioned finance contract.

Automated or AI-assisted extraction may propose candidate facts, confidence,
damage observations, repair operations, costs, flags, valuation comparables,
roadworthiness, total-loss, or salvage evidence only where an allocated
capability and accepted evaluation permit it. `Pegasus.Core` and an authorised
human own accepted facts, economics, findings, outcome, legal use, and approval.

A skill, prompt, model, workspace, external schema, or imported reference never
becomes current OEM instruction, repair policy, valuation authority, legal
advice, Engineer approval, or product policy merely by existing.
## EVA and external engineering handoff

### Focused EVA manual handoff

**Accepted focused-alpha boundary:** EVA remains the authoritative external
engineering/report workflow. Pegasus performs no EVA network call. It
deterministically serializes UTF-8 JSON in the exact 13-key order below,
includes every custody-confirmed eligible Case-vehicle image, and writes a
SHA-256 manifest over the JSON and image identities and bytes. Stable manifest
ordering exists only for reproducible package integrity; Pegasus owns no EVA
presentation, selection, or report-image order.
The two retained populated EVA JSON examples are immutable
reference evidence for the field shape; they do not supply credentials or
activate an adapter.

The JSON keys, in serialization order, are:

1. `Work Provider`
2. `VRM`
3. `Vehicle Model`
4. `Claimant Name`
5. `Reference`
6. `Incident Date`
7. `Instruction Date`
8. `Inspection Date`
9. `Inspection Address`
10. `Accident Circumstances`
11. `VAT Status`
12. `Mileage`
13. `Mileage Unit`

The first successful package generation records the once-per-case `First sent
to Engineer` proxy. Later generations are revisions. The proxy proves Pegasus
export generation only; it does not claim EVA receipt or named-Engineer
assignment, which remain EVA-owned events. An image/document upload into
Pegasus, Box custody, or the presence of a report PDF is not this handoff and is
not external delivery evidence.

Successful focused manual generation makes the complete JSON, all-eligible-image, and manifest bundle available for immediate staff download. Download proves neither EVA receipt nor report delivery and does not change Case state.
The container format is intentionally unspecified: its selection must evaluate
whether a single archive is the clearest usable representation without changing
the exact package contents, manifest, or manual-handoff boundary.

The focused handoff readiness review keeps four source-labelled inputs distinct:
the saved source email, vehicle images, valuation evidence, and initial
instructions. A missing item remains visible and cannot be represented as
present. The Experian adverse-history check remains an EVA-owned downstream
step; Pegasus preserves its source-labelled result if later received but does
not claim that manual package generation performed the check.

The focused alpha exports every custody-confirmed Case-vehicle image except an image that authorised staff have confirmed as third-party vehicle evidence. Pegasus does not select, duplicate, or presentation-order EVA images and exposes no `Use for EVA`/`Exclude` controls. EVA owns image selection, ordering, and report eligibility after import. When EVA is replaced, those Engineering decisions move to the accepted `1.0.0` Engineers screen and remain under Engineer authority. Video-derived screenshots are exported only when retained as distinct Case-vehicle image occurrences with source-video and capture-position provenance. The source observations and their scope are retained in the [Collision Engineers administration overview](reference/reports/collision_engineers_admin_overview.md).

### External boundary

EVA API integration and EVA replacement remain deferred. Activation requires
vendor access; every required Collision Engineers principal code; parity with
the accepted manual JSON/all-eligible-image handoff; stable source and image
identity; accepted mapping; identity/authorization; idempotency;
failure/recovery; current-version handling; real caller proof; and operator
acceptance.

Any later adapter treats a proxy-only case/vehicle/inspection fetch as a
read-only external observation. Fetch, create-with-children, picture upload, and
report-with-PDF handoff retain separate operation, correlation, and outcome
identities; success of one never proves another. A parent or overall success is
not inferred when required child validation failed. The exact vendor contract
must decide whether creation is atomic or partial, and an unknown/partial
outcome remains recoverable rather than being retried as a new creation.

Pegasus preserves structured vendor success, validation failure, rejection,
partial/unknown outcome, and correlation evidence instead of collapsing them
into one Boolean. These are Pegasus evidence classes, not claimed EVA response
labels. No response identifier, fetch result, upload result, or external
success creates, selects, or changes a Pegasus case/reference; only the Core
intake/allocation transaction may do that.

**Source limitation:** the supplied EVA schema is reference evidence, not an
accepted Pegasus operation or wire contract. No allowed accepted source
establishes a proxy-only case/vehicle/inspection fetch, a
create-with-children operation or its validation/atomicity, separate picture
upload, report-with-PDF handoff, response model, or case/reference correlation
semantics. Those details remain unresolved in [EVA API
activation](open-decisions.md#eva-api-activation-070-ext-04); none may be inferred from
the manual export or used to authorize an EVA call.

Audatex remains a separate estimating-system role unless an accepted capability
and integration contract establish otherwise. Guided-capture providers are
candidates/evidence, not active routes.
## Email, mailbox, and background processing

The target product covers the approved mailbox estate and full source messages; the focused alpha mailbox is only the first caller. Mailbox inventory and current-system roles remain in [operator notes](operator-notes.md).

### Settled mailbox taxonomy and correction

The user directly confirmed this taxonomy from the retained current-tree
evidence. This subsection is the sole
product-behavior owner. The [operator confirmation](operator-notes.md#confirmed-mailbox-categorisation)
and retained decision dossier (git history: `docs/history/plans/mailbox-categorisation-and-email-matching/`)
preserve provenance and research context without becoming competing policy
owners.

| Received family | Confirmed examples or subtypes |
| --- | --- |
| `General` | `autoreply`; `undeliverable`; acknowledgements such as “thank you”; `general-chase`; `case-summary` |
| `billing` | payment notifications; remittances; invoice requests; `billing-query`; `general-billing` |
| `new-instruction-received` | initial work instructions: `audit`, `diminution`, `inspection`, `new-client`, `website-enquiry` |
| `non-client-related` | internal/company email from tools, services, software packages, and similar sources |
| `in-progress-cases` | `cancellation`; `case-update`; `client-chasing-for-update`; `provider-chasing-for-update`; other ongoing correspondence |
| `post-report-emails` | queries; disputes; amendment requests; similar post-report correspondence |
| `pre-instruction-emails` | Triage requests; pre-formal-instruction handling requests; images received before formal instructions |
| `internal-cc` | internal copied correspondence |

| Sent family | Confirmed meaning |
| --- | --- |
| `Report sent` | Collision Engineers’ email sending the Engineer report |
| `case-rejected` | Collision Engineers rejects a case |
| `query-sent` | Collision Engineers sends an additional query or information request |
| `additional-image-request` | existing images are insufficient and better or additional images are requested |

Reply is not a standalone recorded type. Collision Engineers’ replies to
Received messages mirror the underlying Received category with reply context;
a correspondent’s replies to Sent messages mirror the underlying Sent category
with reply context. The settled taxonomy also permits `Other`, which requires
both a new category name and reasoning.

A `general-chase` message may refer to several Cases but remains a single unlinked General source occurrence: Pegasus neither copies it nor creates one-to-many Case associations. A `case-summary` is likewise retained as non-actionable General correspondence and creates no intake, Triage, or Case work.

Classification, application queue, Triage routing, and Outlook folder
destination are separate facts. `new-instruction-received` is a Received family
and no equivalent Sent family is confirmed. That direction boundary does not
choose between multiple simultaneously matching rules: exact predicate
precedence and ambiguity handling remain unresolved in [open
decisions](open-decisions.md#mailbox-rule-activation-automatic-matching-and-confidence-display).

Every automated or human categorisation decision retains the source identity,
policy key and version, outcome, material evidence references, applicable
confidence or ambiguity facts, actor or automated identity, and time. An
authorised correction, override, reversal, link, unlink, or relink preserves the
original decision and appends the reason where it overrides or reverses a prior
decision, structured before/after values, actor, event time, outcome, and
policy/evidence references to permanent business history. Dependent queues,
routes, counts, and events recompute deterministically without deleting source
or decision history.

A rule change never silently reinterprets historical decisions. Cohort
re-evaluation requires an explicit approved operation; a technical replay is
idempotent and is not a new business decision. A wrong case allocation follows
the reasoned `Created in error` replacement route and never reuses a reference.
Message/file bodies, credentials, tokens, and secrets do not belong in
permanent action history; routine polling, retry, lease, and adapter mechanics
remain telemetry.

At the allocated `Next / 0.3.0` mailbox-workspace activation, each approved mailbox has an exact mailbox filter and queue scope. The email quick preview is keyboard- and screen-reader-accessible, opens on pointer or keyboard intent without clipping or obscuring adjacent controls, and dismisses when focus moves away. It is evidence navigation only: previewing never changes classification, association, read state, Case state, or source custody.
The workspace does not include `View in Outlook`: operator review accepted that
the in-app full message, attachment and thread view provides the needed value.
It therefore creates no Outlook-navigation integration, action, or external
access requirement.

The default workspace view is the incoming Inbox across all approved mailboxes;
folder-specific, mailbox-specific, queue and search views are explicit
refinements. Sent mail and read-only Deleted Items search remain separate
folder scopes. General mailbox search includes retained message bodies,
attachment filenames and searchable attachment content. An unsupported or
unsearchable attachment remains visibly so; it is not silently omitted.
Search remains within the current mailbox/folder scope unless the operator
explicitly broadens it.
Search returns individual messages, not collapsed conversation groups, because
classification, association and folder actions apply to exact message identity.
Each result identifies whether its match is in the message body, an attachment
filename or an attachment's searchable content, naming the matching attachment
where applicable.
The Inbox and search-result lists use accessible pagination, not infinite
scrolling.
The all-Inboxes view defaults to newest received message first.
Active mailbox, folder, queue and search filters remain visible and are
preserved when returning from message or Case detail.
On a fresh visit, the workspace resets to the default all-Inboxes view rather
than retaining a cross-session user preference.
The workspace provides an explicit manual refresh, last successful update time,
and distinct stale and unavailable states rather than silently presenting old
data. It does not refresh automatically while an operator is reading or acting.
Refresh preserves the active mailbox, folder, queue, search filters,
page and open-message context when that message remains available.
If it no longer remains in that scope, its detail stays visible with an
explicit no-longer-in-this-view state and a return-to-list action.
Each Inbox row includes a short message-body excerpt beneath sender and subject.
Inbox rows visibly distinguish retained read and unread state, but this
workspace does not change that state.
Opening a message preserves the originating list filter and position, shows the
full retained message, attachments and a chronological
thread, and exposes current classification, queue, processing outcome and Case
association before any action. A quick preview remains evidence navigation
only: it shows sender, subject, timestamp, excerpt, classification,
association and attachment names, but no mutation controls. Case linking starts
with deliberate Case search, then a target summary,
reason and explicit confirmation; it may occur while classification remains
unresolved when the link evidence itself is sufficient.
Thread display includes only retained messages within approved mailbox/folder
scope; a matching thread identity never fetches or exposes other messages.
Classification, linking and folder-move actions are available only from opened
message detail, never from an Inbox row or quick preview.
UI-10 provides no bulk classification, linking or folder-move action: each
decision applies to one exact message.
After a classification change is saved, a recommended Outlook-folder move is a
separate explicit confirmation; it is not part of classification confirmation.
Staff may confirm only the designated folder from the applicable classification
policy. A different destination requires correction of that classification, not
an arbitrary folder choice.
If a later reclassification produces a different designated folder, Pegasus
offers another separate explicit move confirmation and never moves it
automatically.
If that move fails, the saved classification remains intact, the failure is
visible, and only a staff-initiated retry may repeat the move.
After a successful move, the message leaves the Inbox view and remains
findable through its destination-folder scope or search; it is not duplicated.
Selecting a Case association opens that Case workspace in the same tab; Back
returns to the exact message detail and originating list context.
Each Case workspace also exposes its associated correspondence as a contextual
filtered view in one chronological history of linked received and Sent items;
it defaults to newest first with an explicit oldest-first option. Cross-mailbox
browsing and reconciliation remain in the email-management workspace.

The allocated workspace includes read-only search of Deleted Items within each
exact approved mailbox/folder scope. It does not introduce a backlog scan,
reconstruction, bulk replay, Case allocation, or mailbox mutation.

An Outlook/Graph route must, before activation:

- use an approved test/live mailbox and exact operation;
- preserve message, conversation, folder, attachment, sender/recipient, and received/sent identity;
- maintain a durable cursor/checkpoint and idempotent occurrence processing;
- separate read/intake scopes from draft/send and administrative scopes;
- queue only stable work identifiers, never full source payloads;
- record poison/retry/dead-letter and operator recovery behavior;
- prove the real Worker timer/queue caller;
- obtain exact Sent-item/reply-chain evidence when delivery is part of a completion gate.

### QDOS-alpha evaluation boundary

The Development/local email evaluation workbench is a separately delivered
evidence harness and is not a QDOS-alpha product surface, caller, or acceptance
checkpoint. QDOS adds and claims no evaluator route, `unchecked`/`checked`
workspace workflow, evaluator command, reviewer report campaign, or
Administrator evaluator approval. A separately delivered evaluator may exercise
shared policy and produce accepted, source-labelled evidence where the shared
mail policy requires it; that call and its review mechanics remain evaluator
evidence, not QDOS delivery or activation proof. The capability inventory's
evaluator allocation boundary owns the unchanged evaluator allocations. Shared Core
mail policy, production intake, Graph replay/live adapters, and their
genuine-evidence and caller requirements remain in QDOS scope.

### Outbound correspondence evidence

Report-sent evidence associates one exact immutable Outlook Sent item from a mailbox on the Administrator-maintained allowlist with exactly one Case. The record retains the mailbox and Sent-folder scope, immutable item and conversation/reply-chain identities, authoritative Outlook `sentDateTime`, separate discovery/link times, actor or matcher identity, Case relationship, reason where required, and available recipient/artifact evidence without storing a message body in action history.

When automatic matching is absent, ambiguous, late, duplicated, or conflicting, the item remains unconfirmed until any authorised staff member reasonedly links the exact item. Any staff role may unlink or relink it with a reason; prior and current associations remain permanent, and dependent events and counts recompute deterministically. A confirmed event remains final if Outlook later moves or deletes the source item.

Confirmation proves only that the exact item existed in the approved Sent scope at confirmation. It does not prove recipient delivery, reading, content correctness, post-report completion, or another terminal outcome. Preparing, viewing, copying, or acknowledging a chaser or other message is also not evidence of sending or closure; a staff-recorded outbound action remains an attributable assertion unless the applicable exact external evidence is retained.

Triage completion uses its separate exact reply-chain evidence contract and has no subject, VRM, manual-item-selection, or manual “sent” fallback.

The local alpha must not mutate a mailbox. A Worker project, queue registration, or timer configuration is not caller proof.

## Provider and intermediary routes

Provider identity, intermediary identity, route identity, and
provider/domain-suffix association are separate facts. The versioned
provider/domain package is evidence and configuration input; package presence
does not activate a route, choose a principal, or define an API client.

Direct-provider and intermediary policies may differ, but both call the same
Core intake contract and fail closed when route identity, enabled policy,
principal, or mandatory evidence is missing. The [capability
inventory](capabilities.md) owns the exact targets for additional-provider
routes and provider APIs.

### Provider API principal and contract boundary

The accepted provider-API security boundary is the stable Pegasus principal,
not an email domain or general external tenant. A provider client receives a
separately issued principal-scoped client ID and opaque secret; only the secret
hash is stored, and rotation and revocation are supported. The client may
submit instructions/attachments idempotently and retrieve only that
principal's own receipt, processing status, and resulting Case/PO. It receives
no staff access, general case search/read, or case-workflow mutation.

Provider operations use the same Core intake and authorization policies as Web
and Worker callers. Receipt, submission, status, result, source-custody, and
idempotency identities remain distinct per principal, and the provider client
is the attributable action actor. Cross-principal query or result disclosure
fails closed.

**Source limitation:** the accepted sources do not define an external tenant
model, exact routes, headers, schema, attachment encoding, request limits,
throttling policy, administration UI, or a Pegasus identity/field named
`provider_domain_key`. No allowed source proves an owner or current/predecessor
consumer for that name. Pegasus therefore does not create, migrate, map, alias,
or retire it. Any later proposal must first establish authoritative source and
consumer evidence, stable-principal/route/provenance mapping, collision and
unknown handling, cutover, rollback, retention, and explicit retirement proof
through the separate [open
decision](open-decisions.md#external-data-submission-and-report-contracts);
none may be inferred from provider-domain evidence.

No provider route is active until its exact capability allocation, accepted
contract, credentials/scopes, failure and recovery proof, real caller, and
operator acceptance exist.
## MCP automation and actor boundary

MCP is a management/development-controlled ingress for one named,
vendor-neutral Automation Actor, not an ordinary staff interface. Ordinary
staff have no MCP access and use the Web UI. The Actor invokes only its approved
ordinary operational Core-action inventory with its own authentication,
identity, and permanent history; it has no Administrator, configuration,
credential, cloud, release, deletion, or other management authority.

An externally scheduled automation client may scan an approved network-drive
scope and submit immutable source occurrences through its approved MCP
document-action inventory. Claude Desktop may provide the initial accepted
client evidence without owning the durable actor identity or Core action. The
client, schedule, and filesystem remain outside Pegasus; custody begins only
with an authenticated accepted MCP submission. Each occurrence follows ordinary
source-occurrence, idempotency, matching, classification, and action-history
policy. Scanning neither associates material nor allocates a Case or reference.

MCP registration, a tool schema, or an endpoint file is not proof. Each tool
requires an exercised real caller, expected success result, authorization
failure, validation failure, and action-history proof.

Background automation follows the same rule. Queues and timers transport stable
work identities; Core owns transitions and idempotency. Poison work remains
recoverable and observable. No AI proposal or workspace service can mutate case
state directly.

## Reports, correspondence, and reviewed proposals

Reports are produced from accepted case facts and source-labelled evidence
through the approved renderer boundary. Renderer source workspaces remain
independent source imports until an accepted integration contract and real
application caller exist.

### Report correction, finality, and post-report work

**Accepted report boundary:** an issued report has an immutable artifact/version identity and hash. A
correction or addendum creates a new reasoned version and retains every earlier
artifact, accepted fact, actor, time, and source; it never silently overwrites
the issued report. A closed case must be reasonedly reopened before its report
or evidence is revised.

The report-sent business event is the exact approved-mailbox Sent-item evidence
defined above and remains final if Outlook later moves or deletes the item.
Outlook `sentDateTime` remains the business time; discovery and link times are
not substitutes. Report sent enters post-report work rather than closing the
case. A Box report PDF, file upload, generated artifact, draft, queue result, or
staff assertion alone proves neither sending nor external receipt.

Post-report queries, disputes, amendment requests, and replies remain
case-owned correspondence with source/reply-chain identity and permanent
history. Collision Engineers' Engineer responds to them, but the exact
CASE-23 states, transitions, correction/reopen interaction, due/chaser
interaction, and closure rules remain `Next`/unallocated and unresolved; no
mailbox adapter may invent them or create a new case/reference. See [external
data, submission, and report
contracts](open-decisions.md#external-data-submission-and-report-contracts).

Requirements:

- deterministic template and payload versioning;
- preserved document/source provenance;
- authorised human review and approval of report facts and content before
  issue, without inventing a separate case-lifecycle pre-send review gate;
- immutable issued artifact identity and hash;
- correction/addendum rather than silent overwrite;
- exact delivery evidence where the workflow requires it;
- accessible staff presentation of status, validation, and failure without
  implying an unproved external delivery.

### Targeted sending and reviewed AI proposals

An allocated targeted report-send transaction is idempotent and records
approved destinations, immutable artifact/version, Box filing, exact send
evidence, completion outcome, and partial-failure recovery. A correction does
not silently alter an issued fee note or invoice; later financial impact uses
its own versioned, authorised contract. Staff-selected AI Assessor and
Engineer-reviewed query proposals remain proposals until the authorised human
accepts or rejects them through Core.

Durable AI proposal work has stable request, lease, evidence, proposal-version,
and human-disposition identities. Stale work cannot overwrite a newer
case/evidence version; expiry/retry is idempotent; no AI caller mutates,
approves, or sends autonomously.

Signatures embedded in governed renderer documents are provenance-sensitive
document assets, not Web decorative imagery.
## Operator experience

The selected alpha direction is Operations-first. The UI must provide:

- an authenticated office-wide dashboard with Europe/London day boundaries and
  Monday-to-Monday weeks;
- actionable receiving, requests, Triage, case, query, and exception queues;
- intake-evidence filters with exact options `All`, `Instructions`, and
  `Images`;
- clear counts that link to their exact filtered work and do not render stale
  zero placeholders;
- list/detail journeys for intake, source evidence, Triage, cases, documents,
  history, and exports;
- supporting-detail navigation from Intake or Case detail that neither commits nor discards the current form and returns to the same detail context, evidence selection, position, and unsaved edits;
- administration for authorised accounts, roles, access, organisations,
  principals, configuration, and mailboxes;
- exact state labels mapped to Core decisions;
- loading, empty, current, stale, unavailable, partial, failed, validation,
  conflict, and access-denied states;
- keyboard, pointer, screen-reader, 200% zoom, forced-colour, and reduced-motion
  support;
- responsive use without hiding required evidence or actions.

Every actionable search result is a full-row keyboard-focusable link or button with visible action affordance. At constrained desktop width, a long Case/PO or Image Intake Reference moves to a labelled second line instead of overlapping the received timestamp. Inbox and intake rows always show received date above received time, and show the precise processing outcome—such as `Case created`, `Image intake registered`, `Associated with Case`, `Needs sorting`, or `Blocked intake`—rather than a generic `New`. One semantic action or state has one consistent icon across Pegasus; no decorative or generated replacement icon is used.

### Dashboard freshness and reconciliation

Every count and query exposes its last successful update time and current
refresh state. `0`, loading, current, stale-with-last-good-time, partial,
unavailable, and failed are distinct outcomes. A refresh never replaces a
last-good value with a false zero, merges partial data into an apparently
complete result, or implies that an external action succeeded.

Manual refresh reruns the same exact filtered query; it does not change policy
or create a business transition. Its caller, start/end time, sources, and
success/partial/failure result remain auditable in content-safe telemetry.
Reconciliation that accepts, rejects, links, or changes an external business
fact instead enters permanent business history with the responsible actor,
source/version, before/after values, time, and reason where required.

`New cases today` counts every instructed Case created in the current Europe/London calendar day, including a Case later closed that day. It excludes pre-Case Image intakes, Triage, `Needs sorting`, and `Blocked intake`. It is separate from `Due today`, `Sent to Engineer`, and `Reports sent`.

`Due by` and overdue/chaser work remain a separate operational view from `New cases today`. The case list and persistent case identity area expose due/overdue state, while the case workspace keeps the missing-material reason, next chase, last recorded outcome, and next permitted action together. Triage has no due/chaser presentation.

The UI never infers state from colour alone, never uses decorative glyphs as
unlabeled controls, and never presents draft, queued, attempted, allocated, or
configured work as completed, delivered, deployed, or accepted.

The durable interaction, visual, component, and source/runtime rules are owned
by [design](../design/README.md).
## Quality, capacity, security, and evidence

Pegasus is designed for the observed office workload of roughly 1,000–1,200 matters per month and a 2,000-per-month capacity target. These are observed workload and design capacity, not throughput proof.

Required qualities:

- deterministic, bounded, cancellable processing;
- least privilege and fail-closed authorization;
- encrypted transport and protected storage appropriate to the data boundary;
- resolved and recorded retention rules for personal data and vehicle images before activating each external flow; this does not create an automated retention workflow;
- confirmation of applicable processor terms before activating any external email, upload, AI, Box, or other external processing;
- no secrets in source, logs, proof artifacts, URLs, or client-rendered configuration;
- immutable source and action provenance;
- structured diagnostics without source-content leakage;
- a 15-minute database recovery-point objective and four-hour restoration objective, proved through the operator-run [production recovery procedure](operations.md#production-recovery) (OPS-09 — deferred; gates no release);
- reasoned recovery, restore, and replay proof without duplicate case/reference allocation;
- local development on a supported platform, and supported-browser accessibility proof on Windows with Microsoft Edge Stable and Narrator;
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
| additional mailboxes and classification | mailbox/source/message/occurrence identity; provider/domain route identity | live Graph caller, automated application of the settled taxonomy, mailbox mutation | accepted rule predicates and holdout, exact mailbox/folder scopes, test mailbox, Worker caller, recovery, and operator acceptance |
| scanned-document OCR | source hash, scan-like decision, page/image provenance | OCR service, flag, route, fallback | accepted OCR slice, provider/licensing/security decision, genuine cohort evaluation, caller and recovery proof |
| provider APIs | intake command, source/correlation/idempotency identity | endpoint, credentials, retry client, activation | provider contract, credential/scopes, failure/recovery, real caller and acceptance |
| EVA API/replacement | manual handoff identity and payload version | network adapter or replacement workflow | vendor access, mapping, auth, idempotency, current-version handling, caller and acceptance |
| guided capture and vehicle data | request/source/vehicle fact provenance | live vendor route, OCR lookup, auto-acceptance | vendor contract, confidence/human confirmation rule, data-age/source policy, failure/recovery and evaluation |
| automated correspondence/chasing | action, channel, party, draft and delivery-evidence identities | autonomous send or completion | allocation, approved channel policy, exact send scopes, pre-send approval and delivery proof |
| AI assistance | typed evidence/proposal/review identity | direct mutation, approval, business policy, shared AI usage ledger | accepted Core proposal port, representative evaluation, abstention/challenge gates, human approval, caller proof, and capability-specific capacity measurement |
| Diminution, Commercial, post-report dispute and finance | stable case/work/document/action identities | dormant case types, calculations, invoicing/accounting routes | allocated release, accepted Core contract, source/provider decisions, UI/caller and operator acceptance |
| production deployment and migration | versioned schema/release/evidence identities | provisioning, deployment, predecessor deletion or data migration | exact target approval, validated IaC, migration/rollback plan, deployed caller proof and acceptance |

No irreversible choice is made merely to reserve a seam. New top-level projects, stores, runtimes, migration streams, or deployment units require an accepted ADR proving the existing boundary cannot carry the work.

## Delivery dependencies

This section owns release precedence. The complete source-labelled dependency
graph, including `Next` parallel branches and `Later` independent gates, is
retained in the dependency-ordered delivery roadmap (git history); the operator execution route is
[Release dependency order](operations.md#release-dependency-order). Those
routes preserve detail and procedure without becoming requirements, allocation,
implementation-status, or acceptance owners.

The alpha delivery order is dependency-bound: relational draft and trusted actors; identity/history/Administrator data; durable source custody and ordinary-image vehicle identity; reference allocation; one definitive acceptance transaction; Box custody; exclusive editing; lifecycle and work scheduling; Operations-first UI; real Graph Worker; Triage; vehicle/EVA handoff; then operator acceptance. Blocking status for each allocated capability is owned by [capabilities](capabilities.md); the Automation MCP and the recovery proof are allocated but non-blocking for acceptance. A later step never treats an allocated file, registration, or green structural check as caller proof.

Deferred provider, mailbox, post-report, finance, AI, external-account, and replacement branches may progress only after their capability activation gate. They rejoin the main route through the same Core identity, authorization, custody, idempotency, history, recovery, caller, and acceptance evidence; they do not create parallel policy owners.

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
