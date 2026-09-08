# Plan — ENG-037: invariant assessment dates

## Objective

Save canonical yyyy-MM-dd dates without depending on the host calendar.

## Starting state

Accepted dev cdaa02584c38ecc27d3bd24784f59da189138bc1; files/files.md@f24bccc84a440a9e.
Read-only inspection found AssessmentPolicy.NormalizeValue still uses the
ambient-calendar overload. Report projection already uses InvariantCulture /
DateTimeStyles.None and has a th-TH regression; preserve and rerun it.
SaveAssessment and EfCaseAssessmentStore are existing production callers.
Current CASE-031, INTK-063 and TICK-085 write maps do not overlap these two files.

## Governing docs

Meets FRD-06 accepted structured engineering facts and FRD-11 deterministic
report snapshot dates. No new product behavior or governing-doc amendment.
EPIC-012/011/008 context was read; EPIC-014/current user instructions own the
current scope, one heavy verifier and focused checks, not historical wave rails.

## Required changes

State InvariantCulture and DateTimeStyles.None on the existing DateOnly parse.
Keep exact ISO format, minimum-date rejection and invariant serialization.
Extend existing AssessmentPolicyTests, restoring CurrentCulture in finally.
Cover all writable date fields in th-TH, ar-SA and en-GB using existing
vocabulary/Request helpers; prove valid ordinary/leap-day values are unchanged
and invalid dates stay rejected. No clock, timezone or display-format change.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | src/Pegasus.Core/Assessment/AssessmentPolicy.cs | Existing date normalizer. |
| Modify | tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs | Calendar-independent regression. |

## Do not modify

- src/Pegasus.Core/Reports/AssessmentReportProjection.cs
- tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs
- src/Pegasus.Infrastructure/**
- src/Pegasus.Web/**
- docs/**
- corpus/**

## Constraints

No package, abstraction, schema, UI, fixture file, provider or cloud change.

## Ordered steps

1. Correct the existing parse overload and extend its current test class.
2. Run focused checks and report exact exits; preserve every failure.

## Acceptance checks

Every writable date retains its Gregorian ISO value under each selected
calendar; invalid dates still fail. Existing report date regression passes.
The change contains only the two expected files.

## Commands

PowerShell 7 in isolated .worktrees/eng-037, branch ENG-037-invariant-dates.
Root alone runs:
    dotnet restore ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --locked-mode
    dotnet build ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-restore
    dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AssessmentPolicyTests|FullyQualifiedName~AssessmentReportProjectionTests"

Use a uniquely named TRX. Final converged solution/release rail remains root
controller work, not a claimed pass here.

## Failure and deviation rules

Retain and diagnose failures; do not weaken assertions. A new caller, file or
behavior requires revised scope before edits.

## Stop condition

Publish the bounded PR to dev after focused evidence; hand off for independent
review. Author must not review or merge its own PR. No deployment.
