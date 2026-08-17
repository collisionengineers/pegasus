# Plan — SIMPLI-011: decompose the Case Details workspace by capability

Diff estimate: ~20 files, ~+2000 / −1350 — of which ~770 lines are new behavioural tests for the 22 currently-untested handlers (the ticket's real cost). Web-only; no Core/Infrastructure/DB change; no ADR.

## Approach

Mechanical extraction along the seams the survey found: every mutation is already PRG and every form already posts the case id, so each of the 28 non-workspace handlers moves unchanged onto a page for its capability, each form gains one `asp-page`, and `Details.cshtml`/the visible workspace do not change. Shared plumbing moves once into an abstract `CaseMutationPageModel` (the third-copy rule); `EvaDownload` becomes its own download page. Then the missing tests are written in the existing `CaseDetailsWebTests` idiom, one file per capability. Reuses: `Cases/Documents/Export.cshtml.cs` (page shape), `Cases/Documents/Download.cshtml.cs` (file response), `Administration/AdministrationPageModel.cs` (base), `CaseDetailsWebTests` helpers (`AssertPrg`, antiforgery/lease setup).

Governing docs: `docs/design/README.md#case` (workspace content — unchanged), FRD-01 (behaviour — unchanged); `docs/current-architecture.md` implementation map updated. Verified premises: all `file:line` in `research`; assumed: none material.

## Steps (staged so the build and `CaseDetailsWebTests` are green after every stage)

1. **Base.** Add `Pages/Cases/CaseMutationPageModel.cs` by *moving* the shared members from `DetailsModel` (actor, command execution, redirect, lease TempData block, proposed-value retention write side, operation-key helpers, logging). `DetailsModel` inherits it. Build + `CaseDetailsWebTests` green with **zero handler moves** — proves the base is a pure move.
2. **Workflow page.** `Cases/Workflow.cshtml{.cs}` with the 7 handlers moved verbatim; the 7 forms in `_CaseWorkflow.cshtml` gain `asp-page="/Cases/Workflow"`; `CaseDetailsWebTests` URLs for `Hold`/`ReleaseHold`/`StartWork` retargeted; green.
3. **Tasks, Custody, Vehicle, Closure pages** — same recipe, one page per commit; the constructor-port assertion in `CaseDetailsWebTests.cs:69-73` retargets to the custody/vehicle models; `handler=` HTML assertions follow the new `asp-page` targets.
4. **EVA download page** `Cases/Eva/Download.cshtml{.cs}` (file response, headers preserved); download form/link retargeted; `Browser/OperatorJourneyTests.cs:127` and the `CaseDetailsWebTests` download assertions follow.
5. **`DetailsModel` trim** — remove the 27 unused ports and dead helpers; confirm 11 deps and only `OnGetAsync` + the five workspace handlers remain.
6. **Tests for the 22 uncovered handlers** — `CaseWorkflowWebTests`, `CaseTasksWebTests`, `CaseCustodyWebTests`, `CaseVehicleWebTests`, `CaseClosureWebTests`: each handler gets GET (antiforgery) → `ClaimLease` (where the command needs edit authority) → POST → `AssertPrg` + one persisted-state or TempData assertion; the lease-loss path once per page via the base.
7. **Docs** — `docs/current-architecture.md` implementation-map row; `docs/design/README.md` page inventory if it lists page files (content section untouched).
8. **Verify** — Release build 0/0; `Pegasus.Core.Tests`; `Pegasus.ArchitectureTests`; integration filter `CaseDetailsWebTests|CaseReportApprovalWebTests|Case*WebTests|CaseCreateWebTests|CasesIndexWebTests`; the Browser lane `OperatorJourneyTests` if the machine has Playwright, else CI's browser job; `rg "asp-page-handler" src/Pegasus.Web/Pages/Cases/Shared` count unchanged (35) and every moved form has `asp-page`.
9. **Simplification pass** over the diff (four lenses + code-simplifier; this diff is large enough for the full pass), findings appended here; post-implementation report; PR to `dev`.
10. **Independent review; CI green; merge; verify on merged `dev`; proof; closeout.**

## Verification (ticket acceptance)

- "The visible workspace remains intact": `Details.cshtml` unchanged; the four partials change only `asp-page` attributes; the design README `#case` list and the state-matrix rows still describe what renders; the Browser journey passes.
- "Extracted operations are covered by behavioural tests": every moved handler has an endpoint test; the 22 gaps are closed.
- `DetailsModel` loads and displays: `OnGetAsync` + edit-mode + completeness/save only; 11 dependencies.

## Risks / stop rules

- Stop and reassess if any handler turns out to re-render with `ModelState` (none found) or if a form lacks the hidden case id (all have it).
- Do not change handler names, form fields, TempData keys, or redirect targets — behaviour-preserving by construction.
- Do not touch Core use cases or FRDs.

## Simplification pass — 2026-08-17 (commit `a30e3a13`)

Scope: `git diff origin/dev...HEAD` after the split and the new tests (30 files). Four independent lens agents (reuse, simplification, efficiency, altitude) plus the `code-simplifier` agent in report-only mode; findings deduplicated and dispositioned below. Net effect of the pass: −100 lines, no behaviour change (handler names, form fields, TempData keys, redirect targets and messages unchanged; architecture 94/94 and the Case*/Export integration filter 44/44 green after the pass).

### Applied (behaviour-preserving)

| # | Lens | Finding | Fix |
| --- | --- | --- | --- |
| 1 | reuse (medium) | `Cases/Documents/Export.cshtml.cs` kept its own copy of the lease TempData vocabulary, `StoreLeaseAuthority`, `ClearLeaseState`, `TryGetActor`, the lease-loss test and three `RedirectToPage("/Cases/Details")` — and its token/case-id encoding had already drifted from Details' (`string`/`"D"` vs `string[]`/`Guid`; only tolerant readers hid it). The open question allowed adoption "if trivial". | `ExportModel : CaseMutationPageModel`; ~45 lines deleted; `PreserveLeaseState` / `IsLeaseLoss` / `RedirectToDetails`; the "stale version keeps the lease here" rule kept and commented. |
| 2 | simplification + altitude | `ExecuteCaseCommandAsync<T>` and `ExecuteTransportCommandAsync` were byte-identical bodies differing only in the `CaseError` text; the `<T>` was unnecessary. | One private `ExecuteCommandAsync(..., successMessage, failureMessage)`; the two protected names stay as one-line forwards so call sites and intent are unchanged. |
| 3 | altitude + simplification | `ClearLeaseAuthority` was `protected virtual` only so `DetailsModel` could reset `LeaseToken`/`CanRecoverLease` — both dead: `LeaseToken` is assigned once immediately before a `return`, `CanRecoverLease = true` is the statement *after* the clear, and every POST redirects. Verified by reading `RestoreLeaseState`/`OnGetAsync`. | Non-virtual base method; override deleted. |
| 4 | simplification + altitude | `RequireOperationKey` on the base had one caller (Details' three lease handlers). | Moved to `DetailsModel` as private. |
| 5 | simplification + code-simplifier | `LeaseTokenKey`, `MaximumRetainedProposed*Characters`, `RetainableFormFields`, `RequiresReacquisition` were `protected` with zero derived readers. | `private` (compiler-verified: build 0/0). |
| 6 | simplification + code-simplifier | Ten `using` directives carried wholesale from the parent file into the generated pages / base / Details (`Pegasus.Core.Cases`, `.Workflow`, `.Actors`, `.Lifecycle`). | Removed; build 0/0 confirms. |
| 7 | code-simplifier | `partial` on the five pages that declare no source-generated member. | Dropped (kept on the base and `Eva/Download`, which generate). |
| 8 | code-simplifier | 32 forms in the partials called `@DetailsModel.NewOperationKey()` although the factory is the base's and the forms post to the capability pages. | `@CaseMutationPageModel.NewOperationKey()`. |
| 9 | code-simplifier | Base class summary said "the command wrapper" (singular). | Names both wrappers and what distinguishes them. |
| 10 | reuse + altitude | Constructor-parameter reflection: `ConstructorPorts` in the integration test duplicated what the new behavioural tests prove (posting through the wire and observing the substituted port), and `ConstructorDependencies` in ArchitectureTests duplicated `GetOnlyConstructorParameterTypes` in the same project. | Integration-test reflection deleted; ArchitectureTests share one `TypeInspection.OnlyConstructorParameterTypes`. |
| 11 | reuse + simplification | `LeasedWorkspace.MutationForm` re-implemented `LifecycleForm`'s envelope. | `LifecycleForm` gained the `params` tail; `MutationForm` forwards to it. |
| 12 | simplification | Nine envelope assertion blocks over `CaseMutationRequest`-typed records repeated five lines each. | `AssertLeasedMutation(workspace, request, operationKey, reason)`; the fifteen per-family records with their own property names stay explicit. |
| 13 | efficiency | Custody test posted a second `CreateRequestUploadLink` only to read the secret; and the empty-upload refusal spun up a second LocalDB host for a guard that runs before any port. | Secret asserted from the first post (TempData survives the revoke because only the workspace reads it); the refusal folded into the page's test as a trailing post. |
| 14 | code-simplifier | `IDownloadEvaHandoff` fake chose refused/prepared with a 13-line ternary; `AssertClaimant`'s second parameter did not say which side is expected; `UploadBytes` was a partial-class-wide field with a file-generic name; `LeasedWorkspace.Client` had no outside reader. | Early `if`; `recordedActor`; `CustodyUploadBytes`; property dropped for the constructor parameter. |
| 15 | altitude (informational) | `ThrowNextFailure()` was armed on the hold/release/transition fakes but not `IRecordManualCaseChase`, so an armed latch could leak. | Added to the manual-chase fake (the approval store is a separate class and is not touched). |

### Skipped or deferred — with reasons

| Lens | Finding | Disposition |
| --- | --- | --- |
| reuse (low) | A second abstract Web base now carries `TryGetActor`/`NewOperationKey` beside `AdministrationPageModel` and six private page copies. | **Deferred → [[PLAT-002]]** (one staff-actor root). Verbatim moves; consolidating eight pre-existing copies is outside this diff. |
| reuse (low) | The 15 pre-existing `CaseDetailsWebTests` set-ups could use the new `EnterEditModeAsync` harness; `Substitute<T>` could live in `IntakeWebTestSupport` for other test files. | **Skipped — outside the diff.** Back-applying to 15 existing tests (two of which need a second client) is its own chore; not filed, low value. |
| reuse (optional) | `Eva/Download` declares `LogEvaDownloadFailed` (Error) where the inherited `LogCaseCommandFailed` (Warning) would do. | **Kept.** The original logged the download failure at Error; keeping the level is the behaviour-preserving choice. |
| simplification (optional) | `PreserveLeaseState` and `StoreLeaseAuthority` both guard whitespace. | **Kept.** Two names carry two intents (store after claim; preserve after refusal); one line each. |
| simplification (optional) | `Readiness(...)` is a pass-through to `new CaseReadinessEvidence(...)`. | **Kept.** Verbatim; two callers; the named helper reads better than a nested target-typed `new` inside another. |
| simplification vs altitude | Move `PeekLeaseToken`/`PeekGuid` to Details (only reader) — or keep the decoder beside the base's `string[]` encoder. | **Kept on the base** (altitude wins: reader beside writer of the same TempData encoding). |
| altitude (confirm-with-caveat) | The architecture test names `CustodyModel` for the custody ports; a scan of all Web page types would survive future re-splits. | **Kept named.** Naming the page is the documentation value; a scan would pass if the port moved to an unrelated page. |
| altitude / reuse | `[Authorize(Roles = …)]` per page; `asp-page`/`asp-route-id` per form. | **Confirmed correct.** No folder convention exists in `Program.cs`; every sibling carries the attribute. With endpoint routing an explicit `page` invalidates ambient route values, so `asp-route-id` per form is required; the same shape is the pre-existing `Documents/Export` convention. |
| efficiency | `command.Content.ToArray()` in the `IAddCaseDocument` fake; `Assert.Equal(bytes, upload.Content.ToArray())`. | **Kept.** The copy protects the fake against the port contract (content guaranteed only during the call); 8 bytes. |
| efficiency (recorded) | `DetailsModel` now injects 10 services instead of ~35, so the workspace GET no longer resolves ~25 use cases it never calls. | Positive side effect, no action. |
| code-simplifier (optional) | `IDownloadEvaHandoff` fake omits `ThrowNextFailure()`. | **Skipped.** Its refusal is a result outcome (`RefuseEvaDownload`), not an exception; nothing arms the latch there. |
| all lenses | The 28 verbatim-moved handler bodies, `SafeMediaType`/`SafeEvaFileName`/`MaximumStaffUploadBytes` (single-use, local), the redundant hidden `id` inputs beside `asp-route-id`, the header trio in `Eva/Download`. | **Checked, left alone** — moved verbatim; changing them is scope, not simplification. |
