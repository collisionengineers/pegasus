# Post-implementation report — SIMPLI-011: decompose the Case Details workspace by capability

Branch `task/simpli-011-case-details` (worktree `../pegasus-worktrees/simpli-011-case-details`), from `origin/dev` `7bb184cb`, merged forward to `376bef3f`. Commits: `919faed1` (split), `8d90490a` (tests + docs), `9feca869` (merge dev), `a30e3a13` (simplification pass). Diff vs merge-base: 30 files, +2830 / −1622.

## What changed, file by file

### Web — the split (`src/Pegasus.Web/Pages/Cases/`)

| File | Change | Why |
| --- | --- | --- |
| `CaseMutationPageModel.cs` (new, 359 lines) | Abstract base for every page that mutates a case: staff actor (`TryGetActor`), the two command wrappers (`ExecuteCaseCommandAsync` / `ExecuteTransportCommandAsync`, one private core differing only in the refusal message), `RedirectToDetails`, the CASE-27 edit-mode TempData protocol (lease authority store/preserve/clear, claim/renew/release operation-key names, proposed-value retention with its 8000/2000-character limits and the retainable/boolean field sets, `PeekLeaseToken`/`PeekGuid` decoders), `IsLeaseLoss`, `Readiness`, `NewOperationKey`, `LogCaseCommandFailed`. Every member moved verbatim from `DetailsModel`; only visibility changed (members no derived page reads are `private`). | The third-copy rule: seven pages share one protocol. Mirrors `Administration/AdministrationPageModel.cs`. |
| `Details.cshtml.cs` (1938 → ~630 lines) | Keeps the workspace: `OnGetAsync`, `ClaimLease`/`RenewLease`/`ReleaseLease`, `ConfirmCompleteness`, `Save`, `RestoreLeaseState`, proposed-value read-back, display helpers; inherits the base; 10 constructor dependencies (was ~35). `RequireOperationKey` is now its private helper. The former `ClearLeaseAuthority` override (which reset two never-rendered properties) is gone. | The ticket's acceptance: `DetailsModel` loads and displays. |
| `Workflow.cshtml{.cs}` (new) | `/Cases/{id:guid}/Workflow` — Hold, ReleaseHold, ReturnToReview, AssignEngineer, StartWork, RecordEngineerFinding, CreateLinkedReplacement. | Named handlers per capability family (open question 1). |
| `Tasks.cshtml{.cs}` (new) | `/Cases/{id:guid}/Tasks` — CreateTask, AssignTask, CompleteTask, CancelTask, RecordManualChase, LinkReportEvidence, UnlinkReportEvidence. | |
| `Custody.cshtml{.cs}` (new) | `/Cases/{id:guid}/Custody` — RetryCustody, UploadDocument (with `MaximumStaffUploadBytes`, `SafeMediaType`), RemoveDocument, ConfirmThirdPartyVehicleEvidence, CreateRequestUploadLink, RevokeRequestUploadLink. | |
| `Vehicle.cshtml{.cs}` (new) | `/Cases/{id:guid}/Vehicle` — RequestVehicleLookup, AcceptVehicleSuggestion, GenerateEvaHandoff. | |
| `Closure.cshtml{.cs}` (new) | `/Cases/{id:guid}/Closure` — RecordReportApproval, Close, Reopen, Archive. | |
| `Eva/Download.cshtml{.cs}` (new) | `/Cases/{id:guid}/Eva/Download` — the former `EvaDownload` handler as `OnPostAsync` (file response, `Content-Digest`/`nosniff`/`no-store` headers, `SafeEvaFileName`), its own `LogEvaDownloadFailed` (Error, as before). | The one action that answers with content lives beside `Documents/Download` (open question 3). |
| `Shared/_CaseWorkflow.cshtml`, `Shared/_CaseDocuments.cshtml` | 29 forms gained `asp-page="/Cases/<Page>" asp-route-id="@workflow.CaseId"`; the EVA download form posts to `/Cases/Eva/Download` with no handler; 32 `NewOperationKey()` calls name the base. The six workspace forms (ClaimLease ×2, RenewLease, ReleaseLease, Save, ConfirmCompleteness) still post to the page. Handler-form count 35 → 34 (the download form). | Every action redirects back to the workspace; `Details.cshtml` itself is unchanged. |
| `Documents/Export.cshtml.cs` | Adopts the base (−45 lines): its private lease-key vocabulary, `StoreLeaseAuthority`, `ClearLeaseState`, `TryGetActor` and three redirects removed; the "stale version keeps the lease" rule kept via `IsLeaseLoss`/`PreserveLeaseState`. Its token/case-id TempData encoding now matches Details' writer (it had drifted). | Open question allowed adoption "if trivial"; the simplification pass showed it was. |

Handler bodies were extracted by script from `git show HEAD:…Details.cshtml.cs` and are verbatim: no handler name, form field, TempData key, redirect target or message changed.

### Tests

| File | Change |
| --- | --- |
| `tests/Pegasus.IntegrationTests/CaseCapabilityPagesTestSupport.cs` (new) | Shared harness as a partial of `CaseDetailsWebTests`: `EnterEditModeAsync` (host + store substitution + GET + ClaimLease + leased GET), `Substitute<T>`, `LeasedWorkspace` (post/get/`MutationForm` → `LifecycleForm`), `AssertLeasedMutation`, `AssertClaimant`, the two base refusal checks (`AssertRefusalKeepsEditModeAsync`, `AssertLostLeaseClearsEditModeAsync`), and the `NextFailure` one-shot latch on the recording store. |
| `CaseWorkflowWebTests.cs`, `CaseTasksWebTests.cs`, `CaseCustodyWebTests.cs`, `CaseVehicleWebTests.cs`, `CaseClosureWebTests.cs` (new) | One test per page walks every handler from a leased workspace and asserts the recorded command (envelope + the handler's own fields) plus the page's TempData banner where distinctive (linked replacement, request secret, EVA revision, archive). Custody also covers the empty-upload guard; Vehicle also covers the EVA download page (file, headers, digest, refused → workspace, revision 0 → 404). The recording store is one `partial` fake extended per file — no second copy. All 22 previously-untested handlers plus RetryCustody, GenerateEvaHandoff and the download are covered behaviourally. |
| `CaseDetailsWebTests.cs` | 19 POST URLs and 3 HTML assertions follow the new routes; the constructor-reflection assertion is gone (the behavioural tests prove the dependency); `LifecycleForm` gained the `params` tail; the store is `partial` and the manual-chase fake honours the latch. |
| `CaseReportApprovalWebTests.cs`, `Browser/OperatorJourneyTests.cs` | Two URLs → `/Closure?handler=RecordReportApproval`; the Playwright wait matches `/Eva/Download`. |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`, `WorkerAzureClientCompositionTests.cs`, `TypeInspection.cs` (new) | `WebCustodialPagesHaveNoDormantTransportPath` asserts the custody ports on `CustodyModel` and `IGetCase` on `DetailsModel`; both files share `TypeInspection.OnlyConstructorParameterTypes`. |

### Docs

`docs/current-architecture.md`: one implementation-map row for the workspace and its capability pages. `docs/design/README.md` lists no page files, so its `#case` content section needed no change (the visible workspace is unchanged).

## Verification

| Check | Result |
| --- | --- |
| `dotnet build --configuration Release` (whole solution, after the pass) | 0 warnings, 0 errors |
| `Pegasus.Core.Tests` (post-merge) | 580/580 |
| `Pegasus.ArchitectureTests` (after the pass) | 94/94 |
| Integration filter `CaseDetailsWebTests|CaseReportApprovalWebTests|QdosCustodialWebTests|CaseCreateWebTests|CasesIndexWebTests` (after the pass; includes the six new tests and the Export page) | 44/44 |
| Browser lane (`FullyQualifiedName~Browser`, post-merge; the operator journey clicks through the split workspace) | 32/32 — re-run in progress on the final commit; result recorded in the ticket scratch before review |
| Form counts | 29 forms retargeted; six ambient workspace forms remain; `asp-page-handler` in `Pages/Cases`: 34 (35 − the download form) |

## Simplification pass

Ran (four lenses + code-simplifier); 15 findings applied, 12 skipped/deferred with reasons — recorded in `plan` under "Simplification pass — 2026-08-17". Follow-up filed: [[PLAT-002]] (one staff-actor root across the two Web bases and six page copies).

## Deviations from the plan

- Steps 2–5 landed as one commit rather than one per page: the extraction script produced all five pages from the HEAD file at once and the whole solution built first time, so the per-page staging bought nothing.
- The plan's "22 uncovered handlers" are covered by six page-level tests (one per page walking every handler) rather than 22 single-handler tests — same coverage, ~40% of the boilerplate; the plan's diff estimate (~770 test lines) held (≈1,110 including the harness).
- `Documents/Export` adopted the base (allowed by the open question, not planned as a step).
