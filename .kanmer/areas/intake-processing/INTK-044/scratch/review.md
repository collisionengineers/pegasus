---
kind: review-attestation
pr: "572"
head_sha: "f1bb150a5487122d756114854e0fd44ed688d983"
verdict: pass
reviewer: "claude-fable-5 independent reviewer (collisionengineers account)"
independent: true
plan_hash: "dfddce47147955b9"
ticket_updated: "2026-08-27T14:24:44.079Z"
findings:
  - id: R1
    severity: minor
    summary: "Simplification finding 6 (six stores still unwrap only DbUpdateException) was dispositioned 'deferred to a follow-up ticket' without a ticket id."
    disposition: deferred-to-ticket
    ticket: INTK-045
  - id: R2
    severity: note
    summary: "BeginAsync at read-committed no longer holds a shared lock on the IntakeReceipts row, so the receipt version could move between the check and commit of the attempt row; acceptance re-checks ExpectedIntakeVersion inside its own Serializable transaction and fails closed as ConcurrencyConflict/ReloadThenRetry, so no incorrect case can result."
    disposition: accepted-risk
    reason: "Fail-closed downstream check already exists (EfCaseAcceptanceStore.AcceptOnceAsync); the window only yields a retryable attempt record, never a case."
  - id: R3
    severity: note
    summary: "Classify's default arm now makes every unclassified fault staff-retryable, not only Audit; this is what the plan states and FRD-02 permits (reasoned staff retry of the immutable command), and SequenceExhausted stays Blocked."
    disposition: rejected-with-reason
    reason: "Intended scope per plan step 2; the dead end applied to any case type, Audit was simply the one with no manual fallback."
---

# Review — INTK-044, PR #572 (`task/intk-044-audit-allocation-recovery` → `dev`)

## Inputs

Ticket in `review`; PR head `f1bb150a5487122d756114854e0fd44ed688d983`
(commits `2112057d`, `f1bb150a`, both reachable from the head; `--merge`
keeps them on `dev`). Read: ticket body, `files`, `plan` (version
`dfddce47147955b9`), `post-implementation-report`, `scratch/live-evidence`,
EPIC-010 `context.md`, FRD-02, `docs/design/README.md` copy rules, the full
diff, and the unchanged surrounding code (`IntakeAllocationModelConfiguration`,
`EfIntakeAllocationStore.BeginAsync`, `AllocateIntake.RetryAsync`,
`Intake/Details.cshtml(.cs)`). No open questions document exists. Reviewer
did not author the change.

## Changes

- Core: `AllocateIntake.Classify` default arm → `Unexpected`/`ReloadThenRetry`;
  `SequenceExhausted` stays `Blocked`, `CaseTypeUnavailable` untouched.
- Infrastructure: `EfCaseAcceptanceStore.IsRetryableConcurrencyFailure`
  unwraps every inner exception (root cause: EF wrapped the 1205 two layers
  deep). `EfIntakeAllocationStore.BeginAsync` runs read-committed.
- Tests: Core retry-with-same-command test; integration concurrent
  audit+inspection reproduction (6 rounds) and flipped taxonomy expectation;
  browser retry route to an `a.` case; shared
  `AllocationTestData.SeedAutomaticAuditEvidenceAsync` replaces the private
  copy in `CustodyOutboxIntegrationTests`.

## Three review questions

1. Plan vs ticket: the ticket's three outcomes are covered — root cause
   reproduced and fixed, staff recovery route, EREF10 exists (`a.QDOS26025`;
   original receipt `f2ac0509` deliberately left `blocked` to avoid a second
   Audit, nothing deleted). Nothing implied by the ticket is missing.
2. Implementation vs plan: every step delivered; the one deviation
   (`BeginAsync` isolation) is recorded in `plan`, `files` and the report with
   its reason.
3. Simplification pass: ran (independent code-simplifier), eight items with
   honest dispositions; the deferred item lacked a ticket id — now INTK-045
   (R1).

## Acceptance checks

- Core sole owner of classification: only `IntakeAllocation.cs` changed
  policy; Infrastructure/Web key off the disposition already. PASS.
- `BeginAsync` isolation: `sp_getapplock` exclusive per receipt serialises
  same-receipt Begins; unique indexes on `OperationKey` and
  `(IntakeReceiptId, AttemptNumber)` enforce the invariants; explained in a
  code comment. Safe (see R2). PASS.
- `SequenceExhausted` stays `Blocked`: asserted in
  `SequenceExhaustionIsBlockedAndUnexpectedFailureIsSafe`. PASS.
- No operator-facing copy added: no Web/Razor files in the diff; browser
  test asserts the existing "Case not created" panel and that the exception
  text is not shown. PASS.
- Deadlock retry proven: `ConcurrentAutomaticAuditAndInspectionAllocationsForOnePrincipalBothSucceed`
  failed on round 0 before the fix with the exact live shape and passes now.
  Staff retry route proven: Core `UnexpectedAutomaticAuditFailureIsRetriedWithTheSameCommand`
  and browser `UnexpectedAutomaticAuditFailureIsRetriedFromTheReceipt`. PASS.
- CI at head: unit, sql-integration (1/2/3), sql-integration-coverage,
  browser, changes, documentation, local-development-scripts,
  reference-data all SUCCESS; infrastructure SKIPPED by path filter. No
  review threads; the only comment is a Codex quota notice. PASS.
- Local full suite by the author: Core 1002/1002, Architecture 100/100,
  Integration 988/989 with the one failure a LocalDB post-login timeout in an
  untouched test that passed on re-run. Not re-run here (another suite is
  occupying this machine); CI covers all three new tests.

## Residual risk

Staff retry of an automatic command is evaluated at staff completeness, so a
retried case may open `Not ready` until confirmed — pre-existing behaviour of
every staff retry (CASE-013), noted in the report. Six other stores keep the
narrow unwrap until INTK-045.
