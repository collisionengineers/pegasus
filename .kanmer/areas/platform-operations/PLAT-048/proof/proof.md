# Proof — PLAT-048: Service health snapshot and Engineer activity report queries

## What was verified, and where

Verified in the primary checkout `C:/Users/PC/Documents/GitHub/pegasus` on
merged `dev` at `b92cb9a7`. PLAT-048 reached `dev` as PR #591, merge commit
`33b99547` ("Merge pull request #591 from
collisionengineers/task/plat-048-service-health-report", 2026-08-28
12:41:37 +0000), first parent `41a17163`. All four recorded ticket commits
are reachable from the verification SHA:

```
git merge-base --is-ancestor 33b99547 b92cb9a7   -> 33b99547 IS ancestor of b92cb9a7
fc0537a1 reachable   40f6d043 reachable   2818fc26 reachable   11ad83b2 reachable
```

The merge carried 14 files, 1950 insertions, 1 deletion (`git show --stat
33b99547`): four Core/Infrastructure source files the ticket owns, three
adapters, two Web composition files, and four test files.

## Evidence

### The Service health snapshot has a real production caller

Tier: **build/test, plus a caller-backed source claim — not deployed.**

`GetServiceHealth` is consumed by the Operations page, not merely
registered. `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs:24` takes it,
and `:76`–`:79` calls it inside `OnGetAsync`:

```csharp
if (getServiceHealth is not null)
{
    ServiceHealth = await getServiceHealth.ExecuteAsync(actor, cancellationToken);
}
```

The snapshot is rendered: `src/Pegasus.Web/Pages/Operations/Index.cshtml:46`
opens the section, `:58` is the `Area | Service | State | Latest evidence |
Dependency | Action` header, and `:80` renders the Retry control as a POST to
the page's `RetryExternal` handler carrying `target.WorkItemId` and
`target.ExpectedAttemptCount` — exactly the `ServiceHealthRetryTarget` the
snapshot supplies. That handler
(`Index.cshtml.cs:84` `OnPostRetryExternalAsync`) executes
`RetryExternalWork`. So the drawn control maps to a named handler.

The caller was added by PLAT-023 (`6bf5f789`,
`git log -S "getServiceHealth" -- src/Pegasus.Web/Pages/Operations/Index.cshtml.cs`),
after PLAT-048 merged. PLAT-048 supplied the query; the page arrived
separately, and both are on `dev` at `b92cb9a7`.

### The snapshot's DI registration, and the composition gate on it

Tier: **registration.**

`src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs:34` registers
`GetServiceHealth` inside `AddPegasusAutomationMcp`, beside the
`IAutomationIngressStatusQueries` adapter it depends on (`:30`). That method
has exactly one call site, `src/Pegasus.Web/Program.cs:684`, inside
`if (automationMcpOptions is not null)` at `:682`.

**Consequence, stated plainly:** the Service health section is behind the
`Features:AutomationMcp` composition gate. Where the gate is closed,
`getServiceHealth` is null, `ServiceHealth` stays null and the page renders
no Service health section at all. The gate is enabled in production from
Bicep since release 9 (`infra/modules/platform.bicep:467`,
`docs/operations.md` § "Automation MCP is implemented and enabled in
production"), but that deployed revision predates this ticket, so it is not
evidence for this code.

Every other dependency of `GetServiceHealth` resolves unconditionally:
`IServiceHealthQueries` and `IEngineerActivityQueries` at
`src/Pegasus.Infrastructure/DependencyInjection.cs:257`–`:258`,
`IEvaSubmissionQueries` at `:165`, `IApprovedMailboxPollStatusQueries` at
`:274`, `ISendToAiControl` at `:334`.

### No probe is invented — every source is a recorded fact

Tier: **build/test.**

All eight ports `GetServiceHealth` composes read stored rows; none contacts
an external system:

| Port | Adapter | Reads |
| --- | --- | --- |
| `IApprovedMailboxPollStatusQueries` | `EfApprovedMailboxPollStatusQueries` | poll cursor rows |
| `IServiceHealthQueries` | `EfServiceHealthQueries` | `ApprovedSentPollStates`, `IntakeWorkItems` |
| `GetRequestOperations` | existing use case | operations projection |
| `IEvaSubmissionQueries` | `EfEvaSubmissionQueries` | `EvaSubmissions`, `ExternalWorkItems` |
| `IAiJobQueries` / `ISendToAiControl` | EF stores | job rows, control row |
| `IAutomationIngressStatusQueries` | `AutomationIngressStatusQueries` | OpenIddict application store (cached) |
| `IAutomationActivityQueries` | `EfAutomationActivityStore` | activity records |

`EfServiceHealthQueries.cs:10` states it: "Both are aggregate reads of rows
the Worker already writes; nothing here contacts a service."
`AutomationIngressStatusQueries.cs:15` delegates to
`AutomationClientRegistry.IsEnabledAsync`, which at
`AutomationClientRegistry.cs:88` reads `applications.FindByClientIdAsync` —
the store, not the network.

### Rows for uncomposed services are absent

Tier: **build/test.**

`ServiceHealthArea` (`ServiceHealth.cs:16`) is `Mail, Intake, Custody, Eva,
Ai, Automation`; `ServiceHealthDependency` (`:47`) is `MicrosoftGraph,
Worker, Box, EvaApi, AiConnector, AutomationClient`. Neither closed list
carries Experian, Glass's, Audatex or Cazana, and `ExecuteAsync`
(`:336`–`:418`) adds a row only per composed source. No guessed row exists
for a service with no source, as EPIC-011 §1.11 and the ticket require.

### D12: "Queries received" matches the decision

Tier: **build/test.**

D12 reads: *retained messages classified post-report-emails associated with
the Engineer's cases in the period.* The implementation at
`src/Pegasus.Infrastructure/Persistence/EfEngineerActivityQueries.cs:44`–`:79`
is clause-for-clause that:

```csharp
var mailboxChannel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
var postReport = MailTaxonomy.CategoryName(ReceivedMailFamily.PostReportEmails);
var queryReceiptIds = await context.IntakeReceipts
    .Where(item => item.SourceChannel == mailboxChannel
        && item.ReceivedAtUtc >= fromUtc
        && item.ReceivedAtUtc < toUtc
        && item.MailClassificationDecision != null
        && item.MailClassificationDecision.Family == postReport)
```

- The family string is the settled one:
  `MailClassificationContracts.cs:65` maps
  `ReceivedMailFamily.PostReportEmails => "post-report-emails"`.
- "Retained message" is the mailbox-channel intake receipt.
- "Associated with the Engineer's cases" is
  `CurrentIntakeAssociations.ReadAsync` (`:55`) — the same association rule
  the Inbox applies — then `CaseWorkflows.AssignedEngineerId` (`:63`–`:72`).
- "In the period" is half-open `[from, to)` on `ReceivedAtUtc`.
- The classification read is the **effective current** one: an operator
  correction rewrites the same row in place
  (`EfRetainedMailboxMessageStore.cs:386` `Apply(after, decision);`, with the
  before/after pair appended to `IntakeMailClassificationHistory`), so a
  corrected message counts under its corrected family.

`EngineerActivityReportPersistenceTests.CountsReportsAndQueriesPerAssignedEngineerWithinThePeriod`
(`tests/Pegasus.IntegrationTests/EngineerActivityReportPersistenceTests.cs:21`)
pins each clause with a seeded estate: a reversed association
(`active: false`), a wrong family (`"instructions"`), a receipt at the
exclusive `To` boundary, and a case with no assigned Engineer are all
excluded; the expected result is
`(engineerA, 2, 2)` and `(engineerB, 1, 1)`, and the `engineerId` filter
returns engineer B alone.

### CSV and the report's shape match EPIC-011 §1.12

Tier: **build/test.**

`EngineerActivityReportCsv.Header` is `"Engineer,Queries received,Reports"`
(`src/Pegasus.Core/Reports/EngineerActivityReport.cs:50`), and rows emit
`DisplayName, QueriesReceived, ReportsSent` in that order — the §1.12
"Engineer Report" table columns. RFC-4180 quoting is applied only where a
value contains `, " CR LF`; lines are CRLF-terminated.

### The new right

Tier: **registration/build.**

`StaffAccessRight.ViewOperationalReports` at
`src/Pegasus.Core/Identity/StaffAuthorization.cs:19`, mapped at `:53` into
the Administrator group (`actor.Kind == ActorKind.Staff &&
actor.IsInRole(StaffRole.Administrator)`), and required by
`GetEngineerActivityReport.ExecuteAsync`
(`src/Pegasus.Core/Reports/EngineerActivityReport.cs:99`). The Automation
actor is excluded — it is granted only `PerformCasework` at `:36`.

### The Engineer report has NO production caller

Tier: **registration only. This is a finding.**

```
git grep -n "GetEngineerActivityReport" -- src
  src/Pegasus.Core/Reports/EngineerActivityReport.cs:76
  src/Pegasus.Infrastructure/DependencyInjection.cs:259

git grep -n "EngineerActivityReportCsv" -- src
  src/Pegasus.Core/Reports/EngineerActivityReport.cs:49

git grep -n "ViewOperationalReports" -- src
  src/Pegasus.Core/Identity/StaffAuthorization.cs:19, :53
  src/Pegasus.Core/Reports/EngineerActivityReport.cs:99
```

There is no Administration Reports page on `dev`: `git ls-files
"src/Pegasus.Web/Pages/Administration/*"` lists Access, Accounts, Automation,
Configuration, MailCategories, Mailboxes, Organizations, Principals, Roles —
no Reports. `git grep -rn "EngineerActivity" -- src/Pegasus.Web` returns
nothing. `GetEngineerActivityReport`, `EngineerActivityReportCsv` and
`ViewOperationalReports` are DI-registered and test-covered but unreachable
from any operator surface. Under AGENTS.md rule 14 that half of the ticket is
not wired. PLAT-051 owns the consuming page.

### Build and test gate

Tier: **build/test.** Cited from the orchestrator's canonical gate evidence
for merged `dev` at `b92cb9a7`; not re-run here.

```
dotnet restore ./Pegasus.slnx --locked-mode                       -> exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore  -> 0 Warning(s), 0 Error(s)
dotnet test  ... --filter 'Category!=Corpus&Category!=Browser'
  Pegasus.ArchitectureTests   Failed: 0, Passed:  100, Skipped: 0
  Pegasus.Core.Tests          Failed: 0, Passed: 1133, Skipped: 0
  Pegasus.IntegrationTests    Failed: 0, Passed: 1022, Skipped: 2
```

The two skips are named in that record and are unrelated to PLAT-048. This
ticket's tests carry no excluded category —
`ServiceHealthPersistenceTests` and `EngineerActivityReportPersistenceTests`
are `[Trait("Category", "SqlServer")]`, which the filter does not exclude —
so they ran inside that `Failed: 0` result. Per-test results cannot be
quoted from an aggregate run; what is proven is that none of them failed.

Tests that exist for this ticket, by name:

- `tests/Pegasus.Core.Tests/Operations/ServiceHealthTests.cs` — nine methods,
  including `SnapshotComposesOneRowPerSourceAndNamesEachEvidenceTime`,
  `ExternalWorkRowsCarryTheRetryIdentityOfEachRetryableFailure`,
  `DispatchStateRanksFailureOverBackoffOverActivityAndNeedsEvidenceToBeCurrent`,
  `SnapshotRejectsASystemWorkerBeforeReadingAnything`.
- `tests/Pegasus.Core.Tests/Reports/EngineerActivityReportTests.cs` — six
  methods, including `ReportIsAdministratorOnly`,
  `ReportRejectsAnEmptyOrOverlongPeriod`,
  `CsvHasTheTableColumnsAndQuotesOnlyWhatNeedsIt`.
- `tests/Pegasus.IntegrationTests/ServiceHealthPersistenceTests.cs` — five
  methods over LocalDB.
- `tests/Pegasus.IntegrationTests/EngineerActivityReportPersistenceTests.cs`
  — two methods over LocalDB.
- `tests/Pegasus.IntegrationTests/OperationsWebTests.cs:69`
  `ComposedServiceHealthRenamesInternalVocabularyAndRetriesThroughTheCanonicalCommand`
  drives `/Operations` over HTTP against the composed application and asserts
  the retry POST produces a `RetryExternalWorkCommand` carrying the
  snapshot's work-item id and attempt count. Note the composition there is
  hand-built in the test (`OperationsWebTests.cs:182`), because production
  composition rides the `Features:AutomationMcp` gate — so this proves the
  page-to-command wiring, not the production registration path.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Every health row names its evidence time | Proven, with a documented caveat | `ServiceHealth.cs:336`–`:418`: each row's `LatestEvidenceAtUtc` is a stored timestamp or explicitly null, and a null one takes the `Configured` state rather than claiming currency. The page renders null as `—` (`Index.cshtml:66`–`:73`). Caveat, accepted at review (plan finding 1b): a `Failed` poll row's evidence time is its last *success*, because the cursor records no failure timestamp. |
| No probe is invented | Proven | All eight sources are store reads — see the table above; `EfServiceHealthQueries.cs:10`, `AutomationClientRegistry.cs:88`. |

## Outstanding

- **The Engineer activity report has no production caller.** `GetEngineerActivityReport`,
  `EngineerActivityReportCsv` and the `ViewOperationalReports` right are
  registered and tested but unreachable. Owner: **PLAT-051** (the
  Administration → Reports page). Under AGENTS.md rule 14 this ticket is not
  fully wired until that page ships.
- **The Service health section is gated.** It composes only where
  `Features:AutomationMcp` is on (`Program.cs:682`). No deployed, exercised
  (tier 3) evidence exists for this code: `main` has not been promoted, and
  the production revision that carries the gate predates this merge.
- **§1.11's "Retry/View" cell renders Retry only.** `Index.cshtml:80` draws a
  Retry form for a row with a `RetryTarget` and nothing for the rest; no
  "View" affordance exists. That cell lives in the Operations page, owned by
  **PLAT-023 / PLAT-049**, not in PLAT-048's Core files. Reported, not fixed.
- **Attribution is by the case's current Engineer.** Both counts resolve
  through `CaseWorkflows.AssignedEngineerId` at query time, so a reassigned
  case moves its history with it. Accepted at review (plan finding 2a);
  recorded for **PLAT-051**.
- **Browser/layout walk not attempted.** No claim is made here about clipped
  text or overflow at 1580/1100/760 for the Operations Service health table.
  Owner: **UIIMP-010**.
- **`StaleAfter` (15 min) reuse and `EvaRecentFailureWindow` (24 h)** remain
  unrecorded engineering choices (`ServiceHealth.cs:134`). Review finding 6
  directs them to PLAT-049's plan or `docs/open-decisions.md`; neither
  carries them yet.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.

---

# HELD — re-verified 2026-08-29, closeout board walk

## Verdict: **this ticket does NOT reach Done.** It stays in Verifying.

Re-verified against **merged `dev` at
`450b9234a6f5626f21adea3c4da244550a3bdace`** (2026-08-29 18:03:20 +0100).
`b92cb9a7`, the SHA the body above was written at, is an ancestor of it.

This remains **dev-merged evidence, pending the single wave-5 `dev` → `main`
promotion**.

## The finding above is confirmed, independently, at the newer SHA

The body already reported that the Engineer activity report has no production
caller. That was re-run from scratch rather than taken on trust:

```
git grep -n "GetEngineerActivityReport" 450b9234 -- src/
  src/Pegasus.Core/Reports/EngineerActivityReport.cs:76   <- its own declaration
  src/Pegasus.Infrastructure/DependencyInjection.cs:270   <- DI registration

git grep -n "EngineerActivityReportCsv" 450b9234 -- src/
  src/Pegasus.Core/Reports/EngineerActivityReport.cs:49   <- its own declaration only

git grep -n "IEngineerActivityQueries" 450b9234 -- src/
  …Reports/EngineerActivityReport.cs:21, :77, :86         <- declaration + ctor
  …Infrastructure/DependencyInjection.cs:269              <- DI registration
  …Infrastructure/Persistence/EfEngineerActivityQueries.cs:17  <- the adapter

git grep -n "ViewOperationalReports" 450b9234 -- src/
  …Identity/StaffAuthorization.cs:19, :54                 <- the right itself
  …Reports/EngineerActivityReport.cs:99                   <- its own guard
```

Every hit is a declaration, a DI registration, or the adapter. **There is no
consumer.** `git ls-tree -r --name-only 450b9234 -- src/Pegasus.Web/Pages/Administration/`
lists Access, Accounts, Automation, Configuration, Index, MailCategories,
Mailboxes, Organizations, Principals, Roles and Shared — **no Reports page**.

## Why that bars Done under D20

The ticket's own **What** section names this capability explicitly, as half (H)
of two:

> (H) `Core/Reports/EngineerActivityReport.cs`
> `IEngineerActivityQueries.GetAsync(from, to, engineerId?)` → reports sent …
> and queries received … ; right `ViewOperationalReports`; CSV export shape.

**D20 — strict rule 14:** *"A ticket reaches Done only when **every capability
it names** has a real production caller — a route, a rendered control posting
to a handler, or a registration plus a named consumer that is itself
reachable. A registered-but-unreachable port does not qualify, however honestly
it is disclosed and ticketed."*

`GetEngineerActivityReport` is registered in DI with **no consumer at all** —
the "registered in DI with no reachable consumer → **No**" row of the D21
table. The disclosure in the body above is honest and complete, and under the
pre-D20 reading ("headline capability reachable, deferred seams disclosed")
this ticket would have passed. D20 changed that reading deliberately, and the
decision record names PLAT-048 as the case that motivated it.

## The ticket that supplies the missing caller

**PLAT-051 — "Administration: Action Logs, Reports and Service health areas."**
Currently `backlog`. When its Reports page ships and calls
`GetEngineerActivityReport`, this half is wired and PLAT-048 can be re-proved
and moved to Done.

Nothing else can supply it: no other ticket on the board names the
Administration Reports page.

## What IS proven, and stays proven

The Service health half is fully wired and is **not** the reason for the hold:

- `GetServiceHealth` consumed at `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs:76`–`:78`,
  rendered from `Pages/Operations/Index.cshtml:46`, with the Retry control at
  `:80` posting to `OnPostRetryExternalAsync`.
- Its composition gate `Features:AutomationMcp` is **open in the deployed
  estate** — `infra/modules/platform.bicep:467` `Features__AutomationMcp=true`,
  and `docs/operations.md:131`, `:138` record it enabled in production since
  release 9. That is the D21 "gate OPEN in the deployed estate → **Yes**" row.

So the hold is narrow and specific: one named half of the ticket, waiting on
one named ticket.

## Commands run, with exit codes

```
dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false
  -> Build succeeded. 0 Warning(s), 0 Error(s).   exit 0

dotnet test ./Pegasus.slnx --configuration Release --no-build -nodeReuse:false
  --filter "…FullyQualifiedName~ServiceHealthTests|FullyQualifiedName~EngineerActivityReportTests…"
  -> Passed!  Failed: 0, Passed: 49, Skipped: 0, Total: 49  (Pegasus.Core.Tests)
     exit 0
```

Note what that green result does **not** mean: `EngineerActivityReportTests`
passing proves the report's logic, and proves nothing about it being reachable.
Test-only exercise is explicitly what rule 14 excludes.

## What this evidence does NOT prove

- **Nothing here is deployed.** `main` is at release 36; neither half of this
  ticket is in production.
- **The Service health section has never rendered in a deployed environment**
  from this code. The `Features:AutomationMcp` production evidence is release 9,
  which predates this merge.
- **No browser or layout walk** — **UIIMP-010** owns it.
- **This walk did not fix anything.** The closeout brief for this pass is board
  work only; no source file was changed.
