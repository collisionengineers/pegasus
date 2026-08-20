Checklist — TICK-027

- [x] Create worktree `../pegasus-worktrees/tick-027` from `origin/dev` and take the ticket (after [[TICK-026]] merged to `dev`)
- [x] Assert successful `pegasus_assessment_get` after an update (already present in `AssessmentUpdateOverHttpMutatesUnderLeaseWithCorrelatedAttribution` on `origin/dev` — no change needed)
- [x] Add HTTP success + replay + ActionHistory for `pegasus_case_update_details`
- [x] Add a validation/lease refusal that does not leak the token
- [x] Run focused `AutomationAssessmentIngressTests` Release — 7/7 passed
- [x] Write post-implementation-report, push, open PR to `dev`, move to Review

## Progress notes

- 2026-08-20: Re-verified the research claim before writing tests: grepped
  `pegasus_case_update_details` across `tests/Pegasus.IntegrationTests/*.cs` — it appeared
  only in the `ExpectedTools` inventory list in `AutomationMcpIngressTests.cs`, confirming
  the gap. The `pegasus_assessment_get`-after-update assertion the research also flagged as
  missing was already present in the file pulled from `origin/dev` (added by TICK-026's
  Aug-17 harness split, commit `7c0387cc`) — no action needed there, checklist item ticked
  as already satisfied.
- Added three tests to `AutomationAssessmentIngressTests.cs`:
  `CaseUpdateDetailsRequiresTheCasesScope`, `CaseUpdateDetailsOverHttpMutatesUnderLeaseWithLoggingParityAndReopensCompleteness`,
  `CaseUpdateDetailsRefusesAMissingEditLeaseWithFailedHistoryAndNoTokenDisclosed`.
- Read `EfCaseDataStore.SaveAsync` to confirm the exact contract before asserting it: case-detail
  saves write `Confirmed` values immediately (`SourceKind = staff_correction`, `ConfirmedByActor`
  set to the automation subject) with no unconfirmed mark — unlike the assessment tranche's
  staff-review boundary — and unconditionally reset `InstructionComplete` /
  `CompletenessPolicySatisfied` to false and the workflow to `NotReady`, reopening completeness
  review exactly as the tool description promises.
  All assertions are pinned against that read, not guessed.
- No production defect found; no production code changed.
- `dotnet build ./Pegasus.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter FullyQualifiedName~AutomationAssessmentIngressTests --configuration Release`: 7/7 passed (4 existing + 3 new).
