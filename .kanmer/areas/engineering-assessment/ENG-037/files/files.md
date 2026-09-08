# Files — ENG-037

| Path | Change |
| --- | --- |
| src/Pegasus.Core/Assessment/AssessmentPolicy.cs | Complete invariant date parsing in existing normalizer. |
| tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs | Existing fixture, non-Gregorian and ordinary culture regression. |

Read-only acceptance context: src/Pegasus.Core/Reports/AssessmentReportProjection.cs
and tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs already
use invariant parsing and test th-TH. Do not rewrite them. No generated files.
