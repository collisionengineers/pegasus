# v29 discussion log

How this round came to be. It records the operator's words and what each round
changed. It is not design authority.

## 23 September 2026: the request and the decisions

**The request.** The operator asked:

> plan the large scale rework for referencing and case structure. This covers:
> 1. Issue 814 2. PR803. Combine both fixing and resolving PR803 with full
> implementation on issue 814.

**What was explored.** The investigation was read-only.

- **Issue 814** and its investigation comment.
- **PR 803:**
  - its `SESSION_PLAN.md`;
  - its single commit;
  - its CI run: 32 SQL integration failures;
  - its conflicts with `dev`.
- **The linked-audit implementation:** `CreateAuditCase`, `EfCreateAuditCaseStore`,
  `AuditOfCaseId`, custody and reports.
- **The Case reference and sequence machinery.**

**The operator's decisions, in their words:**

- **When Create audit is offered:** "A Create audit button can be selected at
  any point after the inspection report is sent."
- **Completion without an Audit:** "Yes, completion stays available."
- **The Audit reference:** `a.QDOS26001`, in an `a.` subfolder.
- **The Audit's data:** "Seperate audit values, but pressing "Create Audit"
  duplicates the values. They can be changed on the audit section if the staff
  member wishes, and this only pertains to the audit section."
- **The mockup and views:** "We also need a mockup designed for this. I propose
  replacing the tabs feature entirely with different views e.g. inspection,
  audit."
- **The Case state after Create audit:** one state, straight to With Engineer.
- **What Create audit copies:** everything, meaning Case details, Claim,
  Inspection details, Vehicle, Damage, Valuation, Repair Spec and Decisions.
- **Fees:** the Audit report has its own fee note, counted separately.
- **Old data:** wipe the test estate first; the migration refuses leftovers.

**What changed.** The approved plan puts Stage 1 first: this mockup round. Stage
2 is the implementation on the PR 803 branch.

**Items raised.** Sign-off items A to Z in [v29-notes.md](v29-notes.md#6-sign-off-list).
