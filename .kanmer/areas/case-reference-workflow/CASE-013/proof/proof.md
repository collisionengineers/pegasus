# Proof

**Shipped:** PR #506, commit `ca564ac5` · **Deployed:** Release 18 (`1f3be493`),
carried to Release 22 (`191ddf33`), the serving revision.

## The operator's complaint, reproduced and resolved end to end

`AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments` (PR #515) drives
the real chain against real SQL — `ProcessIntake` on a QDOS audit instruction,
acceptance, then `IProcessQueuedCustody` — and asserts:

```
CaseWorkflows.State  ==  Review
```

The completeness passed is the **automatic shape the pipeline actually
records** — `(true, true, false, false)`: instruction and images complete,
neither confirmed by staff. That is the exact shape that stranded QDOS26009. A
test passing all four true would prove nothing about this fix, which is why the
existing custody tests did not catch it.

```
dotnet test … ~CustodyOutboxIntegrationTests (Release)   21 passed, 0 failed
CI on PR #515                                            10 checks green
```

## Why this could not be shown before

The promotion to Review is written inside `CompleteCaseCustodyAsync`'s single
transaction. Custody was failing in production for an unrelated reason — the
Worker had no grant on the case-document tables ([[DOCS-008]]) — so the
promotion code never ran at all, and the completeness flags this ticket sets
were never consulted.

That is also why QDOS26010 still reads `NotReady` despite being created after
this fix deployed: its custody failed. It is evidence the fix had not run, not
evidence it does not work.

## The root cause, restated

The automatic route recorded all four completeness flags false, and the
acceptance policy then demanded staff confirmation nobody would ever give.
Meanwhile `CaseCompleteness.IsReadyForReview` — which waives staff review for an
automatically definitive intake — **had no callers at all**, and two stricter
Infrastructure copies had been written instead. The fix records the automatic
shape at allocation and waives staff confirmation for a system-worker actor,
which is what "automatically definitive" means.

## Evidence tier

**End-to-end against real SQL**, through the production chain, at the production
completeness shape. Not yet observed on a live case, because no case has been
created since the custody grant landed — one instruction, or an operator
pressing **Retry custody**, produces that.
