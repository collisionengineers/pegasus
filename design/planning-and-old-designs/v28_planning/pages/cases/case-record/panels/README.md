# Case record panels (sections)

The ten sections in their fixed order (`OperatorLabels.CaseWorkspace.Sections`),
each a foldable `.record-section.panel` whose head carries the section's own
primary/menu controls (where it has any), then the shared `Edit` /
availability label / fold chevron from `_CaseSectionHeadTools.cshtml`.

| # | Section | Read | Edit |
| --- | --- | --- | --- |
| 1 | **Overview** | Stepper, state exception line, outstanding requirements, lifecycle-action buttons, the three fact columns (Case/Principal/Claimant), Case contact, the Notes band (Principal/Claim-source record notes read-only beside this Case's own), the accident band. | Every fact cell above swaps to its input in the same grid cell; this section renders the record's one `#case-edit-form`. |
| 2 | **Inspection details** | Inspection type/date, Inspect-at choice and the recorded address with its provenance word, the Principal-default row (shown only where it differs), Repairer and Storage sub-panels. | Inspection date, the Inspect-at select, the address text, repairer fields, storage location, storage-per-day and recovery-charge money fields. |
| 3 | **Vehicle** | Registration/Make/Model/Year with lookup provenance, VIN/type/body, the read-only lookup-sourced rows (engine, fuel, colour, transmission, tax/MOT expiry), mileage with its source chip, pre-incident condition, vehicle history. | All of the above except the lookup-sourced read-only rows; the head's DVLA/MOT lookup button posts immediately (not through `#case-edit-form`). |
| 4 | **Damage** | The Plan clicker (top-down silhouette, severity legend, numbered recorded-zones list), tyres & belts, unrelated damage, the derived impact location/severity/narrative cells. | Clicking a zone cycles its severity; the recorded-zones list gains per-row severity/note controls and a remove button; tyres/belts and unrelated-damage cells gain their controls. |
| 5 | **Valuation** | The guide-source cards (Glass's/Brego/Super CAP/AI market research/Cazana-seam), the calculation lines, the applied-history block. | The three guide sources become entry cards (Get valuation / Save, each its own immediate post), a Basis radio per card, the calculator's previous-total-loss/condition-deduction/VAT/value-increase controls, and Apply as Engineer's Value. |
| 6 | **Estimate** | The estimate tabs, the Glass's session line, Use/Duplicate/Discard, a one-line read summary, the line grid (read-only), discount/VAT bars, notes, the three work-lists and the rollup. | The full header grid (name, repair days, rate card, labour rate, materials, other costs, VAT%, repairer VAT status), the editable line grid with a phantom trailing row, discount/VAT category controls, and Save estimate. |
| 7 | **Settlement** | The figures strip (Total loss vs. repair layout), the Decisions strip (with the Proposed/AI column when one exists), excess/betterment, costs/hire/delays, the seven salvage fields (Total loss only). | Every Decisions field and every money/date/flag cell above gains its control; Accept / Accept all apply a proposed value into the control. |
| 8 | **Report** | The readiness notice, the preview card and generation facts, the stale notice, reviewed-recipients/delivery, the report fields, images-in-report strip, and (while editing) the report-image preparation cards. | Sign-off Engineer, report date override, agreed fee/fee description, the three content switches, valuation commentary text, plus Generate report/fee note, Prepare delivery, Send prepared report and the preparation cards' Role/Order/Crop/Rotate/Reset. |
| 9 | **Files** | Three tabs — Documents, Images, Correspondence — each fully rendered (the tab strip is the enhancement); custody chip in the head; Add evidence and the More menu (Open in Box, Open Operations). | Documents gain Remove; Images gain the tag picker and Crop; this section has no page-wide-session Edit control (`SectionOffersEdit` excludes it — every action posts immediately). |
| 10 | **Notes** | The one Notes timeline (operator notes and system events, chronological). | The Case-note form and Record chase are always available outside post-report read-only/archived (no page-wide-session Edit control here either). |

## Aside

- **Figures** — outcome/legal-status chips, Repair cost inc VAT, Engineer's
  Value, Repair cost of value.
- **Next action** — AI-draft rows first (Review estimate / Open query /
  Review), then the next permitted lifecycle step, mirroring
  `DetailsModel.NextAction`'s own ordering.

Both fold into a two-up strip above the sections below 1441px per the frame
contract; that responsive behaviour is inherited from `case-workspace.css`
and not re-implemented here.
