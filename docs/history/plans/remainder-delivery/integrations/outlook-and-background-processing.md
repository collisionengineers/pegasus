# Outlook and background processing

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Ready `0.1.0-alpha.1` intake/evidence plan — `Next`/`unallocated` four-mailbox management separate**

## Purpose

Receive `0.1.0-alpha.1` staff-forwarded instructions from the approved `instructions@` mailbox through the Worker and preserve exact Outlook Sent-item evidence. `Next`/`unallocated` four-mailbox management remains a separate expansion of the same Core policy.

## Feature coverage

Primary feature ownership is: `INT-02`, `INT-03`, `MAIL-14`, `MAIL-15`, and
`MAIL-16`. `INT-02` and `INT-03` are the scoped `0.1.0-alpha.1` receipt path and forwarded
provenance; `MAIL-14` and `MAIL-15` cover exact Sent-item evidence and its
reasoned correction; `MAIL-16` is the separately gated automatic exact-item
match. `Next`/`unallocated` mailbox workspace, queues, moves and association have a different
primary plan.

## Authority and current boundary

- **Authority:** [remaining requirements](../../../../product/qdos-alpha-gap.md#3-complete-intake-formats-and-paths), [ADR-0002](../../../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md#outlook-ingestion), and the settled exact-Sent-item/shared-mailbox-allowlist decisions.
- **Policy owner:** `ProcessIntake` remains the single provider-neutral Core intake use case; Worker translates polling/queue delivery only, and the contained QDOS policy owns only QDOS instruction extraction.
- **Current implementation:** Worker composition contains no intake trigger; Graph adapter, cursor, queue handler and mailbox caller are absent.
- **Real callers:** Development `/Intake/Upload` only. The intended caller is a thin isolated Functions timer/queue path.
- **Persistence/adapters:** current SQL receipt store exists; Graph immutable IDs/cursor, outbox and queue processing are planned.
- **Dependencies:** durable source staging/identity and one shared approved-mailbox allowlist consumed by every Outlook read caller. Automatic categorisation/acceptance additionally requires the [combined mailbox categorisation and email-matching research](../../../../product/open-decisions.md#mailbox-categorisation-and-all-email-matching-research), authenticated actor policy and case acceptance. Automatic Sent-item matching remains deferred to that combined research package.
- **Replaces/consolidates:** no background service in Web and no parallel Graph classifier.

## Shared failure and observability rules

Queue messages carry identifiers only. Store attempts, mailbox/folder identity, delta cursor and idempotency in SQL; correlate Graph receipt, outbox and custody events without message content. Bounded transient failures retry; cursor loss, scope violation and permanent failures stop visibly/poison rather than reclassifying or replaying a business action.

## Scoped inbound Outlook receipt and processing

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** `0.1.0-alpha.1` automatic intake is staff-forwarded work in the `instructions@collisionengineers.co.uk` Inbox. Preserve forwarded provenance and never classify solely from the staff envelope sender. All Outlook readers use one approved-mailbox allowlist. The other three shared mailboxes and direct four-mailbox classification are `Next`/`unallocated`. [Microsoft's Exchange Application RBAC guidance](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac) remains execution-time authority.
- **Confirmed facts:** Graph, Exchange RBAC, mailbox cursor and Worker caller are not implemented. Outlook associations live in CollisionSpike; Outlook is not moved/categorised.
- **Decision required before implementation:** populate and approve the shared mailbox allowlist and exact permitted folder/action set for each enabled environment. Shared development requires a confirmed non-production mailbox plus its exact Inbox folder; production automatic intake is `instructions@collisionengineers.co.uk` plus its exact Inbox. If no non-production scope is supplied, shared-development live Graph testing remains withheld rather than using production.

The mailbox-policy dossier gates only categorisation and automatic-matching
slices: an automatic instruction categorisation/acceptance decision and
`MAIL-16` exact-item matcher (and the related Triage matcher). It does not
turn receipt, immutable identity, cursor recovery, forwarded provenance, or
the settled manual report-evidence correction path into a blocked classifier
project. Exact Graph scope and live enablement remain separate external
approval gates.

### Owner and dependencies

- **Policy/implementation owner:** Core intake owner; Infrastructure owns one Graph adapter; Worker owner owns thin trigger/queue translation.
- **Independent evaluator:** test engineer then reviewer.
- **Prerequisites:** durable source identity/custody, outbox, migration stream and approved least-privilege Graph configuration.
- **Consumers/unlocks:** inbox workbench, automatic definitive case creation through the shared Core acceptance transaction, case association and manual follow-up.

### Caller, contract and change boundary

- **Real or intended caller:** planned one-minute Worker delta-poller for the authorised Inbox and receipt handler calling `ProcessIntake`; once the separate category and acceptance dependencies are present, that Core flow may hand definitive `Receiving work` to `AcceptCaseDraft` rather than stopping at a draft.
- **Input/output:** the environment's verified exact mailbox/Inbox pair plus immutable message/attachment identity yields one durable intake receipt, processing status and operator-visible inbox item; a definitive authorised new instruction additionally yields exactly one case/reference outcome from the shared Core transaction.
- **Ordered decisions and failure behavior:** verify Exchange RBAC is no broader than the shared approved-mailbox allowlist and that no unscoped Entra Graph application mail grant exists; reject any mailbox/folder/message/action outside the allowlist before a Graph call; enforce the exact Inbox in the intake adapter because Exchange RBAC scopes mailboxes, not folders; request immutable IDs; persist cursor only after every returned page commits; replay by mailbox+immutable item identity; uncertain association goes to `Needs sorting`.
- **Persistence/migration:** one cursor/receipt/attempt authority and outbox linkage; one database claim/version prevents overlapping timer instances from advancing the same delta cursor, and abandoned claims expire visibly for recovery. No mailbox data appears in queue payloads or a second delivery ledger.
- **Adapters/side effects:** Graph delta polling through the Exchange RBAC `Application Mail.Read` assignment scoped to the one mailbox, with no parallel Entra Graph application mail consent; the adapter separately confines operations to Inbox. Attachment bytes follow the custody plan.
- **Operator surface and observability:** this Worker task surfaces received items and terminal/retry state with content-free cursor/429/poison telemetry. Staff read/search/preview/download/associate/unassociate/open-in-Outlook actions require a separate authenticated Web task after category/correction policy is settled.
- **Documentation affected:** Graph scope/approval record and operations guidance; operator notes are read-only.
- **Replaces/consolidates:** no Web-hosted poller, webhook, mailbox move or category update.

### Scope

- **Included:** the `0.1.0-alpha.1` `instructions@` Inbox poller, immutable identity, bounded idempotent receipt/recovery, the `0.0.0-development`-proved instruction classifier, and exact Sent-item lookup. The allocated `0.1.0-alpha.1` automatic report and Triage matchers are added only after their predicates are accepted.
- **Excluded:** policy inside the transport, the `Next`/`unallocated` four-mailbox workspace/moves/categories/general association, unapproved mailboxes/folders, `Mail.Send`, webhooks, automated outbound messages, and WhatsApp ingestion. `0.1.0-alpha.1` automatic case creation is not excluded.

### Withheld categorisation architecture

The direct decision is that long-term mailbox categorisation is a major architectural scope whose approved rules must be extensible and modifiable. It remains one Core-owned policy consumed by Web, Worker, provider API and MCP; Graph and other channel adapters only supply source identity and evidence. An accepted design must retain each decision in permanent action history with policy version and evidence, support correction without rewriting source history, and fall back conservatively to `Needs sorting` when no unambiguous rule applies.

The [category predicates and governance](../../../../product/open-decisions.md#mailbox-categorisation-and-all-email-matching-research) are not yet settled, so no categorisation implementation task is emitted. Deliberately absent are a generic rule engine, expression language, rule/configuration table, authoring UI, dynamic compiler, dormant evaluator, feature flag, and second classifier. Choosing runtime-managed rules or another new architectural boundary requires an accepted ADR. `QdosInstructionExtractionPolicy` assesses QDOS instruction content only and must not be treated as the provisional mailbox classifier.

Once the `0.0.0-development`/`0.1.0-alpha.1` instruction predicates are accepted, an identified authorised instruction invokes the shared definitive predicate and atomic acceptance transaction. Known principal/code, VRM, unambiguous case type, safe complete processing, and no identity/association conflict are required; standalone Audit also requires the original report's clear assessment. Missing non-identity details create `Not ready`. Non-instructions, uncertain items, `Blocked intake`, and Triage never call the allocator through this path. Detailed `Receiving work`/`Queries`/`Other` routing is `Next`/`unallocated`.

### Exact Sent item report evidence

`Report sent` is a stable case action evidenced by one exact Outlook Sent item from a mailbox in the shared approved allowlist. When automatic matching is absent or ambiguous, the first slice is an authenticated case-workspace action available to every staff role: staff select the exact immutable item and enter a reason, the Outlook adapter re-reads it within the permitted Sent folder, and Core records mailbox, immutable item ID, authoritative Outlook `sentDateTime`, separate discovery/link times, actor, reason and case relationship in permanent action history. It counts every successfully sent report and does not add a pre-send review gate. Any staff role may unlink/relink with a reason; Core recomputes dependent report events and dashboard counts without deleting prior history. Once confirmed, a report-sent event remains final if Outlook later moves or deletes the item.

The `0.1.0-alpha.1` automatic exact-item candidate/match predicates remain unbuilt pending the combined mailbox/email research package. The settled manual link contract handles absent/ambiguous automatic evidence and reasoned correction in the same `0.1.0-alpha.1` release. A fabricated identifier, mismatched case, contradictory immutable identity, item outside the allowlist/folder, missing required reason, or item that cannot be re-read before initial confirmation is refused with no lifecycle mutation. Exact evidence proves that the item existed in the approved Sent folder at confirmation; it does not prove recipient delivery, reading, content correctness or a correct automatically inferred case match.

### Exact Triage reply evidence

The planned authenticated [Triage workflow](../casework/triage-workflow.md) completion caller asks Core to find the exact reply-chain Sent item through this same allowlisted Outlook adapter. Unlike report evidence, Triage has no manual-item-selection fallback: subject or registration similarity is insufficient. The combined research remains the sole owner of the automatic reply-chain candidate/match predicate; once accepted, the adapter supplies immutable mailbox/item identity and `sentDateTime`, while Core alone decides completion. Missing, ambiguous, contradictory or out-of-scope evidence leaves Triage uncompleted and visible. No Outlook move, category or send occurs.

### Implementation checklist

- [ ] Add a typed Graph adapter and durable cursor/receipt/outbox contracts after Core stabilises.
- [ ] Add thin Worker triggers that pass only identifiers to Core receipt processing.
- [ ] After the accepted category policy and case transaction land, wire the Worker through the combined Core receive/process/automatic-acceptance flow; do not stop definitive `Receiving work` at a draft or add a Worker-specific allocator.
- [ ] Configure a separate identity and Exchange Application RBAC assignment for each enabled environment as the sole mail grant, verify no unscoped Entra Graph application mail permission remains, and enforce its exact mailbox/Inbox pair before Graph calls.
- [ ] After the combined research accepts the `0.1.0-alpha.1` exact report predicate, make inbound polling and Sent-item lookup consume the same approved-mailbox allowlist; add the automatic matcher plus all-role guarded reasoned link/unlink/relink fallback through the case workspace, with no pre-send review gate.
- [ ] After the combined research accepts the exact reply-chain predicate, wire the planned Triage completion caller through the same adapter/allowlist; prove subject/registration/manual-selection fallbacks make zero completion calls.

### Validation checklist

- [ ] Test paging, duplicate/replay, cursor loss, 429/transient retry, poison handling and permanent failure with no duplicate case/reference.
- [ ] Prove a definitive `Receiving work` item automatically creates exactly one `Not ready` case/reference, while Query/Other/uncertain/blocked items and the separate `Triage` workflow make zero allocator calls through this category path; replay and concurrent delivery return the original result.
- [ ] Test overlapping timer claims and abandoned-owner recovery; only one committed page may advance a cursor version.
- [ ] Prove arbitrary mailbox/folder/message IDs are denied with zero Graph-client call; verify the service principal cannot access a second mailbox and the adapter refuses a non-Inbox folder in the authorised mailbox.
- [ ] Prove explicit association of one exact approved-folder Sent item records one report event at `sentDateTime`; discovery/link times remain separate, replay is idempotent, every later successfully sent report can record another event, and fabricated/out-of-allowlist/mismatched/contradictory items or missing reason record none.
- [ ] Prove any role may unlink/relink with a reason, dependent events/dashboard counts recompute, prior history remains, and a post-confirmation Outlook move/delete does not reverse the confirmed event.
- [ ] Prove Triage exact reply-chain success/absence/ambiguity/contradiction and unapproved-mailbox denial through the planned Web-to-Core-to-Outlook evidence caller; this does not prove recipient receipt.
- [ ] Exercise the actual Worker caller against the explicitly approved environment mailbox/Inbox pair using approved non-corpus input. If no non-production shared-development pair exists, keep live Graph disabled until an exact production-Inbox smoke is separately approved; never use production as a negative test.
- [ ] Run independent review and `pwsh ./scripts/Invoke-RepoCheck.ps1`, recording exit result and scope limits.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Same immutable item redelivered | one receipt/business outcome | worker/persistence integration test | live Exchange reliability |
| Definitive authorised new instruction | one automatic `Not ready` case/reference through Core | Worker caller and transaction test | operator acceptance of extraction accuracy |
| Non-instruction or uncertain item | visible inbox outcome and zero reference allocation | negative Worker caller test | future correction policy |
| Cursor/page failure | cursor does not skip uncommitted item; visible retry/poison state | fault-injection test | vendor recovery |
| Unauthorised mailbox or non-Inbox folder | scoped denial before Graph call; live permission test denies a second mailbox | adapter negative fixture plus approved RBAC test | future Exchange policy/cache behavior |
| Exact approved Sent item explicitly associated | one report-sent action-history entry and lifecycle outcome; no pre-send review gate | case-workspace-to-Graph/Core integration test | recipient delivery, report correctness or the separate automatic-matcher caller |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** for each environment, approve the service principal, mailbox allowlist and exact folder/action set; create only the matching Exchange RBAC `Application Mail.Read` assignment and remove/verify absence of any unscoped Entra Graph application mail grant. Production automatic intake is limited to `instructions@collisionengineers.co.uk` Inbox; Sent-item evidence is limited to explicitly approved allowlisted mailbox/Sent-folder reads; no `Mail.Send` or `Mail.ReadWrite`.
- **Rollout/activation:** deploy inactive Worker; prove local out-of-pair denials; approve the exact environment pair/smoke; enable exactly one poller for that pair; monitor cursor, queue age and poison items.
- **Rollback/recovery:** pause claims/poller, retain cursor/receipts for deterministic replay, then redeploy prior artifact; do not alter Outlook messages.
- **Irreversible risk:** mailbox read is external data access; it needs exact approval and no corpus transfer.

### Deferred-capability impact

- **Named capabilities:** `0.1.0-alpha.1` automatic exact report/Triage matching; `Next`/`unallocated` broader Outlook intake, association, and email management; `Later`/`unallocated` WhatsApp/chasers; `Later`/`unallocated` automatic reports.
- **Stable seam retained:** shared approved-mailbox identity, folder/action scope, immutable item identity and the single `ProcessIntake` use case; extraction policy, mailbox category and associations remain separate application-owned decisions.
- **Future migration/replacement:** each added mailbox/folder and any matcher, webhook or sending workflow needs separate consent, policy and evidence.
- **Activation boundary:** explicit exact-scope mailbox approval and live caller evidence.
- **Deliberately absent:** tenant-wide scope and unapproved folders; `Next`/`unallocated` mailbox management, `Later`/`unallocated` WhatsApp/chaser sender, and `Later`/`unallocated` report sender remain absent from `0.1.0-alpha.1`. The allocated `0.1.0-alpha.1` exact matchers remain withheld only until their research predicates are accepted.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | planning review | `0.1.0-alpha.1` Graph boundary and recovery are specified | adapter, RBAC, Worker caller, live mailbox evidence or acceptance |
