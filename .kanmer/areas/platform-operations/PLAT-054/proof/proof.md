# Proof — PLAT-054: one Europe/London office-day conversion owner

## Scope of this proof (decision D15)

Written against **merged `dev` at `450b9234a6f5626f21adea3c4da244550a3bdace`**
(2026-08-29 18:03:20 +0100).

This is **dev-merged evidence, pending the single wave-5 `dev` → `main`
promotion**. `main` serves release 36 (`783b4b88`); nothing here is deployed.
Per D15 the ticket walks to Done on this evidence; the exact-SHA, non-force
promotion to `main` happens once, at wave 5.

## The work is on `dev`

PR [#611](https://github.com/collisionengineers/pegasus/pull/611) merged as
`23b0c564` ("PLAT-054: centralize London office boundaries in LondonCalendar",
2026-08-29 15:24:19 +0100).

```
git merge-base --is-ancestor 23b0c564 450b9234   -> exit 0 (ancestor)
```

`git show --stat 23b0c564` — 4 files, 164 insertions, 58 deletions:

```
src/Pegasus.Core/LondonCalendar.cs                    | 81 ++++++++++++---
src/Pegasus.Core/Operations/OperationsSnapshot.cs     | 51 ++--------
tests/Pegasus.Core.Tests/LondonCalendarTests.cs       | 71 ++++++++++++
tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs | 19 +++
```

The shape the ticket asked for: the conversion **left** `OperationsSnapshot.cs`
(−51) and **arrived** in an existing public Core owner (+81). No new file, no
new abstraction — the ticket said "e.g. `OfficeCalendar` beside `LondonCalendar`
if that is the right home — **search first**", and the search found
`LondonCalendar` already was the home.

## Capability → production caller

Capabilities enumerated from this ticket's own **What** and **Owns** sections.

| Capability the ticket names | Production caller | Evidence |
| --- | --- | --- |
| The office-day conversion is one **public** Core owner, no longer private | `LondonCalendar.DayAndWeekBoundariesAt` — `src/Pegasus.Core/LondonCalendar.cs:37`, a `public static` tuple-returning member on the existing `public static class LondonCalendar` (`:11`) | it is public and callable from outside the file, which the old private `GetOperationsSnapshot.OfficeBoundaries` was not |
| `GetOperationsSnapshot` calls it | `src/Pegasus.Core/Operations/OperationsSnapshot.cs:112` — `LondonCalendar.DayAndWeekBoundariesAt(asOfUtc);` | the sole remaining conversion site in that file |
| …and that consumer is itself reachable | `src/Pegasus.Web/Pages/Index.cshtml.cs:16` — `IndexModel(IGetOperationsSnapshot getOperationsSnapshot)`; the page is route `/` (`Pages/Index.cshtml:1` bare `@page`) and is the shell rail's first link (`Pages/Shared/_Layout.cshtml:63`) | registration at `src/Pegasus.Infrastructure/DependencyInjection.cs:267` `AddScoped<IGetOperationsSnapshot, GetOperationsSnapshot>()` |
| Behaviour-preserving for the dashboard | `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs` (+19 in this merge) and `LondonCalendarTests.cs` (+71) | ran green — see Commands |

The full production chain is therefore unbroken and gate-free:

```
route "/"  →  IndexModel (Pages/Index.cshtml.cs:16)
           →  IGetOperationsSnapshot / GetOperationsSnapshot
           →  OperationsSnapshot.cs:112
           →  LondonCalendar.DayAndWeekBoundariesAt   (the new public owner)
```

### The other members of the same owner also have production callers

Not required by this ticket, but recorded because "one conversion owner" is
only true if the owner is the one everybody uses:

| Member | Production caller |
| --- | --- |
| `StartOfDay` / `StartOfNextDay` | `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs:78`, `:79`, `:89`, `:92` (the Search page's received-date filters) |
| `DateAt` | `src/Pegasus.Core/Intake/ProcessIntake.cs:763` |
| `ToUtc` | `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs:85`, `:91`, `:111` |

## On the "and the Reports page both call it" clause — read carefully

The ticket's **What** says: *"make `GetOperationsSnapshot` **and the Reports
page** both call it."* The Administration → Reports page does not exist on
`dev` — `git ls-tree -r --name-only 450b9234 -- src/Pegasus.Web/Pages/Administration/`
lists Access, Accounts, Automation, Configuration, MailCategories, Mailboxes,
Organizations, Principals, Roles, Shared. No Reports.

This is **not** a rule-14 failure for PLAT-054, and the distinction matters:

- The ticket's **Owns** section is explicit and does not include that page:
  *"`src/Pegasus.Core/Operations/OperationsSnapshot.cs` (extract), the new
  owner file, Core tests for the boundary."*
- The ticket's own body names the page as a **downstream dependent**, not a
  deliverable: *"blocks the Reports page conversion in [[PLAT-051]]"*.
- Per the D20 scope note, measuring a ticket against a capability another
  lane owns "would make the epic's own file-ownership design incoherent".
- No new code here is unreachable. Every member of the new public owner has a
  named, reachable production caller today (tables above). PLAT-054's purpose —
  prevent a *second* conversion from being written — is discharged by the
  extraction itself.

**PLAT-051** is the ticket that will make the Reports page the second caller.

## Commands run, with exit codes

Run in the main checkout on `dev` at `450b9234`, Windows + PowerShell 7.

```
dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false
  -> Build succeeded. 0 Warning(s), 0 Error(s).   exit 0

dotnet test ./Pegasus.slnx --configuration Release --no-build -nodeReuse:false
  --filter "FullyQualifiedName~LondonCalendarTests|FullyQualifiedName~DashboardBoundaryTests|
            FullyQualifiedName~ServiceHealthTests|FullyQualifiedName~EngineerActivityReportTests|
            FullyQualifiedName~ProviderSubmissionTests"
  -> Passed!  Failed: 0, Passed: 49, Skipped: 0, Total: 49  (Pegasus.Core.Tests)
     exit 0
```

That focused run covers **both** of this ticket's test files —
`LondonCalendarTests` and `DashboardBoundaryTests` — with zero failures.

CI on the branch head `1e15e832` (run 33254413602): **success**, all four
`sql-integration` shards green.

## What this evidence does NOT prove

- **Nothing here is deployed.** `main` is at release 36. Tier-2 (build/test +
  caller-backed source) evidence only.
- **The 49-test figure is an aggregate.** It is the whole filtered set, not a
  per-class breakdown; what is proven is that none of the tests in those five
  classes failed.
- **Behaviour preservation is proven by tests, not by a production
  observation.** No deployed dashboard was compared before and after.
- **The Reports page conversion is not proven, because the page does not
  exist.** PLAT-051 owns it, and it is the ticket that will demonstrate the
  "one owner, two callers" outcome end to end.
- **PLAT-060** remains open: four Europe/London zone lookups elsewhere in the
  repository are still outside `LondonCalendar`. This ticket did not claim
  repo-wide consolidation, only the office-day conversion; PLAT-060 owns the
  rest.
