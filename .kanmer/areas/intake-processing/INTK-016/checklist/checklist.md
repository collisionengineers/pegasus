# Checklist — INTK-016

- [ ] UploadCaseDecision service (search + attach orchestration) + DI registration
- [ ] UploadOutcomeView.Attach set on ReadyToCreate / PossibleMatch / ImageCaseRegistered; Attached branch provenance-honest
- [ ] _UploadOutcome partial: details-based add-to-existing-case form + Cancel link
- [ ] CaseSearch JSON + Attach POST handlers on both status pages
- [ ] site.js combobox (debounced fetch, keyboard, ARIA by script) + CSS
- [ ] FRD-02 + FRD-12 updated in the same PR
- [ ] Web tests: search authorised / anonymous redirect / matches
- [ ] Web tests: attach end-to-end (instruction receipt + image group), replay-safe
- [ ] Web test: report-not-reoffer when automation already attached
- [ ] Browser combobox accessibility test; AccessibilityTests + Upload browser suites green
- [ ] Release build zero warnings; focused test filters green
- [ ] Simplification pass recorded in plan
