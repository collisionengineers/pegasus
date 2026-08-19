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
