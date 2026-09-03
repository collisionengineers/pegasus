# Open questions — CASE-042 (2026-09-02; question 3 added 2026-09-03)

- [x] Group label: `Pre-case` (mockup, D38) or the shipped `Pre-Case work`
      (CASE-025, EPIC-011 §1.4)? Resolved 2026-09-03 by the controller: keep
      the shipped `Pre-Case work` label — the existing convention wins and
      D38 names the group, not its casing. No OperatorLabels change.
- [x] Vehicle column with no recorded vehicle for an image-initiated case?
      Resolved 2026-09-03 by the controller: the column is absent until a
      vehicle is recorded (D21 absent, not drawn); no data-model ticket.
- [ ] **Create Case on an image-initiated record — keep it, or drop it?** The
      ticket body asks the quick view to offer Create Case, but there is no
      lawful route for it today and FRD-02 says the opposite. Evidence:
      `IntakeDecisionPolicy.CanBecomeCase` returns `false` for
      `IntakeDecision.ImageIntakeRegistered`
      (`src/Pegasus.Core/Intake/IntakeDecisionPolicy.cs:30-40`), so
      `/Cases/Create?receiptId=…` renders a refusal
      (`src/Pegasus.Web/Pages/Cases/Create.cshtml.cs:584-600`); there is no
      blank Create route (`OnGetAsync` returns `NotFound` for an empty
      `receiptId`, `Create.cshtml.cs:210-220`); and FRD-02 states image-only
      material merges into an eligible instructed Case rather than creating a
      formal Case/PO
      (`docs/frd/frd-02-intake-and-source-identity.md:172-174`). D7/D21 forbid
      drawing the control inert. The three answers: (a) drop Create Case from
      CASE-042 — the tab ships with Add to an existing case only; (b) keep it
      and open a separate ticket for the Core-owned creation flow that first
      establishes instruction and identity evidence, with CASE-042 blocked on
      it; or (c) something else the operator intends. CASE-042 assumes (a)
      until answered and cannot leave `preparing` on this question.

## Parked (explicitly deferred)

None.
