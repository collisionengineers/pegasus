# Proof — INTK-001: truthful queued upload status

## Scope of this proof (decision D15)

Written against **merged `dev` at `450b9234a6f5626f21adea3c4da244550a3bdace`**
(2026-08-29 18:03:20 +0100).

This is **dev-merged evidence, pending the single wave-5 `dev` → `main`
promotion**. `main` serves release 36 (`783b4b88`); nothing here is deployed.
Per D15 the ticket walks to Done on this evidence; the exact-SHA, non-force
promotion to `main` happens once, at wave 5.

## The work is on `dev`

PR [#620](https://github.com/collisionengineers/pegasus/pull/620) merged as
`8e4f9346` ("fix(intake): make queued upload status truthful for
retry-scheduled work (INTK-001)", 2026-08-29 15:25:08 +0100).

```
git merge-base --is-ancestor 8e4f9346 450b9234   -> exit 0 (ancestor)
```

`git show --stat 8e4f9346` — 14 files, 314 insertions, 65 deletions, including
the new `src/Pegasus.Web/Presentation/UploadStatusRefresh.cs` and changes to
both status pages, `UploadOutcome.cs`, `DurableIntake.cs`,
`EfQueuedIntakeStatusQueries.cs` and `site.js`.

D17 recorded this ticket's commit `1594ff0e` as real but never pushed. It was
pushed, brought up to `dev`, reviewed and merged rather than discarded, as D17
required; the merged history carries eight commits ending at `6ff999b2`.

## Capability → production caller

Capabilities enumerated from this ticket's own **What**, **Approach**,
**Verification** and **Inherited scope** sections.

| Capability the ticket names | Production caller | Evidence |
| --- | --- | --- |
| (1) A retry-scheduled receipt no longer reads as **Received** and no longer reloads every 2 s | `UploadStatusModel.AutomaticRefreshMilliseconds` — `src/Pegasus.Web/Pages/UploadStatus.cshtml.cs:32`, calling `UploadStatusRefresh.DelayMilliseconds(Status, timeProvider.GetUtcNow())` at `:33`, and **null** once the file stops moving | rendered at `Pages/UploadStatus.cshtml:29` `data-auto-refresh="@Model.AutomaticRefreshMilliseconds"` on the panel |
| The durable fact that makes it honest | `QueuedIntakeStatus.RetryDueAtUtc` — `src/Pegasus.Core/Intake/DurableIntake.cs:99`, documented at `:87`–`:92` as "the time that attempt is due … so a surface can say how long the work genuinely cannot progress for instead of polling as if it were about to move" | projected by `EfQueuedIntakeStatusQueries` (changed in the same merge) |
| The refresh is bounded, and a background tab does not reload | `src/Pegasus.Web/wwwroot/js/site.js:15` reads `data-auto-refresh`; `:37`–`:47` `trackVisibility` clears the timer when `document.hidden` and reloads on return; `:26`–`:35` `reload` holds while `[data-refresh-hold][open]` is open | exactly one pending timer at a time, per the comment at `:18`–`:21` |
| The same treatment on the group page | `Pages/UploadGroupStatus.cshtml.cs:62` `UploadStatusRefresh.MinimumMilliseconds` and `:73` `UploadStatusRefresh.DelayMilliseconds(status, …)` | second production consumer of the same owner — no duplicate refresh policy |
| (2) An auto-associated receipt offers **Open case**, not "Open receipt" | `src/Pegasus.Web/Presentation/UploadOutcome.cs:201` `if (receipt.CurrentCaseId is { } caseId)` → `:209` `new("Open case", $"/Cases/Details/{caseId:D}")` | the comment at `:198`–`:200` states the reuse the ticket demanded: "The receipt's own `CurrentCaseId` is Core's reconciliation of the accepted and staff-linked associations, so it alone decides…" — link **or** association, one resolution owner, not a third copy |
| …rendered on the page | `Pages/UploadStatus.cshtml:55` `<partial name="Shared/_UploadOutcome" model="@outcome" />`, populated at `UploadStatus.cshtml.cs:81` | the route is `@page "/Upload/Status/{id:guid}"` (`UploadStatus.cshtml:1`), authorized to Administrator/Engineer/User (`:9`) |
| (3) **Inherited PLAT-015:** no lede paragraphs beneath the H1 | Absent | the full `Pages/UploadStatus.cshtml` was read at `450b9234`: between `<h1>@Model.Heading</h1>` (`:14`) and the panel (`:27`) there is only a `TempData` validation summary. No lede, no hint sentence, no empty-state prose |

Everything above is reachable through one authenticated route with no feature
gate and no composition flag. Nothing new is registration-only or test-only.

## Simplification rails the ticket set itself

- **"Reuse the existing case-id resolution rather than a third copy"** —
  satisfied: `UploadOutcome.cs:201` reads `receipt.CurrentCaseId`, Core's own
  reconciliation, rather than re-deriving from `CaseIntakeLinks` and
  `IntakeManualAssociations` separately.
- **"One state table"** — satisfied: `UploadStatusRefresh` is a single owner
  with two production consumers (`UploadStatus`, `UploadGroupStatus`).

## Commands run, with exit codes

Run in the main checkout on `dev` at `450b9234`, Windows + PowerShell 7.

```
dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false
  -> Build succeeded. 0 Warning(s), 0 Error(s).   exit 0

dotnet test ./Pegasus.slnx --configuration Release --no-build -nodeReuse:false
  --filter "FullyQualifiedName~UploadOutcomeQueriesTests"
  -> Passed!  Failed: 0, Passed: 16, Skipped: 0, Total: 16, Duration: 236 ms
     exit 0
```

No `SqlException` transport-level error and no build file lock occurred, so
the result is a clean PASS rather than INCONCLUSIVE.

CI on the branch head `6ff999b2` (run 33254732229): **success**, all four
`sql-integration` shards green.

## What this evidence does NOT prove

- **Nothing here is deployed.** `main` is at release 36. Tier-2 (build/test +
  caller-backed source) evidence only.
- **The browser behaviour was not exercised here.** The merge added
  `tests/Pegasus.IntegrationTests/Browser/UploadStatusRefreshBrowserTests.cs`
  (+59), which carries `Category=Browser` — **excluded from this run by
  instruction**. The `document.hidden` / bounded-interval behaviour is proven
  by source reading and by that test's existence, **not** by a browser run I
  performed. The merged-`dev` browser walk is **UIIMP-010**'s.
- **No real retry-scheduled receipt was observed.** The 30 min – 2 h retry
  window was not waited out against a live queue; the honest-state claim rests
  on `RetryDueAtUtc` being projected and consumed, not on an observation.
- **One checklist item remains unticked** on the ticket record (8 of 9). It is
  the browser-verified half of verification item 1, for the reason above.
- **`site.js` is PLAT-029's file.** This ticket appended the `data-auto-refresh`
  consumer to it, which D19 permits with disclosure; recorded here so the
  ownership call is visible rather than silent.
