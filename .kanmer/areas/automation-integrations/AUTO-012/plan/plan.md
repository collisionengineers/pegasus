# Plan — AUTO-012

## The choice that shaped everything

The ticket offered two routes. **The second was taken deliberately.**

Route one — one transaction spanning the provider-submission store, the shared
durable-intake path and action history — would have changed the transaction
shape of the path **every intake lane uses**: mail, upload, image intake and
direct providers. Changing that to fix a route which has admitted no callers,
days before a production promotion, is a bad trade.

Route two reuses machinery that already exists **and already runs in
production**: `StagedArtifactReconciliationFunction`, one of the seven live
Worker functions, on a ten-second schedule. `ReconcileProviderSubmissions` is a
sixth bounded sweep on that existing timer — no new schedule, timer, queue,
function, table or deployment unit.

The reviewer confirmed the result: **blast radius SMALL**, shared intake path at
zero diff lines, and of 8,340 insertions **7,507 are the migration Designer
snapshot**.

The SQL outbox (`ExternalWorkItems`) was considered and rejected with a reason:
it carries work that calls outward; this repairs our own rows.

**Recoverable beats atomic.** The goal was never to make four writes one
transaction — it was that a process loss between any pair leaves a record
something resolves, bounded by a ten-second timer rather than by whether the
provider happens to retry.

## What the review changed, and why each mattered

The first implementation was green — full build, its own tests, and every CI job
— and had four real defects.

**The candidate window poisoned itself.** A bare reservation (row created,
intake never retained) matched the predicate, was skipped in the loop, and could
never be removed: no `DELETE` is granted to either role, and it is unreachable
by `GET` because the caller never received a submissionId. Sixty of them from a
ten-minute storage outage would permanently occupy the oldest-first batch of
fifty, and every genuinely repairable submission would sit behind them forever
— logging `50 candidates, 0 repaired, 0 failures`, indistinguishable from
health.

Fixed by making a bare reservation **unselectable at the source** — an inner
join on the staged receipt — which also deleted the per-candidate round trip and
let the sweep drop its `IIntakeWorkStore` dependency entirely.

*This is the second appearance of this shape in one session.* [[PR-069]]'s
recheck predicate had the same defect. The lesson generalises: **a bounded
oldest-first sweep must make its un-repairable cases unselectable, not skip them
in the loop.**

**Two `Accepted` rows could land in permanent history.** The only guard was a
one-minute grace anchored to `ReceivedAtUtc` — the *original* creation time —
so for the retried-old-submission case the design depends on, it was already
expired. Nothing dedupes: both `ActionHistory` indexes are non-unique.

Fixed by deriving the row's id from the operation key (SHA-256, the same
construction as the existing `PollSentEvidence.CreateStableId`), so the second
insert collides on the **primary key**. No schema change, no new index, no
migration. Rule 11 is honoured on both sides: the sweep counts no repair when it
loses the race rather than double-writing.

**The repaired row misstated when.** Stamped with the sweep's clock, it sorted
*after* the `Replayed` row it logically precedes. Now stamped
`candidate.ReceivedAtUtc`.

**A missing grant would have been invisible** — the 2026-08-14 diagnostic
problem reproduced. `FirstFailure` now travels out on the result and into the
Worker's existing log line.

## The two MEDIUMs the second review found

**The reason still asserted a falsehood.** "The originating request's
correlation id was not retained" is untrue for a retried bare reservation, where
a correlation id existed and was in flight — and the grace period kept to
prevent exactly that cannot, because `ReceivedAtUtc` is never refreshed on
retry. The row now states only what is verifiable from itself.

**The joins silently depended on an unpinned collation.** SQL converts
`uniqueidentifier` to **uppercase**; `ExternalReceiptToken` and `AggregateId`
were written lowercase by .NET. Production is `SQL_Latin1_General_CP1_CI_AS`
(checked live), so it works — but under a case-sensitive collation accept
recovery becomes a permanent silent no-op **invisible even to the new
`FirstFailure` diagnostics**, because no exception is raised. Pinned by
assertion rather than by rewriting the joins: the dependency is reasonable, it
just has to fail loudly.

## Verification

Full canonical gate: Core 1178, Architecture 100, Integration all green, exit 0.
All eleven CI jobs green on #635.

Both HIGH defects were **reproduced on the pre-fix code and confirmed gone** —
starvation gave `Expected: 1, Actual: 50` candidates before the fix; the
duplicate-history mechanism was verified as a real primary-key refusal
(`HasKey(item => item.Id)`, SQL 2627), not a time window.

## Simplification pass — 2026-08-30

Ran. Applied: the per-candidate `FindBySourceIdentityAsync` round trip was
deleted by the join that fixed the starvation, taking a constructor dependency
with it; the re-read that used to narrow the duplicate window was deleted along
with `GetAcceptRecoveryCandidateAsync`, its EF implementation and its fakes;
`ToEntity` was factored out so `AppendAsync` and `TryAppendAsync` share one
mapping. Rejected with a reason: widening `EfIntakeWorkStore.ToCode` to avoid a
seventh copy of the channel-code map — reverted to keep the shared intake path
at zero diff lines, with the agreement held by test instead. No unapplied
findings.
