# Independent reviews — DELIV-012

Reviewed by `claude-code` (release owner), who did not implement any of the
changes below. Each review reads the diff itself, not the implementer's report.

## PR #425 — repair-specification store wiring — **PASS**

Branch `task/deliv-012-wire-repair-spec-store`, head `2d410159`, 5 files, +82/−50.

**Does it fix the finding?** Yes. `git grep -n IRepairSpecificationStore -- src/`
now returns a DI registration (`DependencyInjection.cs:261`) and a genuine
constructor-injected call site (`EfCaseAssessmentStore.cs:26`), which was the
stated end state. The store is no longer reachable only from tests.

**Does it duplicate the concept?** No — it removes duplication. `DraftQuery`,
`AcceptedQuery` and `NewLegacyDraft` become `internal static` members of
`EfRepairSpecificationStore`, and both the store's own guards
(`StartDraftAsync`, `GetCurrentAcceptedAsync`) and `EfCaseAssessmentStore`'s
legacy implicit-draft path now go through them. That is one owner for "what row
is the current specification", which was the point.

**Interface widening.** One method added, `GetCurrentDraftAsync`, exactly
mirroring the existing `GetCurrentAcceptedAsync`. No optional parameters, no
wrapper result type, no flag — the anti-patterns `docs/engineering.md` names are
avoided.

**Concern I raised and checked, rather than assuming.** The read helper
`CurrentSpecificationIdAsync` no longer uses the caller's `PegasusDbContext`; it
calls the store, and each store method opens its **own** context from the
factory. `SaveAsync` runs inside a `BeginTransactionAsync(IsolationLevel.Serializable)`
(`EfCaseAssessmentStore.cs:73-74`) and ends with
`return await GetRequiredAsync(context, ...)`. A second connection reading rows
locked by an open serializable transaction would block until timeout, and would
not see the uncommitted new specification.

Verified: `await transaction.CommitAsync(cancellationToken)` is at line 285, and
the `GetRequiredAsync` call is at line 294 — **after** the commit. So the second
context reads committed state, there is no lock contention and no stale read.
The concern does not apply. Cost is two extra short-lived contexts per read,
which is proportional and consistent with how the other stores in this
assembly already work.

**Evidence quality.** The implementer's claims were specific and checkable:
Release build 0 warnings/0 errors; Core 640/640; Architecture 97/97 (dependency
direction intact, which matters because this moves a Core-owned port into an
Infrastructure call path); `AssessmentPersistenceIntegrationTests` +
`RepairSpecificationMigrationTests` 7/7 **against LocalDB — actually executed,
not skipped**. Those cover the draft-resolution rule under the Automation actor,
the immutability guard and its message, acceptance/correction, and estimate-line
linkage by `RepairSpecificationId`.

**Judgement call I accept.** The implementer did not call `StartDraftAsync`
from `SaveAsync`, and the reason is sound and evidenced: it calls
`RepairSpecificationPolicy.RequireEngineer(actor)` unconditionally, while the
implicit legacy-draft path is also exercised by the Automation actor (proven by
the pre-existing test `AutomationSaveIsUnconfirmedAttributedAndParityLoggedWithAStaffSave`),
and it opens its own transaction and bumps `workflow.Version`, which would
double-guard and double-bump inside `SaveAsync`'s existing transaction. Adapting
the call site instead of widening the workflow method was the right call.

**Migration untouched** — confirmed empty diff for
`20260819112640_VersionedRepairSpecifications.cs`, so it cannot conflict with the
grants lane that edits that exact file.

**Verdict: pass**, subject to CI going green. No blocking findings; nothing
unapplied.
