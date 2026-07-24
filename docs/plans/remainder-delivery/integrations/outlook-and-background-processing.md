# Outlook and background processing

## Purpose

Receive first-MVP inbound instructions from the authorised mailbox through the Worker without widening mailbox access, duplicating Core intake policy, or inferring report-sent evidence.

## Authority and current boundary

- **Authority:** [remaining requirements](../../remaining-requirements.md#3-complete-intake-formats-and-paths), [ADR-0002](../../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md#outlook-ingestion), and [open decision](../../open-decisions.md#authoritative-sent-report-evidence-and-time).
- **Policy owner:** `ProcessIntake` remains the single provider-neutral Core intake use case; Worker translates polling/queue delivery only, and the contained QDOS policy owns only QDOS instruction extraction.
- **Current implementation:** Worker composition contains no intake trigger; Graph adapter, cursor, queue handler and mailbox caller are absent.
- **Real callers:** Development `/Intake/Upload` only. The intended caller is a thin isolated Functions timer/queue path.
- **Persistence/adapters:** current SQL receipt store exists; Graph immutable IDs/cursor, outbox and queue processing are planned.
- **Dependencies:** durable source staging/identity. Automatic categorisation/acceptance additionally requires the [mailbox categorisation decision](../../open-decisions.md#mailbox-categorisation-and-correction), authenticated actor policy and case acceptance.
- **Replaces/consolidates:** no background service in Web and no parallel Graph classifier.

## Shared failure and observability rules

Queue messages carry identifiers only. Store attempts, mailbox/folder identity, delta cursor and idempotency in SQL; correlate Graph receipt, outbox and custody events without message content. Bounded transient failures retry; cursor loss, scope violation and permanent failures stop visibly/poison rather than reclassifying or replaying a business action.

## Scoped inbound Outlook receipt and processing

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** first-MVP intake is only the `instructions@collisionengineers.co.uk` Inbox. [Microsoft's Exchange Application RBAC guidance](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac) is the execution-time authority: Exchange RBAC supplies the resource-scoped `Application Mail.Read` grant, and no unscoped Microsoft Entra Graph application `Mail.Read` grant may coexist because grants are additive.
- **Confirmed facts:** Graph, Exchange RBAC, mailbox cursor and Worker caller are not implemented. Outlook associations live in CollisionSpike; Outlook is not moved/categorised.
- **Decision required before implementation:** name and approve one exact mailbox/folder pair for each enabled environment. Shared development requires a confirmed non-production mailbox plus its exact Inbox folder; production is `instructions@collisionengineers.co.uk` plus its exact Inbox. If no non-production pair is supplied, shared-development live Graph testing remains withheld rather than using another production folder.

### Owner and dependencies

- **Policy/implementation owner:** Core intake owner; Infrastructure owns one Graph adapter; Worker owner owns thin trigger/queue translation.
- **Independent evaluator:** test engineer then reviewer.
- **Prerequisites:** durable source identity/custody, outbox, migration stream and approved least-privilege Graph configuration.
- **Consumers/unlocks:** inbox workbench, automatic definitive case creation through the shared Core acceptance transaction, case association and manual follow-up.

### Caller, contract and change boundary

- **Real or intended caller:** planned one-minute Worker delta-poller for the authorised Inbox and receipt handler calling `ProcessIntake`; once the separate category and acceptance dependencies are present, that Core flow may hand definitive `Receiving work` to `AcceptCaseDraft` rather than stopping at a draft.
- **Input/output:** the environment's verified exact mailbox/Inbox pair plus immutable message/attachment identity yields one durable intake receipt, processing status and operator-visible inbox item; a definitive authorised new instruction additionally yields exactly one case/reference outcome from the shared Core transaction.
- **Ordered decisions and failure behavior:** verify the environment-specific Exchange RBAC mailbox scope and absence of an unscoped Entra Graph application mail grant; reject any mailbox/folder/message outside the configured pair before a Graph call; enforce that environment's exact Inbox in the adapter because Exchange RBAC scopes mailboxes, not folders; request immutable IDs; persist cursor only after every returned page commits; replay by mailbox+immutable item identity; uncertain association goes to `Needs sorting`.
- **Persistence/migration:** one cursor/receipt/attempt authority and outbox linkage; one database claim/version prevents overlapping timer instances from advancing the same delta cursor, and abandoned claims expire visibly for recovery. No mailbox data appears in queue payloads or a second delivery ledger.
- **Adapters/side effects:** Graph delta polling through the Exchange RBAC `Application Mail.Read` assignment scoped to the one mailbox, with no parallel Entra Graph application mail consent; the adapter separately confines operations to Inbox. Attachment bytes follow the custody plan.
- **Operator surface and observability:** this Worker task surfaces received items and terminal/retry state with content-free cursor/429/poison telemetry. Staff read/search/preview/download/associate/unassociate/open-in-Outlook actions require a separate authenticated Web task after category/correction policy is settled.
- **Documentation affected:** Graph scope/approval record and operations guidance; operator notes are read-only.
- **Replaces/consolidates:** no Web-hosted poller, webhook, mailbox move or category update.

### Scope

- **Included:** one explicitly configured mailbox/Inbox pair per environment, inbound delta polling, immutable identity, bounded idempotent receipt and recovery. Items remain visibly unclassified until the category policy is accepted. The category policy and acceptance transaction are implemented in their owning Core tasks, but this Worker caller must integrate them before first-MVP completion.
- **Excluded:** implementing the category policy or acceptance transaction inside this transport task, in-app mail management/association, other mailboxes/folders, Sent Items, `Mail.Send`, `Mail.ReadWrite`, webhooks, mail moves/categories, automated outbound messages and WhatsApp ingestion. Automatic case creation itself is not excluded from the first MVP.

### Withheld categorisation architecture

The direct decision is that long-term mailbox categorisation is a major architectural scope whose approved rules must be extensible and modifiable. It remains one Core-owned policy consumed by Web, Worker, provider API and MCP; Graph and other channel adapters only supply source identity and evidence. An accepted design must make each decision auditable by policy version and evidence, support correction without rewriting source history, and fall back conservatively to `Needs sorting` when no unambiguous rule applies.

The [category predicates and governance](../../open-decisions.md#mailbox-categorisation-and-correction) are not yet settled, so no categorisation implementation task is emitted. Deliberately absent are a generic rule engine, expression language, rule/configuration table, authoring UI, dynamic compiler, dormant evaluator, feature flag, and second classifier. Choosing runtime-managed rules or another new architectural boundary requires an accepted ADR. `QdosInstructionExtractionPolicy` assesses QDOS instruction content only and must not be treated as the provisional mailbox classifier.

Once that mandatory first-MVP decision is settled, `Receiving work` invokes the same Core definitive predicate and atomic acceptance transaction as other authorised channels. Known principal/code, VRM, unambiguous case type, safe complete processing and no identity/association conflict are required; standalone Audit also requires the original report's unambiguous assessment. Missing non-identity details create a `Not ready` case. Queries, Other, Triage, uncertain items and staff-selected `Blocked intake` never call the allocator. The Worker records an automation actor and policy evidence; it does not impose a manual approval gate on every definitive instruction.

### Implementation checklist

- [ ] Add a typed Graph adapter and durable cursor/receipt/outbox contracts after Core stabilises.
- [ ] Add thin Worker triggers that pass only identifiers to Core receipt processing.
- [ ] After the accepted category policy and case transaction land, wire the Worker through the combined Core receive/process/automatic-acceptance flow; do not stop definitive `Receiving work` at a draft or add a Worker-specific allocator.
- [ ] Configure a separate identity and Exchange Application RBAC assignment for each enabled environment as the sole mail grant, verify no unscoped Entra Graph application mail permission remains, and enforce its exact mailbox/Inbox pair before Graph calls.

### Validation checklist

- [ ] Test paging, duplicate/replay, cursor loss, 429/transient retry, poison handling and permanent failure with no duplicate case/reference.
- [ ] Prove a definitive `Receiving work` item automatically creates exactly one `Not ready` case/reference, while Query/Other/Triage/uncertain/blocked items make zero allocator calls; replay and concurrent delivery return the original result.
- [ ] Test overlapping timer claims and abandoned-owner recovery; only one committed page may advance a cursor version.
- [ ] Prove arbitrary mailbox/folder/message IDs are denied with zero Graph-client call; verify the service principal cannot access a second mailbox and the adapter refuses a non-Inbox folder in the authorised mailbox.
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

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** for each environment, approve the service principal, exact mailbox/Inbox pair and action; create only its Exchange RBAC `Application Mail.Read` assignment and remove/verify absence of any unscoped Entra Graph application mail grant. Production is limited to `instructions@collisionengineers.co.uk` Inbox; no other folder, `Mail.Send` or `Mail.ReadWrite`.
- **Rollout/activation:** deploy inactive Worker; prove local out-of-pair denials; approve the exact environment pair/smoke; enable exactly one poller for that pair; monitor cursor, queue age and poison items.
- **Rollback/recovery:** pause claims/poller, retain cursor/receipts for deterministic replay, then redeploy prior artifact; do not alter Outlook messages.
- **Irreversible risk:** mailbox read is external data access; it needs exact approval and no corpus transfer.

### Deferred-capability impact

- **Named capabilities:** broader Outlook intake, Sent Items reconciliation, WhatsApp and automated outbound messages.
- **Stable seam retained:** channel/source identity and the single `ProcessIntake` use case; extraction policy, mailbox category and associations remain separate application-owned decisions.
- **Future migration/replacement:** each mailbox/folder and any webhook/sending workflow needs separate consent, policy and evidence.
- **Activation boundary:** explicit exact-scope mailbox approval and live caller evidence.
- **Deliberately absent:** tenant-wide scope, other mailbox adapter, webhook subscription, outbound sender, WhatsApp adapter, mailbox move/category feature.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | planning review | first-MVP Graph boundary and recovery are specified | adapter, RBAC, Worker caller, live mailbox evidence or acceptance |
