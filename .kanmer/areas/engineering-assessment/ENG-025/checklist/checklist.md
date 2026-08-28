# Checklist — ENG-025

- [ ] Core: `AssessmentAccessPolicy.CanOpen` = {ReportPreparation, PostReport, PostReportComplete} + current export; `IsReadOnly` on PostReportComplete; Core tests updated
- [ ] Gate: unavailable surface (notice + Back) when access refuses; 404 only for unknown case
- [ ] Header "Assessment" / eyebrow "REF · reg" / Back to Case + Refresh
- [ ] 7-item identity ribbon (Case/PO, Registration, Claimant, Principal, State, Mileage, Vehicle)
- [ ] Record bar: edit-lease controls, Import estimate (dialog, real handler, conditioned), Glass's + Audatex disabled D7/EXT-09 seams, Send to Claude (primary, dialog, `ICreateAiJob` Estimate kind, disabled without Engineer's Value / read-only), Generate + Preview report draft (not-ready names the condition)
- [ ] `assessment-v3`: collapsible Evidence rail (Instruction + images, evidence viewer) | Estimates pane (accepted/draft specification + lines + basis, accept form, import dialog; "No estimates recorded." empty state)
- [ ] Old seven section tabs, unbound forms, PAV slider, embers, readiness panel, SaveDamage + damage grid, Send/Reconcile panel machinery removed
- [ ] No inline `<script>`/`style`; no new CSS/JS file; no inert control
- [ ] Tests: policy theory rows, web tests retargeted, browser readiness test retargeted, fake access state ReportPreparation
- [ ] `dotnet restore --locked-mode` + `dotnet build -c Release --no-restore` pass (no test runs)
- [ ] Simplification pass recorded in plan under dated heading
- [ ] PR open to dev, correct title; stop there
