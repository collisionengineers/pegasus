# Open questions — CASE-041

- [x] **Repairer location has no recorded value anywhere.** Operator answer
  2026-09-03: the repairer location is in general extractable from the
  instruction document (see the QDOS instruction e-mails and bodyshop reports
  used as reference material), so it becomes part of the extraction process
  rather than manual entry. A ticket has been filed for that work:
  [[INTK-058]] "Extract the repairer name and location from instruction
  material into a per-case repairer record" (Backlog, EPIC-011, refs frd-05 /
  frd-02 / frd-06).

  For CASE-041 itself this is **option 1**: the ticket's Verification line is
  amended so the Repairer location option is accepted as disabled
  (` · not recorded`) under D33 until INTK-058 ships a value. CASE-041 needs
  no change when it does — the option enables itself once the case carries a
  repairer address. CASE-041 is not blocked by INTK-058.

## Parked (explicitly deferred)
