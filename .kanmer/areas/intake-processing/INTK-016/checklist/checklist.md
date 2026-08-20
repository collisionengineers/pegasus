# Checklist — INTK-016

- [x] UploadCaseDecision service (search + attach orchestration) + DI registration
- [x] UploadOutcomeView.Attach set on ReadyToCreate / PossibleMatch / ImageCaseRegistered; Attached branch provenance-honest (Core-owned `AssociationWasStaffDecision`)
- [x] _UploadOutcome partial: details-based add-to-existing-case form + Cancel link
- [x] CaseSearch JSON + Attach POST handlers, one shared `UploadConfirmationPageModel` for both status pages
- [x] site.js combobox (debounced abortable fetch, keyboard, ARIA by script) + CSS (`[hidden]` fix, `--success-fg` contrast token, `data-refresh-hold`)
- [x] FRD-02 + FRD-12 updated in the same PR
- [x] Web tests: search authorised / anonymous redirect / roleless forbidden / matches
- [x] Web tests: attach end-to-end (instruction receipt + image-group typed-reference merge with reconcile sweep), replay-safe; fail-closed unresolvable reference
- [x] Web test: report-not-reoffer when automation already attached
- [x] Browser combobox accessibility test; AccessibilityTests 24/24 + Upload browser suites 7/7 green
- [x] Release build zero warnings; Core 715/715; Architecture 97/97; focused integration filters green
- [x] Simplification pass recorded in plan (2026-08-20, four lenses; applied + skipped dispositions)
- [x] PR #465 opened against dev
