# Checklist — INTK-016

- [x] UploadCaseDecision service (search + attach orchestration) + DI registration
- [x] UploadOutcomeView.Attach set on ReadyToCreate / PossibleMatch / ImageCaseRegistered; Attached branch provenance-honest (via new `IntakeReceipt.ManualAssociationActorKind`)
- [x] _UploadOutcome partial: details-based add-to-existing-case form + Cancel link
- [x] CaseSearch JSON + Attach POST handlers on both status pages
- [x] site.js combobox (debounced fetch, keyboard, ARIA by script) + CSS (incl. `[hidden]` display fix and green-chip contrast token)
- [x] FRD-02 + FRD-12 updated in the same PR
- [x] Web tests: search authorised / anonymous redirect / roleless forbidden / matches (UploadConfirmationWebTests, 6 tests)
- [x] Web tests: attach end-to-end (instruction receipt + image group typed-reference merge), replay-safe
- [x] Web test: report-not-reoffer when automation already attached; fail-closed unresolvable reference
- [x] Browser combobox accessibility test (UploadCaseSearchBrowserTests); AccessibilityTests 24/24 + Upload browser suites 7/7 green
- [x] Release build zero warnings; Core 715/715; Architecture 97/97; focused integration filters green
- [ ] Simplification pass recorded in plan (running)
