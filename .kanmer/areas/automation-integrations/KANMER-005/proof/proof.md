# Proof — KANMER-005: Enforce exclusive editing leases between staff and Automation Actors

## What was verified, and where

Verified read-only against merged `dev` at `b92cb9a7` in the primary checkout
`C:/Users/PC/Documents/GitHub/pegasus`. PR #593 ("KANMER-005: exclusive edit
leases across staff and Automation Actors") merged into `dev` on
2026-08-28T15:51:21Z as merge commit `0b49e8dc`. The three recorded ticket
commits — `2ab02db3` (fix), `4a91c5c1` (tests), `8218b3f3` (simplification) —
are each an ancestor of `dev` `b92cb9a7` (`git merge-base --is-ancestor <sha>
HEAD` → exit 0 for all three), as is the review-round docs commit `45a43b63`
(the FRD-01 rollout sentence). The merge diff touches 24 files and nothing
outside the ticket's `files` document.

## Evidence

### Core owns one holder rule, and it is kind plus subject

Tier: source on merged `dev` (wired; build/test below).

`src/Pegasus.Core/Workflow/CaseEditAuthority.cs:49`:

```csharp
public static bool IsHolder(
    ActorKind? retainedLeaseHolderKind,
    string? retainedLeaseHolder,
    ActionActor actor)
{
    ArgumentNullException.ThrowIfNull(actor);
    return retainedLeaseHolderKind == actor.Kind
        && string.Equals(retainedLeaseHolder, actor.SubjectId, StringComparison.Ordinal);
}
```

`RequireLease` refuses through that same matcher at `CaseEditAuthority.cs:86`,
keeping the settled refusal order (expired before conflict). `git grep -n
IsHolder src/` returns exactly seven call sites and no second implementation:
Core (`:49`, `:86`), the replay read (`EfCaseWorkflowStore.cs:1247`), and the
four Web self-holder checks (`CaseMutationPageModel.cs:147`,
`Cases/Details.cshtml.cs:425`, `Cases/Assessment/Index.cshtml.cs:776`,
`Triage/Details.cshtml.cs:491`).

### The retained holder kind is written, cleared, parsed and projected

Tier: source on merged `dev`.

- Claim writes it — `EfCaseWorkflowStore.cs:178`:
  `workflow.EditLeaseHolderKind = request.Actor.Kind.ToString();`
- Release and save clear it with the rest of the tuple —
  `CaseMutationGuard.cs:78`, inside `ClearLease` (`:71`).
- One parser, `CaseMutationGuard.RetainedHolderKind` (`:95`), returning null
  for a pre-column lease and throwing `InvalidDataException` on an
  unrecognised value rather than reading it as a holder. Its four callers are
  the guard (`:63`), the replay read (`EfCaseWorkflowStore.cs:1248`), the case
  projection (`EfCaseQueryStore.cs:202`) and the operations projection
  (`EfOperationsStore.cs:665`). No second parser exists.
- `CaseEditLeaseSnapshot` carries `ActorKind? HolderKind`
  (`src/Pegasus.Core/Cases/CaseQueries.cs:99`).

### Migration `CaseEditLeaseHolderKind`

Tier: source on merged `dev`, applied by the fresh-schema census test.

`src/Pegasus.Infrastructure/Persistence/Migrations/20260828110108_CaseEditLeaseHolderKind.cs`
is a single additive `AddColumn` (`nvarchar(40)`, `nullable: true`) on
`CaseWorkflows`, with a column-only `Down`. The committed-migration census
pins it in order at
`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:111`,
followed by `Assert.Empty(await context.Database.GetPendingMigrationsAsync())`
— so a fresh schema built from the committed migrations carries the column and
nothing is pending.

Permissions (AGENTS.md rule 16): no grant migration accompanies it, and none
is required. `scripts/Test-MigrationGrants.ps1:71` scopes its rule to tables
created by `CreateTable(` in `Up()`; this migration creates no table, and the
column rides the existing `CaseWorkflows` table grant. CI runs that script
(`.github/workflows/ci.yml:60`) and it passed on PR #593 (below).

### Every case mutation reaches the one guard

Tier: source on merged `dev`.

`git grep -n 'CaseMutationGuard.RequireLease' src/` returns eight mutation
stores — `EfCaseAssessmentStore.cs:551`, `EfCaseDataStore.cs:478`,
`EfCaseTaskStore.cs:351`, `EfCaseWorkflowStore.cs:1342`,
`EfLinkedCaseReplacementStore.cs:422`, `EfRecordEngineerFinding.cs:221`,
`EfRepairSpecificationStore.cs:548`, `EfVehicleWorkflowStore.cs:970` — all
calling the single overload that now passes
`RetainedHolderKind(workflow.EditLeaseHolderKind)` to Core
(`CaseMutationGuard.cs:63`). There is no second write path that compares the
holder itself.

### Production caller — the staff path

Tier: rendered control plus its handler and DI registration, on merged `dev`.
Not deployed (see Outstanding).

The workspace renders the claim form at
`src/Pegasus.Web/Pages/Cases/Details.cshtml:229`
(`<form method="post" asp-page-handler="ClaimLease">`), and only in the
`details.ActiveEditLease is null` branch — an Automation-held case renders no
claim control. It posts to `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs:247`
(`OnPostClaimLeaseAsync`), which delegates to
`CaseMutationPageModel.ClaimLeaseAsync`, which calls the Core port at
`src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs:204`
(`await acquireLease.ExecuteAsync(...)`). The port is registered at
`src/Pegasus.Infrastructure/DependencyInjection.cs:296`
(`services.AddScoped<IAcquireCaseEditLease, AcquireCaseEditLease>()`) and its
store at `DependencyInjection.cs:291`
(`AddScoped<ILeaseCaseForEdit>(p => p.GetRequiredService<EfCaseWorkflowStore>())`).

Read-only rendering for a non-holder is the same wiring: `RestoreLeaseState`
clears this page's lease state when `IsHolder` is false
(`CaseMutationPageModel.cs:147`), and the sentence "Case locked - AI is
editing." is composed in `src/Pegasus.Web/Pages/EditModeDisplay.cs:18` from
`CaseEditAuthorityHolder.IsAutomation`, which is now derived from the retained
kind (`CaseEditAuthority.cs:139`) instead of from the subject's shape.

### Production caller — the Automation Actor path

Tier: tool declaration plus registration on a production-composed endpoint;
the fix itself is not deployed (see Outstanding).

`src/Pegasus.Web/Mcp/CaseMcpTools.cs:249` declares `pegasus_case_edit_begin`,
whose body calls the same Core port at `CaseMcpTools.cs:274` with
`context.Actor` — an `ActorKind.Automation` identity resolved from the bearer
token. The tool type is registered at
`src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs:117`
(`.WithTools<CaseMcpTools>()`) inside `AddPegasusAutomationMcp` (`:17`), which
the composition root calls at `src/Pegasus.Web/Program.cs:684`, with the
endpoint mapped at `Program.cs:1029` (`app.MapPegasusAutomationMcp()`). Both
sit inside `if (automationMcpOptions is not null)`, so the surface is composed
only where `AutomationMcp` configuration is present — and per
`docs/operations.md` the `/mcp` surface has been live in production since
release 9 (2026-08-18) with the gate enabled from Bicep. The Automation caller
is therefore a real production caller; what is not yet deployed is this
ticket's change to what that caller is held to.

`pegasus_case_edit_renew` (`:289`) and `pegasus_case_edit_end` (`:337`) reach
the same ports, and the write tools reach the same
`CaseMutationGuard.RequireLease`. No MCP schema or refusal text changed.

### Build and test

Tier: build/test on merged `dev` — cited from the canonical gate evidence,
not re-run.

Orchestrator run, 2026-08-29, Windows 11 + PowerShell 7, `dev` `b92cb9a7`:

```
dotnet restore ./Pegasus.slnx --locked-mode                        -> exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore   -> 0 Warning(s), 0 Error(s)
dotnet test ./Pegasus.slnx --configuration Release --no-build \
  --filter 'Category!=Corpus&Category!=Browser'
  Pegasus.ArchitectureTests   Failed: 0, Passed:  100
  Pegasus.Core.Tests          Failed: 0, Passed: 1133
  Pegasus.IntegrationTests    Failed: 0, Passed: 1022, Skipped: 2
```

The two skips are named in that evidence
(`QdosMappingExtractionTests.MappedInstructionEmailExtractsItsDocumentedFieldSet`,
`CustodyOutboxIntegrationTests.AcceptedCaseRetainsEmbeddedPhotographsBesideTheSource`);
neither is a KANMER-005 test. This ticket's five integration tests sit in
classes carrying `[Trait("Category", "SqlServer")]`
(`CaseWorkflowPersistenceTests.cs:16`, `AutomationAssessmentIngressTests.cs:17`,
`CaseDetailsWebTests.cs:20`), so they are inside that filter and among the 1022
passes. The Core matcher tests are in
`tests/Pegasus.Core.Tests/Workflow/CaseEditAuthorityTests.cs` (for example
`OnlyTheExactKindAndSubjectIsTheHolder` at `:105`).

CI on PR #593 (run `33184111505`, 2026-08-28) was green before merge: `unit`,
`sql-integration (1)`, `(2)`, `(3)`, `sql-integration-coverage`, `browser`,
`changes`, `documentation`, `local-development-scripts` and `reference-data`
all `SUCCESS`; `infrastructure` `SKIPPED`. That run is where the `browser`
category and `Test-MigrationGrants.ps1` were exercised.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| With an Automation Actor lease active, a staff user cannot claim the lease or edit the item. | Proven, build/test tier | `CaseWorkflowPersistenceTests.cs:1563` `AnAutomationHeldLeaseRefusesEveryStaffOperationAndTheHolderStillSaves` on a real SqlServer harness; its helper `AssertCompetitorIsRefusedAsync` (`:1684`) proves claim, write (`PutCaseOnHold`), renew, heartbeat and release each throw `CaseEditLeaseConflictException`. `AutomationAssessmentIngressTests.cs:517` proves it end to end: Automation claims over real HTTP MCP, the staff claim through the same Core port the workspace posts to throws (`:531`), and the workspace page fetched over HTTP contains "Case locked - AI is editing" with no `handler=ClaimLease` form (`:542-544`). |
| With a staff lease active, an Automation Actor cannot claim the lease or edit the item. | Proven, build/test tier | `CaseWorkflowPersistenceTests.cs:1609` `AStaffHeldLeaseRefusesEveryAutomationOperationAndTheHolderStillReleases`; `AutomationAssessmentIngressTests.cs:445` `AStaffHeldLeaseRefusesAutomationBeginWriteAndEndOverHttp` — real HTTP `pegasus_case_edit_begin`, `pegasus_assessment_update` (presenting the staff holder's own token) and `pegasus_case_edit_end` each refused with "case edit authority is held by another actor". |
| The active holder can save edits and release its lease successfully after a competing claim attempt. | Proven, build/test tier | `:1563` — after the five refusals the Automation holder heartbeats, then saves through `PutCaseOnHold`; the save consumes the lease (`Version == 1`; holder, kind and token null). `:1609` — the staff holder renews with the same token and releases, and the competitor then claims the free lease. Over HTTP, the Automation holder's own `pegasus_case_edit_end` returns 200 after the refusals (`AutomationAssessmentIngressTests.cs:560`). |
| Lease ownership remains unchanged after rejected claim or write attempts. | Proven, build/test tier | `Assert.Equal(held, await harness.ReadLeaseRowAsync(harness.CaseId))` — whole-row equality immediately after the refusals, in both directions (`:1577`, `:1623`) and for the same-subject impostor (`:1677`). Over HTTP, direct SQL reads confirm `EditLeaseHolderKind = 'Staff'` and `EditLeaseHolder = staff.SubjectId` untouched after three refused tool calls (`AutomationAssessmentIngressTests.cs:487-493`), and `EditLeaseHolderKind = 'Automation'` in the mirror direction (`:548`). |

Two precision notes, so the tier is not overstated:

- `CaseDetailsWebTests.cs:1241`
  `AnAutomationHeldCaseIsReadOnlyToStaffAndAPostedClaimIsRefused` posts the
  real `handler=ClaimLease` form over HTTP to the real Razor page, but
  substitutes fakes for `IGetCase` and `IAcquireCaseEditLease` (`:1252-1255`),
  so the refusal it asserts is stubbed. It proves the page behaviour — no
  claim control, locked copy, no subject disclosed, PRG on refusal — while the
  refusal itself is proven by the SqlServer and ingress tests.
- In `AutomationAssessmentIngressTests.cs:517` the staff claim is made through
  `IAcquireCaseEditLease` resolved from the application's own DI scope
  (`ClaimAsStaffAsync`, `:569`), not through the HTTP POST handler. That is
  the exact port `OnPostClaimLeaseAsync` calls
  (`CaseMutationPageModel.cs:204`), so the two tests together cover the path;
  no single test drives the HTTP POST claim against a real database.

The report's stated risk — that hosting the staff test identity and the MCP
bearer scheme in one factory might make the token endpoint reject — is
retired: that test is inside the gate run's 1022 passes.

## Outstanding

- **Deployed evidence (tier 3) does not exist for this change.** `8218b3f3`
  is not an ancestor of `origin/main` (`git merge-base --is-ancestor` → false;
  `origin/main` is at `783b4b88`, release 36). Production still serves the
  pre-KANMER-005 image, so `EditLeaseHolderKind` is not applied to the
  production database and no production lease has been exercised across the
  two actor kinds. Expected under D15; owned by the wave-5 `dev` → `main`
  promotion and its release.
- **The cutover window is unexercised.** A lease claimed by the current
  revision carries a null kind and, once the new revision serves, identifies
  nobody until it expires (≤ 5 minutes) — refusing its own holder's heartbeat
  and save. The rule is pinned by Core tests and written into
  `docs/frd/frd-01-case-identity-and-lifecycle.md:134` (`45a43b63`), but the
  live cutover is only observable at deployment. It belongs to the wave-5
  release, not to a further code ticket.
- **No browser/layout walk applies here.** This ticket changed no Razor markup
  and deliberately regenerated no snapshots (checklist item 9), so the
  1580/1100/760 clipping walk is out of scope; UIIMP-010 owns that walk for
  the epic.
- Carried forward unchanged from the ticket's own out-of-scope list: Details
  and Assessment still evaluate the holder match twice (a pre-existing
  CASE-024 shape) — for the CASE lanes; structured lease telemetry remains
  separate observability scope.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.
