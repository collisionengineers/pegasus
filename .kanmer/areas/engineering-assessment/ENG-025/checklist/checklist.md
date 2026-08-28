# Checklist — ENG-025

- [x] Core: `AssessmentAccessPolicy.CanOpen` = {ReportPreparation, PostReport, PostReportComplete} + current export; `IsReadOnly` on PostReportComplete; Core tests updated
- [x] Gate: unavailable surface (notice + Back) when access refuses; 404 only for unknown case
- [x] Header "Assessment" / eyebrow "REF · reg" / Back to Case + Refresh
- [x] 7-item identity ribbon (Case/PO, Registration, Claimant, Principal, State, Mileage, Vehicle)
- [x] Record bar: edit-lease controls, Import estimate (dialog, real handler, conditioned), Glass's + Audatex disabled D7/EXT-09 seams, Send to Claude (primary, dialog, `ICreateAiJob` Estimate kind, disabled without Engineer's Value / read-only), Generate + Preview report draft (not-ready names the condition)
- [x] `assessment-v3`: collapsible Evidence rail (Instruction + images, evidence viewer) | Estimates pane (accepted/draft specification + lines + basis, accept form, import dialog; "No estimates recorded." empty state)
- [x] Old seven section tabs, unbound forms, PAV slider, embers, readiness panel, SaveDamage + damage grid, Send/Reconcile panel machinery removed
- [x] No inline `<script>`/`style`; no new CSS/JS file; no inert control
- [x] Tests: policy theory rows, web tests retargeted, browser readiness test retargeted, fake access state ReportPreparation
- [x] `dotnet restore --locked-mode` + `dotnet build -c Release --no-restore` pass — and, beyond the plan, the focused Assessment/SendToAi filter runs green (88/88 Core, 49/49 Integration)
- [x] Simplification pass recorded in plan under dated heading
- [ ] PR open to dev, correct title; stop there

## Added after the plan was written

- [x] Scope split: the multi-estimate editor reverted off this branch (`bc16d8fa`) and salvaged to `task/eng-028-estimate-editor` (`6b4d11db`, pushed); ENG-028 scratch records its state
- [x] `origin/dev` @ `9868cf58` merged (`93766579`)
- [ ] Ticket verification 2 — "No clipped text/overflow at 1580/1100/760": NOT proven. The one browser test on this page runs at 1920×1080; the three-width walk is the orchestrator's wave gate
