## 2026-08-29 — built by Codex luna (xhigh), refuted by Claude; six defects

### The design was right, and the steering held

The lane took the ticket's **second** option — reconciliation, not one
transaction across the shared durable-intake path — and the verifier confirmed
the blast radius is **SMALL**, which was the whole point of steering it there.

`git diff origin/dev...HEAD --stat`: 13 files, 8,340 insertions, of which
**7,507 are the migration Designer snapshot**. `IIntakeSubmission.ExecuteAsync`,
`IIntakeWorkStore.ReceiveAsync`, `ProcessIntake`, `DurableIntake` and the shared
transaction shape have **zero diff lines**. The only Core edit inside the accept
sequence is a byte-identical swap of the literal
`$"provider-submission:{existing.Id:N}"` for
`ProviderSubmissionPolicy.OperationKey(existing.Id)`. Nothing mail, upload or
image intake executes changes. The migration is grants-only — no schema change.

`ReconcileProviderSubmissions` is a **sixth bounded sweep on the already-live
`StagedArtifactReconciliationFunction` timer** (`*/10 * * * * *`) — no new
schedule, timer, queue, function, table or deployment unit, so the seven-function
Worker census is unchanged. The SQL outbox (`ExternalWorkItems`) was considered
and rejected with a reason: it carries work that calls outward, this repairs our
own rows. No new persisted state was needed — the staged receipt is already
derivable through `IIntakeWorkStore.FindBySourceIdentityAsync`.

The lane also found and fixed two real defects during the work rather than
hiding them: an EF Core translation failure where the candidate predicate could
not apply after projection (caught by its own new SQL-backed test — that test
earned its place), and two `WorkerCompositionTests` failures proving
`IActionHistoryWriter` was **never composed in the Worker at all**.

### Defect 1, HIGH — the committed-migration census. FIXED (`b332f7f6`)

`CommittedMigrationCreatesTheSqlServerSchema` pins the exact applied-migration
list and the new grants migration was missing from it, so the canonical
`--filter "Category!=Corpus"` command exits 1. Appended — an append to an
append-only ledger, not a weakened assertion.

### Defect 2, HIGH — the sweep poisons its own candidate window

The candidate query selects `StagedReceiptId == null OR no Accepted row`, orders
oldest-first and takes 50. A **bare reservation** — row created, intake never
retained — matches the first disjunct, is skipped without repair at
`ReconcileProviderSubmissions.cs:57`, and **can never be removed**:
`20260828111732_GrantProviderSubmissions` records that no `DELETE` is granted to
either role, and a bare reservation is unreachable by `GET` because the caller
never received a submissionId.

A ten-minute blob-storage degradation producing 60 such rows makes them the 60
**oldest** candidates permanently. From that moment every genuinely repairable
submission — exactly the after-write-2 and after-write-3 cases this ticket
exists to fix — sits outside the window and is never repaired. The log line is
**indistinguishable from health**: `50 candidates, 0 repaired, 0 failures` every
ten seconds, burning 300 reads a minute to achieve nothing.

The lane's own flag (c) analysed this backwards: it argued "oldest-first so
recent rows cannot starve older ones". The risk is the reverse.

**This is the second appearance of this exact shape today.** [[PR-069]]'s
`ListResolutionsToRecheckAsync` has the same defect — a capped, oldest-first
window permanently occupied by rows that can never advance, failing silently
with a healthy-looking log. Worth naming as a pattern rather than two
coincidences: *any bounded oldest-first sweep needs its un-repairable cases
excluded at the source, not skipped in the loop.*

### Defect 3, MEDIUM — two `Accepted` rows can land in permanent history

The only duplicate guard is a 1-minute grace anchored to `ReceivedAtUtc`, the
**original row-creation** time — so for the retried-old-submission case the
design depends on, the grace is already expired and gives zero protection. The
sweep can append `Accepted` between the inline path's write 3 and write 4.

Nothing dedupes them: `PegasusDbContext.cs:896-898` declares
`HasIndex(AggregateType, AggregateId, OccurredAtUtc)` and
`HasIndex(AggregateType, CorrelationId)` — **neither `IsUnique`** — and the two
rows carry different CorrelationIds. The same race fires without any retry
whenever the inline path takes over 60 s between `CreateAsync` and write 4,
which a 42 MB body plus one Azure Storage SDK retry can reach.

### Defect 4, MEDIUM — the repaired row states a time the provider did nothing

The recovered `Accepted` row is stamped with the sweep's clock and carries the
operation key instead of the request correlation id. In the replay case,
permanent history reads `Replayed 10:00` then `Accepted 10:05` — attributed to
the Provider Principal, for an accept that occurred days earlier, **ordered
after the `Replayed` row it logically precedes**, and unjoinable to the
originating request's logs. FRD-09 calls this record "the attributable action
actor in permanent history"; a permanent record that misstates when is worse
than the gap it fills. `candidate.ReceivedAtUtc` is available and honest.

### Defect 5, LOW — a missing grant would be invisible

The per-candidate catch counts failures but records no exception type or
message. Without the new grant applied, every write raises SqlException 229,
`IntakeExceptionPolicy.IsRecoverable` swallows it, and production logs only
`N candidates, 0 repaired, N failures` with no cause — locally invisible because
tests run full-privilege. **This is the 2026-08-14 worker-grant-gap diagnostic
problem reproduced.** The verifier notes it matches the existing convention
(`ReconcileUnidentifiedDestinations` behaves identically), so it is not a new
rule-12 violation — but it is being fixed anyway, because it is the difference
between a ten-minute and a ten-hour diagnosis.

### Defect 6, LOW — predicted FRD merge conflict. RESOLVED

Both lanes edited the same FRD-09 paragraph. [[AUTO-013]] merged first (#634);
`origin/dev` was merged forward into this branch and the file reconciled. The
verifier's scope check passed independently: `MaySubmit` is still enforced at
`ProviderSubmission.cs:393` and `CaseDataSnapshotFactory` is not in this diff.

### Disposition

Defects 2, 3, 4 and 5 go to a remediation lane — Claude Opus fixes, **Codex
`gpt-5.6-terra` (high)** verifies, rotating the pairing since Codex luna built
the original. The remediation must make the un-repairable case *unselectable*
rather than merely skipped, and must make the `Accepted` append genuinely
idempotent rather than time-guarded.
