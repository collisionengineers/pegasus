# Legacy Case/PO marker decision superseded for Pegasus

**Status:** Superseded for Pegasus on 2026-07-24 by the direct user
decision recorded in `docs/history/product/project-discovery-questionnaire.md`.

**Historical references:** `docs/reviews/160726/decisions.md` and
`docs/reference/reports/0014-audit-case-type-second-inspection.md` are not
present in this worktree. Their former paths are retained as historical labels,
not current authority or live links.

## Current Pegasus decision

- The normal Case/PO is `{principal code}{YY}{three-digit shared sequence}`.
- Inspection, Audit, and Inspection + Audit use one shared principal/year
  sequence. There is no independent counter per marker or work type.
- A standalone Audit derives lowercase `a.` or `ap.` from the repairable or
  total-loss assessment in the original Engineer's report. Missing or ambiguous
  evidence blocks case creation and reference allocation.
- Inspection + Audit starts with the standard Inspection reference. After
  Collision Engineers' own Engineer records the assessment, the applicable
  lowercase `a.` or `ap.` reference is created inside the same case folder; it
  is not allocated from a separate marker sequence.
- Diminution and Commercial are deferred. Do not expose active `D.` or `C.`
  markers, counters, case types, or allocation paths without a later direct
  product decision.
- A case principal/reference is immutable immediately on allocation. Wrong-
  principal work uses the terminal `Created in error` linked-replacement path;
  neither number is reassigned or reused.

## Historical proposal not adopted

The predecessor proposal used independent per-provider/year counters for
standard, `A.`, `AP.`, `D.`, and `C.` markers and rendered different casing for
different external systems. That design is not a Pegasus requirement and must not be
used as allocator, schema, UI, Box, EVA, or test authority.

## Current evidence state

The current provider-neutral intake path is called at `/Intake/Upload` through
`ProcessIntake`, but it creates only a pre-case receipt/draft and allocates no
case or reference. The shared allocator, Audit reference behavior, immutable
identity, and linked replacement flows remain planned and require proof through
their future authenticated callers.
