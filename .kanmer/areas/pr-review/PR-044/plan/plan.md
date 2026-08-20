# Plan — PR-044

## Chosen approach

After the durable Pending reservation and once the provider move block has begun, catch request cancellation, conditionally update that operation from Pending to Uncertain through a fresh DbContext and a bounded internal cancellation token, then rethrow the original cancellation. The conditional update avoids downgrading Success if its write committed before cancellation surfaced. Existing same-key replay performs the only recovery probe.

## Governing docs

`docs/frd/frd-08-email-mailbox-and-background-processing.md` requires duplicate-safe, visible and recoverable external move outcomes. Durable Uncertain preserves the active slot and exposes the existing status check without repeating the move. No governing-doc or ADR change is needed.

## Steps

1. Add one private cancellation-handoff method on the dedicated EF store: fresh context, bounded internal token, conditional Pending→Uncertain update and safe failure reason.
2. Invoke it only when the request token cancels inside provider move/success completion, then rethrow to preserve caller cancellation semantics.
3. Add exact LocalDB tests for cancellation during provider execution and during Success SaveChanges. In each, assert caller cancellation, durable Uncertain, new-key exclusion, same-key probe-only recovery, terminal Success and no duplicate move.
4. Re-run existing Pending/Uncertain/concurrency/provider-failure tests, full retained-mail persistence tests, Release build and diff checks.
5. Record four simplification lenses, PR-044/TICK-049 PIR/checklist/traceability, push to PR #477 and leave Review.

## Risks

- Internal persistence must not inherit the cancelled request token; a bounded independent token limits the synchronous handoff.
- Save cancellation may surface after Success committed; the conditional Pending predicate prevents regression.
- If the database is unavailable beyond the bounded handoff, no in-request design can guarantee persistence without the explicitly excluded background worker. This task handles request cancellation, not database outage.

## Simplification pass — 2026-08-20

- **Reuse:** Reused the existing Pending/Uncertain vocabulary, filtered active-operation index, same-key probe recovery, EF context factory and LocalDB interceptor/fake patterns.
- **Simplification:** Added one conditional fresh-context update and one private handoff method. No worker, lease, timer, new state, result wrapper or generic command framework.
- **Efficiency:** The extra SQL update runs only when request cancellation interrupts provider work or the success save. Recovery probes current location and never repeats the provider move.
- **Altitude:** The external-operation lifecycle remains inside the dedicated Infrastructure store; Core and Web contracts are unchanged.
- **Applied findings:** cancellation after provider work begins durably changes Pending to Uncertain before the original cancellation is rethrown; a Success committed before cancellation cannot be downgraded because the update is conditional on Pending; exact tests cover cancellation during move and during Success SaveChanges.
- **Unapplied findings:** none.
