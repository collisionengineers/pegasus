# v29 notes: Case referencing and structure

Round opened 23 September 2026 for the combined rework of PR 803 (Triage as a
Case type) and issue 814 (Inspection + Audit on one Case). The operator's
decisions that shaped it are in [discussion-log.md](discussion-log.md). Stage 2
will follow the approved plan once this list is settled.

## 1. What changes and why

| Today (live) | Proposed | Shots |
| --- | --- | --- |
| Create audit makes a second, linked Audit Case with its own page, row and working-set tab (ADR-0051) | The Audit lives on the same Case. **Create audit** stays in the Actions menu, offered once the Inspection report is sent, and moves the Case to With Engineer with the same Engineer | p09, p10 |
| The section row ends in a Scroll/Tabs switch | Scroll/Tabs is gone and the page always scrolls. Once an Audit exists, the same place and the same control carry the **Inspection · Audit** views | p01, p03, p07, p11 |
| The Audit's values live on the second Case | Create audit copies every Case value into the Audit. The Audit view edits only that copy. The Inspection view shows the Inspection's values and its sent report, read-only | p01, p03, p06 |
| The Audit report is the second Case's report, `a.{Case/PO}` | The Audit report carries `a.QDOS31001` and is named on the ribbon. The Inspection's sent report stays one line in the Audit view's Report, with a link to its own view | p01, p02, p06 |
| The Audit's Box folder is the second Case's root under the original's folder | The same `a.` folder under the Case folder, named in Files beside the Case folder | p05 |
| Triage is a separate record at `/Triage/{id}` with a `T-00001` reference | A Triage is a Case at `/Cases/{id}` with a `t.` Case/PO from the Principal's shared sequence. It uses the Case frame: ribbon, section row, one Actions menu and Files with upload | p12, p13 |
| The Work Centre counts Not ready, Review, Held and Unidentified | A fifth metric, **Triages** | p14, p15 |
| Cases lists Triage under Pre-Case work | Triage sits in the Workflow group, after Query | p16 |
| Create case offers Inspection and Inspection and Audit | Create case also offers **Triage**, asking only for Principal and registration | p17 |
| Open the Triage asks for a registration; the Principal comes from the receipt | Open the Triage also names the Principal, because a Triage Case needs one before it takes a number | p18, p19 |
| Search does not find a Triage | Search finds the Triage Case by its Case/PO and registration | s11, p20 |

## 2. Live rules the mockup mirrors

- The view switch reuses `.layout-switch` from `site.css`, the Scroll/Tabs
  control, in the same place in `section-tools`. The views are links, so one
  rule gives `a` the button's values.
- **Create audit.**
  - The item sits where `Details.cshtml` places it, after Correct principal.
  - The dialog is the live `_CaseDialogs.cshtml` markup: `dialog--compact`,
    `audit-facts`, one primary button.
  - The item is offered only inside an edit session, as `offersActionsMenu`
    requires today.
- **The Report card.** The Inspection's sent-report line and the Audit card are
  the live `.pv` report card from `_CaseReport.cshtml`. The title format is
  `Model.ReportTitle`, "Repairable Report — AB12CDE".
- **Files.** The Audit folder chip follows the live custody chip, "Box case
  folder: preparing" (`data-custody-chip`).
- **The Triage Case frame** is the Case page's own frame from `Details.cshtml`:
  `article.record.case-record`, then `sticky-block` (`ribbon` + `section-row`),
  then `workspace`. The Triage panels are the live ones from
  `Pages/Triage/Details.cshtml`, in the live order: `site.css` puts
  `.triage-source-panel` first.
- **The Triages metric** is the live `.metric` in `.metric-strip--5`; the
  five-wide rules already exist in `site.css`.
- **Open the Triage** is the live `unidentified-triage-dialog` markup and
  labels from `Pages/Unidentified/Details.cshtml`.

## 3. Frame rules

These are unchanged:

- ribbon 56px;
- section row 40px;
- view-switch items 24px high, the Scroll/Tabs geometry;
- 285px aside from 1441px.

The Triage Case has no Figures or Next action, so its workspace is one column
at every width. That is the live narrow-width rule.

## 4. Decisions taken and their authority

Operator, 23 September 2026 (see [discussion-log.md](discussion-log.md)):

- **Create audit** can be selected at any point after the Inspection report is
  sent.
- **Case state.** There is one state. Create audit goes straight to With
  Engineer with the same Engineer, and the Audit then drives the Case.
- **Completion.** Mark completed stays available without an Audit.
- **The Audit reference** is `a.{Case/PO}`, with files in an `a.` Box subfolder.
- **Separate Audit values.** Create audit duplicates them: all of Case details,
  Claim, Inspection details, Vehicle, Damage, Valuation, Repair Spec and
  Decisions. Changes only affect the Audit. Files and Notes are shared.
- **Fees.** The Audit report has its own fee note, counted separately.
- **Views.** The Scroll/Tabs feature is replaced entirely by views, e.g.
  Inspection and Audit.
- **PR 803.** Its `SESSION_PLAN.md` is the Triage contract:
  - a `t.` Case/PO from the shared sequence;
  - `/Cases/{id}` only;
  - the Triages counter;
  - Triage in the Workflow group;
  - manual creation with Principal and registration.

## 5. Deliberate departures from live

- **The Triage record** loses its page header ("Back to Cases") and its record
  bar. Edit moves to the ribbon, and Assign to me and Assign to Engineer move
  into the Actions menu. The Determinations buttons stay where they are.
- **"Triage reference"** becomes "Our ref", as the Case card labels the Case/PO.
- **Set principal is gone.** A Triage Case's Principal is its identity (item T).
- **The Inspection view** omits every Edit rather than showing a disabled one.
  This follows the design authority's omission rule.

## 6. Sign-off list

Each item reads "Confirm, or …". The proposed default is what the mockup shows
unless the item names a variant.

- **A. Views replace Scroll/Tabs entirely.**
  - Confirm, or change the placement or labels. The Inspection · Audit switch
    sits where Scroll/Tabs was and appears only once an Audit exists.
  - The default view is the Audit. The page always scrolls, and there is no
    Tabs mode.
  - A Case without an Audit, a standalone Audit and a Triage Case each have one
    view and no switch.
- **B. The Inspection view is read-only after Create audit.**
  - No Edit anywhere, and no report generation.
  - Files and Notes are shared and stay usable. The Actions menu and Next
    action follow the Audit.
  - Confirm, or show a label on each read-only head. Variant `opt=rolabel:on`
    shows "Read-only · Audit created". That is new copy and needs your
    approval before use.
- **C. The Audit reference on the ribbon.**
  - Proposed: in the Audit view, "Audit reference a.QDOS31001" sits beside the
    heading, which stays the Case/PO.
  - Cost: at 1580px the extra item shortens the "Case workspace ·
    registration" line (p01).
  - Confirm, or show it in both views (`opt=auditref:both`) or not on the
    ribbon at all (`opt=auditref:none`).
- **D. Create audit dialog.**
  - Facts: Case, Audit reference, Engineer. No reason is asked.
  - The live Outcome row and the recorded-outcome requirement go, because a
    sent report implies an outcome.
  - Confirm, or keep the Outcome row.
- **E. A Case held after its report was sent.** "Any point after sent" could
  include Held, but Create audit leaves the hold, and Release Hold needs a
  reason. Offer Create audit from Held, with the release reason asked for in
  the dialog? Or release first? Create audit is not offered on closed
  dispositions.
- **F. No assigned Engineer.** Create audit is refused, with the refusal Return
  to Engineer uses today. Confirm, or allow it and assign in the dialog.
- **G. The Inspection report after Create audit** can be opened and downloaded,
  but not regenerated or sent again (p06). Confirm.
- **H. Report image choices** (Include in report, Close-up, Overview) stay
  shared, because they belong to the Case's files. Confirm, or make them per
  report.
- **J. What lists, search, matching and the ribbon read.** They show the
  Inspection's claimant, Principal and registration even after the Audit
  changes its copy. Confirm, or follow the Audit.
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
- **O. The Audit folder chip** reads "Box audit folder: preparing", following
  the live Case folder chip (p05). Confirm the wording.
- **P. The Triage Case page** (p12).
  - The Case ribbon: `t.` Case/PO, Principal, Assignee, Opened, and the state
    and Triage chips.
  - **Edit Case** opens the Triage's own edit scope, as today. Variant
    `opt=triageedit:triage` keeps "Edit Triage".
  - One Actions menu holding Assign to me and Assign to Engineer.
  - Sections Source · Determinations · Files · Notes, in one column.
  - Files adds Add evidence and the Case folder chip.
  - Confirm, or change the order, the labels or which actions move.
- **Q. Placement.**
  - The Triages metric goes last. Variant `opt=metric:afterheld` puts it after
    Held.
  - On the Cases rail, Triage goes after Query in Workflow.
  - Confirm, or place them elsewhere.
- **R. Create case with Triage** asks only for Principal code and Registration
  (p17). Confirm.
- **S. Open the Triage names the Principal** (p18). Confirm, or keep the live
  dialog, where the Principal comes from the receipt and the action is refused
  when it has none (p19, `opt=principal:off`).
- **T. Wrong Principal on a Triage Case.** Set principal is removed, and a
  Triage Case under the wrong Principal uses Correct principal like any Case.
  Confirm.
- **U. Provider API.** A declared Triage's result returns its `t.` Case/PO, as
  FRD-09 already promises. No Case intake link is written for the Triage's
  origin receipt. Confirm.
- **V. MCP Triage tools** identify a Triage by its Case id (`triageId` becomes
  `caseId`). Confirm, as this changes the Automation contract.
- **W. Search** finds a Triage Case by its `t.` Case/PO and by registration, and
  shows the Triage state (p20). Confirm.
- **X. Re-send naming.** The Audit report's re-send dot suffix counts only Audit
  sends. Confirm.
- **Y. The Cases list's Triage quick detail** keeps its "Open Triage" button,
  which now opens `/Cases/{id}` (p16). Confirm, or relabel it "Open Case".
- **Z. The Triage Case's reference cell** reads "Our ref", as the Case card
  labels the Case/PO (p12). Confirm.

## 7. Self-check

- **Run on 23 September 2026 (final pass):**
  `RESULT {"fail":[],"okCount":358}`.
  - 11 captured states.
  - 20 proposal presets, each checked at 1580 and 1440.
  - Live parity against the running fixture host: all 10 directly addressable
    states match the live application element for element.
- **The screenshot run** (`v29-build/shoot.mjs`) had 0 page errors: 11 baseline
  states and 20 presets, each at three widths.
- **Not application evidence.** None of this is application evidence.

## 8. Known limits

- **The fixture Case was worked through the live edit session**
  (`v29-build/enrich.mjs`). Damage areas and guide valuation cards were not
  entered, because those controls changed after v28's script was written. The
  Engineer sections are not what this round is about.
- **The Inspection-report-sent state (`stage=sent`) reuses the capture in
  report preparation.** Only the Create audit item and the sent report status
  are drawn, so its Actions menu still lists Return to Review.
- **The fixture's Unidentified item does not qualify for Open the Triage.** The
  action and dialog are drawn from the live markup.
- **The Triage Case's number, `t.QDOS31003`, is illustrative.** It is the next
  number in the fixture's QDOS 2031 sequence.
- **The Inspection and Audit copies hold the same values** in the fixture,
  because no Audit edit was made after the copy.
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

- 23 September 2026: first pass (this document).
