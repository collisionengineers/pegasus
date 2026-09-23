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

## 23 September 2026: second pass

**What the operator said.** The operator reviewed the first pass and pointed at
the working-set strip (the open-records tabs):

> intention was to replace this, not the tabs vs scroll button
>
> triage cases dont require anything changing here from the prior requirements
>
> agreed on the on screen wording.
>
> for J: it should surface both in the search as seperate entries that go to the
> same case
>
> E: no, held cases wouldn't have a report sent
>
> redo the mockup

**What changed.**

- **P1** now replaces the working-set strip, not Scroll/Tabs. The strip goes
  from every page. On an Inspection + Audit Case with an Audit, the same strip
  holds the Inspection and Audit views, and each tab names its report's
  reference. Scroll/Tabs is back as live.
- **P2** no longer puts the Audit reference on the ribbon by default. The Audit
  view's tab carries it, and the ribbon version is now a variant.
- **P3** shows the approved "Read-only · Audit created" label by default.
- **P7** is reduced to what PR 803's plan requires. The live Triage page stays
  as it is: the header, record bar, panels, Set principal and labels. It gains
  only the Case's Files with upload, and P9's `t.` Case/PO and `/Cases/{id}`.
- **P12** defaults to the live Open the Triage dialog, with the Principal pick
  as a variant.
- **P14 is new.** Search lists the Inspection + Audit Case as two entries to
  the same Case.

**Items settled:** B (wording), E (no), J for Search, O (wording) and P (Triage
unchanged beyond prior requirements).

**Items raised:**

- A now asks whether a Case with one view shows no strip or its single tab, and
  notes that the working set's way back to open records goes.
- J stays open for the Cases lists and for intake matching.
- T asks what happens to Set principal on a Triage Case.
