# Proof — PLAT-053: External-work state vocabulary, one internal owner

## What was verified, and where

Verified on merged `dev` at `b92cb9a7`. PR
[#613](https://github.com/collisionengineers/pegasus/pull/613)
"PLAT-053: unify external-work state vocabulary under one owner" merged into
`dev` at `2026-08-29T09:12:21Z` as merge commit `940062c2`, carrying branch
tip `99483f55` and its parent `8a358ad4` — both of the SHAs recorded on the
ticket. All three are ancestors of `b92cb9a7`
(`git merge-base --is-ancestor` exit 0 for each), so the recorded commits are
reachable on the merge target. The merge is four files, 62 insertions, 35
deletions, all under `src/Pegasus.Infrastructure/Persistence/` — no
migration, no schema file, no test file, no Core file.

## Evidence

### One owner: the six constants have exactly one declaration site

Tier: file:line on merged `dev`.

`git grep -n "class ExternalWorkStatePersistence" b92cb9a7 -- src tests`
returns exactly one hit,
`src/Pegasus.Infrastructure/Persistence/ExternalWorkStatePersistence.cs:11`.
The whole file is 19 lines and holds the six constants and nothing else — no
parse method, no format method, no interface:

```csharp
internal static class ExternalWorkStatePersistence
{
    public const string Pending = "pending";
    public const string Dispatching = "dispatching";
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
```

### The three stores read it, and hold no literal of that vocabulary

Tier: file:line on merged `dev`.

`git grep -c "ExternalWorkStatePersistence" b92cb9a7 -- src tests`:

```
Persistence/EfEvaSubmissionQueries.cs:2
Persistence/EfEvaSubmissionWorkStore.cs:13
Persistence/EfExternalWorkStore.cs:24
Persistence/ExternalWorkStatePersistence.cs:1
```

The three named readers are exactly the three files the ticket's "Owns" list
names, and they are the owner's only readers anywhere in `src` or `tests`.

Grepping those same three files for the six words as literals leaves two
hits, both a different column:

```
EfExternalWorkStore.cs:480:  work.Case!.CustodyState = "failed";
EfExternalWorkStore.cs:646:  work.Case!.CustodyState = "failed";
```

`CustodyState` is a distinct property on the Case entity
(`PegasusDbContext.cs:1097`, configured at `PegasusDbContext.cs:485` and
`:666`), not `ExternalWorkItemEntity.State` (`PegasusDbContext.cs:1197`
onward). Leaving them literal is correct and is what the ticket documents
said would happen.

### The persisted strings did not change

Tier: mechanical source proof, corroborated by build/test.

The decisive check. Taking the three changed files at `940062c2`,
substituting each `ExternalWorkStatePersistence.X` reference back to its
declared literal value, and comparing against the same files at `940062c2^1`
with whitespace and line-wrapping normalised away (`tr -d ' \t\r\n'`):

```
EfExternalWorkStore.cs:        IDENTICAL after inlining the constants
EfEvaSubmissionWorkStore.cs:   IDENTICAL after inlining the constants
EfEvaSubmissionQueries.cs:     IDENTICAL after inlining the constants
```

So the diff is a literal-for-constant substitution and nothing else: no
control flow changed, no comparison operator changed, no branch added or
removed, and every word written to or compared against
`ExternalWorkItems.State` is what `dev` wrote before the merge.
`RetryScheduled` still persists as `pending` — the
`_ => ExternalWorkStatePersistence.Pending` catch-all in
`EfEvaSubmissionWorkStore.cs:174` is the same catch-all `_ => "pending"` that
was there before, and the `is not (...)` unknown-state guard in
`ClaimProcessingAsync` is present and unchanged in meaning.

Two independent test-side literals corroborate this, both green in the
canonical gate run.

`tests/Pegasus.IntegrationTests/ServiceHealthPersistenceTests.cs:96-122`
(`EvaFailuresAndActivityReadTheAttemptsAndTheQueue`) seeds `ExternalWorkItems`
rows with the raw strings and asserts the count returned by
`EfEvaSubmissionQueries.GetActivityAsync`, one of the three changed
methods:

```csharp
context.ExternalWorkItems.AddRange(
    Work(caseId, ExternalWorkKinds.SubmitCaseToEva, "pending"),
    Work(caseId, ExternalWorkKinds.SubmitCaseToEva, "completed"),
    Work(caseId, ExternalWorkKinds.VehicleLookup, "pending"));
...
Assert.Equal(new EvaSubmissionActivity(1, FixedUtcNow.AddHours(-1)), activity);
```

If `ExternalWorkStatePersistence.Completed` were anything but `"completed"`,
the in-flight count would be 2 and this assertion would fail.

`tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs:2077-2079`
does the same for `EfExternalWorkStore`: a `SavingChangesAsync` interceptor
fires only when a modified `ExternalWorkItemEntity` carries
`string.Equals(entry.Entity.State, "completed", StringComparison.Ordinal)`,
and `CancellationSqlFaultAndLeaseLossUseExactTaxonomyAndRequireStaffRecovery`
(line 817, arming it at line 868) depends on that injected failure firing.
The same class writes the literal `"processing"` into the column at lines
2113 and 2208.

### Build and test

Tier: build/test — cited, not re-run.

Canonical gate evidence for merged `dev` at `b92cb9a7`, run by the
orchestrating session on 2026-08-29:

```
dotnet restore ./Pegasus.slnx --locked-mode           -> exit 0
dotnet build ./Pegasus.slnx --configuration Release  -> 0 Warnings, 0 Errors
dotnet test  ... --filter 'Category!=Corpus&Category!=Browser'
  Pegasus.ArchitectureTests   Failed: 0, Passed:  100, Skipped: 0
  Pegasus.Core.Tests          Failed: 0, Passed: 1133, Skipped: 0
  Pegasus.IntegrationTests    Failed: 0, Passed: 1022, Skipped: 2
```

PR #613's own CI was green on every required job, including the categories
that gate run excluded (`gh pr checks 613`, Actions run `33242910635`):
`unit` pass, `sql-integration (1,2,3)` pass, `browser` pass (13m 01s),
`sql-integration-coverage` pass, and
`changes`/`documentation`/`local-development-scripts`/`reference-data` pass.
`infrastructure` was skipped by its path filter.

### The changed code has named production callers

Tier: registration plus a named consumer — not deployed.

`EfExternalWorkStore` — `DependencyInjection.cs:217-225` registers the
concrete type and four interfaces onto it (`IExternalWorkStore`,
`IQueuedExternalWorkReader`, `ICustodyRecoveryPersistence`,
`ICaseCustodyQueries`), unconditionally. Consumers of `IExternalWorkStore`:
`DispatchPendingExternalWork` (`Core/Custody/CustodyContracts.cs:561`,
implementing `ICommittedExternalWorkPublisher`),
`ReconcilePoisonedExternalWork` (`CustodyContracts.cs:684`), and
`EfQueuedCustodyProcessor` (`Persistence/EfQueuedCustodyProcessor.cs:13`).

`EfEvaSubmissionQueries` — `DependencyInjection.cs:165` registers
`IEvaSubmissionQueries` unconditionally. The changed method,
`GetActivityAsync`, has one caller: `Core/Operations/ServiceHealth.cs:376`,
inside `GetServiceHealth`, rendered by
`src/Pegasus.Web/Pages/Operations/Index.cshtml.cs:76-78` into
`Pages/Operations/Index.cshtml:13`. Composition note: `GetServiceHealth` is
registered only at `Web/Mcp/AutomationMcpExtensions.cs:34`, reached from
`Program.cs:682-684` only when `automationMcpOptions is not null`
(`Features:AutomationMcp`). `docs/operations.md:134-139` records that gate as
enabled in production since release 9 (2026-08-18), so the caller is real
rather than dormant — but it sits behind a composition gate and this proof
does not independently re-check the live flag.

`EfEvaSubmissionWorkStore` — `DependencyInjection.cs:620` registers
`IEvaSubmissionWorkStore` inside `AddEvaApiSubmission`, which the Worker
composes only on the production branch of
`WorkerDependencyInjection.cs:88-95` ("Composed only in production, and only
when EVA is configured"). Its one consumer is `ProcessQueuedEvaSubmission`
(`Core/Eva/EvaSubmissionWorkItem.cs:101-104`, calling `ClaimProcessingAsync`
at line 126), registered at `WorkerDependencyInjection.cs:94`. So this file's
caller chain is real but profile-conditional — and it is also the file with
no test of its own (see Outstanding).

## The ticket's own verification items

PLAT-053 has no `checklist` document; its acceptance is the ticket body's two
sentences plus the six steps in `plan`. Each is judged below.

| Item | Status | Evidence |
| --- | --- | --- |
| Give the six persisted words one internal owner | Proven | One declaration site, `ExternalWorkStatePersistence.cs:11`; 19 lines, constants only |
| Make the three named callers read it | Proven | `git grep -c` — 24 / 13 / 2 references in `EfExternalWorkStore.cs`, `EfEvaSubmissionWorkStore.cs`, `EfEvaSubmissionQueries.cs`; no other reader in `src` or `tests` |
| Behaviour-preserving | Proven | Inlining the constants makes all three files identical to `940062c2^1`; gate suite green |
| No schema change | Proven | `git diff --stat 940062c2^1 940062c2` — 4 files, all under `Persistence/`, no migration |
| Step 2: leave the two `Case.CustodyState = "failed"` sites alone | Proven | `EfExternalWorkStore.cs:480,646` still literal; `CustodyState` is a different property (`PegasusDbContext.cs:1097`) |
| Step 3: control flow of `EfEvaSubmissionWorkStore` untouched | Proven | Same normalisation; the `is not (...)` guard and the `_ => Pending` catch-all are present and unchanged |
| Step 5: build Release, run the focused filter | Proven at a higher tier | Not re-run here; the full-suite gate run on `b92cb9a7` supersedes the focused run, and PR #613's CI was green including `browser` and all three `sql-integration` shards |
| Step 6: touch no file outside the three | Proven | The merge's fourth file is the new owner class; nothing else changed |
| Follow-ups raised, not absorbed | Proven | `PLAT-056` and `PLAT-057` exist on the board, both `status: backlog`, both linked to PLAT-053 |

## Outstanding

**The vocabulary is not yet the repository's single spelling.** Nine further
Infrastructure classes still write or compare the same six words as literals
against `context.ExternalWorkItems`: `EfVehicleLookupWorkStore.cs` (15
literal occurrences), `EfQueuedCustodyProcessor.cs` (7),
`EfOperationsStore.cs` (7), `EfLinkedCaseReplacementStore.cs` (4),
`EfImageIntakeStore.cs` (4), `EfCaseWorkflowStore.cs` (4),
`EfCaseAcceptanceStore.cs` (3), `EfVehicleWorkflowStore.cs` (2),
`EfAutomaticEvaSubmissionStore.cs` (1). This is disclosed rather than
hidden — the owner class's own doc comment says so at
`ExternalWorkStatePersistence.cs:7-9` ("Not yet the vocabulary's only
reader … folding them onto this class is PLAT-056"). Owned by [[PLAT-056]].

**Record inaccuracy: "ten" should read "nine."** `files`, `plan`,
`post-implementation-report` and [[PLAT-056]]'s body each say "ten further
Infrastructure classes", but all four then enumerate nine files (five named
in the first review round, four added in the second). The enumeration is
correct and complete: a sweep of every file referencing
`ExternalWorkItems`/`ExternalWorkItemEntity` that also contains one of the six
words returns exactly those nine, plus the owner class itself and the two
`CustodyState` sites in `EfExternalWorkStore.cs`. Only the count word is
wrong. It changes no disposition and leaves PLAT-056 actionable, but it
should be corrected there rather than carried forward.

**`EfEvaSubmissionWorkStore` has no test anywhere.**
`git grep -n "EfEvaSubmissionWorkStore" b92cb9a7 -- tests` returns nothing
(exit 1). The gap predates this ticket, and it is tolerable for this diff only
because the file's change is compile-time-identical substitution — proven
above rather than asserted. Owned by [[PLAT-057]].

**Not deployed.** Every claim above sits at the registration or build/test
tier. This change is on merged `dev`; production runs the previously promoted
`main` image, so nothing here is claimed at the deployed-and-exercised tier.

**No browser/layout item applies.** PLAT-053 changes no Razor, no
`OperatorLabels`, no CSS and no rendered markup, so the 1580/1100/760 walk
owned by UIIMP-010 has nothing to assert against this ticket.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.
