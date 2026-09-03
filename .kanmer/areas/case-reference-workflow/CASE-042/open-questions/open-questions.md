# Open questions — CASE-042 (2026-09-02; question 3 added 2026-09-03)

- [x] Group label: `Pre-case` (mockup, D38) or the shipped `Pre-Case work`
      (CASE-025, EPIC-011 §1.4)? Resolved 2026-09-03 by the controller: keep
      the shipped `Pre-Case work` label — the existing convention wins and
      D38 names the group, not its casing. No OperatorLabels change.
- [x] Vehicle column with no recorded vehicle for an image-initiated case?
      Resolved 2026-09-03 by the controller: the column is absent until a
      vehicle is recorded (D21 absent, not drawn); no data-model ticket.
- [x] **Create Case on an image-initiated record — keep it, or drop it?**
      **Operator answer 2026-09-03: option (a) — drop Create Case.** The tab
      ships with "Add to an existing case" only, and the ticket body's
      Verification line is amended to match. Reason: there is no lawful route
      today (`IntakeDecisionPolicy.CanBecomeCase` is false for
      `IntakeDecision.ImageIntakeRegistered`) and FRD-02 states image-only
      material merges into an eligible instructed Case rather than creating a
      formal Case/PO. Nothing is drawn inert (D7/D21).

      The operator additionally asked for the reverse direction to exist: an
      instructed case should be able to pull image material in, with "Add
      evidence" made generally available (case action bar and the main rail),
      offering either an upload or the absorption of an existing
      image-initiated case. Filed as [[CASE-044]] (Backlog, EPIC-011); it is
      not in CASE-042's scope and does not block it.

## Parked (explicitly deferred)

None.
