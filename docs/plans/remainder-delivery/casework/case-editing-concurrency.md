# Exclusive case editing

## Purpose

Prevent two staff users or staff-facing callers from editing the same case at the same time, while allowing other authorised staff to inspect it read-only and ensuring an abandoned browser cannot leave a permanent lock.

## Authority and current boundary

- **Authority:** [remaining requirements §5](../../remaining-requirements.md#5-work-management-and-operator-ui) and the [questionnaire first-release case-editing requirement](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#15-first-release-scope). Exclusive editing is a proposed implementation contract pending operator confirmation; stale-write refusal remains mandatory.
- **Policy owner:** planned Core `CaseEditing` lease and mutation guard; Web/MCP translate sessions or commands but never decide ownership.
- **Current implementation:** there is no accepted-case edit page, case edit lease, row-version contract or production caller. Current receipt review is development-only and does not prove concurrent editing safety.
- **Real callers:** planned authenticated case-detail edit mode first; later staff MCP mutations use the same guard. Read-only case views do not acquire a lease.
- **Persistence/adapters:** the authoritative case row carries an optimistic concurrency version; one SQL-backed lease row per case records holder, opaque token, acquisition/renewal/expiry timestamps and its own concurrency version.
- **Dependencies:** [staff identity and permanent audit](../identity-and-access/staff-identity-authorisation-and-audit.md), accepted case identity, named case mutation use cases and the single migration stream.
- **Replaces/consolidates:** no process-memory flag, page-local boolean or long-running database transaction; every staff case mutation goes through the same guard.

## Shared failure and observability rules

Lock acquisition is atomic and server-authoritative. Exact lease/renewal timings remain a configurable usability decision and are not fixed by this plan. Save, transition, assignment, matching, principal correction and other staff case mutations require both the opaque lease token and the case version originally loaded. Losing or expiring a lease never overwrites data: the stale caller is refused, its unsaved values remain available for comparison, and it must reload/reacquire. Acquire, release, expiry recovery and material denial are audited; heartbeat success is content-safe telemetry pending the [audit-catalogue decision](../../open-decisions.md#permanent-business-audit-catalogue).

## Acquire, renew and release one case edit lease

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** only one staff editor may hold a case; other authorised staff may view it read-only and see who holds the lease.
- **Confirmed facts:** the application uses ordinary HTTP across Web instances, so a database lease is durable across process restarts and does not hold an SQL transaction open across requests.
- **Decision required before implementation:** confirm exclusive-lease behavior and select evidence-led configurable lease/renewal timings before enabling the first mutable caller.

### Owner and dependencies

- **Policy/implementation owner:** Core `CaseEditing` lease use cases with one Infrastructure SQL lease store and thin Web endpoints.
- **Independent evaluator:** a test engineer writes the two-session race/expiry fixtures; a different reviewer and operator evaluate the final behaviour.
- **Prerequisites:** trusted staff actor, case identity, audit writer, server UTC clock and explicit migration.
- **Consumers/unlocks:** [case workspace actions](operator-workspace.md#deliver-case-search-and-workspace-actions), lifecycle transitions, principal correction, matching, assignment and staff MCP mutations.

### Caller, contract and change boundary

- **Real or intended caller:** planned authenticated `Enter edit mode`, heartbeat and `Leave editing` Web actions. A later MCP mutation atomically obtains and releases a command-scoped lease through the same Core owner rather than bypassing an active editor.
- **Input/output:** case ID plus trusted staff actor yields a lease token only when no unexpired lease exists; otherwise it yields the current holder display name and expiry/recovery state without exposing credentials or session details.
- **Ordered decisions and failure behavior:** authorise case access; atomically acquire if absent/expired or return the holder; renew only with matching actor/token; release only with matching token; let server time expire abandoned leases. Browser unload release is best effort and expiry is authoritative.
- **Persistence/migration:** one unique lease per case with holder staff ID, hashed/opaque token reference, acquired/renewed/expires times and concurrency token; no case content in the lease row.
- **Adapters/side effects:** none outside SQL/audit/telemetry. Do not add Redis, distributed caches, SignalR, queues or a Web-instance memory lock.
- **Operator surface and observability:** read-only case detail shows `Being edited by` followed by the authenticated holder's display name and the recoverable wait state; the holder sees renewal/lost-lock warnings and an explicit leave action. Metrics cover acquisition, contention, expiry and renewal failure without case content.
- **Documentation affected:** implementation and recovery guidance after the real caller exists; operator notes remain read-only.
- **Replaces/consolidates:** every UI/MCP case-edit path uses this owner; delete any view-local or per-action locking alternative in the same slice.

### Scope

- **Included:** exclusive staff edit mode, durable lease acquisition/renewal/release/expiry, read-only concurrent viewing, holder visibility, multi-instance behavior and command-scoped MCP compatibility.
- **Excluded:** collaborative field-level editing, record locking for read-only views, long database transactions, automatic administrator takeover, provider API case mutation and background append-only receipt/document processing.

### Implementation checklist

- [ ] Add atomic Core acquire/renew/release operations and one SQL lease persistence model using server time and a unique case key.
- [ ] Wire authenticated edit-mode, heartbeat, leave and read-only lock-state presentation through Web; stop all case edit controls when another valid lease exists.
- [ ] Route every staff case mutation through lease-token plus case-version validation, and preserve submitted values for safe comparison when validation fails.
- [ ] Ensure staff MCP mutations use the same guard and background append-only handlers use explicit optimistic-concurrency behavior rather than bypassing case versions.

### Validation checklist

- [ ] Introduce the first named case mutation behind a failing lease-required contract fixture, then prove only one of two authenticated sessions acquires edit ownership.
- [ ] Test matching holder renewal/release, wrong token/actor, browser close, process restart, network loss and configured expiry/reacquisition using an injected server clock.
- [ ] Test a stale holder submitting after expiry and another editor's save: no field, state, reference, association, audit or outbox value is overwritten.
- [ ] Exercise two real browser sessions against SQL Server and, later, two Web instances; verify read-only visibility, holder name, warnings and accessible keyboard flow.
- [ ] Test an MCP command is denied while a Web lease exists and succeeds through a command-scoped lease when free.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1` and record the exact result and limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Two staff enter edit mode concurrently | Exactly one receives the lease; the other remains read-only and sees the holder/recovery state | SQL concurrency plus two-browser test | Production-scale contention |
| Holder saves with current token/version | Mutation and audit commit once; lease remains renewable until explicit leave or expiry | Web-to-Core integration test | Later MCP usability |
| Browser crashes or loses network | Lease expires after the server-owned window; another staff user can acquire it without support intervention | injected-clock and browser test | Recovery from database outage |
| Expired/stale editor submits | Server refuses the mutation and preserves newer case data; user can compare and reload | negative integration/browser test | Automatic merge of competing edits |
| Second staff only views | Case remains readable without acquiring or displacing the active edit lease | authorisation/browser test | Permission to edit |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** production migration and enabling case editing require release/operator acceptance; no cloud or external-system mutation is authorised here.
- **Rollout/activation:** migrate lease/version data, deploy read-only lock state, enable acquisition for one case-edit caller, then require the guard on all staff mutations before broader editing is enabled.
- **Rollback/recovery:** disable edit entry points and retain case/lease/audit data; expired rows are harmless and may be cleared only by an audited maintenance operation after confirming no valid lease.
- **Irreversible risk:** no forced takeover in the first MVP; a stale submit is refused rather than merged or overwritten.

### Deferred-capability impact

- **Named capabilities:** real-time collaborative editing, external/customer accounts, broader MCP clients, mobile/guided capture and later scale-out infrastructure.
- **Stable seam retained:** stable case/staff IDs, lease command contract, opaque token and optimistic case version remain independent of Web instance and future client.
- **Future migration/replacement:** collaborative field-level editing would replace the exclusive lease with a versioned merge/conflict model and new operator policy; additional clients must implement the same acquisition contract.
- **Activation boundary:** direct product decision, conflict-resolution UX, security review and independently measured multi-user evidence.
- **Deliberately absent:** no Redis lock, SignalR presence service, long SQL lock, forced takeover route, collaborative merge engine, external-editor role or dormant feature flag.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Exclusive editing policy, caller, persistence and race evidence are defined | Code, SQL behavior, browser usability, deployment or acceptance |
