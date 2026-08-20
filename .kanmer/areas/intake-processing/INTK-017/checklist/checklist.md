# Checklist — INTK-017

- [x] Red-first fixtures written and observed failing (8 failed before implementation: cross-fragment resolution, validated-beats-unvalidated, sole/ambiguous VRM, synonyms, flattened multi-field line; 2 regression pins passed as expected)
- [x] Engine: rank-aware candidate collection + deterministic conflict resolution (`IsValidTyped` narrowing, earliest-fragment preference, same-fragment conflict preserved)
- [x] Engine: value truncation at a following known field label (+ mid-line label recognised only with an explicit `:`/`-`)
- [x] Engine: sole current-format VRM fallback for Vehicle registration (fail-closed on multiple distinct VRMs)
- [x] Policy: registration label synonyms (longest-first) + `IsValidTyped` wiring (registration/mileage/dates)
- [x] All new fixtures green (20/20 policy tests); full `Pegasus.Core.Tests` 706/706; Release build 0 warnings
- [x] Simplification pass over the branch diff recorded in the plan
- [ ] PR opened against `dev` (dependency on ENG-004 noted); post-implementation-report written; ticket moved to review
