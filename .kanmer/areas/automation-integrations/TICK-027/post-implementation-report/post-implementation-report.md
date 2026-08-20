# Post-implementation report — TICK-027

## What shipped

Tests-only PR closing the one caller-evidence gap a verification pass found in MCP-06:
`pegasus_case_update_details` was implemented, registered, and present in the fourteen-tool
inventory check, but had no functional test exercising it. Added three tests to
`tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs`, mirroring the file's
existing `pegasus_assessment_update` conventions:

1. `CaseUpdateDetailsRequiresTheCasesScope` — `automation.cases` scope enforcement.
2. `CaseUpdateDetailsOverHttpMutatesUnderLeaseWithLoggingParityAndReopensCompleteness` —
   lease-guarded HTTP success, work-request correlation, `mcp:` replay, ActionHistory logging
   parity (business `case_data_saved` event and `pegasus_case_update_details` ingress
   attribution), and confirmation that the save re-opens completeness review
   (`InstructionComplete`/`CompletenessPolicySatisfied` reset to false, workflow back to
   `NotReady`).
3. `CaseUpdateDetailsRefusesAMissingEditLeaseWithFailedHistoryAndNoTokenDisclosed` — validation
   refusal with `Failed` ActionHistory and no token ever sent to leak.

The second test also pins a contract fact read directly from `EfCaseDataStore.SaveAsync`
before writing the assertion: case-detail values land `Confirmed` immediately
(`SourceKind = staff_correction`, `ConfirmedByActor` set to the automation subject) with no
unconfirmed mark — a different, and per ADR-0021/FRD-11 correct, boundary than the assessment
tranche's staff-review gate on `pegasus_assessment_update`.

## What was already true

The research doc also flagged `pegasus_assessment_get` as never exercised after a successful
update. On re-check, that assertion already exists in
`AssessmentUpdateOverHttpMutatesUnderLeaseWithCorrelatedAttribution` on `origin/dev` (added by
[[TICK-026]]'s harness-split commit `7c0387cc` on 2026-08-17). No change was needed there; the
checklist item is ticked as already satisfied.

## Defects found

None. All three new tests pass against the existing `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs`
implementation as-is. No production code was changed.

## Verification

- `dotnet build ./Pegasus.slnx -c Release --no-restore` — 0 warnings, 0 errors.
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --filter FullyQualifiedName~AutomationAssessmentIngressTests --configuration Release` — 7/7 passed (4 pre-existing + 3 new).

## Simplification pass

Recorded in `plan` under "Simplification pass — 2026-08-20": tests-only diff, full reuse of
`AutomationMcpTestSupport` helpers and the file's existing test structure, no new abstraction,
no findings requiring action.

## PR

https://github.com/collisionengineers/pegasus/pull/445 (branch
`task/tick-027-mcp06-update-details-tests` → `dev`). Not merged — awaiting review per workflow.

## Out of scope (unchanged from plan/files)

New MCP host or live activation; finding-confirmation/report-approval/dispatch tools; estimate
derivation (EXT-09); EVA generate happy-path with real images; [[TICK-023]] tier-5 client
evidence.
