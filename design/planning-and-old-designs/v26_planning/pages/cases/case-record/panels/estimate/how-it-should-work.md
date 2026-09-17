# Estimate — how it should work (dialogs so far)

The three head actions that were placeholders in v25 and v26 now open their dialogs, on the live field sets (`_CaseEstimate.cshtml`, 13 September 2026):

| Dialog | Fields | Result |
| --- | --- | --- |
| Import estimate | one file, PDF, JSON or XML | a new Draft estimate tab named after the file, source "Imported · PDF"; logged |
| Send to AI | Direction (free text), Target estimate as a percentage slider with the Case valuation and the target amount read out | the AI job is queued; its draft arrives as an AI draft on Next action (Work Centre D9) |
| Compare estimates | none; a table of Estimate, State, Net, VAT, Gross for every estimate on the Case | read-only |

The Report section's **Preview draft** opens the draft in the page's document viewer instead of a new browser tab, with Download draft.
