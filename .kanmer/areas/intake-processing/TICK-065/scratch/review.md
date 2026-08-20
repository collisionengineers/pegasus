## Independent review — PR #461 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green.

- The remaining INT-32 half shipped at minimum correct machinery: `ImageIntakeChaseSchedule.IsChaseDue` is a pure derivation reusing `CaseChaseSchedule.FirstChaseAt` (one owner for the 7-day rule), no new table/migration/sweep — right, since image halves have no manual chase controls to persist.
- Chase visibility lands as a chip column on the existing image table via `_StatusChip`, whose missing tone entries (a real pre-existing gap also affecting case-side chips) were fixed in passing.
- The two "verified, not rebuilt" dispositions are evidence-backed: Received already carries the age fact (no relative-age convention exists anywhere to mirror), and capabilities.md:232's commitment is the derived Associated-with-Case label + the CaseHistory merge row, both shipped at release 12 — inventing a notification subsystem would have violated the no-abstraction rule.
- FRD-02 updated; 5/5 + 5/5 + 97/97 + 105/105 focused suites.
