# Case record

- **Mockup route:** `pegasus_case_record_v28.html` in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Cases/Details.cshtml` and `Shared/_Case*.cshtml`

- **Dialogs:** [`dialogs/`](dialogs/README.md) · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)
- [**How it works**](how-it-works.md)
- Child: [**Create audit**](../create-audit/README.md) · [**EVA handoff**](eva-handoff/README.md)

## Screenshots

- [s19-case-record-review-1580.png](../../../current/v28-shots/s19-case-record-review-1580.png) · [1440](../../../current/v28-shots/s19-case-record-review-1440.png) · [760](../../../current/v28-shots/s19-case-record-review-760.png)
- [s20-case-record-edit-1580.png](../../../current/v28-shots/s20-case-record-edit-1580.png) · [1440](../../../current/v28-shots/s20-case-record-edit-1440.png) · [760](../../../current/v28-shots/s20-case-record-edit-760.png)
- [s21-case-record-held-1580.png](../../../current/v28-shots/s21-case-record-held-1580.png) · [1440](../../../current/v28-shots/s21-case-record-held-1440.png) · [760](../../../current/v28-shots/s21-case-record-held-760.png)
- [s23-case-record-audit-1580.png](../../../current/v28-shots/s23-case-record-audit-1580.png) · [1440](../../../current/v28-shots/s23-case-record-audit-1440.png) · [760](../../../current/v28-shots/s23-case-record-audit-760.png)
- [s24-case-record-tabs-1580.png](../../../current/v28-shots/s24-case-record-tabs-1580.png) · [1440](../../../current/v28-shots/s24-case-record-tabs-1440.png) · [760](../../../current/v28-shots/s24-case-record-tabs-760.png)
- EVA handoff: [s22-case-record-evasend-1580.png](../../../current/v28-shots/s22-case-record-evasend-1580.png) · [1440](../../../current/v28-shots/s22-case-record-evasend-1440.png) · [760](../../../current/v28-shots/s22-case-record-evasend-760.png)

## Notes

- This is the largest lane of the baseline capture: one page models all ten
  sections, the aside, the full-screen viewer, every frame- and
  section-owned dialog, and the standalone EVA handoff fallback page, driven
  by thirteen `cr*`-prefixed mockup-strip controls (see `states/README.md`).
- **Damage Plan clicker geometry.** The live diagram is drawn from
  `DamagePlanGeometry`'s own body/glass/zone SVG paths (a stylised outline
  with ~19 named panels plus 4 wheels). This capture approximates the same
  top-down silhouette and interaction (click a zone, cycle through Light →
  Light to moderate → Moderate → Moderate to heavy → Heavy → none; a
  numbered recorded-zones list; the same severity legend and colour ramp
  via the live `.is-damaged[data-sev="…"]` CSS) with a simplified rectangle
  grid rather than reproducing that exact path geometry. The severity codes,
  labels and colour mechanism are the real ones
  (`AssessmentVocabulary.DamageSeverities`, `case-workspace.css`).
- **AssessmentCanOpen / AssessmentIsReadOnly are not modelled fact-for-fact.**
  These two flags gate whether the Engineer sections (Damage, Valuation,
  Estimate, Settlement, Report) are assessable at all; the source they are
  read from (`assessmentAccess`) is outside `Details.cshtml.cs` and was not
  traced further. This capture treats the Engineer sections as assessable
  once the Case has reached Review or a later/closed state, and never
  assessable in Not ready or Held — a reasonable, clearly-labelled
  simplification, not a traced fact. See `states/README.md`.
- **Closure outcomes are not individually re-derived.** `AvailableClosureOutcomes`
  puts every `CaseClosureOutcome` through `CaseLifecycleRules.ValidateClose` /
  `RequireClosureIsAllowed`, which live in Core and were not opened. The
  capture offers "Close case" (with all four named adverse outcomes in its
  chooser) whenever the page-wide edit session is open and the Case is not
  already in one of the four closed states — not a traced per-outcome gate.
- **Create audit's full precondition set is simplified to one strip toggle**
  (`crhasreport`): the live gate also requires no existing Audit Case and no
  existing Original Case link (`CanCreateAudit`), which this single-fixture
  capture does not model since it never shows a second, already-linked Case.
- The ribbon identity (Case/PO, registration) switches between the fixture
  sheet's two named Cases when the strip's Case type is set to Audit
  (`a.QDOS26150` / `BN65 UUY`) versus Inspection or Inspection + Audit
  (`QDOS26214` / `MA59 BDY`) — a deliberate two-fixture convenience, not a
  literal re-identification the live page performs (a real Case's identity
  never changes under an operator's feet).
- The full-screen viewer's crop tool is captured as the tool-switch state
  (view tools hide, crop tools with Aspect/Rotate/Full frame/Reset/Save/Cancel
  show) per the skill's stub rule, not the drag-and-resize geometry itself;
  Save crop and the per-image Rotate/Reset controls route to a toast rather
  than silently mutating a stored rectangle.
- Estimate, Valuation and Settlement show one representative fixture
  (one current estimate, one recorded Glass's card, one AI-proposal set)
  rather than re-deriving totals live from edited inputs; the printed
  rollup, VAT bar and worklists are a fixed fixture snapshot, consistent
  with every other v28 lane's stated scope.
