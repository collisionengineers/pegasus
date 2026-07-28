# Lifecycle and work management

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Ready `0.1.0-alpha.1` plan — post-report disputes `Next`/`unallocated`; automated chasers `Later`/`unallocated`**

## Purpose

Own case state, the pre-assignment review gate, held/incomplete behaviour, terminal history, due-date work and source matching after a case is accepted. It keeps a case recoverable with permanent action history and applies the settled chase, Held-release and reopen rules.

## Feature coverage

Primary matrix IDs: `CASE-13`, `CASE-14`, `CASE-15`, `CASE-16`, `CASE-17`, `CASE-18`, `CASE-19`, `CASE-20`, `CASE-24`, `CASE-25`, `CASE-26`, `CASE-28`, `CASE-30`, `INT-29`, `INT-30`, and `MAIL-18`. Their routes are [state, reviews, terminal history and matching](#implement-state-reviews-terminal-history-and-matching) and [due work and manual chasers](#surface-due-work-and-manual-chasers). Allocation remains owned by the [maturity map](../../feature-maturity-map.md); this list is a route, not implementation evidence.

## Authority and current boundary

- **Authority:** [source order](../../../../agent-guidance/source-of-truth.md), [questionnaire §§4–7](../../../product/project-discovery-questionnaire.md), [remaining requirements §§4–5](../../../../product/qdos-alpha-gap.md), and [open decisions](../../../../product/open-decisions.md).
- **Policy owner:** planned Core `CaseLifecycle` and `CaseWork` policies.
- **Current implementation:** there is no lifecycle, transition, review gate, due date, chaser, merge or reopen policy. Existing intake receipt decisions are pre-case processing outcomes, not a case state machine.
- **Real callers:** `/Intake/Upload` currently creates only a pre-case receipt/draft. Accepted case detail, review actions, Worker reminders and workspace queues are **planned**.
- **Persistence/adapters:** planned state/history, review, due date, completeness, held reason, preserved chase interval, association and reminder data. Existing `IntakeReceiptEvents` is receipt-owned provenance, not permanent business action history.
- **Dependencies:** accepted case/identity, staff authorisation/action history, source custody and the [exclusive case-edit guard](case-editing-concurrency.md) for every mutable caller. Domain policy can be defined independently, but no unguarded caller is emitted.
- **Replaces/consolidates:** replace any route-specific state logic with one Core policy called by Web, Worker and future API/MCP.

## Shared failure and observability rules

Administrator, Engineer and User may transition/review; automated actions follow the same policy. Every transition, reason/context, prior/new state and actor enters permanent action history. `Triage` is a separate pre-case business workflow with its own owner and caller, never an inbox category or case state; `Blocked intake` is not a case state. `Held` has a required reason and pauses progression/chasers while due dates remain visible. A failure to decide state or association must be operator-visible and retain work rather than lose/guess it.

## Implement state, reviews, terminal history and matching

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §§4–5, remaining requirements §4.
- **Confirmed facts:** active work moves through incomplete/chasing, ready/review and inspection/report preparation; provider cancellation, Collision Engineers rejection and `Created in error` are independent terminal outcomes. The required review gate is before Engineer assignment; there is no review gate before sending a report. Reopening requires a reason and an operator-selected otherwise-valid nonterminal state; normal gates apply, `Held` remains a separate action, and `Created in error` never reopens. A report-send action is evidenced by an exact Outlook Sent item from the approved mailbox allowlist. When automatic matching is absent or ambiguous, any staff role may link the exact item with an entered reason; Outlook `sentDateTime` is authoritative while discovery/link times are retained separately. Any staff role may unlink/relink with a reason and dependent events/dashboard counts are recomputed. Once confirmed, the report-sent event remains final if Outlook later moves or deletes the item.
- **Decision required before implementation:** none for the state transitions in this slice. Exact automatic Sent-item matching and ambiguity rules remain deferred to the combined mailbox/email research package; the current caller must require explicit staff association of the exact item rather than guess.

### Owner and dependencies

- **Policy/implementation owner:** Core `CaseLifecycle` with an explicit transition contract.
- **Independent evaluator:** test engineer tests transition matrix and merge reversal; operator validates vocabulary/workflow.
- **Prerequisites:** accepted case identity, current actor/roles, append-only action-history writer and typed case data.
- **Consumers/unlocks:** the exclusive case-edit guard, work queues, case-detail UI, export/custody lock and planned remote callers.

### Caller, contract and change boundary

- **Real or intended caller:** planned case-detail actions and review pages; no current lifecycle caller. The policy may be tested first, but no mutable Web or MCP caller may be enabled until the exclusive lease/version guard calls it.
- **Input/output:** authorised actor, requested state/work action and reason/context yield an allowed new state/action-history entry or a typed refusal. Definitive instruction/image match may associate records; uncertain match goes to `Needs sorting`.
- **Ordered decisions and failure behavior:** the public staff mutation entry first validates the exclusive lease token and case version, then authorises and delegates to lifecycle policy; validate current state/pre-assignment review/completeness gates; require held/cancel/reject/merge-reversal/reopen reasons; atomically mutate state and action history. Do not impose a pre-send report review gate. Reopen permits an operator-selected otherwise-valid nonterminal destination subject to normal gates, refuses `Held` as a reopen destination, and refuses `Created in error`. Record `Report sent` only from an exact Sent item re-read within the approved mailbox/folder scope. When staff link because automatic evidence is absent/ambiguous, require the entered reason and store `sentDateTime`, discovery time and link time separately. Reasoned unlink/relink atomically recomputes first/report events and dashboard totals without erasing earlier history; later Outlook move/delete does not reverse a confirmed event.
- **Persistence/migration:** persist state/history, review records, staff-complete instruction/images flags, held reason, associations and reversible merge history; never delete cases.
- **Adapters/side effects:** no direct EVA assignment/report send; emit external work only after lifecycle transaction. Closed files are application read-only until a reasoned authorised reopen to an otherwise-valid nonterminal state.
- **Operator surface and observability:** show current state, pending gate, held reason, history and association provenance; record typed denied-transition outcomes.
- **Documentation affected:** links to decision register; no edit to operator notes.
- **Replaces/consolidates:** no implemented lifecycle to preserve; ensure receipt decisions do not become duplicate case states.

### Scope

- **Included:** permitted states, reasoned `Held`, pre-assignment review, all four `0.1.0-alpha.1` terminal outcomes, reasoned reopen, completeness semantics, manual image/instruction linking, and action-history-backed reversal.
- **Excluded:** `Next`/`unallocated` automatic image/instruction matching and post-report dispute workspace, a pre-send report review gate, principal/reference mutation, document revision implementation, direct EVA assignment, and automatic messaging. Automatic exact report matching remains a separate `0.1.0-alpha.1` research-gated dependency.

### Implementation checklist

- [ ] Define one case transition policy and persist state/action-history/review/completeness/association data in the existing migration stream.
- [ ] Implement the pre-assignment review, Held enter/release, terminal/reopen and explicit association/merge/reversal actions through the planned case-detail caller.
- [ ] Record report-sent state only from the exact approved-mailbox Sent item; support reasoned staff link/unlink/relink and separate authoritative sent/discovery/link times, then add the allocated `0.1.0-alpha.1` automatic exact matcher only after its research predicate is accepted.

### Validation checklist

- [ ] Exercise each allowed role across incomplete, review, Held enter/release, inspection, report sent, terminal and reopen outcomes; prove missing reasons, invalid destinations, `Created in error` reopen and a fabricated/unapproved-mailbox Sent item are refused.
- [ ] Prove missing/ambiguous automatic evidence can be resolved only by an exact-item link with reason; conflicting item/case/mailbox/folder/time evidence is refused, unlink/relink recomputes events/counts, and later Outlook move/delete leaves a confirmed event final.
- [ ] Verify configurable completeness gate blocks Engineer assignment only when enabled and both staff confirmations are absent.
- [ ] Verify definitive merge retains original origins; uncertain association remains `Needs sorting`; reversal restores traceable history.
- [ ] Verify terminal cases are retained and cannot revise files until a reasoned authorised reopen; `Created in error` never reopens.
- [ ] Run the planned real caller only after the lease/version guard is called; prove direct/unguarded and stale-version mutations are refused, then run `pwsh ./scripts/Invoke-RepoCheck.ps1` and record results/limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Complete case awaiting pre-assignment | `Review` and gate visible; unauthorised/ungated assignment refused | caller/domain test | EVA submission |
| Held case released to `Not ready` | progression resumes and the preserved local-clock chase remainder continues; due date stayed visible | transition/query/clock test | outbound delivery |
| Uncertain image/instruction association | retained `Needs sorting`, no silent merge | negative test | Match-model accuracy |
| Closed case reopened with reason | operator selects an otherwise-valid nonterminal state; normal gates apply; `Held` and `Created in error` are refused | case-detail-to-Core positive/negative tests | operator acceptance |
| Report sent action | exact linked Outlook Sent item records one report event at authoritative `sentDateTime`; discovery/link times and reason are retained without a pre-send review gate | caller/adapter contract test | automatic matching or recipient receipt |
| Reasoned unlink/relink | dependent events/counts recompute with full history; moved/deleted Outlook item does not undo a previously confirmed event | transaction/query tests | continued Outlook availability |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Outlook read access and any Sent-folder scope require exact mailbox/folder approval; no external mutation is authorised.
- **Rollout/activation:** deploy migration and Core policy without a mutable caller; enable case-detail/MCP mutations only after role/action-history and exclusive lease/version proof, then run the operator walkthrough per permitted transition.
- **Rollback/recovery:** disable new actions while retaining append-only state history; repair with a new action-history-backed transition, never delete/overwrite history.
- **Irreversible risk:** incorrect terminal/review state; mitigated by a forward correction in permanent action history and no permanent delete.

### Deferred-capability impact

- **Named capabilities:** EVA replacement/API, estimating/valuation, Diminution/Commercial, external accounts and automated document/file operations.
- **Stable seam retained:** explicit state/action/action-history contracts, case type, external Sent-item identity and configurable completeness boundary do not hard-code a principal field matrix or automatic match rule.
- **Future migration/replacement:** deferred workflows need their own states/actions only after product authority; direct EVA calls remain adapters.
- **Activation boundary:** accepted requirements and real caller evidence for any additional workflow.
- **Deliberately absent:** no separate state engine, EVA assignment adapter, customer workflow or dormant case-type route.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | State owner, settled reopen/Held/report boundaries and proof are defined | Implementation, caller behavior, automatic Sent matching or acceptance |

## Surface due work and manual chasers

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §§5 and 7; remaining requirements §5.
- **Confirmed facts:** extract inspection/equivalent deadline as `Due by`; missing material needs seven-day chasers; chaser text is copyable and outbound sending is manual. The first chase is due at the same Europe/London local clock time seven calendar days after entry to `Not ready`. `Held` preserves the remaining local-clock interval. On release, staff choose the prior state or `Review`; `Not ready` resumes the preserved remainder and `Review` ends the missing-information chase.
- **Decision required before implementation:** none for the manual `0.1.0-alpha.1` cadence. Automated outbound delivery remains deferred.

### Owner and dependencies

- **Policy/implementation owner:** Core `CaseWork` due/chaser policy.
- **Independent evaluator:** test engineer; operator validates wording/copyability from genuine business shape without synthetic operational messages.
- **Prerequisites:** lifecycle state/completeness, extracted/confirmed due-by field and source custody/Box file-request plan.
- **Consumers/unlocks:** workspace overdue/`Not ready` views; planned Worker scheduler.

### Caller, contract and change boundary

- **Real or intended caller:** planned authenticated case workspace prepares/copies the message and exposes a separate `RecordManualChaserOutcome` action; planned Worker evaluates due work. Neither exists today.
- **Input/output:** `Not ready` entry time, Europe/London clock rules, due-by, missing-material reason, lifecycle state and any preserved Held remainder yield one visible next-chase time plus optional file-request link. The confirmation action accepts trusted actor, case/version, scheduled-chase occurrence/idempotency key, selected manual channel, staff-confirmed outcome and optional note and returns the persisted result/next schedule.
- **Ordered decisions and failure behavior:** schedule seven calendar days from `Not ready` entry at the same London local clock time; on `Held`, preserve the remaining interval; on release to `Not ready`, resume that remainder, while release to `Review` ends the chase. Stop future chasers on material receipt/terminal state; never send automatically. Preparing, viewing or copying text is never evidence of sending. Only the explicit authenticated confirmation records the staff-confirmed outcome; double-submit returns the original occurrence result. Unauthorised, stale-version, unknown occurrence, closed, `Held` or no-longer-`Not ready` confirmation is refused without advancing cadence.
- **Persistence/migration:** due-by provenance, reminder schedule/occurrence, idempotency key, actor/time/case/channel/staff-confirmed outcome and optional note belong to case work. The confirmation and schedule advancement commit with permanent action history; prepared/viewed/copied UI interactions remain content-safe telemetry and do not create sent history. No hidden timer in Web/Worker.
- **Adapters/side effects:** Box file-request creation is separately approved. Preparation, copy and confirmation make no email, WhatsApp or other outbound adapter call; future delivery evidence is a separate integration boundary.
- **Operator surface and observability:** visible due date/overdue condition, missing reason, copyable text and explicit confirmation control with pending/success/refused state. Neither telemetry nor permanent history stores the message body; both use occurrence/case/channel/result identifiers only.
- **Documentation affected:** canonical decision link until settled.
- **Replaces/consolidates:** no reminder implementation exists.

### Scope

- **Included:** due-by visibility, settled manual-chaser scheduling/cadence/resumption, copy action and authenticated staff-confirmed outcome action.
- **Excluded:** automatic email/WhatsApp sending and unsanctioned Box calls.

### Implementation checklist

- [ ] Persist and display confirmed/extracted due-by provenance through case workspace.
- [ ] Implement one Europe/London-aware Core reminder calculation and copyable message action; wire the planned Worker only when it invokes that policy.
- [ ] Persist the original interval and Held remainder so restarts and daylight-saving changes cannot silently reset the schedule.
- [ ] Add the guarded manual-outcome command to the case workspace with actor/time/case/channel/outcome/optional note, expected version and occurrence idempotency; make no outbound adapter call.

### Validation checklist

- [ ] Valid/absent/contradictory deadline renders correct visible due state without silently guessing.
- [ ] Prove exact seven-calendar-day first timing, Held preservation, `Not ready` remainder resumption, `Review` termination, material receipt and terminal suppression across London daylight-saving boundaries.
- [ ] Prove no outbound email/WhatsApp/Box operation occurs when copying or viewing a chaser.
- [ ] Prove prepared/viewed/copied states never appear as sent; explicit confirmation commits outcome/schedule/action history once; double-submit returns the original result.
- [ ] Prove unauthorised, stale, closed, `Held`, terminal and superseded-occurrence submissions are refused without adapter call or schedule advancement, and no message body enters telemetry/history.
- [ ] Exercise actual workspace and later Worker caller, plus `pwsh ./scripts/Invoke-RepoCheck.ps1`.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Enter `Not ready` | next chase is the same Europe/London local clock time seven calendar days later | injected-clock policy/caller test | message delivery |
| Release `Held` | prior state or `Review` is selectable; `Not ready` resumes preserved remainder and `Review` ends the chase | transition/schedule integration test | automated delivery |
| Copy chaser | Staff can copy text; no message is sent | UI/adaptor-negative test | Delivered communication |
| Staff confirms a manual outcome | actor/time/case/channel/outcome/optional note persist once and cadence advances only when policy permits | Web-to-Core transaction/idempotency test | external send or delivery |
| Double-submit or stale/closed/Held case | original result or typed refusal; no duplicate history/schedule advance and zero outbound calls | negative caller/transaction test | future messaging adapter |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** Box file request and any external write require exact-target approval; manual copy requires none.
- **Rollout/activation:** show due dates, migrate schedules and enable one real Worker caller with bounded retries after focused clock/restart proof.
- **Rollback/recovery:** disable scheduler/caller, retain scheduled history and reconstruct pending work visibly; never lose a due date.
- **Irreversible risk:** unwanted chaser activity; `0.1.0-alpha.1` has no automated send.

### Deferred-capability impact

- **Named capabilities:** automated outbound chasers, WhatsApp ingestion/automation, wider mailbox coverage and Box file requests.
- **Stable seam retained:** explicit missing-material reason, due-by provenance, scheduled occurrence identity and staff-confirmed channel/outcome support later adapters without treating manual confirmation as external delivery evidence.
- **Future migration/replacement:** outbound delivery status/retries/consent need a separately approved communication slice.
- **Activation boundary:** real Worker evidence and explicit external approval for any Box or sending side effect.
- **Deliberately absent:** no mail sender, WhatsApp client, cron-only policy or dormant reminder queue.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Due-work sequence, settled cadence and proof boundaries | Scheduler implementation, outbound delivery or acceptance |
