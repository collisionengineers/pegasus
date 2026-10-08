# How the Case aside works

Read from the live source on 8 October 2026 (`origin/dev` 6be875542).

## What the page does not show

- In Review the step's words and its button are the same two words, "Assign Engineer".
- Every blocker's card repeats its source and reason. Most field blockers share the same two lines, "Assessment record" and "No value is recorded.".
- With eleven blockers the aside's content is 1,809px tall at 1580. The sticky aside scrolls, so the step and Figures leave view while the operator reads the list.
- At 1440 and below the folded strip stretches Figures to Next action's height.

## Governing documentation

| Document | Settles |
| --- | --- |
| [FRD-16](../../../../../../docs/frd/frd-16-case-record-workspace.md) | The aside's cards, the Next action's contents, page order, the stale notice, and the scroll inside the aside |
| [FRD-13](../../../../../../docs/frd/frd-13-case-lifecycle-and-workflow.md) | Blockers are specific: field, source, reason and what clears it; no count; the list's place per state |
| [Design README](../../../../../../docs/design/README.md) | Amber for incomplete; `blocker-list`, `blocker`, `blocker-actions`; the 285px aside folding below 1441px |

## Source

| Layer | File | Owns |
| --- | --- | --- |
| Web | `Pages/Cases/Shared/_CaseAside.cshtml` | The Views, Linked cases, Figures and Next action cards |
| Web | `Presentation/CaseNextAction.cs` | The step per state |
| Web | `Pages/Cases/Details.Frame.cs` | `NextAction`, `NextActionBlockers` |
| Web | `Pages/Cases/Details.Report.cs` | `BlockerSectionKey`, `BlockerEditFocus`, `BlockerOpensAccounts` |
| Web | `Presentation/CaseWorkspaceLabels.cs` | `Report.BlockerSection`, `BlockerTab`, `BlockerFocus`, `InPageOrder` |
| Web | `wwwroot/css/site.css`, `case-workspace.css` | `.blocker*`, `.sub-panel.blockers`, the aside cap |
| Core | `Assessment/AssessmentPolicy.cs`, `Reports/CaseReportGeneration.cs` | The blocker words |

## Behaviours

### The step

`CaseNextAction.Of` names the step. In Review it is Assign Engineer with the Actions menu's dialog (issue 1025). With Engineer while the report is not ready there is no step line, because the list is the step. When the report is ready the step is Generate report, Waiting for the report to be stored or Send report, linking to Report. After the send it is Create audit or Mark completed. The step renders as its words with a small button beside them.

### The blockers

`_CaseAside.cshtml` draws an amber `sub-panel blockers` headed "Report not ready", with one `blocker` card per item in page order. Each card shows the requirement, then **Source:**, **Why:** and what clears it, then one small button. The button opens the Repair Spec for editing with focus, jumps to the section and tab, or opens Accounts for an Administrator. The list shows while the viewed work's report is not ready and its assessment is writable.

## Things the FRD does not settle

- How the step is drawn when its words and its control are the same.
- How a blocker's four facts are laid out.
