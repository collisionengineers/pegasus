# Post-implementation report — INTK-035

*Promote an Unidentified triage request once its registration is known.*

- Branch `task/intk-035-open-triage`, HEAD **`26e463ee`**
- Base `task/intk-033-triage-from-intake` at `e6144344` (**PR #525, not merged**)
- **PR #533** — https://github.com/collisionengineers/pegasus/pull/533 —
  opened with `--base task/intk-033-triage-from-intake`. **Stacked: must merge
  after #525.**
- Diff against its base: 4 files, +218 / −1.

## What shipped

The staff half of the operator's Stage 0 rule — *"keep it as Unidentified until
a vehicle registration is known, then open the Triage"*. INTK-033 wired both
ends of that rule, but the only thing that could open a Triage was intake
processing re-running over the same receipt and happening to read a
registration it had missed. A triage request stating its registration only
inside an image dead-ended in Unidentified with no operator action to rescue
it. Now somebody can supply the registration.

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | `IsTriageRequest` widened `internal` → `public`. One word. |
| `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs` | `Triage` / `CanOpenTriage` state, `LoadTriageAsync`, `OnPostOpenTriageAsync`, `CloseUnidentifiedForTriageAsync`. |
| `src/Pegasus.Web/Pages/Intake/Details.cshtml` | Triage destination panel + "Open the Triage" form panel. |
| `tests/Pegasus.IntegrationTests/TriageFromIntakeIntegrationTests.cs` | `StaffSupplyingTheRegistrationOpensTheTriageAndClosesTheUnidentifiedItem`. |

## What was reused, and from where

Nothing new was introduced that an existing owner already covered.

- **`ProcessIntake.IsTriageRequest`** — the single owner of "did the accepted
  route classify this as a Triage request". Widened rather than copied; a second
  copy of the rule in the page is what "one list per concept" forbids.
- **`ITriageQueries.GetByOriginReceiptAsync`** — INTK-033's lookup, reused
  unchanged.
- **`ICreateTriageFromIntake`** — previously single-caller
  (`ProcessQueuedIntake.CreateTriageIfQualifyingAsync`); this is its second
  caller. No new port, no adapter.
- **`IImageIntakeOriginResolver`** — its `ImageIntakeOrigin` is the same four
  fields in the same order as `TriageOrigin`, so the mapping is field-for-field
  construction at the call site. No abstraction added: no second concrete caller
  would justify one.
- **`ReconcileUnidentifiedDestinations.ResolveForReceiptAsync`** — the one owner
  of INTK-007's supersession rule, including the Triage branch INTK-033 added.
  No second supersession written.
- **`ImageIntakeLifecycleRules.NormalizeRegistrationInput`** — existing
  normalization.
- **`StaffAuthorization.Require`** — the idiom the other non-`[Authorize]` staff
  surfaces use.
- **`Pegasus.Web.Pages.Triage.IndexModel.StateLabel`** — the existing single
  owner of the Triage state vocabulary, for the destination panel's status line.
- **Shapes copied from neighbours:** `OnPostOpenTriageAsync` is modelled on
  `OnPostRegisterImageIntakeAsync`; `LoadTriageAsync` on `LoadImageIntakeAsync`;
  the form panel on the Image-intake panel beside it; the test on the existing
  triage/unidentified fixtures.

## The `CorrectDraft` trap, and why it was avoided

`OnPostCorrectDraftAsync` → `IResolveIntake` with
`IntakeResolutionKind.CorrectDraft` is the obvious way to supply a registration
to a receipt, and it is **wrong** here.

`EfIntakeMutationStore.cs:194-220` **unconditionally rewrites the receipt's
decision** to `CaseCreated` (or `BlockedIntake`). For a triage request that has
two consequences, both faults:

1. It sends a triage request **back into case allocation** — precisely the
   fault INTK-033 was raised to fix. Re-using this path would have re-created
   the defect its own base branch removes, in the same stack.
2. It **breaks the deferral rule**. `ProcessIntake.IsDeferredForAutomation`
   keys off `NeedsSorting`; rewriting the decision away from `NeedsSorting`
   silently takes the receipt out of the deferred set.

So the handler deliberately does **not** touch the receipt's decision at all.
It reads the receipt, opens the Triage from evidence already recorded on it,
and closes the Unidentified item through the existing supersession owner. The
receipt stays `NeedsSorting`, which is what both the deferral rule and the
`CanOpenTriage` gate depend on. This is recorded as a rejected option in the
plan and as a `<remarks>` block on the handler, so the next person to reach for
`CorrectDraft` finds the reason.

## Simplification pass — honest dispositions

Run over this branch's own diff against its base: four self-applied lenses,
then an independent `code-simplifier` pass told the project rules and the
load-bearing constraints. Full tables are in the plan under
*Simplification pass — 2026-08-24*. **Five applied, six declined.**

**Applied (5):** (1) the Triage lookup was being issued on every receipt page
load including the vast majority that could never have one — gated on
`IsTriageRequest` first, behaviour-preserving because
`Triage is not null ⇒ IsTriageRequest`; (2) the three gate clauses were inline
unlike the sibling image-intake load — extracted `LoadTriageAsync`; (3)
`Where(…).Take(2).SingleOrDefault()` collapsed to one `SingleOrDefault(predicate) ?? throw`,
since `Take(2)` bought nothing; (4) `LoadTriageAsync` took a `receipt`
parameter while its sibling read the `Receipt` property — made consistent; (5)
a stray double blank line.

**Declined (6) — named, with the actual reason:**

1. **Inline `@Guid.NewGuid().ToString("N")` instead of the inherited
   `StaffPageModel.NewOperationKey()`.** Declined: *every* form panel in
   `Details.cshtml` does it inline. Changing only the new one makes the file
   less internally consistent; changing all of them is scope outside this
   branch. Pre-existing and file-wide. Not raised as a ticket — cosmetic and
   file-local.
2. **The new test hoists `FormUrlEncodedContent` where `ImageIntakeWebTests`
   hoists the token instead.** Declined: purely cosmetic, no readability gain,
   and the file it lives in sets no competing local convention.
3. **The handler's "exactly one accepted Triage-match record" message restates
   a check `EfTriageStore.CreateAsync` also makes.** Declined: the page must
   *select* the evidence in order to pass it, and `SingleOrDefault` enforces
   exactly-one as a by-product of that selection, not as a second copy of the
   rule. The store stays the authority; the page's message only reaches the log.
4. **`CloseUnidentifiedForTriageAsync`'s comment claims it behaves "exactly as
   the suggestion bookkeeping below", which logs where the other stays
   silent.** Declined: the claim is accurate about the *disposition* —
   advisory, non-blocking, sweep is the backstop — which is the point the
   comment makes. Logging is the better of the two behaviours. Reworded, the
   intent would be the thing lost.
5. **`IsTriageRequest` could move to the already-public
   `IntakeDecisionPolicy`.** Declined: the one-word widening is far the smaller
   change; moving the member would edit code outside this branch's diff and
   reassign ownership. That is a plan decision, not a simplification-pass one.
   Recorded rather than silently dropped.
6. **`Pages/Triage/*.cshtml.cs` gate on class-level `[Authorize(Roles = …)]`
   while this page and the Administration/Connect/Mcp surfaces call
   `StaffAuthorization.Require` — two authorization idioms for Triage
   mutations.** Declined: entirely pre-existing and outside this diff. Flagged
   for the reviewer, not reconciled here.

**Three bug suspicions raised and disproved** during the pass, written up so
the reviewer need not repeat them: a double POST minting two Triages
(unreachable — `EfTriageStore.CreateAsync` runs a `Serializable` transaction
and returns the existing record); the staff path accepting evidence that would
hit `EfTriageStore`'s null-forgiving `MatcherKey!` deref (unreachable —
`ValidateAcceptedMatchEvidence` runs first and fails closed with a handled
fault); and missing authorization (**confirmed the explicit
`StaffAuthorization.Require` is necessary, not redundant** — this page carries
no class-level `[Authorize]`). No defect was found in this branch's diff.

## Test evidence

**This is the correction to the caveat raised at implementation time.** The
original integration run was started *before* the simplifier's two edits and
used `--no-build`, so it exercised pre-simplification binaries. Everything
below was built and run against the final commit `26e463ee`.

### Built fresh, then run

| Command | Result |
| --- | --- |
| `dotnet build --configuration Release` | **0 warnings, 0 errors** |
| `dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build` | **947 passed, 0 failed**, 2 s |
| `dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build` | **99 passed, 0 failed**, 36 s |

### Contended — the whole slice this change touches

Run while other test runs were in flight on this machine (ten `dotnet`
processes observed concurrently):

```
dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build \
  --filter "FullyQualifiedName~Triage|FullyQualifiedName~Intake|FullyQualifiedName~Unidentified"
```

**141 passed, 0 failed, 6 skipped, 147 total, 6 m 58 s.**

The run was clean on the first attempt, so **no isolated re-run was needed to
disambiguate a failure** — none of the known contention artefacts on this
machine (`Connection Timeout Expired … post-login phase`,
`RegexMatchTimeoutException`) appeared. The 6 skips are all pre-existing
corpus-gated `QdosIntakeWebTests`, unrelated to this change.

### Isolated — this ticket's own new test

```
dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build \
  --filter "FullyQualifiedName~TriageFromIntakeIntegrationTests.StaffSupplyingTheRegistrationOpensTheTriageAndClosesTheUnidentifiedItem"
```

**1 passed, 0 failed, 1 m 18 s.** Green both inside the contended slice and
alone.

### What CI covers, not this machine

The **full** integration suite was deliberately not re-run locally — it takes
25–40 minutes here. CI runs it in three shards on the pushed SHA `26e463ee`
and **CI is the authority**. At the time of writing, PR #533's fast jobs
(`documentation`, `local-development-scripts`, `reference-data`) had passed and
the `changes` job — which gates the test shards — was still in progress. **The
shard results are not yet known and are not claimed here**; the reviewer should
read them off the PR rather than from this document.

Local coverage is therefore: build + unit + architecture + the triage / intake
/ unidentified integration slice. Everything outside that slice rests on CI.

## Not verified

- The full integration suite locally (delegated to CI, as above).
- CI's three test shards had not completed when this report was written.
- No UI screenshot or manual browser pass was taken; the panel's behaviour is
  covered by the integration test through the page handler, not visually.

---

## CI result — settled after this report was first written

All checks on PR #533 for SHA `26e463ee`
([run 32720795281](https://github.com/collisionengineers/pegasus/actions/runs/32720795281))
have since completed **green**:

| Check | Result |
| --- | --- |
| `changes` | pass (3 m 34 s) |
| `unit` | pass (3 m 49 s) |
| `sql-integration (1)` | pass (11 m 30 s) |
| `sql-integration (2)` | pass (8 m 40 s) |
| `sql-integration (3)` | pass (11 m 36 s) |
| `sql-integration-coverage` | pass (17 s) |
| `browser` | pass (7 m 30 s) |
| `documentation` | pass (31 s) |
| `local-development-scripts` | pass (19 s) |
| `reference-data` | pass (19 s) |
| `infrastructure` | skipping (not applicable to this diff) |

This closes the one gap the *Test evidence* section left open: the full
integration suite, which was delegated to CI's three shards rather than re-run
locally, **passed on all three**. The *Not verified* item "CI's three test
shards had not completed when this report was written" is therefore now
resolved; the remaining two items in that section still stand.
