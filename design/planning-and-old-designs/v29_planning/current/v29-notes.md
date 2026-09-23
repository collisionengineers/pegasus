# v29 notes: Case referencing and structure

Round opened 23 September 2026 for the combined rework of PR 803 (Triage as a
Case type) and issue 814 (Inspection + Audit on one Case). The operator's
decisions and both passes are in [discussion-log.md](discussion-log.md). Stage 2
follows the approved plan once this list is settled.

## 1. What changes and why

| Today (live) | Proposed | Shots |
| --- | --- | --- |
| Create audit makes a second, linked Audit Case with its own page, row and working-set tab (ADR-0051) | The Audit lives on the same Case. **Create audit** stays in the Actions menu, offered once the Inspection report is sent, and moves the Case to With Engineer with the same Engineer | p10, p11 |
| The working-set strip under the utility bar holds a tab per open record | The strip goes from every page. On an Inspection + Audit Case whose Audit exists, the same strip holds the Case's two **views**, each tab naming its report's reference: Inspection `QDOS31001` and Audit `a.QDOS31001`. Scroll/Tabs is unchanged | p01, p03, p07, p08, p12 |
| The Audit's values live on the second Case | Create audit copies every Case value into the Audit. The Audit view edits only that copy. The Inspection view shows the Inspection's values and its sent report, read-only, each head labelled **Read-only · Audit created** | p01, p03, p05 |
| The Audit report is the second Case's report, `a.{Case/PO}` | The Audit report carries `a.QDOS31001`. The Inspection's sent report stays one line in the Audit view's Report, with a link to its own view | p02, p05 |
| The Audit's Box folder is the second Case's root under the original's folder | The same `a.` folder under the Case folder, named in Files as **Box audit folder: preparing** beside the Case folder | p04 |
| Search finds an Inspection + Audit Case once | Search lists it twice, `QDOS31001` and `a.QDOS31001`, and both go to the same Case | p20 |
| Triage is a separate record at `/Triage/{id}` with a `T-00001` reference | A Triage is a Case at `/Cases/{id}` with a `t.` Case/PO from the Principal's shared sequence. Its page keeps the live Triage layout and gains the Case's Files with upload, as PR 803's approved plan requires | p13 |
| The Work Centre counts Not ready, Review, Held and Unidentified | A fifth metric, **Triages** | p14, p15 |
| Cases lists Triage under Pre-Case work | Triage sits in the Workflow group, after Query | p16 |
| Create case offers Inspection and Inspection and Audit | Create case also offers **Triage**, asking only for Principal and registration | p17 |
| Search does not find a Triage | Search finds the Triage Case by its Case/PO and registration | s11, p21 |

## 2. Live rules the mockup mirrors

- **The view tabs** are the working-set strip's own markup and classes
  (`nav.workspace-tabs`, `.workspace-tab`, `.workspace-tab-link`, `.ref`, `.reg`,
  `is-active`), as `site.js` builds them. There is no close button, because a
  view cannot be closed. The strip keeps its 40px geometry and
  `body.has-working-set`.
- **Create audit.**
  - The item sits where `Details.cshtml` places it, after Correct principal.
  - The dialog is the live `_CaseDialogs.cshtml` markup: `dialog--compact`,
    `audit-facts`, one primary button.
  - The item is offered only inside an edit session, as `offersActionsMenu`
    requires today.
- **The Report card.** The Inspection's sent-report line and the Audit card are
  the live `.pv` report card from `_CaseReport.cshtml`. The title format is
  `Model.ReportTitle`, "Repairable Report — AB12CDE".
- **Section heads.** The read-only label uses the live `.gated` availability
  label in the section head.
- **Files.** The Audit folder chip follows the live custody chip, "Box case
  folder: preparing" (`data-custody-chip`).
- **Search.** The Audit entry is the live results row, cloned. Its link opens
  the Audit view; the Inspection entry opens the Inspection view.
- **The Triages metric** is the live `.metric` in `.metric-strip--5`; the
  five-wide rules already exist in `site.css`.
- **The Triage Case's Files** is the Case page's Files head: the custody chip,
  Add evidence and the Documents empty state.

## 3. Frame rules

These are unchanged:

- utility bar 48px;
- strip 40px, now holding views;
- ribbon 56px;
- section row 40px, with Scroll/Tabs kept;
- 285px aside from 1441px.

A Case with one view has no strip, so its page starts 40px higher. See item A.

## 4. Decisions taken and their authority

Operator, 23 September 2026 (see [discussion-log.md](discussion-log.md)):

- **Create audit** can be selected at any point after the Inspection report is
  sent. A Held Case never has a sent report, so it is never offered there.
- **Case state.** There is one state. Create audit goes straight to With
  Engineer with the same Engineer, and the Audit then drives the Case.
  Completion stays available without an Audit.
- **The Audit reference** is `a.{Case/PO}`, with files in an `a.` Box subfolder.
  Fees: the Audit report has its own fee note, counted separately.
- **Separate Audit values.** Create audit duplicates everything, and changes
  affect only the Audit. Files and Notes are shared.
- **The working-set tabs are replaced** by views, e.g. Inspection and Audit.
  Scroll/Tabs is not the feature meant.
- **Triage Cases** need nothing beyond their prior requirements, which are PR
  803's `SESSION_PLAN.md` and FRD-03.
- **Search** surfaces the Inspection and the Audit as separate entries that go
  to the same Case.
- **The on-screen wording is approved:** "Read-only · Audit created" and "Box
  audit folder: preparing".

## 5. Deliberate departures from live

- **The working set is removed from every page.** Open records no longer
  collect as tabs; the view tabs are all that remains of the strip (item A).
- **The Inspection view omits every Edit** and states why once per head with
  the approved label.
- **A Triage Case keeps the live Triage page as it is** (page header, record
  bar, panels, Set principal, labels) apart from its `t.` Case/PO and the added
  Files section.

## 6. Sign-off list

Each item reads "Confirm, or …". The proposed default is what the mockup shows
unless the item names a variant. Settled items keep their letter.

- **A. The working set goes, and views take its strip.**
  - The open-records tabs are removed from every page.
  - On an Inspection + Audit Case whose Audit exists, the strip holds
    Inspection `QDOS31001` and Audit `a.QDOS31001`, and Audit is the default.
  - A Case with one view (no Audit yet, a standalone Audit, a Triage Case) and
    every other page have no strip.
  - Confirm, or give a one-view Case its single tab (`opt=singleview:tab`,
    p09).
  - **Consequences to confirm:**
    - Nothing replaces the working set's way back to recently opened records.
      Search and the Ctrl K palette remain.
    - The states a working-set tab shows lose their home (FRD-12: unsaved edits,
      a Glass's session open, a colleague holding the record). The ribbon
      already shows Editing and "{name} is editing". Should a view tab carry
      the unsaved-edits dot and the Glass's glyph for its view?
    - Closing the current record no longer returns to the Work Centre, because
      a view cannot be closed.
- **B.** *Settled 23 September:* the Inspection view is read-only after Create
  audit, and each head carries "Read-only · Audit created". Files and Notes stay
  usable; Actions and Next action follow the Audit.
- **C. The Audit reference is carried by the Audit view's tab.** The ribbon
  heading stays the Case/PO. Confirm, or also name it on the ribbon
  (`opt=auditref:audit`, p06). That costs width on the "Case workspace ·
  registration" line.
- **D. Create audit dialog.**
  - Facts: Case, Audit reference, Engineer. No reason is asked.
  - The live Outcome row and the recorded-outcome requirement go, because a
    sent report implies an outcome.
  - Confirm, or keep the Outcome row.
- **E.** *Settled 23 September:* not offered on a Held Case, because a Held Case
  would not have a report sent. It is not offered on closed dispositions either.
- **F. No assigned Engineer.** Create audit is refused, with the refusal Return
  to Engineer uses today. Confirm, or allow it and assign in the dialog.
- **G. The Inspection report after Create audit** can be opened and downloaded,
  but not regenerated or sent again (p05). Confirm.
- **H. Report image choices** (Include in report, Close-up, Overview) stay
  shared, because they belong to the Case's files. Confirm, or make them per
  report.
- **J.** *Settled 23 September for Search:* two entries, `QDOS31001` and
  `a.QDOS31001`, both to the same Case (p20). The Inspection entry opens the
  Inspection view and the Audit entry the Audit view.
  - Still open for the **Cases lists and queues**: one row per Case, showing
    the Inspection's claimant, Principal and registration. Confirm, or follow
    Search with two rows.
  - Still open for **intake matching**: it matches on the Inspection's values.
    Confirm.
- **K. Image intake association and evidence promotion.** While the Audit is
  prepared, these reopen as for any Case before its report is sent. Confirm, or
  keep them closed once the Inspection report is sent.
- **L. What Create audit copies.**
  - Copied: live estimates with their lines, guide valuations with the applied
    Engineer's Value, report wording blocks, the fee, and confirmed values
    (which stay confirmed).
  - Not copied: open AI proposals, discarded estimates and estimate revision
    snapshots.
  - Confirm, or name what else to copy or leave.
- **M. Deadlines and MI.**
  - The Inspection deadline and completeness stay Case-level.
  - MI-01 turnaround for an Audit report runs from Create audit.
  - MI-02 gains an Inspection/Audit split.
  - Confirm each.
- **N. Correct principal after Create audit.** The replacement Case starts from
  the Inspection's values only. Confirm, or refuse Correct principal once an
  Audit exists.
- **O.** *Settled 23 September:* "Box audit folder: preparing".
- **P.** *Settled 23 September:* a Triage Case changes only as its prior
  requirements say (p13). The mockup draws two of them: the `t.` Case/PO and the
  Case's Files with upload. The page is otherwise live.
- **Q. Placement.**
  - The Triages metric goes last. Variant `opt=metric:afterheld` puts it after
    Held (p15).
  - On the Cases rail, Triage goes after Query in Workflow.
  - Confirm, or place them elsewhere.
- **R. Create case with Triage** asks only for Principal code and Registration
  (p17). Confirm.
- **S. Open the Triage** stays the live dialog (p18). The Triage takes the
  receipt's Principal and is refused when the receipt has none, as the prior
  requirements say. Confirm, or add a Principal pick (`opt=principal:on`, p19).
- **T. Set principal on a Triage Case.** The live button stays (item P). A Triage
  Case's Principal is now part of its `t.` Case/PO, which never changes. Should
  Set principal go, with a wrong Principal handled by Correct principal like any
  Case? This is a Stage 2 question either way.
- **U. Provider API.** A declared Triage's result returns its `t.` Case/PO, as
  FRD-09 already promises. No Case intake link is written for the Triage's
  origin receipt. Confirm.
- **V. MCP Triage tools** identify a Triage by its Case id (`triageId` becomes
  `caseId`). Confirm, as this changes the Automation contract.
- **W. Search** finds a Triage Case by its `t.` Case/PO and by registration, and
  shows the Triage state (p21). Confirm.
- **X. Re-send naming.** The Audit report's re-send dot suffix counts only Audit
  sends. Confirm.
- **Y. The Cases list's Triage quick detail** keeps its "Open Triage" button,
  which now opens `/Cases/{id}` (p16). Confirm.
- *Z (first pass, "Our ref" on the Triage page) is withdrawn under P.*

## 7. Self-check

- **Second pass, 23 September 2026:** `RESULT {"fail":[],"okCount":343}` across
  11 captured states and 21 proposal presets, each checked at 1580 and 1440.
- **Captures are unchanged since the first pass.** That pass's live parity
  check still stands: all 10 directly addressable states matched the running
  application element for element.
- **The screenshot run had 0 page errors:** 21 presets at three widths.
- **Not application evidence.** None of this is application evidence.

## 8. Known limits

- **The fixture Case was worked through the live edit session.** Damage areas
  and guide valuation cards were not entered, because those controls changed
  after v28's script was written.
- **The Inspection-report-sent state (`stage=sent`) reuses the capture in
  report preparation.** Only the Create audit item and the sent report status
  are drawn, so its Actions menu still lists Return to Review.
- **The fixture's Unidentified item does not qualify for Open the Triage.** The
  action and dialog are drawn from the live markup.
- **The Triage Case's number, `t.QDOS31003`, is illustrative.** It is the next
  number in the fixture's QDOS 2031 sequence.
- **The Inspection and Audit copies hold the same values** in the fixture,
  because no Audit edit was made after the copy. This is why the Search Audit
  entry shows the same claimant.
- **In the mockup, a view tab or search entry lands on the captured page.** The
  mockup has no server, so the view each should open is not carried into the
  page it lands on.
- **Not drawn:**
  - the Triage Case in its edit session;
  - the Audit report after generation (file name `A_QDOS31001_assessment.pdf`).

## 9. Found in the live read (for the operator; not part of this round)

The page documentation for this round (see [pages](../pages/README.md))
found these differences between live and the FRDs:

- **Triage completion needs exactly one linked response.**
  `EfTriageStore.cs:1207-1215` refuses completion unless exactly one
  response-evidence link exists, but FRD-03 says sending is never a gate.
- **`/Triage` redirects to `/Cases`,** not `?tab=triage` (FRD-12).
- **Return to Review is offered in With Engineer** (`Details.cshtml:65-66`),
  but FRD-16 says Completed or Query.
- **The Search results table** has no type or due column (FRD-15), and its
  preview's "Our ref" shows the claim number.
- **The Unidentified record's panels are headed "Resolve" and "Resolution",**
  which FRD-15 forbids.
- **Create case's "created" message is never shown.** It writes
  `TempData["CaseDetailsStatus"]`, but the Case record reads `CaseStatus`.
- **The Web gate for Create audit ignores the recorded outcome;** Core refuses
  without one.
- **The combined type has three spellings:** "Inspection + Audit", "Inspection
  and audit" and "Inspection and Audit".
- **Triage "Link case" asks for the Case's GUID.**
- **The Cases rail counts every Triage;** the Work Centre counts Open and
  Awaiting information.

## 10. Rounds

- 23 September 2026, first pass: the views replaced Scroll/Tabs. Corrected by
  the operator.
- 23 September 2026, second pass (this document): the views replace the
  working-set strip; Triage keeps its prior requirements; B, E, J (Search), O
  and P are settled.
