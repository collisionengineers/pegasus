## 2026-08-29 — the first fix was REFUTED, and it was worse than the bug

A Codex `gpt-5.6-terra` (high) lane implemented PR-069 inside PR #601. It was
well designed on paper — single Core owner preserved, `UnidentifiedState` not
extended, the previous branch precedence restored and pinned with two new tests
— and it was **green**: Core 1173/1173, Architecture 100/100, integration 21/21,
and every CI job on #601 passing.

Independent Claude verification **reproduced a blocker against real SQL
persistence**. It must not merge as written.

### The blocker

The Resolved-branch correction calls `ReopenAsync` and then re-resolves, but the
re-resolve rebuilds the **same** operation key the original resolve used —
`intake-unidentified-reconcile:{receipt.Id}:{receipt.Version}` — and **a
destination change does not require the receipt to be mutated**.

Reproduced sequence, with the observed history rows:

1. Triage-request e-mail, no readable registration → U-item `Open`, no Triage.
   After the staff link, `receipt.Version` is 1.
2. Staff link the receipt to a case; the sweep resolves the U-item through
   INTK-048's new trailing `CurrentCaseId` branch.
   `Open->Resolved intake-unidentified-reconcile:2c2a…:1`
3. Staff click "Open the Triage" and supply the registration.
   **`EfTriageStore.CreateAsync` reads the receipt `AsNoTracking` and never
   writes it, so `receipt.Version` is still 1.**
4. `SynchronizeForReceiptAsync` takes the Resolved branch and calls
   `ReopenAsync`, **which commits its own serializable transaction**.
   `Resolved->Open intake-unidentified-reopen:2c2a…:1`
5. The follow-on `ResolveAsync` rebuilds
   `intake-unidentified-reconcile:2c2a…:1`, which `EfUnidentifiedStore` finds
   as an existing row with a different Reason/TargetKind and rejects:
   `UnidentifiedOperationConflictException`.
6. The page handler's `when (IntakeExceptionPolicy.IsRecoverable(...))` catch
   **swallows it and the POST returns 302 — the operator sees success.**

End state: `State=Open`, `ResolutionTargetKind=null`, sitting in the open
Unidentified queue **beside a live Triage** — the exact two-queues defect
INTK-033 closed. And **un-repairable**: every 10-second sweep thereafter returns
`{Candidates=1, Resolved=0, Corrected=0, Failures=1}`, forever, because nothing
will ever mutate the receipt again and the key stays taken.

**The decisive evidence is the contrast run.** With only the reopen branch
disabled — a one-token edit, everything else identical — the item stays
`Resolved -> InstructionCase`: the wrong destination, but **stable,
single-queue, zero failures**. So the fix as written converts a stable
wrong-target into permanently double-queued material plus an endlessly failing
sweep. It is worse than the defect it closes.

The same collision fires for any destination change that does not mutate the
receipt — correcting a Triage's vehicle registration, for instance.

### Second defect — the recheck predicate never advances and starves itself

`ListResolutionsToRecheckAsync` selects rows whose association timestamp is `>=`
the resolution timestamp. **A recheck concluding "destination unchanged" writes
nothing**, so `ResolvedAtUtc` never advances and the row is re-selected on every
sweep forever. Because the query is `orderby item.ResolvedAtUtc, item.Sequence`
with `.Take(50)`, stuck rows hold the **head** of the ordering — so once 50
accumulate, every genuinely stale resolution written later is **silently never
rechecked**, with no error and no log signal (`Corrected` 0, `Failures` 0). The
correction this ticket exists to deliver simply stops happening.

It is invisible to the Core tests because
`FakeUnidentifiedStore.ListResolutionsToRecheckAsync` returns a hand-populated
list — **the real predicate has no test at all**.

### Why this matters beyond this ticket

Both defects passed a full green build, 1,294 local tests and all ten CI jobs.
Neither is a style problem; the first is production data loss reported to the
operator as success. This is the third time in this programme that cross-model
adversarial verification has caught a defect in code that compiled, passed its
own tests, and looked right — and the only reason it was caught is that the
verifier wrote a throwaway real-persistence test rather than reasoning about the
code.

### Disposition

**Remediation in the lane.** Claude Opus fixes both — the pairing rotates, since
Codex built the version under repair — and **Codex `gpt-5.6-sol` (high)**
verifies it, including reproducing the original failure on the parent commit and
confirming it no longer reproduces. The operation key must become unique per
reopen/re-resolve cycle while staying **replay-stable**, or idempotent replay
protection is traded away for the fix.
