# Plan — ENG-006

Branch `task/eng-assessment-page` from origin/dev (0857f7b9), worktree `../pegasus-worktrees/eng-assessment`.

1. **Copy strip** (rule: PLAT-016): delete every `<small id="*-hint">` under assessment controls — where the hint began "Required", the label gains `class="req"` (CSS `::after " *"`) and the control `aria-required="true"`; delete the three `status-card--info` explainer cards, the two estimate table caption sentences, the "Working" column (Item/Amount stay), the "Remove-and-refit lines…" line, and the damage/report-images lede sentences.
2. **Estimate containment**: drop the "Where this came from" and "Why this line" entry columns (over-explaining provenance micro-labels on an unbound entry grid — PLAT-015 owns the dead-control debt); reduce `.line-grid` min-width 78rem → 56rem so the section fits typical widths; the `.table-wrap` scroll stays for narrow screens.
3. **Damage diagram** (reuse: `AssessmentVocabulary.ImpactLocation` codes, `ISaveAssessment`, `IAcquireCaseEditLease`, the estimate-import lease pattern): the SVG becomes 9 clickable regions (front, left/right front, left/right side, left/right rear, rear, roof) rendered as buttons in a POST form; selecting sets `assessment.impact_location`; `OnPostSaveDamageAsync` acquires a lease and calls `ISaveAssessment` with that one field; saved value highlights its region and the Impact location dropdown preselects it (one field, two views). The report draft already requires/prints ImpactLocation — no report change needed.
4. **Method preselect**: radios check from the case's recorded inspection mode (`CaseDetails` case data → Inspection.Mode current value; QDOS default Image Based Assessment is already recorded server-side).
5. **Tests**: web test — page renders no "Required." hint text and no explainer cards; damage save persists and re-renders highlighted; method radio preselected for an IBA case. Suites: assessment web tests + Release build 0/0.

Deviation: subagents barred — self-review recorded.
