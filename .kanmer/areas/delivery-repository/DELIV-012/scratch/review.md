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

## `task/deliv-012-renderer-container` — **PASS with two recorded trade-offs**

Head `f1f439b8`. Diff is three build files plus one doc — proportional to the change.

**Single-sourcing is real, not claimed.** `Directory.Build.props` defines
`<PlaywrightVersion>1.61.0</PlaywrightVersion>`; `Pegasus.Infrastructure.csproj`
now reads `Version="$(PlaywrightVersion)"` and `Pegasus.Web.csproj` sets
`<ContainerBaseImage>mcr.microsoft.com/playwright/dotnet:v$(PlaywrightVersion)-noble</ContainerBaseImage>`.
Both derive from one value, so a Playwright bump cannot leave the base image tag
behind — which matters because Playwright refuses to drive a browser build it did
not pin. Comments explain *why* rather than restating the code.

**Evidence is appropriate to the constraint.** There is no Docker on this
workstation and `az acr build` is prohibited by the runbook, so the image could
not be run locally. Rather than claiming more than that allows, the lane proved
containment structurally: `oras manifest fetch --oci-layout` → 14 layers
(13 base + 1 app); the config blob carries `PLAYWRIGHT_BROWSERS_PATH=/ms-playwright`,
`Entrypoint=["dotnet","/app/Pegasus.Web.dll"]`, `ExposedPorts 8080/tcp`,
`APP_UID=1654`; and replaying `config.history` against `rootfs.diff_ids`
identified the exact Chromium layer (`sha256:2c236c77…`, 776 MB) built by
`playwright.ps1 install --with-deps`. Separately the renderer was exercised
against a real Chromium locally — `AssessmentReportRendererTests` 6/6 in 29 s.
That is honest: structure proven now, execution-in-container proven at §7
verification after the deploy.

**Trade-off 1 — the base image carries the .NET SDK.** `mcr.microsoft.com/playwright/dotnet`
is an SDK image (`DOTNET_SDK_VERSION=10.0.301`), so production gains a compiler
toolchain it does not need. The alternatives — a custom slim base with Chromium
deps, or a multi-stage Dockerfile — both need Docker or `az acr build`, neither of
which is available or permitted here. Accepted for this release and worth a
follow-up ticket to build a minimal base once tooling allows; recorded rather than
silently absorbed.

**Trade-off 2 — image size, checked rather than assumed.** The archive is 1.36 GiB
versus roughly 0.10 GB for the previous `aspnet` base. I checked the registry:
`az acr show-usage` reports **1,320,700,039 bytes used of a 10,737,418,240 byte
limit** on the Basic SKU across 26 existing tags. The new image adds roughly
1.4 GB of base layers that do not dedupe with the existing aspnet-based tags,
taking usage to about 2.7 GB — comfortably inside the limit. Subsequent releases
add only the app layer, because the Playwright base layers will then dedupe. So
this is not a capacity problem, but the registry is worth watching.

**Not accepted into this branch, correctly:** the lane recommended raising the
Web Container App from 0.5 vCPU / 1 GiB to 1.0 vCPU / 2 GiB and did **not** edit
`infra/modules/platform.bicep`. That is right — it is an operator cost decision
and any `infra/` change alters what `azd provision --preview` should show, which
is a release stop condition.

**Found while doing its own job, and out of its scope:** `Test-AzureDeploymentPlan.ps1`
`-Mode Local` and `-Mode Artifact` both fail on clean `dev`. The lane correctly
established via `git log`/`git diff` that the cause predates its change, did not
attempt a fix outside its brief, and flagged it. I reproduced it independently and
routed it to the grants lane.

**Verdict: pass.** Blocking only on the release-gate fix landing first, since
`Build-ReleaseArtifacts` is validated by the same script.

## PR #422 — TICK-045 / MAIL-03 after takeover — **PASS**

Head `1d2a9ee4`, 6 files, +204/−2. Compare with what it was: 2 files, zero production lines, a fabricated classification result and a fabricated mailbox address.

**The evidence problem is genuinely fixed, and I can see why.** The test no
longer stores a `MailClassificationResult.Ambiguous(..., "shared-mail-policy", 9)`
literal that no policy emits. It resolves `IMailClassificationPolicy` from DI and
feeds content that makes the real `QdosMailClassificationPolicy` produce a
genuine Ambiguous outcome (a Triage phrase and an Audit title both matching), then
asserts `PolicyKey`/`PolicyVersion` come from the resolved policy. That is a test
that can fail for the reason it claims — the original could not.

**Falsifiability was demonstrated, not asserted.** The lane removed the DI
registration, observed `InvalidOperationException: No service for type
'IMailClassificationPolicy' has been registered`, reverted, and reran green; and
did the same break/revert cycle for the two new viewer tests, confirming the
revert with `git diff`. I weight this heavily because the defect being remediated
was precisely a green test that proved nothing.

**Fabricated mailbox removed.** `claims@collisionengineers.co.uk` — which appears
nowhere in `docs/operator-notes.md` and trips the repository's "never fabricate
domain emails" rule — replaced with the documented `engineers@collisionengineers.co.uk`.
The lane also stated plainly that no approval-path helper exists in that test
support and it used the file's existing `SeedPollStateAsync`, rather than
implying a cleaner route than it took.

**The caller is real.** `MailOperationalDestinationPolicy` was dark; it now has a
production caller at `/Inbox/{id}` rendering the operational destination and its
policy key/version inside the existing Classification-evidence panel, as a pure
derivation of already-loaded data — no new persistence, no new panel style, and
`OperatorLabels` reuses the exact "Needs sorting" string the page already renders
so no new operator-visible copy is introduced before INTK-007 migrates that
vocabulary.

**Capability wording now matches the evidence.** MAIL-02 names the exact caller
file and the two proving tests and states explicitly that UI-14 (categorised
queues) remains undelivered; MAIL-03 states it is proven against the registered
policy across the two documented mailboxes and is "Not deployed or live-mailbox
verified". Neither overclaims.

**Checklist honesty.** Item 5's "unsupported message failures" claim was never
true and was **restated rather than quietly left ticked** — the disposition I
care about most, since the review found this ticket's original 12/12 unearned.

**Verdict: pass.** Evidence: build 0/0, Core 640/640, targeted integration 31/31
on LocalDB.

**Owed after merge, not by this PR:** (a) reconcile TICK-044's checklist — the
caller its operator ruling demanded landed on *this* ticket's diff; (b)
`docs/capabilities.md` MAIL-04 still reads "Allocation only; owning evidence
still required" although TICK-046 delivered it — that row belongs to TICK-046 and
should be corrected in the release docs refresh, not silently here.

## Least-privilege scrutiny on the Unidentified census — worth recording

I challenged the INTK-007 lane's first census, which granted "both roles" on all
three Unidentified tables. Granting the Worker at all was correct — I verified
the path myself rather than accepting it: `IntakeWorkFunction`
(`src/Pegasus.Worker/IntakeFunctions.cs:30`) → `ProcessQueuedIntake`
(`src/Pegasus.Core/Intake/DurableIntake.cs:388-390`, which takes `ProcessIntake`
as a dependency) → the terminal outcomes INTK-007 routes into Unidentified. So
the Worker really does create these rows in production.

But "both roles" is not "the same permissions for both roles". Asked for a named
caller behind every permission, the lane traced each consumer and **removed two
grants that had no code path**:

| Table | Worker | Web | Change |
|---|---|---|---|
| `UnidentifiedItems` | SELECT, INSERT, UPDATE | SELECT, UPDATE | **Web INSERT dropped** — nothing in `src/Pegasus.Web` calls `IRegisterUnidentified`; only `ProcessIntake`/`DurableIntake` do |
| `UnidentifiedSequences` | SELECT, INSERT, UPDATE | *nothing* | **Web's grant dropped entirely** — `ResolveAsync` never touches the sequence table and Web never calls `RegisterAsync`, so the grant was unused |
| `UnidentifiedHistory` | SELECT, INSERT (UPDATE/DELETE denied) | same | unchanged — both paths insert, both read, nothing updates or deletes |

The Worker's `UPDATE` on items survived scrutiny with a specific justification:
`ProcessQueuedIntake.SynchronizeUnidentifiedAsync` resolves a stale open item
once a receipt reaches `CaseCreated`/`ImageIntakeRegistered`, and `ResolveAsync`
mutates the entity. That is a real path, not a defensive grant.

Migration SQL and census were updated **together** so the two match exactly —
the failure mode that matters is divergence, because the bootstrap then fails
against the real database mid-release rather than in CI. Core suite 655 passed.

Two process points worth keeping:

- The INTK-008 lane hit the same blocked gate and, rather than editing a tracked
  file or importing `Test-MigrationGrants.ps1` from another branch's unmerged
  work, copied scripts/infra/migrations to a **disposable scratch directory**,
  stubbed only the other branch's missing name to get past the early throw,
  proved its own assertion passed, and deleted it. Correct instinct: it answered
  the question the blocked script could not, without touching work that was not
  its own.
- Both lanes correctly identified `20260819104953_MailClassificationCorrectionHistory`
  as somebody else's failure and did not "fix" it. That is the behaviour the
  warning in their briefs was meant to produce.

## PR #426 — release gate, grants and guard — **PASS** (with a disclosure)

Head `5c24e61e`. All ten lanes SUCCESS.

**Disclosure of my own involvement.** I did not implement the migrations, the
census entries or `Test-MigrationGrants.ps1` — those are the lane's work and I
review them as an independent reader. But I authored two commits on this branch
myself: the CI step adding `Test-AzureDeploymentPlan -Mode Local` to the
always-on `changes` job. I cannot be an independent reviewer of my own commit,
so I am not claiming to be; instead that commit rests on objective evidence
rather than my judgement — GitHub's own job record for run `32263089802` shows
the steps "Migration runtime-grant check" and "Azure deployment plan (Local)"
both with conclusion `success`, i.e. the gate demonstrably executes and passes
in CI rather than merely being configured.

**Root cause, now pinned exactly.** I traced how a broken release gate reached
`dev`: `Test-AzureDeploymentPlan -Mode Local` already ran in CI, as the
"Validate infrastructure plan locally" step of the `infrastructure` job — but
that job is path-gated on `needs.changes.outputs.infrastructure == 'true'`.
Checking TICK-046's own PR #418, `infrastructure` is **SKIPPED**. So a
grant-carrying migration merged without the gate ever running against it, and
nothing surfaced until a release was attempted. That is the whole story, and it
is why the fix is "run it unconditionally" rather than "remember to run it".

**The three defects.** The `CaseRepairSpecifications` grant is justified per
operation from the two stores, with no Worker grant because the Worker reaches
neither. The `EvaHandoffDownloadOperations` migration mirrors its siblings'
verified production shape and closes a defect that is live right now. The census
additions match the SQL each migration emits — which is the property that
matters, since divergence fails against the real database mid-release rather
than in CI.

**The guard is honest about its own limits.** It searches the whole migrations
folder for a satisfying `GRANT` rather than only the creating file, because a
follow-up migration is a legitimate way to close a gap — and it was self-tested
against a synthetic ungranted table to prove it still fails when it should. 65
tables across 16 pre-least-privilege migrations are exempted, each confirmed
covered by `20260729199000_RuntimeRoleReconciliation`'s own grant arrays rather
than waved through.

**One observation I am deliberately not acting on now.** `Test-AzureDeploymentPlan
-Mode Local` now runs twice on an infra-touching PR: unconditionally in `changes`
(ubuntu) and again in `infrastructure` (windows). That is not pure duplication —
the release itself is executed from Windows, so a Windows run of the release
script has real value, while the ubuntu run guarantees it always runs at all.
The cost is roughly 40 seconds on infra PRs. Recorded rather than churned,
because editing `ci.yml` again would re-run the full ~15-minute lane set on the
one PR every other branch is blocked behind. Worth a comment clarifying the
intent in a later PR so a future reader does not mistake it for an accident.

**Verdict: pass.** Merging first, by necessity — every other branch needs this
`dev` baseline before it can verify its own gate.

## PR #416 — INTK-005 grouped upload after takeover — **PASS**

Head after my census merge, 20 files, +7436/−65, all 10 lanes SUCCESS (one
earlier shard failure was a SQL **connection timeout** on the runner, not a
test assertion — re-run passed).

**Both blockers verified in the code, not the report.** `GroupedIntake.cs:153-154`:
`ChildToken(token, ordinal) => ordinal == 0 ? submissionToken : $"{token}:{ordinal}"`
— a single-file upload keeps its bare token, so receipt correlation and replay
are preserved. `Upload.cshtml.cs:141-155`: a one-member group redirects to
`/UploadStatus` exactly as before; only genuinely multi-file groups go to
`/UploadGroupStatus`. Those are the two behaviours CI was proving broken.

**Grants** are SELECT+INSERT for the Web role only, evidenced from
`EfIntakeSubmissionGroupStore` (no UPDATE, no Remove) and no Worker reference —
and the census entries I added match them.

**The thing I weight most:** the lane caught that its `dev` merge had silently
dropped its own migration id with no conflict markers, and restored it. That is
the overwrite-loss class this ticket exists to prevent, and it is why every
later INTK merge was told to diff the list against the folder.

**Honest gaps, stated not hidden:** full-solution `dotnet test` was not run
(~28 min) and is left unticked; Core/persistence tests for empty-group,
duplicate-filename, partial-failure and concurrent-replay were not added.
Checklist 7/33 → 28/38 with reasons for the rest.

Verdict: pass. Merge first of the INTK set — #417 is stacked on it.
