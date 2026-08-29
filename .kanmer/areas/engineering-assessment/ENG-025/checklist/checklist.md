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
- [x] `dotnet restore --locked-mode` + `dotnet build -c Release --no-restore` pass — and, beyond the plan, the focused Assessment/SendToAi filter runs green
- [x] Simplification pass recorded in plan under dated heading
- [x] PR open to dev, correct title; stop there — [#616](https://github.com/collisionengineers/pegasus/pull/616), OPEN, base `dev`, not draft, "ENG-025: Port the Assessment workspace shell (assessment-v3, evidence rail, D11 access)"

## Added after the plan was written

- [x] Scope split: the multi-estimate editor reverted off this branch (`bc16d8fa`) and salvaged to `task/eng-028-estimate-editor` (`6b4d11db`, pushed); ENG-028 scratch records its state
- [x] `origin/dev` @ `9868cf58` merged (`93766579`)
- [ ] Ticket verification 2 — "No clipped text/overflow at 1580/1100/760": NOT proven. The one browser test on this page runs at 1920×1080; the three-width walk is the orchestrator's wave gate

## Round 2 — verifier remediation (2026-08-29)

- [x] Blocker: the plan's false `ISaveAssessment` justification corrected in the plan and the report; the twelve inert fields rejected with the form-boundary evidence; **impact location's real regression deferred to [[ENG-029]]** (created, EPIC-011 wave 4, linked)
- [x] Major: outbound-payload PII guard (`DoesNotContain("claimant", request.Body)`) restored, plus `schema_version` and `case_reference`, in `SendToAiConnectorAdministrationTests.cs`; test green
- [x] Major: report corrected — "Behavioural deletions" names SaveDamage + grid, Send/Reconcile, the field editor and the two dropped tests
- [x] Minor: the twice-typed D7 condition string is one page-model property, `EstimatingServiceCondition`
- [x] Minor: cross-lane share of `AssessmentWorkspaceTestData.cs` with **TICK-058** disclosed (disjoint hunks, not edited around)
- [x] Minor: site.js `Owns` departure rejected with the step-0 reuse reason
- [x] Rebuilt (0 warnings, 0 errors); focused filter re-run: **88/88 Core, 43/43 Integration** (Browser excluded — round 1's 49 included the six browser tests)
