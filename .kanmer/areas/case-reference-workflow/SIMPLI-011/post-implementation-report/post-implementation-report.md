# Post-implementation report — SIMPLI-011: decompose the Case Details workspace by capability

Branch `task/simpli-011-case-details` (worktree `../pegasus-worktrees/simpli-011-case-details`), from `origin/dev` `7bb184cb`, merged forward to `376bef3f`. Commits: `919faed1` (split), `8d90490a` (tests + docs), `9feca869` (merge dev), `a30e3a13` (simplification pass), `ec0c2220` (review follow-ups). Diff vs merge-base: 31 files, ≈ +2960 / −1622.

## What changed, file by file

### Web — the split (`src/Pegasus.Web/Pages/Cases/`)

| File | Change | Why |
| --- | --- | --- |
| `CaseMutationPageModel.cs` (new, 359 lines) | Abstract base for every page that mutates a case: staff actor (`TryGetActor`), the two command wrappers (`ExecuteCaseCommandAsync` / `ExecuteTransportCommandAsync`, one private core differing only in the refusal message), `RedirectToDetails`, the CASE-27 edit-mode TempData protocol (lease authority store/preserve/clear, claim/renew/release operation-key names, proposed-value retention with its 8000/2000-character limits and the retainable/boolean field sets, `PeekLeaseToken`/`PeekGuid` decoders), `IsLeaseLoss`, `Readiness`, `NewOperationKey`, `LogCaseCommandFailed`. Every member moved verbatim from `DetailsModel`; only visibility changed (members no derived page reads are `private`). | The third-copy rule: seven pages share one protocol. Mirrors `Administration/AdministrationPageModel.cs`. |
| `Details.cshtml.cs` (1938 → ~630 lines) | Keeps the workspace: `OnGetAsync`, `ClaimLease`/`RenewLease`/`ReleaseLease`, `ConfirmCompleteness`, `Save`, `RestoreLeaseState`, proposed-value read-back, display helpers; inherits the base; 10 constructor dependencies (was ~35). `RequireOperationKey` is now its private helper. The former `ClearLeaseAuthority` override (which reset two never-rendered properties) is gone. | The ticket's acceptance: `DetailsModel` loads and displays. `Save`/`ConfirmCompleteness` are the workspace's own edit form (open question 4, amended). |
| `Workflow.cshtml{.cs}` (new) | `/Cases/{id:guid}/Workflow` — Hold, ReleaseHold, ReturnToReview, AssignEngineer, StartWork, RecordEngineerFinding, CreateLinkedReplacement. | Named handlers per capability family (open question 1). |
| `Tasks.cshtml{.cs}` (new) | `/Cases/{id:guid}/Tasks` — CreateTask, AssignTask, CompleteTask, CancelTask, RecordManualChase, LinkReportEvidence, UnlinkReportEvidence. | |
| `Custody.cshtml{.cs}` (new) | `/Cases/{id:guid}/Custody` — RetryCustody, UploadDocument (with `MaximumStaffUploadBytes`, `SafeMediaType`), RemoveDocument, ConfirmThirdPartyVehicleEvidence, CreateRequestUploadLink, RevokeRequestUploadLink. | |
| `Vehicle.cshtml{.cs}` (new) | `/Cases/{id:guid}/Vehicle` — RequestVehicleLookup, AcceptVehicleSuggestion, GenerateEvaHandoff. | |
| `Closure.cshtml{.cs}` (new) | `/Cases/{id:guid}/Closure` — RecordReportApproval, Close, Reopen, Archive. | |
| `Eva/Download.cshtml{.cs}` (new) | `/Cases/{id:guid}/Eva/Download` — the former `EvaDownload` handler as `OnPostAsync` (file response, `Content-Digest`/`nosniff`/`no-store` headers, `SafeEvaFileName`), its own `LogEvaDownloadFailed` (Error level as before; the message now names the download instead of "the authorized case detail query"). | The one action that answers with content lives beside `Documents/Download` (open question 3). |
| All six new pages | Carry the old workspace's `[Authorize(Roles = Administrator, Engineer, User)]` and `[ResponseCache(Location = None, NoStore = true)]` (the latter restored on review). | Each page answers exactly as the old `DetailsModel` did. |
| `Shared/_CaseWorkflow.cshtml`, `Shared/_CaseDocuments.cshtml` | 29 forms gained `asp-page="/Cases/<Page>" asp-route-id="@workflow.CaseId"`; the EVA download form posts to `/Cases/Eva/Download` with no handler; 32 `NewOperationKey()` calls name the base. The six workspace forms (ClaimLease ×2, RenewLease, ReleaseLease, Save, ConfirmCompleteness) still post to the page. Handler-form count in the workspace partials 35 → 34 (the download form). | Every mutation redirects back to the workspace; `Details.cshtml` itself is unchanged. |
| `Documents/Export.cshtml.cs` | Adopts the base (−45 lines): its private lease-key vocabulary, `StoreLeaseAuthority`, `ClearLeaseState`, `TryGetActor` and three redirects removed; the "stale version keeps the lease" rule kept via `IsLeaseLoss`/`PreserveLeaseState`. Its token/case-id TempData encoding now matches Details' writer (it had drifted). | Open question allowed adoption "if trivial"; the simplification pass showed it was. |

Handler bodies were extracted by script from `git show HEAD:…Details.cshtml.cs` and are verbatim: no handler name, form field, TempData key, redirect target or message changed (the reviewer's normalised comparison: 33/34 byte-identical; the 34th differs by name and log message).

### Tests

| File | Change |
| --- | --- |
| `tests/Pegasus.IntegrationTests/CaseCapabilityPagesTestSupport.cs` (new) | Shared harness as a partial of `CaseDetailsWebTests`: `EnterEditModeAsync` (host + store substitution + GET + ClaimLease + leased GET), `Substitute<T>`, `LeasedWorkspace` (client, post/get, `MutationForm` → `LifecycleForm`), `AssertLeasedMutation`, `AssertClaimant`, the two base refusal checks (`AssertRefusalKeepsEditModeAsync`, `AssertLostLeaseClearsEditModeAsync`), and the `NextFailure` one-shot latch on the recording store. |
| `CaseWorkflowWebTests.cs`, `CaseTasksWebTests.cs`, `CaseCustodyWebTests.cs`, `CaseVehicleWebTests.cs`, `CaseClosureWebTests.cs` (new) | One test per page walks every handler from a leased workspace and asserts the recorded command (envelope + the handler's own fields) plus the page's TempData banner where distinctive (linked replacement, request secret, EVA revision, archive). Custody also covers the empty-upload guard; Vehicle also covers the EVA download page (file, headers, digest, refused → workspace, revision 0 → 404). The recording store is one `partial` fake extended per file — no second copy. Every handler on the six new pages is covered behaviourally: the 20 extracted handlers the research found untested, plus RetryCustody, GenerateEvaHandoff and the download. |
| `CaseEditModeWebTests.cs` (new, on review) | The two research handlers that stayed on the workspace — `RenewLease` and `ReleaseLease` — from a leased workspace: renew stores the renewed token and rotates the renew key; a non-lease-loss refusal keeps edit mode and the same key for the retry; leave clears edit mode and re-offers the claim. |
| `CaseDetailsWebTests.cs` | 19 POST URLs and 3 HTML assertions follow the new routes; the constructor-reflection assertion is gone (the behavioural tests prove the dependency); `LifecycleForm` gained the `params` tail; the store is `partial` and the manual-chase fake honours the latch. |
| `CaseReportApprovalWebTests.cs`, `Browser/OperatorJourneyTests.cs` | Two URLs → `/Closure?handler=RecordReportApproval`; the Playwright wait matches `/Eva/Download`. |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`, `WorkerAzureClientCompositionTests.cs`, `TypeInspection.cs` (new) | `WebCustodialPagesHaveNoDormantTransportPath` asserts the custody ports on `CustodyModel` and `IGetCase` on `DetailsModel`; both files share `TypeInspection.OnlyConstructorParameterTypes`. |

### Docs

`docs/current-architecture.md`: one implementation-map row for the workspace and its capability pages (which pages redirect, which answer with a file). `docs/design/README.md` lists no page files, so its `#case` content section needed no change (the visible workspace is unchanged).

## Verification

| Check | Result |
| --- | --- |
| `dotnet build --configuration Release` (whole solution) | 0 warnings, 0 errors on `a30e3a13` and `ec0c2220` |
| `Pegasus.Core.Tests` | 580/580 (`a30e3a13`) |
| `Pegasus.ArchitectureTests` | 94/94 (`a30e3a13`) |
| Integration filter `CaseDetailsWebTests|CaseReportApprovalWebTests|QdosCustodialWebTests|CaseCreateWebTests|CasesIndexWebTests` (includes the six page tests and the Export page) | 44/44 (`a30e3a13`); the new edit-mode test + two page tests 3/3 (`ec0c2220`) |
| Browser lane (`FullyQualifiedName~Browser`; the operator journey clicks through the split workspace) | 32/32 (`a30e3a13`) |
| Form counts | 29 forms retargeted; six ambient workspace forms remain; `asp-page-handler` in the two workspace partials: 34 (35 − the download form); folder total 38 with the pre-existing Assessment/Create forms |
| CI (PR #395) | first run: unit, browser, sql-integration 2/3, docs, reference-data pass; sql-integration (1) failed on a GitHub `setup-dotnet` download 503 (not code); the follow-up push re-runs the workflow |

## Simplification pass

Ran (four lenses + code-simplifier); 15 findings applied, 12 skipped/deferred with reasons — recorded in `plan` under "Simplification pass — 2026-08-17". Follow-ups filed: [[PLAT-002]] (one staff-actor root across the two Web bases and six page copies), [[CASE-001]] (unread `CaseDetailsStatus`, open question 5).

## Deviations from the plan

- Steps 2–5 landed as one commit rather than one per page: the extraction script produced all five pages from the HEAD file at once and the whole solution built first time, so the per-page staging bought nothing.
- The plan's "22 uncovered handlers" included `RenewLease`/`ReleaseLease`, which were never extracted; the six page tests covered the 20 extracted ones, and `CaseEditModeWebTests` (added on review) covers the two workspace ones. Six page-level tests rather than 22 single-handler tests — same coverage, ~40% of the boilerplate; the plan's diff estimate (~770 test lines) held (≈1,200 including the harness).
- "The lease-loss path once per page" became once in total (`CaseWorkflowWebTests`) plus the preserve-lease refusal once per page: both paths live once in the base's single `ExecuteCommandAsync`, so repeating the loss check per page proves nothing extra.
- `Documents/Export` adopted the base (allowed by the open question, not planned as a step).
- The pages initially dropped the workspace's `[ResponseCache(NoStore)]`; restored on review (`ec0c2220`).
