# Create audit — how it should work

Decided with the operator on 13 September 2026.

## The action

- An **Inspection + Audit** Case offers **Create audit** in the record bar. It is absent on Inspection and Audit Cases, once the Audit exists, and on a Case recorded Created in error or archived.
- Create audit creates a **new Case**: a duplicate of the original with the same Principal, claimant and parties, vehicle, incident, inspection details, Figures, Files and estimate. It is a Case in its own right, of type Audit, and starts in Review.
- Its reference is derived from the Inspection's recorded outcome: **Repairable → `a.{Case/PO}`**, **Total loss → `ap.{Case/PO}`**. When the outcome is neither (Cash in lieu, Contract repair), the dialog asks the person to state Repairable or Total loss before creating.
- The two Cases link both ways: the original's bar shows **Audit case ap.QDOS26214**, the Audit's bar shows **Original case QDOS26214**. Neither identity changes and the original keeps its own lifecycle.
- The original's Notes record it: "Audit case ap.QDOS26214 created by A Mercer from QDOS26214 — total loss."
- The Audit Case joins the person's working set (a tab) and the Cases list like any new Case. On the Work Centre it is a new Case with the Manual arrival chip (Work Centre D8).
- Its Box folder is the `a.`/`ap.` subfolder under the original's folder, created by Pegasus when the Case is created. This replaces the Engineer's hand-made subfolder in FRD-01.

## The dialog

Compact. Facts: Original case, Outcome, Audit reference. When the outcome is neither Repairable nor Total loss, a radio pair replaces the Audit reference row: Repairable `a.…` / Total loss `ap.…`. One primary, **Create audit**. No reason is required; the action is its own record.

## Where this lands

| Page | Entry |
| --- | --- |
| Case record | Create audit in the record bar; Audit case / Original case links; the Case type chip in the ribbon; Case type in Overview |
| Cases list | the Audit Case is listed with its `a.`/`ap.` reference, type Audit, in Review |
| Work Centre | a new Case (D8), Review target applies (D3) |

## Documentation impact when the FRD is written

- FRD-01 Inspection + Audit: Pegasus creates the Audit Case and its Box subfolder; the `a.`/`ap.` reference is the Audit Case's own reference, not a second reference on the Inspection. The "Engineer manually creates the subfolder" sentence goes.
- CONTEXT.md Inspection + Audit: "One Case in which…" becomes an Inspection Case and a linked Audit Case.
- FRD-11: the Audit report is produced on the Audit Case.

## Open

- Whether the Audit Case inherits the original's engineer or starts unassigned (the mockup keeps the engineer).
- Whether Cash in lieu and Contract repair should always derive `a.` rather than asking.
- Whether Create audit is offered before the Inspection's report is sent (the mockup offers it at any time).
