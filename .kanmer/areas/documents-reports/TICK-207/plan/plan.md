# Plan — TICK-207: Define the missing Audit renderer template

## Diff estimate

No repository diff and no Audit template. The operator explicitly deferred Audit rendering until a representative Audit report/template is supplied or approved. Current evidence contains assessment templates only, so this ticket records and verifies the closed boundary rather than fabricating product behaviour.

## Approach

Close the historical “define the template” question as an evidence-gated deferral: RPT-03, Audit template registration, and every Audit render action remain unavailable and fail closed. `reference/rendererref1/` and the imported renderer cannot be stretched into Audit authority; no placeholder, generic-expert fallback, assessment clone, dormant descriptor, or feature-gated template is permitted. TICK-207 is a Kanmer-only reconciliation. When a concrete representative Audit artifact is supplied, a new linked activation ticket must research and obtain explicit approval for its wording, layout, field/conditional rules, labels, signatures, and representative cases before FRD/Core/template work begins.

## Governing docs

- **Meets FRD-11:** preserves accepted-evidence, deterministic template/payload identity, human review, immutable artifact identity/hash, fail-closed inputs, and correction/addendum rules. Because FRD-11 has no approved Audit-specific wording/layout contract, this ticket keeps Audit unavailable and does not modify the FRD.
- **Meets ADR-0025:** leaves future Audit rendering inside the integrated Core-port/Infrastructure-adapter boundary. It adds no renderer template, generic authoring loophole, workspace activation, package, API, MCP host, service, job, or deployment unit.

No governing document is modified. The future evidence-triggered activation ticket will modify FRD-11 first only after operator approval of an actual representative artifact.

## Steps

1. Reconcile TICK-207's Outcome and acceptance statements to the explicit deferral: no approved representative Audit template exists; Audit rendering is unavailable/fail-closed; assessment evidence cannot supply Audit wording.
2. Record the forbidden substitutes: no assessment clone, generic expert template, caller-authored blocks, placeholder/dormant descriptor, inferred legal wording, or fabricated reference artifact.
3. Record downstream ownership and activation conditions: TICK-205 supplies the dual-specification data decision; TICK-098 remains the RPT-03 owner; a future linked activation ticket starts only when concrete representative evidence is supplied and explicitly approved.
4. Write a zero-diff post-implementation report tying the deferral to FRD-11 and ADR-0025 and confirming SIMPLI-014 remains assessment/fee-note only.
5. Verify the operator resolution, absence of Audit evidence/model/template/registration, explicit parked questions, downstream unavailable state, and empty repository diff; capture proof only at the deferral/closed-boundary tier.

## Verification

The post-implementation report and later proof will record:

- the resolved TICK-207 open question and parked activation question;
- `rg --files reference/rendererref1 workspaces/report-renderer | sort` plus focused searches for `Audit|conservative|maximised|uplift`, confirming rendererref1 is assessment-only and no accepted Audit contract exists;
- direct inspection of rendererref1 schema showing one assessment outcome/worklist rather than the required Audit comparison pair;
- SIMPLI-014 plan/diff inspection confirming its activation surface is assessment + fee note and Audit stays unavailable;
- TICK-098/TICK-205 evidence confirming the accepted data direction is necessary but insufficient without presentation approval;
- `git diff --stat origin/dev...HEAD` on the TICK-207 branch, expected empty;
- evidence that no FRD, Core, Infrastructure, template, reference, artifact, Azure, Worker, or `main` change occurred.

This proves the deferral and fail-closed boundary only. It does not define or deliver an Audit report.

## Risks / open questions

- Risk: “planned” is read as authority to create a template. Mitigation: every step is zero-diff and explicitly prohibits implementation until supplied/approved evidence exists.
- Risk: a disabled placeholder is treated as shipped capability. Mitigation: no dormant template/descriptor/flag is created; Audit remains absent and unavailable.
- No operator question is currently actionable. The future question is evidence-triggered: approve or reject the actual supplied representative artifact and its detailed rules.
- Next step now: execute this Kanmer-only deferral reconciliation and close it through independent review/verification. Next product step: wait for the representative Audit artifact, then create a new linked activation ticket to research/approve it before any template work.

## Operator correction — shared Audit/Inspection physical report — 2026-08-19

This supersedes any earlier plan statement that Audit rendering requires a separate representative template, layout, wording artifact, dormant family, or future activation ticket. The operator confirmed that Audit and Inspection processes differ internally, but the physical report output has no differences. Reuse the approved inspection/assessment report template and presentation through the existing Core render contract. Preserve Audit-specific workflow/data rules in their owning Core capabilities; do not create a second renderer template or presentation policy.
