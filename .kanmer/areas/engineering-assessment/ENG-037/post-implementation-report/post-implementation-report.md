# Post-implementation report — ENG-037

## Outcome

The remaining assessment save parser now uses Gregorian ISO dates independently
of the host calendar. Focused checks PASS; not independently reviewed,
integrated or deployed.

## Scope and actual caller

Base dev cdaa02584c38ecc27d3bd24784f59da189138bc1; author commit
7c86e9bc91f12f03a37a9a7c33dd1f5afcf2e524 on ENG-037-invariant-dates in
.worktrees/eng-037. Fresh ready packet preceded creation/take; exact root,
common Git, clean base, ignored path and absent human reference files checked.
Plan7acbd02e0af054f3, filesf24bccc84a440a9e.

Only two files changed:
- src/Pegasus.Core/Assessment/AssessmentPolicy.cs: use InvariantCulture and
  DateTimeStyles.None in the existing DateOnly.TryParseExact call.
  SaveAssessment and EfCaseAssessmentStore call this existing normalizer.
- tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs: three culture
  cases (th-TH, ar-SA, en-GB) cover every writable Date vocabulary entry,
  exact ordinary/leap-day values and invalid date/format/minimum-date refusal.
  CurrentCulture is restored in finally; existing vocabulary and request
  fixtures are reused, no domain evidence is fabricated.

The report projection had already been corrected; its source and th-TH test
are unchanged and included in this focused run. No extra date parser, timezone
change, UI/schema/provider/dependency or generated artifact change.

## Verification — root session12906, 2026-09-08

PowerShell7, Windows, cwd .worktrees/eng-037. Root was the sole heavy owner.
Each command was exit-guarded; overall script exit0.

    dotnet restore ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --locked-mode
    dotnet build ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-restore
    dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AssessmentPolicyTests|FullyQualifiedName~AssessmentReportProjectionTests" --logger "trx;LogFileName=eng-037-dates.trx" --results-directory ./artifacts/verification

Restore PASS: Core310ms, Core.Tests389ms. Focused Core/test project build
PASS17.14s, zero warnings/errors. Test PASS78/78,129ms, no skipped.
Artifact artifacts/verification/eng-037-dates.trx SHA256
E70AEAE038C0D62BEDD05104026ACCC5BBC6D2F024B16F4B5227231CBBD3B4D1.

git diff --check exit0; exactly two approved paths, +43/-1. No failed build or
test attempt occurred. Initial read-only path/glob searches returned errors;
corrected discovery supplied actual files. They were not application failures.
The packet's four advisory findings (unstructured steps, evidence-pin format
and acceptance-command heuristic) were inspected; it was ready with no blocker.
Named executable filters and file-version pin are present in the actual plan.

## Governing docs and proportionality

Meets FRD-06 canonical accepted engineering data and FRD-11 deterministic
report dates. No normative behavior changed beyond correcting the stated bug.
Current user/EPIC-014 focused verification supersedes historical wave-wide
repeat builds. A final converged solution/CI/release run remains controller
work, not an assertion made by this ticket.

## Handoff

Independent review on exact pushed head, then merged-SHA focused acceptance
and closeout are outstanding. Author must not attest to or merge its own PR.
No cloud, email or deployment action occurred.
