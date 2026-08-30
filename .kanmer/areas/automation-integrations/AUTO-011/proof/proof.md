# Proof — AUTO-011: AI job ledger and automation.jobs connector tools

## What was verified, and where

Verified on merged `dev` at `b92cb9a7b8bf7727b452aa397d9df04084da1270`, the
head this proof was taken against. AUTO-011 arrived on `dev` as PR #590,
merge commit `658a7984e381252b902540bc301e862391553ea5` (2026-08-28
11:30:05 +0100, parents `690ca579` and `b273a49e`), confirmed an ancestor of
`b92cb9a7` by `git merge-base --is-ancestor`. The merge touched 21 files,
+17,145 / -7 lines. Build and test results are cited from the orchestrator's
canonical gate evidence for `b92cb9a7`; they were not re-run here. Two
static census scripts were re-run locally because they are read-only file
scans, not part of the suite.

## Evidence

### The Core ledger exists with the kinds, states, record and policy claimed

Tier: build/test.

`git grep -n -E "^public (enum|sealed record|static class|interface)" dev --
src/Pegasus.Core/AiWork/AiJobs.cs` returns, among 24 public types:

```
src/Pegasus.Core/AiWork/AiJobs.cs:13:public enum AiJobKind
src/Pegasus.Core/AiWork/AiJobs.cs:21:public enum AiJobState
src/Pegasus.Core/AiWork/AiJobs.cs:32:public enum AiJobSubjectKind
src/Pegasus.Core/AiWork/AiJobs.cs:39:public enum AiJobResultKind
src/Pegasus.Core/AiWork/AiJobs.cs:65:public sealed record AiJobRecord(
src/Pegasus.Core/AiWork/AiJobs.cs:182:public interface IAiJobStore
src/Pegasus.Core/AiWork/AiJobs.cs:191:public interface IAiJobQueries
```

`AiJobKind` is `{Estimate, UnidentifiedResolution, QueryResponse,
UnidentifiedQueuePass}` (AiJobs.cs:13–20) and `AiJobState` is `{Queued,
Taken, DraftReady, Completed, Failed, Cancelled, Expired}`
(AiJobs.cs:21–30) — the exact sets the ticket names.

The 30-minute lease and the staff right are literal, not asserted:

```
src/Pegasus.Core/AiWork/AiJobOperations.cs:27:    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(30);
src/Pegasus.Core/AiWork/AiJobOperations.cs:28:    public static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(24);
src/Pegasus.Core/AiWork/AiJobOperations.cs:145:        StaffAuthorization.Require(transition.Actor, StaffAccessRight.PerformCasework);
```

Nine Core tests cover it (`tests/Pegasus.Core.Tests/AiWork/AiJobTests.cs`),
named at lines 29, 33, 54, 67, 80, 111, 137, 155, 177. They ran inside the
gate suite's `Pegasus.Core.Tests  Failed: 0, Passed: 1133`.

### The seven connector tools exist, carry the new scope, and are registered

Tier: build/test (registration plus in-process HTTP exercise). **Not
deployed.**

`src/Pegasus.Web/Mcp/AiJobMcpTools.cs` declares seven `[McpServerTool]`
methods at lines 49, 84, 129, 158, 188, 233 and 263. Every one opens with
the same scope gate, for example at line 62:

```csharp
var context = await resolver.RequireAsync(AutomationMcp.JobsScope, cancellationToken);
```

The scope is registered, not merely named:

```
src/Pegasus.Web/Mcp/AutomationMcp.cs:34:    public const string JobsScope = "automation.jobs";
src/Pegasus.Web/Mcp/AutomationMcp.cs:40:        [CasesScope, IntakeScope, DocumentsScope, AssessmentScope, MailScope, JobsScope];
```

The tool type is wired into the MCP server at
`src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs:124`
(`.WithTools<AiJobMcpTools>()`), and that server is mapped by
`MapPegasusAutomationMcp` → `app.MapMcp(AutomationMcp.McpEndpointPath)`.

### The production caller of the tools is the /mcp ingress, behind a gate

Tier: registration, with the composition gate open only in IaC.

`src/Pegasus.Web/Program.cs:683-685` and `1027-1030` are the two real call
sites:

```csharp
if (automationMcpOptions is not null)
{
    builder.Services.AddPegasusAutomationMcp(automationMcpOptions, productVersion);
}
...
if (automationMcpOptions is not null)
{
    app.MapPegasusAutomationMcp();
}
```

`automationMcpOptions` comes from `AutomationMcpOptions.TryCreate(
builder.Configuration)` (Program.cs:284), so the tools are reachable only
when `Features:AutomationMcp` is set. `infra/modules/platform.bicep:467`
renders `{ name: 'Features__AutomationMcp', value: 'true' }` for the Web
container app — that is infrastructure-as-code, i.e. what the *next* deploy
would set, not evidence of a running revision.

All seven tools are exercised over real HTTP against the in-process ingress
in `tests/Pegasus.IntegrationTests/AutomationAiJobIngressTests.cs`:
`pegasus_ai_job_list` (124, 186), `_create` (152, 168), `_take` (201, 335),
`_progress` (216, 346), `_complete` (230, 249), `_release` (271), `_fail`
(358). The tool inventory assertion carries all seven at
`AutomationMcpIngressTests.cs:55-61`. Those tests ran inside the gate
suite's `Pegasus.IntegrationTests  Failed: 0, Passed: 1022, Skipped: 2`.

### The migration and its grants ride the same diff, with the bootstrap census

Tier: build/test plus static census.

Both migrations are in the merge diffstat:
`20260828084601_AiJobs.cs` (+84) creates the table with five check
constraints and four indexes; `20260828084644_GrantAiJobs.cs` (+69) grants:

```csharp
migrationBuilder.Sql(
    $"GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[AiJobs] TO [{WebRole}];");
```

`WebRole` is `pegasus_web_runtime_role`; the Worker gets nothing, as D5
requires, and DELETE is deliberately absent. The bootstrap census carries
the matching block at `scripts/Invoke-AzureDatabaseBootstrap.ps1:353-361`
(`$expected.Add("pegasus_web_runtime_role|G|$permission|AiJobs")`), and the
committed-migration census names both files at
`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:107-108`.

Both census scripts re-run here on a clean tree at `b92cb9a7`:

```
pwsh -NoProfile ./scripts/Test-MigrationGrants.ps1
  -> Test-MigrationGrants: 82 migration files checked, every created table
     is granted or exempted.        exit=0

pwsh -NoProfile ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local
  -> Azure deployment plan validation passed (Local; Worker Disabled
     settings render 'true').       exit=0
```

Both are CI gates (`.github/workflows/ci.yml:60`, `:69`, `:129`).

### Real production callers of each shipped port

Tier: registration plus a named call site. Registrations are
`src/Pegasus.Infrastructure/DependencyInjection.cs:335-341`.

| Port | Production caller on `b92cb9a7` |
| --- | --- |
| `IAiJobStore` | `EfAiJobStore`; consumed by every use case and by `Core/Assessment/Estimates.cs:389,438` |
| `IWorkAiJob` | `src/Pegasus.Web/Mcp/AiJobMcpTools.cs:45` — the take/progress/complete/fail/release tools |
| `ICreateAiJob` | `src/Pegasus.Web/Mcp/AiJobMcpTools.cs:44` **and** `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:40` |
| `IAiJobQueries.ListOpenAsync` | `AiJobMcpTools.cs:71` |
| `IAiJobQueries.ListRecentAsync` / `GetCountsAsync` | `src/Pegasus.Core/Operations/ServiceHealth.cs:388-389` |
| `IConfirmAiJob` | `src/Pegasus.Core/Assessment/Estimates.cs:439` (`SetCurrentEstimate`) — see Outstanding |
| `ICancelAiJob` | **none** — see Outstanding |
| `IAiJobQueries.ListForSubjectAsync` | **none** — see Outstanding |

The strongest caller evidence is a rendered control posting to a named
handler that calls the port. `Pages/Cases/Assessment/Index.cshtml:516`
draws the form:

```html
<form method="post" asp-page-handler="SendToClaude" asp-route-id="@caseId">
```

and `Pages/Cases/Assessment/Index.cshtml.cs:409,442-450` is the handler it
posts to:

```csharp
public async Task<IActionResult> OnPostSendToClaudeAsync(
    Guid id, string operationKey, string? direction, int? targetPercent, ...)
...
    await createAiJob.ExecuteAsync(
        new(AiJobKind.Estimate, id, details.Summary.Reference,
            instruction, targetPercent, actor, operationKey),
        cancellationToken);
```

That caller landed after AUTO-011, in `36655f26` (ENG-025). The service
health caller landed in `fc0537a1` (PLAT-048) and reaches a rendered page:
`Pages/Operations/Index.cshtml.cs:24,76-78` resolves `GetServiceHealth` and
`Pages/Operations/Index.cshtml:63-76` renders its rows.

`ISendToAiControl`, the kill switch the ledger depends on, is registered
ungated at `DependencyInjection.cs:334`, so the Assessment caller resolves
whether or not the MCP gate is open.

### Consent descriptions

Tier: build/test.

`src/Pegasus.Web/Pages/Connect/Authorize.cshtml.cs:32-34`:

```csharp
[AutomationMcp.AssessmentScope] = "Read and update assessment values under an edit lease.",
[AutomationMcp.MailScope] = "List and read retained mail and correct a message's classification.",
[AutomationMcp.JobsScope] = "List, take, progress, complete, fail and release AI jobs; create Unidentified-queue passes."
```

`automation.jobs` and `automation.mail` are present and the stale "generate
EVA bundles" text is gone from `automation.assessment`.

### The ActionHistory events

Tier: build/test.

`src/Pegasus.Infrastructure/Persistence/EfAiJobStore.cs` writes aggregate
`ai_job` (line 25) with `ai_job_created` (69), `ai_job_expired` (149),
`ai_job_taken` (172), `ai_job_progress` (177), `ai_job_released` (183),
`ai_job_draft_ready` (196) and a state-derived kind for the terminals
(202). The integration test asserts the rows in SQL at
`AutomationAiJobIngressTests.cs:295,301,371`.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Grant census passes | Proven | `Test-MigrationGrants.ps1` exit 0, 82 files; `Test-AzureDeploymentPlan.ps1 -Mode Local` exit 0; CI gates at `ci.yml:60,69,129`; census rows at `Invoke-AzureDatabaseBootstrap.ps1:353-361` and `IntakePersistenceIntegrationTests.cs:107-108` |
| Kill switch refuses takes | Proven (build/test) | `AiJobTests.cs:111` `CreationAndClaimsAreRefusedWhileTheAdministratorSwitchIsOff`; `AutomationAiJobIngressTests.cs:308` `TheAdministratorSwitchRefusesClaimsAndProgressButNotFinishing`; both inside the gate suite's 1133 + 1022 passes |
| Lease expiry returns jobs to Queued | Proven (build/test) | `AiJobTests.cs:33` `ALapsedLeaseReadsAsQueuedAndAnUntakenJobPastExpiryReadsAsExpired`; `AiJobTests.cs:137` `ATakeCarriesAThirtyMinuteLeaseAndProgressRenewsIt`; `AutomationAiJobIngressTests.cs:26` `TheStoreReplaysCreationGuardsVersionsAndExpiresLeasesWithHistory` |
| Tools exercised through the MCP ingress test | Proven (build/test) | All seven tool names called over HTTP in `AutomationAiJobIngressTests.cs` (124, 152, 201, 216, 230, 271, 358); inventory assertion `AutomationMcpIngressTests.cs:55-61` |

Every item is proven at tier 2 (green build/green test) or by a static
census. **None is proven at tier 3.** Nothing in this ticket has been
exercised against the deployed estate.

## Outstanding

- **Not deployed.** `docs/operations.md` records release 36 as the estate's
  head, source revision `84132d01ccb0afca7af6c6ce519e6f3491aee160`
  (2026-08-28 02:38 UTC), migration head
  `20260827143200_GrantEvaSubmissions`. `git merge-base --is-ancestor
  658a7984 84132d01` returns false: release 36 predates the AUTO-011 merge
  by nine hours. `20260828084601_AiJobs` and `20260828084644_GrantAiJobs`
  are therefore **not applied in production**, the `automation.jobs` scope
  is not offered by any running revision, and no connector has consented to
  it. Owner: the wave-5 `dev` → `main` promotion and the release that
  follows it.
- **`ICancelAiJob` has no production caller.** It is registered at
  `DependencyInjection.cs:340` and implemented at `AiJobOperations.cs:499`,
  and `git grep -n ICancelAiJob dev -- src/` returns only those two lines
  plus the interface. The Operations "AI Job List" panel that would call it
  is not on `dev` — `git grep -rn "AiJob" dev -- src/Pegasus.Web/Pages/`
  returns only the Assessment page. Owner: **PLAT-049**.
- **`IAiJobQueries.ListForSubjectAsync` has no production caller.** Only the
  interface (`AiJobs.cs:196`) and the EF implementation
  (`EfAiJobStore.cs:239`). Owner: **PLAT-049**.
- **`IConfirmAiJob` is reached only by Core code that itself has no
  composition-root caller.** `Estimates.cs:439` consumes it inside
  `SetCurrentEstimate`, but `git grep -n "ISetCurrentEstimate|
  SetCurrentEstimate" dev -- src/` finds no Razor handler and no Web call
  site — the "Use estimate" control is unbuilt. The port is wired one layer
  deeper than it was at merge, but it is still not operator-reachable.
  Owner: **ENG-028**.
- **No browser/layout walk.** The "Send to Claude" dialog this ticket's
  ledger backs has not been checked at 1580/1100/760 for clipped text or
  overflow. Owner: **UIIMP-010**.

These four unwired seams are the same set the PR-#590 review recorded as
findings 6–8 and accepted for wave 4; two of them (`ListRecentAsync` /
`GetCountsAsync`, and the `ICreateAiJob` Estimate path) have since acquired
real callers, and are proven above. The remaining three have not, and are
not claimed as delivered.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.

## 2026-08-29 — Reversed out of Done under the strict rule 14 (D20/D21)

The operator settled rule 14 in favour of the strict reading after this proof was
written, and separately ruled that a disabled control or a closed feature gate is
never a delivered capability (D21). An independent GPT-5.6 audit, adjudicated
against this ticket's own What/Owns/Verification scope, found the following named
capabilities are not delivered on merged `dev` at `b92cb9a7`:

| Capability | Why it does not qualify | Wired by |
| --- | --- | --- |
| `ICancelAiJob` — the What names "cancel (staff)" | Census is exactly three lines: `src/Pegasus.Core/AiWork/AiJobs.cs:227` (interface), `AiJobOperations.cs:499` (implementation), `src/Pegasus.Infrastructure/DependencyInjection.cs:340` (`services.AddScoped<ICancelAiJob, CancelAiJob>();`). D21's "Registered in DI with no reachable consumer — No" row. | [[PLAT-049]] — `plan/plan.md:53-58` adds `OnPostCancelAiJobAsync` → `ICancelAiJob`; in review on PR #617, not merged at `b92cb9a7` |
| `IAiJobQueries.ListForSubjectAsync` — the What names "`IAiJobQueries` (open, by subject, recent, counts)" | Census is `AiJobs.cs:196`, `src/Pegasus.Infrastructure/Persistence/EfAiJobStore.cs:239`, and two test fakes (`ServiceHealthTests.cs:406`, `OperationsWebTests.cs:477`). Test-only code is named explicitly by rule 14. PLAT-049 does not supply it — its `plan/plan.md:39` loads `ListOpenAsync()` unioned with `ListRecentAsync(200)`, so `proof.md:249` assigning it to PLAT-049 is false. | **no ticket supplies this** — raised as [[AUTO-014]] |
| Staff creation of `AiJobKind.UnidentifiedResolution` — the What names the kind and "create (staff `PerformCasework`)" | Only Core mapping, validation and construction lines exist: `AiJobOperations.cs:33,41,276,370`. No Web caller. | [[PLAT-049]] — `plan/plan.md:53-58` adds `OnPostSendUnidentifiedToAiAsync` → `ICreateAiJob` with `AiJobKind.UnidentifiedResolution` |
| Staff creation of `AiJobKind.QueryResponse` — same clause of the What | Only `AiJobOperations.cs:32,43,275,326,338` plus an MCP parameter description string. No Web caller. [[TICK-101]] (AI-08) is backlog, plan-and-research only and blocked pending activation; [[MAIL-026]] only prefills the composer from an existing draft. | **no ticket supplies this** — raised as [[AUTO-014]] |
| `IConfirmAiJob` / `ConfirmAiJobCommand` (staff DraftReady→Completed) — this ticket's own new code under its Owns path `src/Pegasus.Core/AiWork/**`, enumerated in `plan/plan.md:11` | Its sole consumer is `Estimates.cs:439 IConfirmAiJob confirmJob,` inside `SetCurrentEstimate`, and the `ISetCurrentEstimate` census ends at Core, `EfRepairSpecificationStore.cs:334` and `DependencyInjection.cs:324` — no route or tool reaches it, so the consumer is itself unreachable. | [[PLAT-049]] (`OnPostCompleteAiJobAsync` → `IConfirmAiJob`) and [[ENG-028]] (the "Use estimate/Current chip" control that makes `SetCurrentEstimate` reachable) |

Nothing in the proof above is withdrawn — it remains accurate at the tier it claims.
What changed is the bar, not the evidence. This proof already recorded the facts
honestly at `proof.md:157-158` ("none" against `ICancelAiJob` and
`ListForSubjectAsync` under a heading claiming real production callers), and
`plan.md:61` accepted the gap as "not claimed as delivered". Under D20 honest
disclosure is no longer an exemption.

Checked and cleared, not findings: the Estimate creation path is real and ungated
(`Pages/Cases/Assessment/Index.cshtml:216-231` renders `Send to Claude`
conditionally disabled on `Model.SendToClaudeCondition` — D21's legitimate
"conditionally disabled with a named condition" row — and `Index.cshtml.cs:409
OnPostSendToClaudeAsync` calls `createAiJob.ExecuteAsync`). All seven
`pegasus_ai_job_*` tools are composed at `AutomationMcpExtensions.cs:124
.WithTools<AiJobMcpTools>()` behind `Features:AutomationMcp`, which
`docs/operations.md:122` and `:134-139` record as enabled in production since
release 9 (2026-08-18) — an OPEN gate, so those are real callers, and
`ListRecentAsync`/`GetCountsAsync` are reached the same way through
`GetServiceHealth` (`AutomationMcpExtensions.cs:34` → `Pages/Operations/Index.cshtml.cs:78`).
Nothing this ticket names is permanently inert or behind a closed gate.

### Findings that were NOT counted against this ticket

- The absent `/Operations` AI Job List panel, the "Send Unidentified to AI"
  control and the Review / Complete job / Cancel row actions — owned by
  [[PLAT-049]] (Owns: `src/Pegasus.Web/Pages/Operations/**`). This ticket's Owns
  names no file under `Pages/Operations`.
- The absent "Use estimate" control and Estimate-confirmation UI on the
  Assessment page — owned by [[ENG-028]] (Owns:
  `src/Pegasus.Web/Pages/Cases/Assessment/**`).
- `ISetCurrentEstimate` having no Razor caller —
  `src/Pegasus.Core/Assessment/Estimates.cs` is [[ENG-026]]'s file in the wave-3
  split; its caller is [[ENG-028]]'s to supply.
- Query-response draft consumption in the mail composer — [[MAIL-026]] (backlog).
- Post-report query origination workflow — [[TICK-055]] / [[TICK-101]] (both
  backlog, plan-and-research only).
- The seven `pegasus_ai_job_*` MCP tools and the `/authorize` consent
  descriptions behind `Features:AutomationMcp` — the gate is OPEN in production
  per `docs/operations.md:122` and `:134-139`, so these are real callers and not
  a rule-14 failure. Recorded here so it is not re-litigated.

---

# Re-audited 2026-08-30 against deployed `main` — the rule-14 gap is closed

Re-audited against **`origin/main` at `fb3f07acc8cca8d9d8b57db8a431b607772436dc`**,
deployed to production as release 37 on 2026-08-30. This is deployed evidence,
not dev-merged evidence.

## What this ticket was reversed out of Done for

The D20 strict re-reading of rule 14 found two capabilities this ticket's own
*What* section names that had **no production caller** — only declarations, DI
registrations and test fakes:

1. `IAiJobQueries.ListForSubjectAsync` — the by-subject AI job list.
2. `AiJobKind.QueryResponse` — staff creation of a query-response job.

[[AUTO-014]] was filed to supply exactly those two. It merged as PR #629 and
shipped in release 37.

## Both are now wired, verified by census rather than by trusting AUTO-014

### 1. `ListForSubjectAsync` has a routed consumer

```
git grep -n ListForSubjectAsync origin/main -- src/
  src/Pegasus.Core/AiWork/AiJobs.cs:196                      <- declaration
  src/Pegasus.Infrastructure/Persistence/EfAiJobStore.cs:239 <- adapter
  src/Pegasus.Web/Pages/Mail/Message.cshtml.cs:706           <- CONSUMER
```

`Pages/Mail/Message.cshtml.cs:706` is a routed Razor page's model, not a fake.
The previous audit's census of this same symbol returned only the first two
lines plus two test fakes; the third line is new and is the caller that was
missing.

### 2. `AiJobKind.QueryResponse` has a staff caller, and it is not an inert control

The chain is complete — a rendered form, a named handler, and the job creation:

```
Pages/Mail/Message.cshtml:74       <form method="post" asp-page-handler="CreateQueryResponse"
Pages/Mail/Message.cshtml.cs:228   OnPostCreateQueryResponseAsync(...)
Pages/Mail/Message.cshtml.cs:259       AiJobKind.QueryResponse,
```

The control is **conditionally** disabled (`Message.cshtml:94`, a `.gated` span)
when the case is unavailable, is not in post-report work, or automation is
stopped — each naming its reason via `OperatorLabels.QueryResponseJobs`. That is
a state-dependent affordance with a real enabled path at `:74`, not the
permanently inert control D21 forbids.

### 3. The MCP surface and its scope

`src/Pegasus.Web/Mcp/AiJobMcpTools.cs` carries 14 references to the seven
`pegasus_ai_job_*` tools, and `Mcp/AutomationMcp.cs:34` defines
`JobsScope = "automation.jobs"`.

**Its composition gate is open in the deployed estate**, which is what D21
requires — not merely registered:

```
infra/modules/platform.bicep:467   { name: 'Features__AutomationMcp', value: 'true' }
```

confirmed live on the running Web app during the release-37 deployment
([[DELIV-037]] proof).

### 4. The table and its grant shipped

```
scripts/Invoke-AzureDatabaseBootstrap.ps1:353
  # 20260828084644_GrantAiJobs: AUTO-011 added the pull-based AI job ledger
:361  $expected.Add("pegasus_web_runtime_role|G|$permission|AiJobs")
```

That migration is in release 37's applied set (head advanced 76 → 87 rows), and
the bootstrap census passed 544/377 with no missing grant.

## Verdict

Every capability AUTO-011 names now has a production caller that is itself
reachable in the deployed estate. **The reversal reason no longer holds.**

## What this evidence does NOT prove

- **No AI job has been created in production by a real operator.** The path is
  reachable; nobody has walked it. This is the same distinction release 37 drew
  for the Provider API.
- **The MCP tools are not exercised here.** Their ingress test covers them; this
  audit checked registration, scope and gate state, not a live tool call.
- **Single-model audit.** This pass was not independently refuted by a second
  model family. The evidence is a symbol census and a configuration read rather
  than a behavioural judgement.
- **`ListForSubjectAsync` is consumed on the Mail message page only.** Whether
  that is the right surface for it is AUTO-014's question, answered in that
  ticket, not re-opened here.

## Commands run

```
git grep -n ListForSubjectAsync origin/main -- src/            -> exit 0, 3 hits
git grep -n QueryResponse origin/main -- src/Pegasus.Web       -> exit 0
git grep -n 'automation.jobs' origin/main -- src/Pegasus.Web   -> exit 0
git grep -n Features__AutomationMcp origin/main -- infra/      -> exit 0
git grep -n AiJobs origin/main -- scripts/Invoke-AzureDatabaseBootstrap.ps1 -> exit 0
```
