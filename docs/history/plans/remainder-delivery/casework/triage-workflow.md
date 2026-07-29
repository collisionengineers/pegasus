# Triage workflow

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Ready `0.1.0-alpha.1` plan — exact matcher remains research-gated**

## Purpose

Deliver the bounded `0.1.0-alpha.1` pre-case roadworthiness workflow without turning `Triage` into an inbox category or case state. A Triage record remains separate from any later case and reaches completion only from exact Outlook reply-chain evidence.

## Feature coverage

Primary matrix IDs: `TRI-01`, `TRI-02`, `TRI-03`, `TRI-04`, `TRI-05`, `TRI-06`, `TRI-07`, `TRI-08`, and `TRI-09`. All route through [Triage workflow](#triage-workflow). Allocation remains owned by the [maturity map](../../feature-maturity-map.md); this list is a route, not implementation evidence.

## Authority and current boundary

- **Authority:** [questionnaire Triage decisions](../../../product/project-discovery-questionnaire.md#4-the-case-lifecycle), [remaining requirements §3](../../../../product/qdos-alpha-gap.md#3-complete-intake-formats-and-paths), and the [combined mailbox/email research boundary](../../../../product/open-decisions.md#mailbox-categorisation-and-matching-evidence).
- **Policy owner:** planned Core `TriageWorkflow`; it alone owns states, findings, correction, reopen, cancellation and case-link policy.
- **Evidence state:** **Planned; not implemented, called, deployed, live verified or accepted.** No production Triage type, persistence, route, Graph evidence matcher or caller exists.
- **Intended callers:** planned authenticated Web Triage list/detail/actions; planned Web completion action calling a Core reply-evidence query backed by one Infrastructure Outlook adapter. Graph/Outlook supplies evidence only and owns no workflow decision.
- **Persistence/adapters:** planned Triage records, finding revisions, exact reply evidence, optional assignee, source identity, optional later-case link, row version and permanent action-history entries in the existing DbContext/migration stream. Outlook retains the message; CollisionSpike retains stable evidence identity and business state.
- **Dependencies:** staff identity/roles/action history, durable source occurrence, Administrator-maintained shared approved-mailbox allowlist, and accepted combined research for the exact automatic reply-chain predicate. A missing vehicle registration stays in `Needs sorting` and creates no active Triage.
- **Replaces/consolidates:** no implementation exists. Do not put Triage rules in the mailbox adapter, generic inbox classifier, case lifecycle, page model or tests.

## Contract and failure behavior

The Core command accepts an authenticated actor, expected Triage version, action, required reason where applicable, and stable source/evidence identifiers. It returns the persisted Triage state and action-history result or a typed refusal. All three staff roles may act; only Administrators manage the mailbox allowlist.

The state contract is `Open` or `Awaiting information` -> `Finding recorded` -> `Completed`. The finding is exactly `Roadworthy` or `Unroadworthy`. `Cancelled` is the only end state without a finding. A Triage may have an optional assignee, no due date and no chasers.

Completion requires the exact reply-chain Outlook Sent item from a mailbox in the shared approved allowlist. Subject text, vehicle registration and manual message selection are never sufficient fallbacks. Until the combined research accepts the automatic reply-chain predicate, the completion transition remains unavailable; the rest of the Triage record must not guess evidence. The adapter rejects unapproved mailbox/folder/item scope before Graph and re-reads the immutable item before Core commits completion.

Before a response is sent, replacing a finding requires a reason and retains the earlier revision. After a response, a changed finding supersedes the earlier finding, returns the work to `Finding recorded`, requires a new exact reply-chain response, and retains the earlier evidence/history. Reopening `Completed` or `Cancelled` requires a reason and always returns to `Open`.

A Triage links to at most one later case; a case may link multiple Triage records. Automatic linking remains disabled until the combined research proves a definitive shared match; otherwise staff confirm. Any staff role may unlink or relink with a reason. Linking never merges the Triage into the case, allocates a reference, or changes either record's identity.

Missing registration, invalid state/finding, stale version, missing reason, unapproved mailbox, absent/ambiguous reply-chain evidence, contradictory evidence or unavailable Graph read produces a visible typed outcome with no partial mutation. A transient Graph failure may retry with a bound; a permanent failure stops visibly. Routine lookup/retry detail is content-safe telemetry; every Triage mutation and material denial/failure enters permanent action history without message bodies.

## Scope and exclusions

- **Included:** Web list/detail/actions, registration requirement, optional assignment, settled state/finding transitions, cancellation, correction/supersession, reopen, exact Outlook completion evidence, manual later-case linking and reasoned unlink/relink.
- **Excluded:** case/reference creation, due dates, chasers, generic inbox categorisation, automatic reply or case matching before the combined research is accepted, manual Sent-item selection, Outlook mutation, outbound sending, WhatsApp automation, roadworthiness inference from AI/vision, and Diminution/Commercial workflow.
- **Files likely to change when implemented:** one cohesive Triage feature folder in Core, parallel Infrastructure persistence/Outlook translation, authenticated Web pages, the existing DbContext migration stream, focused unit/integration/browser tests, and composition registration. No new project, service, queue, data store or generic rules framework.

## Implementation sequence

- [ ] Define the typed Core state/action/finding/evidence contracts and one `TriageWorkflow` owner.
- [ ] Add the separate relational model and one ordered migration with registration, source, assignee, state, current/superseded finding revisions, exact reply evidence, optional case link and concurrency version.
- [ ] Add authenticated Web list/detail/mutation callers with all-role action permission, optimistic concurrency, explicit reason fields and no case-state reuse.
- [ ] Reuse the shared approved-mailbox allowlist and add the narrow Outlook exact-reply evidence port/adapter only after the combined research accepts its predicate; do not add a second matcher.
- [ ] Add case-workspace links through named Triage link/unlink commands; never write case tables directly from the page or Outlook adapter.
- [ ] Remove any interim generic-category wording or duplicate transition logic in the same slice.

## Validation ladder

- [ ] **Core policy:** cover every allowed transition and finding, required reasons, no-registration refusal, cancel-without-finding, invalid completion, reopen-to-`Open`, finding supersession and one-to-many case-link cardinality. This proves policy decisions only, not persistence or callers.
- [ ] **Persistence:** prove atomic Triage/action-history/finding/evidence/link commits, rollback on history failure, stale-version refusal, migration from the supported prior schema and concurrent correction/link attempts. This does not prove Outlook scope or UI behavior.
- [ ] **Outlook contract:** with controlled protocol/security fixtures, prove the shared allowlist and Sent-folder/reply-chain constraints deny before the Graph client, exact evidence replay is idempotent, and transient/permanent failures map correctly. Fixtures are not operational evidence or proof of live Exchange behavior.
- [ ] **Actual Web caller:** navigate the authenticated Triage surface through create, assign, await information, record/correct finding, complete, supersede/respond again, cancel/reopen and link/unlink. Verify loading/empty/stale/denied/failure states, keyboard access and business language.
- [ ] **Approved live gate:** after exact mailbox/folder/data approval, exercise the planned Outlook evidence caller with approved non-corpus input and verify the exact item identity/time. This proves that approved-scope Graph evidence can reach the caller; it does not prove recipient delivery or business acceptance.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1` and record its exit result separately from the focused behavior evidence.

## Acceptance and negative cases

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Source lacks vehicle registration | retained `Needs sorting`; no active Triage | Web-to-Core negative test | future extraction accuracy |
| `Roadworthy` recorded | `Finding recorded`, actor/reason/version visible | caller/persistence test | response sent |
| Completion without exact approved reply-chain item | refused; state/history unchanged; clear evidence warning | zero/failed-adapter-call tests | `0.1.0-alpha.1` automatic matcher accuracy |
| Exact approved reply evidence after accepted research | `Completed` once with immutable mailbox/item/sent-time evidence | Web/Core/Graph integration test | recipient delivery or reading |
| Finding changed after completion | prior finding/evidence retained, superseding finding recorded, new response required | transaction/browser test | report correctness |
| Completed or cancelled record reopened | reason retained and state returns to `Open` | caller/state test | operator judgment |
| Staff relinks to another case | prior link retained in history; at most one current case link | concurrency/cardinality test | automatic association accuracy |

## Approval, rollout and rollback

- **Approval-triggering action and exact scope:** live Outlook reads require the approved environment identity, mailbox allowlist, Sent folder, data class and non-corpus input. No Outlook write or send is authorised.
- **Rollout/activation:** deploy schema and Web workflow first with completion fail-closed; after combined-research acceptance and exact Graph approval, activate the single reply-evidence adapter/caller and run the operator journey.
- **Rollback/recovery:** disable Triage mutations/evidence lookup and redeploy the prior compatible artifact while retaining records, finding revisions, evidence and action history. Repair only through forward recorded actions; never delete or rewrite history.
- **Irreversible/concurrency risks:** an incorrectly completed roadworthiness response or lost superseded finding is material. Atomic expected-version transactions, exact evidence, append-only history and no manual evidence fallback mitigate it.

## Deferred-capability impact

- **Later and excluded capabilities:** `Next`/`unallocated` broader mailbox/email management and general association, `Later`/`unallocated` WhatsApp and Diminution/Commercial cases, conditional guided capture, later AI/vision roadworthiness assistance, and `Not planned` external accounts/later resilience infrastructure. The exact Triage reply matcher is a `0.1.0-alpha.1` research-gated requirement, not a deferral.
- **Stable seam retained:** stable Triage/source/case/external-message identities, typed binary finding/revisions, explicit state/action contracts, shared mailbox allowlist and narrow Outlook evidence port.
- **Future migration/replacement:** the accepted `0.1.0-alpha.1` exact reply matcher supplies the same evidence command; later channels or assistance add provenance/adapters and approved UI without replacing Triage policy. External accounts remain `Not planned` unless a new direct product decision changes that boundary.
- **Activation boundary:** accepted combined research for the `0.1.0-alpha.1` automatic reply matcher and `Next`/`unallocated` general case matching; explicit product, licence/security, accuracy, and operator acceptance for any later channel or automated finding assistance.
- **Deliberately absent:** no generic classifier/rule table, automated sender, WhatsApp client, vision model, customer role, case/reference allocator, due/chaser scheduler, queue or feature flag.

## Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Plan review | Scope, owner, intended callers, contracts, negative cases, proof ladder and rollback are defined | Implementation, a production caller, local pass, live Outlook access, deployment or acceptance |
