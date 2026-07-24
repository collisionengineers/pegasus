# Lifecycle and work management

## Purpose

Own the case state, review gates, held/incomplete behaviour, terminal history, due-date work and source matching after a case is accepted. It keeps a case recoverable and auditable; it does not invent a chase cadence or reopen destination.

## Authority and current boundary

- **Authority:** [source order](../../../agent-guidance/source-of-truth.md), [questionnaire §§4–7](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md), [remaining requirements §§4–5](../../remaining-requirements.md), and [open decisions](../../open-decisions.md).
- **Policy owner:** planned Core `CaseLifecycle` and `CaseWork` policies.
- **Current implementation:** there is no lifecycle, transition, review gate, due date, chaser, merge or reopen policy. Existing intake receipt decisions are pre-case processing outcomes, not a case state machine.
- **Real callers:** `/Intake/Upload` currently creates only a pre-case receipt/draft. Accepted case detail, review actions, Worker reminders and workspace queues are **planned**.
- **Persistence/adapters:** planned state/history, review, due date, completeness, held reason, association and reminder data. Existing `IntakeAuditEvents` is receipt-owned provenance, not the permanent business-audit catalogue.
- **Dependencies:** accepted case/identity, staff authorisation/audit, source custody and the [exclusive case-edit guard](case-editing-concurrency.md) for every mutable caller. Domain policy can be defined independently, but no unguarded caller is emitted.
- **Replaces/consolidates:** replace any route-specific state logic with one Core policy called by Web, Worker and future API/MCP.

## Shared failure and observability rules

Administrator, Engineer and User may transition/review; automated actions follow the same policy. Every transition, reason/context, prior/new state and actor is permanent audit history. `Triage` stays a reserved pre-case work type; `Blocked intake` is not a case state. `Held` has a required reason and pauses progression/chasers while due dates remain visible. A failure to decide state or association must be operator-visible and retain work rather than lose/guess it.

## Implement state, reviews, terminal history and matching

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §§4–5, remaining requirements §4.
- **Confirmed facts:** active work moves through incomplete/chasing, ready/review and inspection/report preparation; provider cancellation and Collision Engineers rejection are independent terminal outcomes; both review gates are required. Post-report states exist conceptually but are not enterable until report-sent evidence is settled.
- **Decision required before implementation:** [reopen destination](../../open-decisions.md#reopen-destination), [leaving `Held`](../../open-decisions.md#leaving-held), and [authoritative sent-report evidence](../../open-decisions.md#authoritative-sent-report-evidence-and-time) block only their named transitions.

### Owner and dependencies

- **Policy/implementation owner:** Core `CaseLifecycle` with an explicit transition contract.
- **Independent evaluator:** test engineer tests transition matrix and merge reversal; operator validates vocabulary/workflow.
- **Prerequisites:** accepted case identity, current actor/roles, immutable audit writer and typed case data.
- **Consumers/unlocks:** the exclusive case-edit guard, work queues, case-detail UI, export/custody lock and planned remote callers.

### Caller, contract and change boundary

- **Real or intended caller:** planned case-detail actions and review pages; no current lifecycle caller. The policy may be tested first, but no mutable Web or MCP caller may be enabled until the exclusive lease/version guard calls it.
- **Input/output:** authorised actor, requested state/work action and reason/context yield an allowed new state/audit or a typed refusal. Definitive instruction/image match may associate records; uncertain match goes to `Needs sorting`.
- **Ordered decisions and failure behavior:** the public staff mutation entry first validates the exclusive lease token and case version, then authorises and delegates to lifecycle policy; validate current state/review gate/completeness gate; require held/cancel/reject/merge-reversal reasons; atomically mutate state and audit. Review before Engineer assignment and before report send is mandatory. Reopen remains unavailable until the decision gate is settled.
- **Persistence/migration:** persist state/history, review records, staff-complete instruction/images flags, held reason, associations and reversible merge history; never delete cases.
- **Adapters/side effects:** no direct EVA assignment/report send; emit external work only after lifecycle transaction. Closed files are application read-only until an authorised reopen in the later settled slice.
- **Operator surface and observability:** show current state, pending gate, held reason, history and association provenance; record typed denied-transition outcomes.
- **Documentation affected:** links to decision register; no edit to operator notes.
- **Replaces/consolidates:** no implemented lifecycle to preserve; ensure receipt decisions do not become duplicate case states.

### Scope

- **Included:** permitted pre-report states, entry to reasoned `Held`, review gates, provider cancellation/Collision Engineers rejection, completeness semantics, definitive image/instruction matching and audited merge reversal.
- **Excluded:** leaving `Held`, reopening, report-sent transition, entry to post-report, post-report completion, principal correction/freeze, document revision implementation, EVA assignment and automatic messaging until their decisions are settled.

### Implementation checklist

- [ ] Define one case transition policy and persist state/audit/review/completeness/association data in the existing migration stream.
- [ ] Implement review gates, held reason, terminal outcomes and explicit association/merge/reversal actions through planned case-detail caller.
- [ ] Keep reopen unavailable pending the canonical decision; remove any route-local state decision.

### Validation checklist

- [ ] Exercise each allowed role across incomplete, review, entry to held, inspection and the two independent terminal outcomes; prove every withheld edge and missing reason is refused.
- [ ] Verify configurable completeness gate blocks Engineer assignment only when enabled and both staff confirmations are absent.
- [ ] Verify definitive merge retains original origins; uncertain association remains `Needs sorting`; reversal restores traceable history.
- [ ] Verify terminal cases are retained and cannot revise files without a later authorised reopen policy.
- [ ] Run the planned real caller only after the lease/version guard is called; prove direct/unguarded and stale-version mutations are refused, then run `pwsh ./scripts/Invoke-RepoCheck.ps1` and record results/limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Complete case awaiting pre-assignment | `Review` and gate visible; unauthorised/ungated assignment refused | caller/domain test | EVA submission |
| Held case with reason | progression and chasers pause; due date remains visible | transition/query test | Chase timing rule |
| Uncertain image/instruction association | retained `Needs sorting`, no silent merge | negative test | Match-model accuracy |
| Closed case reopen request | no transition until canonical decision exists | policy test | Future reopen destination |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** product decision is required for reopen destination; no external mutation is authorised.
- **Rollout/activation:** deploy migration and Core policy without a mutable caller; enable case-detail/MCP mutations only after role/audit and exclusive lease/version proof, then run the operator walkthrough per permitted transition.
- **Rollback/recovery:** disable new actions while retaining append-only state history; repair with a new audited transition, never delete/overwrite history.
- **Irreversible risk:** incorrect terminal/review state; mitigated by auditable forward correction and no permanent delete.

### Deferred-capability impact

- **Named capabilities:** EVA replacement/API, estimating/valuation, Diminution/Commercial, external accounts and automated document/file operations.
- **Stable seam retained:** explicit state/action/audit contracts, case type and configurable completeness boundary do not hard-code a principal field matrix.
- **Future migration/replacement:** deferred workflows need their own states/actions only after product authority; direct EVA calls remain adapters.
- **Activation boundary:** accepted requirements and real caller evidence for any additional workflow.
- **Deliberately absent:** no separate state engine, EVA assignment adapter, customer workflow or dormant case-type route.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | State owner, exclusions and proof are defined; reopen is withheld | Implementation, reopened case behaviour or acceptance |

## Surface due work and manual chasers

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §§5 and 7; remaining requirements §5.
- **Confirmed facts:** extract inspection/equivalent deadline as `Due by`; missing material needs seven-day chasers; chaser text is copyable and outbound sending is manual; `Held` pauses chasers.
- **Decision required before implementation:** [chase timing around `Not ready` and `Held`](../../open-decisions.md#chase-timing-around-not-ready-and-held) blocks scheduler persistence, first-due calculation and resumption tests.

### Owner and dependencies

- **Policy/implementation owner:** Core `CaseWork` due/chaser policy after the decision gate.
- **Independent evaluator:** test engineer; operator validates wording/copyability from genuine business shape without synthetic operational messages.
- **Prerequisites:** lifecycle state/completeness, extracted/confirmed due-by field and source custody/Box file-request plan.
- **Consumers/unlocks:** workspace overdue/`Not ready` views; planned Worker scheduler.

### Caller, contract and change boundary

- **Real or intended caller:** planned case workspace creates copyable message; planned Worker evaluates due work. Neither exists today.
- **Input/output:** due-by, missing-material reason and lifecycle state yield visible overdue state and, once settled, one next chase/reminder plus optional file-request link.
- **Ordered decisions and failure behavior:** display due-by now; do not schedule until first-interval and held-exit rules are settled. Stop future chasers on material receipt/terminal state; never send automatically.
- **Persistence/migration:** due-by provenance and eventual reminder schedule/history belong to case work; no hidden timer in Web/Worker.
- **Adapters/side effects:** Box file-request creation is a separately approved adapter; copying text causes no system send.
- **Operator surface and observability:** visible due date/overdue condition, missing reason and copyable text; log reminder decision without content.
- **Documentation affected:** canonical decision link until settled.
- **Replaces/consolidates:** no reminder implementation exists.

### Scope

- **Included:** due-by visibility and bounded planned shape for manual chasers.
- **Excluded:** scheduling/cadence/resumption until decision, automatic email/WhatsApp sending and unsanctioned Box calls.

### Implementation checklist

- [ ] Persist and display confirmed/extracted due-by provenance through case workspace.
- [ ] Record the first-chase and Held-exit decision before adding schedule data, Worker trigger or acceptance tests.
- [ ] After settlement, implement one Core reminder calculation and copyable message action; wire planned Worker only when it invokes that policy.

### Validation checklist

- [ ] Valid/absent/contradictory deadline renders correct visible due state without silently guessing.
- [ ] Prove material receipt, terminal state and Held suppress future planned chases once cadence exists.
- [ ] Prove no outbound email/WhatsApp/Box operation occurs when copying or viewing a chaser.
- [ ] Exercise actual workspace and later Worker caller, plus `pwsh ./scripts/Invoke-RepoCheck.ps1`.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Confirmed deadline past due | Due date and overdue work visible | UI/query test | Reminder cadence |
| Chase timing unresolved | No scheduler enabled; decision register linked | configuration/policy test | Future schedule correctness |
| Copy chaser | Staff can copy text; no message is sent | UI/adaptor-negative test | Delivered communication |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** settle cadence/restart policy before schedule activation; Box file request and any external write require exact-target approval.
- **Rollout/activation:** show due dates first; after decision, migrate schedules and enable one real Worker caller with bounded retries.
- **Rollback/recovery:** disable scheduler/caller, retain scheduled history and reconstruct pending work visibly; never lose a due date.
- **Irreversible risk:** unwanted chaser activity; first MVP has no automated send.

### Deferred-capability impact

- **Named capabilities:** automated outbound chasers, WhatsApp ingestion/automation, wider mailbox coverage and Box file requests.
- **Stable seam retained:** explicit missing-material reason, due-by provenance and a Core reminder decision support later adapters.
- **Future migration/replacement:** outbound delivery status/retries/consent need a separately approved communication slice.
- **Activation boundary:** canonical cadence decision, real Worker evidence and explicit external approval.
- **Deliberately absent:** no mail sender, WhatsApp client, cron-only policy or dormant reminder queue.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Due-work sequence and explicit cadence withholding | Scheduler, outbound delivery or acceptance |
