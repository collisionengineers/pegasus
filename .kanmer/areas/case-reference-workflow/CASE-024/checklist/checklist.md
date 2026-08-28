# Checklist

## Core

- [ ] `HeartbeatCaseEditLeaseRequest(Guid CaseId, ActionActor Actor, string LeaseToken)` in `CaseWorkflowContracts.cs`
- [ ] `HeartbeatAsync` on `ILeaseCaseForEdit`
- [ ] `IHeartbeatCaseEditLease` in `CaseCommandContracts.cs`
- [ ] `HeartbeatCaseEditLease` + `CaseCommandSeamRules.ValidateHeartbeat` in `CaseCommandSeams.cs`
- [ ] `CaseEditAuthority.HeartbeatInterval = TimeSpan.FromSeconds(60)`; `EditLeaseDuration` left at `EfCaseWorkflowStore.cs:20`

## Infrastructure

- [ ] `EfCaseWorkflowStore.HeartbeatAsync` — transaction, app lock, authorization, archived guard, `RequireLease`, `expiry = now + EditLeaseDuration`
- [ ] it writes no `CaseEditLeaseOperations` row
- [ ] it does not write `EditLeaseOperationKey` or `EditLeaseRequestHash`, and does not rotate the token
- [ ] `DependencyInjection.cs` registration
- [ ] delete the lease yield at `EfIntakeMutationStore.cs:107-112`; keep `ArchivedCaseGuard`
- [ ] confirm `EfIntakeMutationStore.cs:510` is untouched

## Persistence tests (before any Web work)

- [ ] `HeartbeatExtendsTheLeaseWithoutWritingAReplayRow` — expiry `now + 5m`, `LeaseOperationCountAsync == 1`, operation key still the claim key
- [ ] `HeartbeatIsRefusedForANonHolderAnExpiredLeaseAndAnArchivedCase`
- [ ] **(req 1)** `RepeatedHeartbeatsHoldTheLeaseIndefinitelyAndStoppingThemLetsItLapse` — beats past five minutes, then silence, then another actor claims
- [ ] **(req 2)** `ASaveEndsEditModeImmediatelyEvenWhileHeartbeatsContinue` — the next beat throws `CaseEditLeaseExpiredException`
- [ ] `AutomaticMailAssociationSucceedsWhileAStaffEditLeaseIsLive`, asserting the case version and lease are untouched by it
- [ ] the image-intake path still yields to a live lease

## Web

- [ ] `protected` heartbeat helper on `CaseMutationPageModel` — 204/409, **no TempData access on any path**
- [ ] `Details.cshtml.cs`: inject port, `OnPostHeartbeatLeaseAsync`
- [ ] `Details.cshtml`: hidden `data-edit-heartbeat` form
- [ ] extract `_EditFinishConfirm.cshtml` from `Details.cshtml:296-306`; render on both pages
- [ ] `site.js`: heartbeat IIFE — hide Renew, interval from the rendered value, `visibilitychange` beat, stop on non-204 or missing form
- [ ] `TheHeartbeatReturnsNoContentAndLeavesLeaseTempDataUntouched`
- [ ] `TheHeartbeatPostIsRefusedWithoutAnAntiforgeryToken` (400)

## Copy

- [ ] `EditModeDisplay.cs:28,31-32,46-49` — expiry clauses deleted
- [ ] `availableAtUtc`, `WallClock`, `ResolveLondonTimeZone` removed; call sites at `Details.cshtml:97` and `Triage/Details.cshtml.cs:501` updated
- [ ] `Details.cshtml.cs:184,230`; `Operations/Index.cshtml.cs:148`
- [ ] every change deletes a clause and adds no sentence

## Assessment

- [ ] `IndexModel` rebased onto `CaseMutationPageModel` (+ `ILogger`)
- [ ] claim / release / heartbeat handlers, redirecting to the current section
- [ ] edit-mode controls in the existing `record__bar` (`Index.cshtml:88-109`)
- [ ] `:216` and `:535` read the held token; no inline claim
- [ ] `:409` claim removed; `:442` re-claim kept and its token carried forward with `StoreLeaseAuthority`
- [ ] no call to `ExecuteCaseCommandAsync` / `ExecuteTransportCommandAsync`
- [ ] `AssessmentEntersAndLeavesEditModeAndItsMutationsUseThatOneLease` — zero new claims
- [ ] `AssessmentRefusesAMutationWhenEditModeWasNotEntered`

## Changed suites

- [ ] `CaseEditLeaseTests.cs:139` `RecordingLeaseStore`
- [ ] `CaseDetailsWebTests.cs` DI composition
- [ ] `CaseEditModeWebTests.cs:42`
- [ ] `AssessmentDamageAndCopyWebTests.cs:286`, `AssessmentEstimateImportWebTests.cs:426`
- [ ] `AutomationMcpIngressTests.cs:511` — tool census unchanged
- [ ] `CaseWorkflowPersistenceTests.cs` expiry arithmetic still passing untouched

## Docs

- [ ] `frd-01:87` — editing stays held while the session is open; L85 and L89 verbatim
- [ ] `frd-02:313-317` — automatic association no longer yields
- [ ] `capabilities.md:151` CASE-27 note
- [ ] `current-architecture.md:635`, and repair the false sentence at `:519`
- [ ] `design/README.md` — recorded as needing no change, with the reason

## Before the PR

- [ ] simplification pass over the branch diff, findings and dispositions dated in `plan`
- [ ] `dotnet restore --locked-mode`, `build --configuration Release`, `test --filter "Category!=Corpus"` — chunked, full log kept
- [ ] manual check on LocalDB with `DevelopmentOffline`
- [ ] operator sign-off: copy deletions, and the UI-15 exception widening
