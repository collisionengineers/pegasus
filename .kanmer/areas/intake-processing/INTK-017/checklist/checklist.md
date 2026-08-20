# Checklist — INTK-017

- [ ] Red-first fixtures written and observed failing (cross-fragment resolution, validated-beats-unvalidated, sole/ambiguous VRM, synonyms, flattened multi-field line)
- [ ] Engine: rank-aware candidate collection + deterministic conflict resolution (`IsValidTyped` narrowing, earliest-fragment preference, same-fragment conflict preserved)
- [ ] Engine: value truncation at a following known field label
- [ ] Engine: sole current-format VRM fallback for Vehicle registration (fail-closed on multiple distinct VRMs)
- [ ] Policy: registration label synonyms (longest-first) + `IsValidTyped` wiring (registration/mileage/dates)
- [ ] All new fixtures green; full `Pegasus.Core.Tests` green; Release build 0 warnings
- [ ] Simplification pass over the branch diff recorded in the plan
- [ ] PR opened against `dev` (dependency on ENG-004 noted); post-implementation-report written; ticket moved to review
