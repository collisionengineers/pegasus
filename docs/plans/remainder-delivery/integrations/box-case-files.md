# Box case files

Status: **Ready V1 plan — external writes remain separately approval-gated**

## Purpose

Make Box the long-term original-file custody store while proving every operation stays inside an exact subtree approved immediately before the call.

## Feature coverage

Primary feature ownership is: `DOC-01`, `DOC-02`, `DOC-03`, `DOC-04`,
`DOC-05`, `DOC-06`, `DOC-07`, and `EXT-14`. The first seven are scoped Box
custody/file actions; `EXT-14` is only manually received WhatsApp material as
V1 document evidence. It is not an automated WhatsApp channel, inbox,
association engine, or external adapter.

## Authority and current boundary

- **Authority:** [remaining requirements](../../remaining-requirements.md#6-box-vehicle-data-eva-and-email) and [ADR-0002](../../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md#files-box-and-document-processing). No exact development root is authorised by this plan.
- **Policy owner:** Core custody/document use case; Infrastructure owns one guarded Box adapter.
- **Current implementation:** no Box SDK, adapter, registration, persisted Box identity or production caller exists.
- **Real callers:** none; Web/Worker/provider/MCP are planned callers through Core only.
- **Persistence/adapters:** Box must be authoritative for original files; SQL owns workflow, relationships, permanent action history and persisted Box identifiers.
- **Dependencies:** durable source receipt/custody hand-off and case acceptance.
- **Replaces/consolidates:** the ignored local store only after confirmed custody; no Box search/webhook second path.

## Shared failure and observability rules

Unknown/missing ancestry, unsupported operation, or root type/name mismatch is terminal `Box scope violation`: make no Box-client call and retain durable staging with an operator-visible reason. Before acceptance, this prevents case/reference creation; after an accepted case/reference and custody outbox exist, it blocks progression but never removes or reuses the issued identity. Persist root/parent proof, folder/file/version IDs, SHA-1, local SHA-256, etag and semantic role; telemetry contains IDs/correlation/outcome, not file content.

## Scoped Box folder and version custody

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** every Box operation is confined to a directly approved root ID/type/name and descendants proven from that root; arbitrary caller-supplied IDs are refused before the client boundary.
- **Confirmed facts:** no Box root or operation is authorised by this plan. Identity details and secrets remain outside source/output.
- **Decision required before implementation:** the user must name the acting identity, exact root ID/type/name and permitted operations. A separate approval is still required immediately before each live read/write or smoke run.

### Owner and dependencies

- **Policy/implementation owner:** Core custody/document owner; Infrastructure Box adapter owner.
- **Independent evaluator:** test engineer plus independent reviewer; administrative confirmation of effective app access is separate.
- **Prerequisites:** persisted receipt/root-and-parent proof and the custody outbox transaction.
- **Consumers/unlocks:** case folders, retained versions, file requests, manual WhatsApp uploads, scoped document tools and EVA image bundle.

### Caller, contract and change boundary

- **Real or intended caller:** planned Core custody use case called by Web/Worker/provider/MCP, never a supplied arbitrary Box ID.
- **Input/output:** only a root verified by immutable ID/type/name and created/traversed descendant proof may yield a persisted folder/file/version confirmation.
- **Ordered decisions and failure behavior:** reject arbitrary/unknown IDs before SDK call; discover descendants only from verified-root traversal; fail closed on mismatched/missing parent; create version rather than overwrite; keep closed-case files read-only until a reasoned reopen recorded in permanent action history.
- **Persistence/migration:** persist immutable IDs, root/known-parent proof, hashes/version/etag and semantic role in the existing evidence authority.
- **Adapters/side effects:** no account-root search, enterprise event feed, global list, arbitrary item fetch, move/copy/share/collaboration/delete/tag operations or event ingestion.
- **Operator surface and observability:** show the folder link and confirmed custody/reconciliation state. Principal/reference remain read-only; a wrong-principal case is terminal `Created in error` and links to its replacement without renaming either issued folder identity.
- **Documentation affected:** approved action records and source-custody plan; no operator-note edit.
- **Replaces/consolidates:** one adapter and scope guard; no separate Box SDK callers.

### Scope

- **Included:** root verification, descendant-only case/holding folders, original/version storage, metadata confirmation, action-history-backed confirmation and file requests beneath the root.
- **Excluded:** production folders, global search/events, deletion, automatic principal-reference folder rename, collaboration/sharing and unapproved reads/writes.

### Implementation checklist

- [ ] Implement a persisted-ID-only scope guard before the Box SDK boundary and test it with zero-client-call denials.
- [ ] Implement idempotent folder/file/version operation keys through the custody saga; persist confirmation before releasing Blob/progression.
- [ ] Add descendant-only file requests and application read-only enforcement for closed cases after core document policy exists.

### Validation checklist

- [ ] Supply synthetic/out-of-scope/unknown IDs and prove `Box scope violation` with zero SDK calls.
- [ ] Test duplicate-safe folder/file/version replay, confirmed version/hash/root persistence and closed/reopen behaviour.
- [ ] After exact approval only, run one controlled protocol fixture or approved non-corpus input in the permitted subtree; prove type/name, acting identity and positive scope.
- [ ] Do not probe a live production folder as a negative test; run repository check and independent review.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Unknown/out-of-root ID | terminal scope violation, durable retained source, zero SDK call | scope-guard fixture | production access denial by Box |
| Approved descendant write | folder/file/version/hash/root confirmation persisted | approved smoke and integration test | extraction accuracy/end-to-end case acceptance |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** each Box read/write must name the acting identity, action, target and verified root/ancestry; no approval expands that allowlist.
- **Rollout/activation:** deploy guard first; prove local out-of-scope denial; obtain action approval; run one scoped smoke; enable one caller at a time.
- **Rollback/recovery:** disable claims/caller, retain Blob/SQL receipts and recorded Box IDs/versions, then replay or redeploy prior artifact; never delete Box content to roll back.
- **Irreversible risk:** creation of content/versions in the permitted subtree; external write approval is mandatory.

### Deferred-capability impact

- **Named capabilities:** live production folders, broader mailbox/WhatsApp, MCP document tools, and future storage/infrastructure choices. Malware scanning is `Never`, with no activation path or seam.
- **Stable seam retained:** immutable Box IDs, root/parent provenance, versions and semantic roles; downstream callers use the same custody use case.
- **Future migration/replacement:** production-folder enablement needs new allowlist/user decision and separate negative-scope evidence; webhooks need a confined-dedup design.
- **Activation boundary:** exact action approval, scope smoke and later direct production decision.
- **Deliberately absent:** production-root configuration, global search/list/event feed, sharing/collaboration, delete/move/copy, second Box identity/app or webhook.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | planning review | allowlist contract and recovery boundary | an approved exact root, SDK, live Box scope, custody or acceptance |

## Add manually received WhatsApp material to a case

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** [remaining requirements §6](../../remaining-requirements.md#6-box-vehicle-data-eva-and-email) requires manual WhatsApp coexistence without a first-MVP WhatsApp integration.
- **Confirmed facts:** staff, not CollisionSpike, receive the material through WhatsApp and decide which case it belongs to; the application must retain the file and its manual source provenance through the same custody boundary as other case material.
- **Decision required before implementation:** None for authenticated manual upload. Any automated WhatsApp access, network-drive discovery or external Box write remains separately approval-gated.

### Owner and dependencies

- **Policy/implementation owner:** Core case-document intake/custody use case; the guarded Box adapter remains the only long-term file writer.
- **Independent evaluator:** test engineer verifies source provenance, association and closed-case denial; operator confirms the upload flow.
- **Prerequisites:** authenticated actor/action history, accepted case, [exclusive edit lease](../casework/case-editing-concurrency.md), durable staging and the scoped Box custody operation.
- **Consumers/unlocks:** case workspace documents, EVA image bundle and retained correspondence history.

### Caller, contract and change boundary

- **Real or intended caller:** planned authenticated `Add case material` action from case edit mode; no current caller.
- **Input/output:** staff-selected file already received outside CollisionSpike, target case, semantic role and source channel `Manual WhatsApp` yield one immutable source occurrence, traceable association and custody-pending/confirmed result.
- **Ordered decisions and failure behavior:** authorise actor and active case lease; validate size/type and target; retain original bytes before processing; preserve manual source/actor/time; refuse closed cases until a reasoned reopen enters permanent action history; stage visibly on Box failure. The application never reads a WhatsApp account or guesses a case association.
- **Persistence/migration:** store immutable occurrence identity, source channel, actor/time, case association, hashes, storage key and custody status in the existing receipt/document authority; equal bytes remain separate occurrences with provenance.
- **Adapters/side effects:** the same guarded Box operation may write only beneath the approved subtree after exact approval; no WhatsApp, network-drive or OCR call.
- **Operator surface and observability:** upload control names `WhatsApp` as the source, shows target case/file role and custody state, and reports safe validation/custody failures without file content in telemetry.
- **Documentation affected:** implementation evidence and case-document guidance after operator acceptance; operator notes remain read-only.
- **Replaces/consolidates:** no separate WhatsApp store, inbox, uploader or Box caller.

### Scope

- **Included:** manual single/multiple-file selection, explicit case/role/source attribution, durable local staging, version-safe Box custody and closed/reopen enforcement.
- **Excluded:** WhatsApp API/client automation, message/contact scraping, outbound WhatsApp, network-drive scanning, automated case matching and synthetic operational evidence.

### Implementation checklist

- [ ] Extend the existing case-document intake contract with the explicit `Manual WhatsApp` source channel and immutable occurrence provenance.
- [ ] Wire one authenticated, lease-guarded case upload caller that stages before processing and uses the shared custody outbox/Box adapter.
- [ ] Show custody pending/failure/confirmed states, enforce closed-case rules and remove any parallel file-save path.

### Validation checklist

- [ ] Use a genuine local ignored image/document only for local business-shape evaluation; prove bytes/provenance remain local and the corpus is unchanged.
- [ ] Test missing/invalid/oversize file, wrong case, closed case, expired lease, duplicate bytes, retry and Box failure without losing the staged occurrence.
- [ ] Exercise the actual authenticated Web caller and prove another staff user cannot mutate the same case while its edit lease is held.
- [ ] After exact approval only, use an approved non-corpus protocol fixture for one permitted-subtree custody smoke; do not upload corpus material.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1` and record the exact result and limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Staff adds received WhatsApp image to an open case | One immutable occurrence shows manual source, actor, role and custody state on that case | Web-to-Core integration plus local genuine-input evaluation | WhatsApp account access or Box production scope |
| Same bytes added from two source occurrences | Both provenance records remain; storage may deduplicate bytes without deleting occurrence history | persistence/idempotency test | They describe different business evidence |
| Closed case or another user's active edit lease | Upload is refused with a clear reopen/lock outcome and no external write | negative caller/zero-adapter-call test | Operator choice of a valid reopen destination |
| Approved Box custody fails | Original remains durably staged and visibly retryable; no false confirmation | fault-injection test | Vendor recovery time |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** local upload needs no external approval; every Box call still requires the exact action/target approval defined above and must use non-corpus proof material.
- **Rollout/activation:** prove local receipt/provenance and lock/closed-case denials, then activate approved Box custody for this caller after the shared guard is live.
- **Rollback/recovery:** disable the upload caller, retain staged occurrences/action history/custody results and replay through the shared outbox; never delete Box content as rollback.
- **Irreversible risk:** a permitted-subtree file/version is an external write; no live production folder or WhatsApp operation is authorised.

### Deferred-capability impact

- **Named capabilities:** WhatsApp ingestion/automation, guided/mobile capture, AI/vision assistance, and production Box roots. Malware scanning is `Never`, with no activation path or seam.
- **Stable seam retained:** channel/source identity, immutable occurrence provenance, semantic role and custody contract support a later approved adapter without changing case policy.
- **Future migration/replacement:** automated WhatsApp requires vendor/legal/security approval, stable message identity, consent, matching policy, credentials and a caller-backed adapter.
- **Activation boundary:** direct product/security decision, approved data flow, vendor terms and independently verified association evidence.
- **Deliberately absent:** no WhatsApp SDK/account, webhook, sender, network-drive watcher, OCR/vision client, scanner service or production Box configuration.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Manual WhatsApp caller, provenance, custody and failure evidence are defined | Implementation, WhatsApp integration, Box write, deployment or acceptance |
