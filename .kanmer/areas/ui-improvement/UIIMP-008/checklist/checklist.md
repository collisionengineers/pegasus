# Checklist — UIIMP-008

- [x] Core projection repaired (Case row title/detail/reason/source audit
      findings applied; Triage no-finding states queried directly; pushed
      structure otherwise kept)
- [x] OperatorLabels extended (kind + priority lists, string overloads); no
      duplicate spellings
- [x] Index.cshtml.cs: selected-item binding, metric figures, view mapping,
      no old dashboard sections
- [x] Index.cshtml: header, five-metric strip, two panes per contract;
      classes from the component map only; no inline styles; no new JS
- [x] Blocked metric = BlockedIntake and links to /Cases?tab=unidentified (D14)
- [x] Every metric and work-item links to a real route; every figure queried
- [x] No explanatory copy / no Filter no-op / no empty-state prose
- [x] DashboardBoundaryTests repaired + composition coverage added
      (incl. buried-Triage regression)
- [x] DashboardCountersWebTests rewritten for the new strip markup
- [x] Release build green (compiler feedback; tests/snapshots deferred to
      orchestrator)
- [x] Simplification pass recorded under a dated heading in plan/plan
- [x] PR opened to dev (#598); stopped before merge/proof
