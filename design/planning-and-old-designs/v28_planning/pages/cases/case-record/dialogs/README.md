# Case record dialogs

Every named dialog the Case record opens, its trigger, and its live source
partial. All are `.dialog-backdrop[data-dialog="<id>"]`, opened by any
`[data-dialog-open="<id>"]` control (mock-engine.js mirrors site.js's
convention exactly).

## Frame-level (`Shared/_CaseDialogs.cshtml`)

| Dialog id | Trigger | Shape |
| --- | --- | --- |
| `case-hold-dialog` | Actions → Place on Hold | Reason + optional Review on date |
| `case-release-hold-dialog` | Actions → Release Hold | Reason dialog (`Shared/_ReasonDialog.cshtml`) |
| `case-complete-dialog` | Actions → Mark completed | Reason dialog |
| `case-return-review-dialog` | Actions → Return to Review; Overview → Lifecycle actions | Reason dialog |
| `case-unlink-evidence-dialog` | Overview → Lifecycle actions → Unlink report evidence | Reason dialog |
| `case-correct-principal-dialog` | Actions → Correct principal | Replacement principal code + reason; creates a linked replacement Case |
| `case-archive-dialog` | Actions → Archive case; Overview → Lifecycle actions | Reason dialog |
| `case-close-dialog` | Actions → Close case (red, after a divider) | Outcome chooser (the four adverse `CaseClosureOutcome` names) + reason |
| `case-return-engineer-dialog` | Actions → Return to Engineer | Reason dialog |
| `case-handoff-dialog` | Actions → Hand to Engineer | Engineer picker + reason envelope, plus an "Assign to me" second form for an eligible unassigned Engineer |
| `eva-handoff-dialog` | Actions → Send to EVA (also a real link to `/Cases/Eva/Send`) | `Shared/_EvaHandoff.cshtml`: Sign-off Engineer, Export ZIP / Send via API |
| `case-report-sent-dialog` | Actions → Mark report sent | One confirmation form per detected Sent-evidence candidate |
| `case-create-audit-dialog` | Actions → Create audit | Original Case, Outcome, derived Audit reference; no reason field |

## Section-owned

| Dialog id | Trigger | Source partial |
| --- | --- | --- |
| `import-estimate-dialog` | Estimate head → Import | `Shared/_CaseEstimate.cshtml` |
| `compare-estimates-dialog` | Estimate → More → Compare | `Shared/_CaseEstimate.cshtml` |
| `delete-estimate-dialog` | Estimate → Discard | `Shared/_CaseEstimate.cshtml` |
| `send-to-ai-dialog` | Estimate head → Send to AI | `Shared/_CaseEstimate.cshtml` |
| `case-chase-dialog` | Notes → Record chase | `Shared/_CaseHistory.cshtml` |
| `remove-doc-<occurrenceId>` (one per document row) | Files → Documents tab → Remove | `Shared/_CaseDocuments.cshtml` (via `Shared/_ReasonDialog.cshtml`) |

## Non-dialog full-screen overlay

The **image viewer** (`data-case-viewer`, `Shared/_CaseViewer.cshtml`) is
not a `.dialog-backdrop` — it is its own full-screen `role="dialog"` region
opened from any `[data-evidence-item]` inside a `[data-evidence-set]`
(Files' Documents/Images tabs, the Report section's images-in-report strip).
Crop happens on its stage, not in a separate dialog.
