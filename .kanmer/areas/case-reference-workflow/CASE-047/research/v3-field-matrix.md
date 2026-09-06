# UI v3 field / manager matrix (Stream B, Wave 1 record)

Recorded 2026-09-06 by a read-only Sonnet inventory. Sources: `pegasus_pack/more_docs/Pegasus_UI_v3_Full_Specification.md`, `Pegasus_UI_v3.html` (label dictionary `L`, `CASE_SECTIONS`, calc functions, `SECTIONS.*` renderers, `ZONES` table), `pegasus_pack/ui/managers-ui-proposal/pegasus_case_dashboard_v24.html` (zones, presets, `adjCalc`), the Stream B plan, and the repo at D (`CaseDataContracts.cs`, `AssessmentContracts.cs`, `Estimates.cs`, `Valuations.cs`, `RepairSpecifications.cs`, `Details.cshtml(.cs)`, `Shared/_Case*.cshtml`). No product file was changed.

## A. Eleven-section field matrix

### A1. Overview

| v3 field/control | v3 label (exact) | Input type / values | Core member at D | Razor partial | Notes |
| --- | --- | --- | --- | --- | --- |
| type | "Case type" | select `L.caseType` | none in `CaseDataContracts` | `_CaseSummary.cshtml` (`OperatorLabels.CaseTypeName`) | read-only in v3 |
| providerRef | "Claim / provider reference" | text, mono | `CaseClaimData.Number` | `_CaseSummary.cshtml` | provenance chip `extracted` |
| engineer | "Engineer" | select staff (role=engineer, enabled) | assigned Engineer lives in workflow, not `CaseDataContracts` | `_CaseSummary.cshtml` (`Model.EngineerDisplayName`) | v3 editable inline; repo read-only display |
| signoff | "Sign-off Engineer" | select `signoffEngineers()` | CASE-040 sign-off resolver | `_CaseSummary.cshtml` / ribbon | resolver: persisted sign-off → assigned → configured default |
| received | "Received" | date, read-only | `summary.ReceivedAtUtc` | `_CaseSummary.cshtml` | |
| instructionDate | "Instruction date" | date | `CaseInstructionData.InstructionDate` | `_CaseSummary.cshtml` | |
| due | "Due" | date | workflow `DueWork.DueBy` | `_CaseSummary.cshtml` | |
| auditRef / origin | "Audit reference" / "Origin" | text / select `L.origin` | `summary.Origin` | `_CaseSummary.cshtml` | |
| originalVerdict | "Original report verdict" | read-only, dialog `audit-verdict` | NONE | NONE | NEW (Audit only) |
| claimant.name/phone/email/address | "Name"/"Telephone"/"E-mail"/"Address" | text | `CaseClaimantData.Name` / `ContactNumber` / `Address` (no email) | `_CaseSummary.cshtml` | claimant contact ≠ `CaseContactData` (file handler) |
| repairer.* | "Name"/"Contact"/"Telephone"/"E-mail"/"Address" | text | NONE (no `CaseRepairerData`) | `_CaseSummary.cshtml` "Not recorded" | NEW repairer contact block |
| thirdParty / intermediary / imageSource | "Third party" / "Intermediary" / "Image source" | read-only | NONE | NONE | NEW display-only |
| incident.date/circumstances/special | "Incident date"/"Circumstances"/"Special instructions" | date/textarea | `CaseAccidentData.IncidentDate`/`.Circumstances` | `_CaseSummary.cshtml` | "Special instructions" NEW |
| Principal-wide note | "Principal-wide" (+ version) | read-only textarea | NONE | NONE | NEW (C directory supplies; B reads) |
| This case notes (principal) | "This case" | textarea | NONE | NONE | NEW |
| Claim source record | "Source record" | select claimSources | NONE | NONE | NEW (C `ClaimSourceAdministration.cs`; B selects ID + copies snapshot) |
| Source name/contact/telephone/email | same | text | NONE | NONE | NEW copied snapshot |
| Source-wide notes / This case notes (source) | same | read-only / textarea | NONE | NONE | NEW |
| Instruction complete | "Instruction complete" | checkbox | `CaseCompleteness.InstructionComplete` | `_ReadinessHiddenFields.cshtml` | factual control |
| Images complete | "Images complete" | checkbox | `CaseCompleteness.ImagesComplete` | same | factual control |
| Confirm requirements | — | — | — | — | RETIRED (spec §1.2, §4.3; plan B02) |

### A2. Engineer notes

| v3 field/control | v3 label | Input type | Core member | Razor partial | Notes |
| --- | --- | --- | --- | --- | --- |
| notes list | "Engineer notes" entries date/time/who/text | read-only list | `EngineerNotes.cs` append command | `_CaseEngineerNotes.cshtml` | matches |
| Add note | "Note" textarea, required | dialog in v3, inline form in repo | `AddEngineerNote` | `_CaseEngineerNotes.cshtml` | cosmetic only |
| count badge | "N notes" | read-only | `Model.EngineerNotes.Count` | same | matches |

### A3. Inspection

| v3 field/control | v3 label | Input type | Core member | Razor partial | Notes |
| --- | --- | --- | --- | --- | --- |
| inspection.method | "Inspection method" | select image/physical | `CaseInspectionMode` = `PhysicalAddress`, `ImageBasedAssessment` | `_CaseInspectionAddress.cshtml` | CONFLICT §I.1: never label as attendance; desktop always, report-address treatment only |
| inspection.at / location | "Inspection location" | select IBA / claimant / repairer / storage / previous / manual | `CaseInspectionData.Address`, `.StorageLocation`, `.RepairerAddress`; `InspectionAddressChoiceKind` (C) | `_CaseInspectionAddress.cshtml` | matches C's choice kinds 1:1 |
| Provider default | "Provider default" | read-only | `CaseDataSourceKind.ProviderSetting` | `_CaseInspectionAddress.cshtml` | matches |
| inspection.on | "Inspect on" | date | `CaseInspectionData.InspectionDate` | NONE surfaced | NEW UI, Core exists |
| inspection.present | "Vehicle present" | Yes/No | NONE | NONE | NEW |
| inspection.condition | "Condition at inspection" | text | NONE | NONE | NEW |
| inspection.contact/phone/email | "Contact"/"Telephone"/"E-mail" | text | NONE | NONE | NEW |
| inspection.notes | "Inspection notes" | textarea | NONE | NONE | NEW |
| Storage business/address/daily rate/recovery | "Storage business"/"Storage address"/"Daily rate"/"Recovery charge" | text/money | address `CaseInspectionData.StorageLocation`; amounts `SettlementStoragePerDay`/`CostStorageCharge`/`CostRecoveryCharge` | storage address only | one amount shared with Settlement, never duplicated |
| Deadline | (repo only) | date | `CaseInspectionData.Deadline` | NONE | RETIRED-CANDIDATE check: folds into "Due" |

### A4. Vehicle

| v3 field/control | v3 label | Input type | Core member | Razor partial | Notes |
| --- | --- | --- | --- | --- | --- |
| reg | "Registration" | text | `CaseVehicleData.Registration` | `_CaseVehicle.cshtml` | |
| vin | "VIN" | text | none in CaseData (lookup evidence only) | `_CaseVehicle.cshtml` via evidence | |
| make/model | "Make"/"Model" | text | `CaseVehicleData.Make`/`.Model` | `_CaseVehicle.cshtml` | |
| body/cls/colour/fuel/transmission/cc/year/firstReg | "Body"/"Class"/"Colour"/"Fuel"/"Transmission"/"Engine"/"Year"/"First registered" | text/select/number | `AssessmentVocabulary.VehicleBody/VehicleType/VehicleColour/VehicleFuel/VehicleTransmission/VehicleEngineCc/VehicleYear` | not surfaced | NEW wiring; "First registered" has no member |
| odometer | "Mileage" + mi/km toggle | number + unit switch | `CaseVehicleData.Mileage`/`MileageUnit` | read-only `mileageText` | edit control NEW; 1 mi = 1.609344 km; original value/unit/source retained |
| mileageSource | "Mileage source" | select `L.mileageSource` | `VehicleMileageSource` | label only | |
| taxExpiry/motExpiry | "Tax expires"/"MOT expires" | date | `VehicleTaxExpiry`/`VehicleMotExpiry` | NONE | NEW wiring |
| condition | "Condition" | select | `VehicleCondition` | NONE | NEW wiring |
| mods | "Modifications and extras" | text | NONE | NONE | NEW |
| faultCodes | "Fault codes" | text | `VehicleFaultCodes` | NONE | NEW wiring |
| airbags | "Airbags deployed" | Yes/No | `VehicleAirbagsDeployed` (text today) | NONE | NEW wiring |
| roadworthy/unroadworthyWhy | "Roadworthy"/"If not, why" | select/text | `LegalStatus`/`UnroadworthyReason` (findings) | NONE | NEW wiring; Engineer authority |
| tempRepairs/tempHow/tempCost | "Temporary repairs possible"/"How"/"Cost excl. VAT" | select/text/money | `VehicleTemporaryRepairsPossible/Method/Cost` | NONE | NEW wiring |
| historyNotes | "Vehicle history" | textarea | NONE explicit (`HistoryCheck` is report text) | NONE | NEW |
| vehicle.engineerNotes | "Engineer notes on vehicle" | textarea | NONE | NONE | NEW, distinct from Case Engineer notes |
| Look up DVLA & MOT | one button | action | `VehicleLookupResult` | two buttons today (Refresh DVLA / Refresh DVSA MOT) | v3 combines into one action |
| Suggestion chips / Apply | per-field chip | button | `VehicleSuggestionDecision.Accept/Correct` | form-based accept/correct | PR 670 ports chips |
| Experian check | "Experian check" | disabled | seam only | `_CaseVehicle.cshtml` gated | matches |

### A5. Damage

| v3 field/control | v3 label | Input type | Core member | Razor partial | Notes |
| --- | --- | --- | --- | --- | --- |
| Damage diagram | 23 regions + broad + wheels | SVG zones, keyboard | `AssessmentImpact(Zone, Severity, Note)`; `DamageZones` has 16 keys, no detailed 23 | `_CaseDamage.cshtml` single impact only | NEW diagram + parent map (§B) |
| impacts list | severity select, note, remove | per-impact row | `DamageImpacts` JSON | single ImpactLocation/Severity | NEW multi-impact UI over existing JSON |
| extra zone chips | underside/interior/mechanical | toggle chips | in `DamageZones` | NONE | NEW wiring |
| tyre×4 + belt×4 | "Tyre" OK/Worn/Damaged/Illegal, "Seat belt" OK/Locked/Deployed/Not fitted | select | `DamageTyre*`, `DamageBelt*` | NONE | NEW wiring |
| spare tyre / centre belt | "Spare tyre"/"Centre belt" | select | `DamageSpareTyre`/`DamageCentreBelt` | NONE | NEW wiring |
| unrelated / deduction / transfer | "Unrelated or pre-existing damage"/"Deduction for pre-existing damage"/"Paint or material transfer" | textarea/money/textarea | `DamageUnrelated`/`DamageUnrelatedDeduction`/`DamageMaterialTransfer` | NONE | NEW wiring |
| Type field | — | — | — | — | FORBIDDEN (spec §1.2, §5.5; plan B02) |

### A6. Valuation

| v3 field/control | v3 label | Input type | Core member | Razor partial | Notes |
| --- | --- | --- | --- | --- | --- |
| Source cards | source, Retail, Trade, guide month, mileage, note | cards + "guide-choice" | `ValuationDetails(Source, Date, Time, Mileage, RetailValue, TradeValue)`; `ValuationSource` = Glasses, Cazana, EngineersValue, AiMarketResearch; v3 also names Brego, Super CAP, AutoTrader, CAP HPI, Vehicle data | none yet (`_CaseValuation.cshtml` ported by B01) | manual source language extension; no live provider |
| Guide selection | "Use X as the guide basis" | button | NONE | NONE | NEW explicit guide basis |
| Engineer's value | "Engineer's value" (Accepted/Original) | money + "Rationale" | `ValueEngineer`; `ValuationPolicy.EngineersValueField` | NONE | NEW wiring, owner exists |
| Valuation history | When/By/Value/Reason | read-only | NONE (only recorded/last-edited stamps) | NONE | NEW ordered snapshot history |
| Adjustments — presets | checkbox + label + editable amount | checkbox/money | NONE | NONE | NEW (B03 `ValuationCalculations.cs`; F entity) |
| Custom addition | label + amount | text+money | NONE | NONE | NEW |
| Commercial VAT +20% | "Commercial VAT +20%" | checkbox, disabled when claimant VAT registered | NONE | NONE | NEW |
| Previous total loss | "Previous total loss" + 10%/20% | checkbox + segmented | NONE | NONE | NEW |
| Condition deduction | "Condition deduction" | money ≥ 0 | NONE | NONE | NEW |
| Apply | "Apply to Engineer's value" | button, disabled when negative | NONE | NONE | NEW command, professional finding |
| Recorded adjustments (legacy) | `<details>` "Recorded adjustments" | read-only | existing broad adjustments | NONE | RETAIN AS HISTORY, never folded in |

### A7. Estimate

| v3 field/control | v3 label | Input type | Core member | Razor partial | Notes |
| --- | --- | --- | --- | --- | --- |
| Estimate tabs | name + state chip, "+" new | tabs | `RepairSpecificationVersion`, `EstimateDetails.Name`, `RepairSpecificationState` | `_CaseEstimate.cshtml` `estimate-tab` | matches |
| Source / repair days / rate card | "Source"/"Repair days"/"Labour-rate card" | select/number/select | `RepairSpecificationSource.Route`; `RepairDays`, `LabourRate` typed manually | `_CaseEstimate.cshtml` | versioned rate-card selector NEW |
| Additional materials / Other costs | same | money | `EstimateDetails.PaintMaterials`/`OtherCosts` | `_CaseEstimate.cshtml` | matches |
| Row: Operation | "Operation" | Replace/Repair/R&I/Paint/Blend/Specialist/Other | `EstimateOperation` = Replace/Repair/RemoveAndRefit/Paint/Other; `EstimateLineCodes.Types` has `paint_blend`, `specialist_fixed`, `specialist_wu` | 5-option select | add Blend + Specialist, extend `EstimateOperations.ToLineType/FromLineType` |
| Row: Description/Part number/Qty/Labour h/Paint h/Unit £ | same | text/number/money | `EstimateLineInput`/`CaseEstimateLineRecord` | table | matches |
| Row: Materials | "Material £" | money | NONE on line | NONE | NEW |
| Row: provenance badge | Imported/Amended/AI proposal/Manual | chip | header-level `RepairSpecificationSource` only | header only | NEW per-line origin/current + amendment actor/time |
| Discounts Parts/Materials/Specialist/Overall | same | % 0–100 | NONE | NONE | NEW |
| VAT categories Labour/Parts/Materials/Specialist | same | checkboxes + override chip + "Reset to repairer status" | NONE | NONE | NEW |
| Repairer VAT registered | "Repairer VAT registered" | Yes/No (+Unknown state) | `CostRepairerVatRegistered` (flag) | `_CaseSettlement.cshtml` read-only | tri-state + override NEW |
| Totals Parts/Labour/Paint/Other/Subtotal/VAT/Total | same | read-only | `EstimateTotals.Compute` flat calculator | `RenderEstimateTotals` (literal-text defect ENG-039) | extend in place: panel/paint split, materials, specialist, off-pattern, discounts, category VAT, raw+printed |
| Hours strip | Panel/Paint/Specialist not costed/Total costed/Rate | read-only | NONE | NONE | NEW |
| Calculation breakdown | `<details>` | read-only | NONE | NONE | NEW |
| Derived lists | "New parts required"/"Repairs required"/"Additional operations" | read-only | `RepairSpecificationPolicy.ToDisplayLists` exact match | not surfaced | wiring NEW |
| Glass's / Audatex / Import / Send to Claude | buttons | actions | Audatex parser exists; JSON import exists; Glass's NONE | Import + Send to Claude only | Glass's launch NEW (B04) |
| Versions dialog | "Versions (N)" | read-only table | supersession chain | NONE | presentation NEW |
| Compare dialog | "Compare estimates" | table | `IListCaseEstimates` | NONE | NEW UI |
| Repairer name/costs agreed/contract value | same | text/select/money | NONE | NONE | NEW |

### A8. Settlement

| v3 field/control | v3 label | Input type | Core member | Razor partial | Notes |
| --- | --- | --- | --- | --- | --- |
| Outcome picker | "Repairable"/"Total loss"/"Cash in lieu"/"Contract repair" | button group / chip | `Outcome` codes exact match | read-only | write path NEW |
| Category | "Category" A/B/S/N/N/A | select | `SalvageCategory` | `_CaseSettlement.cshtml` | matches |
| Salvage value | "Salvage value" | money | `SalvageValue` | same | matches |
| Excess / Betterment contribution / Claimant VAT registered / Reserve | same | money/money/Yes-No/money | `SettlementExcess`/`SettlementBetterment`/`SettlementClaimantVatRegistered`/`SettlementReserve` | NONE | NEW wiring |
| Repair duration / Repair delays / Report delay | "Repair duration" (working days)/"Repair delays"/"Report delay" | number/text/text | `SettlementRepairDelays`, `SettlementReportDelay`; duration NONE | NONE | duration NEW |
| Storage per day / Recovery | same | money | `SettlementStoragePerDay`/`CostRecoveryCharge` | read-only | shared with Inspection |
| Diminution / Hire start / Daily hire cost | same | money/date/money | `SettlementDiminution`/`SettlementHireStart`/`SettlementHireDailyCost` | NONE | NEW wiring |
| Salvage at/agent/reference/moved/owner retains/value agreed/settled | same | text/select/date | all seven `SettlementSalvage*` present | NONE | wiring only |
| Derived figures | "Repair cost incl. VAT"/"After betterment"/"Engineer's value"/"Salvage"/"Equity" | read-only | combine `EstimateTotals` + accepted Engineer value | NONE | NEW; distinct from total-loss PAV − salvage |

### A9. Report

| v3 field/control | v3 label | Input type | Core member | Razor partial | Notes |
| --- | --- | --- | --- | --- | --- |
| Engineer's comments / Vehicle history check | same | textarea | `EngineersComments`/`HistoryCheck` | `_CaseReport.cshtml` | matches |
| Signing Engineer | "Signing Engineer" | select flagged accounts | `EngineerName/Qualifications/Signature` retired (D18); signatory account/version is owner | text only | selector NEW; retired gates removed (ENG-038) |
| Agreed fee / Fee description | same | money/textarea | `AgreedFee`/`FeeDescriptionLines` | `_CaseReport.cshtml` | matches |
| Statement of truth | computed | — | `StatementOfTruth`; `StatementOfTruth3` hardcodes Glass's | `_CaseReport.cshtml` | must become source-aware |
| Disclose guide source / Valuation commentary / Include unrelated damage | same | checkboxes | NONE | NONE | NEW |
| Override report date / Report date | same | checkbox + date | NONE | NONE | NEW |
| Report wording | `<details>` | read-only composed | NONE | NONE | NEW disclosure |
| To / CC / Subject | same | text | NONE | NONE | NEW (B07) |
| Report images | Close-up/Overview/Supporting n/Not used + crop/move/reset | select per image | NONE | NONE | NEW (B06) |
| Readiness list | ordered labels | read-only | `AssessmentReadinessItem` | inline text | itemised list |
| Generate report / Preview report / Preview fee note | buttons | actions | report projection/rendering | Generate/Preview draft only | fee-note preview NEW |
| Prepare delivery | button + dialog | action | NONE | NONE | NEW (B07) |
| Issued history | Issued/Document/Outcome/Total | read-only | NONE | NONE | NEW |

### A10. Files and A11. Notes

Files: Documents (name/role/size/source/date, custody chip, Preview, Save as), Vehicle images gallery/viewer, Correspondence (Reply/Compose), Post-report queries (Mark resolved), Upload requests (Withdraw), Create upload link. Existing partials `_CaseFiles.cshtml`, `_CaseDocuments.cshtml`, `_CaseCorrespondence.cshtml`; B06 verifies parity. Notes: `_CaseHistory.cshtml` newest-first list with Add Case note / Record chase; matches.

## B. Damage regions (verbatim from v3 `ZONES`, manager source agrees)

| # | Key | Label | Parent |
| --- | --- | --- | --- |
| 1 | `front_left_corner` | Front N/S corner | `left_front` |
| 2 | `front_centre` | Front centre | `front` |
| 3 | `front_right_corner` | Front O/S corner | `right_front` |
| 4 | `left_front_wing` | N/S front wing | `left_front` |
| 5 | `left_front_door` | N/S front door | `left_side` |
| 6 | `left_rear_door` | N/S rear door | `left_side` |
| 7 | `left_quarter` | N/S rear quarter | `left_rear` |
| 8 | `right_front_wing` | O/S front wing | `right_front` |
| 9 | `right_front_door` | O/S front door | `right_side` |
| 10 | `right_rear_door` | O/S rear door | `right_side` |
| 11 | `right_quarter` | O/S rear quarter | `right_rear` |
| 12 | `rear_left_corner` | Rear N/S corner | `left_rear` |
| 13 | `rear_centre` | Rear centre | `rear` |
| 14 | `rear_right_corner` | Rear O/S corner | `right_rear` |
| 15 | `bonnet` | Bonnet | `front` |
| 16 | `windscreen` | Windscreen | `front` |
| 17 | `roof` | Roof | `roof` |
| 18 | `rear_screen` | Rear screen | `rear` |
| 19 | `tailgate` | Boot / tailgate | `rear` |
| 20 | `wheel_lf` | N/S front wheel | `wheel` |
| 21 | `wheel_rf` | O/S front wheel | `wheel` |
| 22 | `wheel_lr` | N/S rear wheel | `wheel` |
| 23 | `wheel_rr` | O/S rear wheel | `wheel` |

Broad regions (`BROAD_ZONES`): `front, left_front, right_front, left_side, right_side, left_rear, right_rear, rear`. Additional chips: `underside`, `interior`, `mechanical`. Existing `AssessmentVocabulary.DamageZones` has the 8 broad + `roof` + four `wheel_*` + underside/interior/mechanical (16 keys), none of the 23 detailed keys. A broad legacy impact is never auto-split into detailed impacts.

## C. Valuation presets, order, example

Presets (manager `INCS`, v3 `DATA.valuationPresets`): Tow bar £300; PCO plated £1,500; Decals £500; Camper conversion £0; Driving tuition £500. (Manager's "VAT (on commercial)" is the +20% rule, not a preset; blank "Other" slots are custom additions.)

Order (manager `adjCalc`, v3 `calculateValuation`, spec §9.1): V = round(B × 20%) when commercial VAT applies; A = B + V; T = round(A × p) for prior total loss p ∈ {10%, 20%}; Proposal = round(A − T + F − C), F = enabled fixed additions (≥ 0), C = condition deduction (≥ 0). Whole-pound rounding at each step; plan specifies `RoundAwayFromZero`. Example: B £3,100 → V £620 → A £3,720 → T (20%) £744 → F £300 → C £100 → £3,176 (prints `£3,176.00`).

## D. Estimate rows, operations, controls, totals

Row columns: Operation | Description | Part number | Qty | Labour h | Paint h | Unit £ | Material £ | Source | remove. Operations (`L.estimateOp`): Replace, Repair, R&I, Paint, Blend, Specialist, Other. Controls: one labour-rate card (paint rate mirrors it); discounts Parts/Materials/Specialist/Overall (%); VAT categories Labour/Parts/Materials/Specialist with "Manual override" chip and "Reset to repairer status"; "Repairer VAT registered". Totals: hours strip; Parts, Labour, Paint, Specialist, Net, `VAT n%`, Total; `<details>` "Calculation breakdown" (panel labour, paint labour, materials, specialist, off-pattern, each discount negative, taxable after discounts).

The v3 HTML `estimateTotals()` JS is a simpler blended-VAT formula with no penny reconciliation; the plan's B04 arithmetic (independent per-category rounding away from zero, printed Net = sum of printed components, printed Gross = printed Net + printed VAT, never move a residual penny) is canonical. Repo `EstimateTotals.Compute` (`Estimates.cs` ~L82–106) is the single flat calculator to extend in place.

## E. Settlement

Outcomes/report titles: `total_loss` "Total loss" (TOTAL LOSS REPORT); `repairable` "Repairable" (REPAIRABLE REPORT); `cash_in_lieu` "Cash in lieu" (CASH IN LIEU REPORT); `contract_repair` "Contract repair" (CONTRACT REPAIR REPORT). Fields: Category, Salvage value, Excess, Betterment contribution, Claimant VAT registered, Reserve, Repair duration (working days), Repair delays, Report delay, Storage per day, Recovery, Diminution, Hire start, Daily hire cost; Salvage at, Salvage agent, Agent's reference, Moved, Owner wishes to retain, Value agreed, Settled.

## F. Report

Content switches: Disclose guide source, Valuation commentary, Include unrelated damage. Override report date + Report date. Signing Engineer (flagged accounts). Agreed fee, Fee description. Report wording `<details>`. Readiness (ordered): Current estimate; Labour-rate card on {estimate}; Engineer's value; Outcome; Category and salvage value (total loss); Sign-off Engineer; One Close-up and one Overview report image; Roadworthiness; Unroadworthy reason (if unroadworthy); Inspection method; Report date (if override). Image roles: Close-up, Overview, Supporting n, Not used; keyboard "Move earlier"/"Move later". Delivery: To, CC, Subject, "Prepare delivery" dialog (v3 mockup has no send; production send is A's `IStaffReportSend`).

## G. Ribbon / action bar / rail

Ribbon: Case/PO, Registration, Claimant, Principal, Engineer, Sign-off, State (repo `Details.cshtml` already matches). Action bar: Edit Case/Finish editing; Place on Hold/Release Hold; Create upload link; Send to EVA; Report sent (confirm)/"No Sent item found"; Close Case; Reopen Case (+ Open replacement); Return to Engineer; Preview report (complete). Repo matches nearly exactly. Rail: Current position (State, Version, Due, Engineer, Sign-off, Editing) matches; Next action has no per-blocker resolve button in the repo (gap); Figures (Repair cost incl. VAT, Engineer's value, Salvage, Outcome, Repair cost of value %) is entirely missing in the repo (NEW). Scroll/Tabs: repo has scroll-spy jump nav only; Tabs mode NEW (`layout-switch`, persisted preference). Narrow-width rail collapse: to verify in B08 CSS.

## H. Explanatory copy in v3 to omit

Prototype disclaimers ("Not connected in this preview", "PDF / XML parsing is not connected in this preview", "Issued document metadata is retained…", "Email delivery is not connected in this preview"); the manager's tooltip "Claimant is VAT registered — VAT cannot be added to the value." (use a disabled state + `data-condition` gate); outcome-picker helper subtitles ("Settle at pre-accident value less salvage", "Repair at the current estimate", "Cash settlement for the repair", "Repair capped at the agreed total"). Report-wording sentences are report body, not UI copy.

## I. v3 statements the plan overrides

1. "Physical inspection" method label implies attendance; implement desktop-always with report-address treatment (blank / IBA / selected physical vehicle address).
2. Local `addressHistory` fixture; consume only C's S05 directory query.
3. Role editing only in Report; Files viewer must show identical preparation.
4. Glass's as a valuation source is manual evidence only; no live valuation; Estimate has the only real Glass's integration.
5. v3 `estimateTotals()` JS is not the port target; plan B04 arithmetic wins.
6. No periodic review, Confirm requirements, review checkboxes or AI-approval panel.
7. No damage Type field.
