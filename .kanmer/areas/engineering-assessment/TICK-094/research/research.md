# Research — Engineer-owned report decisions

## Question

Which Engineer-owned accepted values drive derived figures and report narratives without retyping?

## Findings

1. Core assessment already stores keyed fields and estimate lines with confirmation actor/time and blocks report readiness on unconfirmed values.
2. Rendererref1's four outcomes require Engineer-owned outcome, final vehicle value, deductions, salvage category/value for total loss, roadworthiness/reason, assessment method, repair figures, and selected engineer/signature/qualifications. Its generator computes totals/settlement/VAT from raw accepted components.
3. Automation MCP writes assessment values only as unconfirmed working data. Professional-finding confirmation remains staff-Engineer-only; no automation caller approves or issues reports.
4. Current free-form vocabulary includes renderer-path-like keys but needs typed/enumerated validation for the approved outcome and legal/category values. Unknown values must fail closed rather than flow into templates.
5. Derived monetary figures must be computed once in Core from accepted inputs using a versioned calculation policy. Templates display results and compose approved wording; they do not recalculate or accept caller-supplied totals.
6. The accepted report snapshot binds Engineer identity/decision timestamps and source versions. Later corrections create a new report input/artifact version.

## Implications

- Reuse existing assessment confirmation authority and readiness evaluation.
- Introduce typed outcome/roadworthiness/salvage/VAT/engineer mappings at the Core boundary for the four approved variants.
- Keep raw economic inputs and Core-derived figures distinct; reject supplied derived totals.
- Map only accepted, current Engineer decisions into rendering; preserve who/when/version.
