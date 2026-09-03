# Open questions — CASE-042 (2026-09-02)

- [ ] Group label: the mockup (`05-state.js`) and D38 say `Pre-case`; the
      shipped `/Cases` rail (CASE-025, EPIC-011 §1.4) says `Pre-Case work`.
      Which wording is the contract? (Label lives in `OperatorLabels.cs`,
      a CASE-038 shared lock this wave.)
- [ ] Vehicle column: the ticket names "vehicle" but Pegasus records no
      vehicle make/model for an image-initiated case (`ImageIntakeSummary`,
      `EfImageIntakeStore`). Drop the column until a vehicle is recorded
      (D21: absent, not drawn), or open a separate data-model ticket first?
