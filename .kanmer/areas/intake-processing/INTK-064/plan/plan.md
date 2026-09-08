# Plan — INTK-064

## Objective and starting state

Automatically link a uniquely identified Triage and formal Case in either arrival
order, preserving permanent Triage reference/findings and reasoned manual control.
Read-only accepted baseline19e6f523bf6760cab39104b4dca3674b0ac8a512.
Do not take a ticket/worktree until root approves this plan, predecessors merge/
release and a fresh packet/census confirms ownership. Root owns the frozen
one-ticket supplemental run; original roster unchanged.

## Governing documents

Current operator brief, EPIC-014, FRD-02 source identity and FRD-03 Triage.
Clarify obsolete pre-Case prose to distinct-reference Case terminology while
preserving no normal Case/PO allocation from Triage itself.

## Implementation

1. Extend existing Triage lifecycle/store with a distinct SystemWorker-only
   automatic entry and bounded pending query. Do not relax manual
   TriageCaseLinkRequest's Staff authorization, reason or Case edit lease.
2. Feed accepted origin/Triage identity and known persisted principal through
   EvaluateIntakeCaseMatch's existing typed-key route. Reuse EfCaseMatchIndex in
   a context-bound form so the SAME complete candidate query/eliminator can run
   under the existing Triage serializable transaction. No duplicate matching
   grammar, query owner, candidate priority or arbitrary first result.
3. Before writing, recheck Triage state/version/principal, target archive/terminal
   state/version, live Staff lease and complete current candidate uniqueness.
   Prior deliberate manual unlink/relink prevents automatic override. Use
   deterministic operation key and existing operation hash/replay/history and
   Case workflow event transaction. Cancelled stays unlinked; Completed may
   link without reopening or promoting its finding.
4. Invoke after Triage create/replay and formal acceptance/replay; add bounded
   recovery to the existing staged reconciliation timer. Eligible-before-cap
   queries avoid unknown/nonmatch/manual-override starvation. Report recoverable
   failures and continue unrelated work; durable records remain retryable.
5. Update FRD/as-built and focused existing fixtures. No UI redesign, Case/PO
   allocation, finding copy, new queue/schema/grants or provider mutation.

## Verification

Root alone executes build and focused existing Core/real SQL fixtures after
freeze. Prove both arrival orders, duplicate replay, timer-only failure recovery,
current corrected identity and competing-candidate race, known principal
contradiction/unknown principal, manual unlink/relink, lease and stale version,
Cancelled refusal, Completed association and actual restricted Worker entry.
Assert one current link/history, unchanged references/findings, no extra Case/PO,
and unchanged manual Staff/lease rejection. No full corpus/soak/new host.

## Risks and stop

Context-bound candidate recheck is necessary for transaction correctness; using
only a pre-read or target version is insufficient. If existing adapter cannot
carry this small refinement, report before another abstraction. Root must
approve any newly evidenced migration/grant need. Preparation stops for root
plan review; no take/branch/source edit now. Execution later stops at focused
root verification and independent review, never self-merge.
