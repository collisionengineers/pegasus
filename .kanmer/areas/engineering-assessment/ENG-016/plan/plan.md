# Plan — ENG-016: one Review readiness rule and one EVA Export

*Final target after the operator's clarification. This supersedes both the earlier permissive-export plan and the later accepted-only/custody plan.*

## Target behaviour

`Review` is the sole business state meaning a Case has complete instructions and images and is ready to send to an Engineer. Export verifies that state, treats the operator's press as confirmation of the populated values, creates the deterministic EVA package, records permanent action history, and records the once-per-Case first-sent proxy.

Box Case/Audit custody is independent storage state and is not a second Export readiness gate. Suggested status is not an Export blocker.

## Implementation

1. Merge current `origin/dev` normally into the isolated task worktree, preserving unrelated staged user files in the original checkout.
2. Reuse the existing Case completeness/lifecycle owner. Tighten `ValidateReviewReadiness` so incomplete instructions or images cannot be overridden by staff-review flags. Keep the existing save behaviour that invalidates completeness and moves the Case to `Not ready`, and show that consequence after save.
3. Keep one mapping, `MapForOperatorExport`. Preserve real provenance/status, permit populated suggestions, leave VAT and Mileage optional, require Mileage Unit only when Mileage exists, and default a missing Inspection Date to the Export date.
4. Make the surviving Export POST enforce `Review` server-side. Retain only technical image/package validation: an eligible retained image must have verified readable bytes, and mapping activation, authorization and replay must pass. Do not add a duplicate field/custody policy.
5. Record every successful export in existing `ActionHistory` with case version, mapping, values/provenance, archive hashes and image identities. Use the operation key for exact replay. Keep `EvaFirstHandoffProxies` as the once-per-Case dashboard fact.
6. Preserve deletion of the duplicate hand-off UI/routes/MCP/ports/tables and the direct pre-release migration permitted by ADR-0030. Add no rollback or compatibility path.
7. Reconcile operator notes, FRD-07, capabilities, current architecture, ticket report and PR description to the three-route model.
8. Run focused and canonical verification, the four simplification lenses, push normally, then require fresh green CI and independent review.

## Simplification pass — 2026-08-25

- Reuse: kept the existing Case lifecycle/completeness owner, EVA mapper, image selector, action-history helper and first-export proxy. No second business policy was added.
- Simplification: removed the old hand-off stack and avoided compatibility/rollback machinery for unreleased state.
- Efficiency: removed an extra `Cases` query because the existing `CaseDataProjection.Identity` already supplies the canonical case reference.
- Altitude: documentation now states business readiness in FRD-07 and implementation/storage facts in current architecture. No new abstraction or service was required.
- Disposition: all findings applied; no deferred finding.

## Acceptance

- Incomplete instructions or images cannot enter Review, even when staff-review flags are true.
- Blank VAT and blank Mileage do not block; Mileage without Unit does.
- Suggested populated values export; missing Inspection Date becomes the Export date.
- Direct Export outside Review fails server-side.
- Export has no duplicate Case/Audit custody or accepted-only status gate.
- One package route remains; every-export history, first proxy, exact replay, digest and package shape pass.
