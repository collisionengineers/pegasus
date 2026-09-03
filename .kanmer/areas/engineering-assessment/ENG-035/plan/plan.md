## Simplification pass (2026-09-03, gpt-5.6-sol low, reviewed by Claude Opus)

Ran over the branch's own uncommitted diff (`git diff origin/dev`) across the four lenses:

| # | Lens | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | Efficiency | `src/Pegasus.Core/Reports/AssessmentReportProjection.cs:161-164,235-236` — the expanded projection repeatedly used `CaseAssessmentProjection.Field`, causing a linear scan for every projected field. | Applied: built one projection-local ordinal lookup and reused it across snapshot and sub-record construction; `TryAdd` preserves the former first-match behaviour for duplicate paths. |
| 2 | Efficiency | `src/Pegasus.Core/Assessment/AssessmentPolicy.cs:129-152,478-503` — an impacts value is parsed during normalization and parsed once more when persistence requests derived values. | Reported, not applied: the value is bounded to 4,000 characters and each boundary parses once; avoiding the second parse would require widening the normalized request contract or adding a cache, which is disproportionate and less simple than the current shape. |
| 3 | Reuse | `src/Pegasus.Core/Assessment/AssessmentContracts.cs:115-136,163-167` — checked for a duplicated impact zone/severity catalogue. | No change needed: validation, derivation, vocabulary codes, and report presentation all reuse the same Core dictionaries; one list per concept holds. |
| 4 | Simplification | `src/Pegasus.Core/Assessment/AssessmentPolicy.cs:478-550` and `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs:155-170` — checked the new normalization and derived-row path for dead code or redundant branching. | No change needed. |
| 5 | Altitude | `src/Pegasus.Core/Reports/AssessmentReportRendering.cs:167-188`, `src/Pegasus.Core/Reports/AssessmentReportProjection.cs:271-326`, and `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs:185-212` — checked that Core still owns vocabulary, derivation, equity, and code display text. | No change needed: Infrastructure only persists Core-derived rows and formats/renders snapshot values; no policy leaked outward. |

Applied fix (#1) verified: focused Core test rerun 70/70 passed after the change, then the full
canonical `dotnet test ... --filter "Category!=Corpus"` was re-run (see post-implementation report) and
passed with the fix in place. No assertion was weakened. `git diff --check origin/dev` reported only
line-ending notices, no whitespace errors. Only files inside the ENG-035 owned-path list were touched by
the pass.
