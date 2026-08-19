# Research — diminution rendering

## Question

Is diminution rendering part of the approved CollisionRenderer activation?

## Findings

1. RPT-04 is allocated for diminution using accepted original-case data plus an Engineer-entered percentage, but FRD-11 does not yet specify exact wording/calculation/approval evidence.
2. The workspace contains a generic `diminution-rebuttal` template, but rendererref1—the operator-approved initial evidence—contains only four assessment variants and fee note.
3. The operator approved activating only rendererref1 assessment/fee-note families and leaving unsupported catalogue entries inactive.
4. A workspace template is non-caller evidence and cannot invent the Core diminution contract, percentage semantics, amendment linkage, or accepted wording.
5. Diminution can reuse future report identity/version/custody infrastructure, but its capability remains unavailable until separately governed and accepted.

## Implications

- Do not migrate/activate `diminution-rebuttal` in the initial integration.
- Preserve RPT-04 as deferred work; later research must define original-report linkage, percentage meaning/precision, calculation, wording, approval, and correction behavior.
- Shared renderer infrastructure must not hard-code assessment-only assumptions that prevent a later typed template.
