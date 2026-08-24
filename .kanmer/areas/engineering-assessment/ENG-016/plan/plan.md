# Plan — ENG-016: one Review readiness rule and one EVA Export

*Rewritten after the operator's 2026-08-24 clarification. This supersedes both the permissive-export plan and the later accepted-only/custody plan.*

## Target behaviour

`Review` is the sole business state meaning a Case is ready to send to an Engineer. The transition into Review owns completeness. Export verifies Review, treats the operator's press as confirmation of all populated values, creates the deterministic EVA package, records permanent action history, and records the once-per-Case first-sent proxy.

Box Case/Audit custody is independent storage state and is not an Export readiness gate. Suggested status is not an Export blocker.

## Readiness policy

One Core policy controls entry to and continued membership in Review. It requires:

- Work Provider
- Claimant Name
- Claim Number / external Reference
- VRM
- Vehicle Model
- Accident Circumstances
- Incident Date
- Instruction Date
- Inspection Address or Image-based Assessment
- at least one eligible Case-vehicle image

Rules:

- Inspection Date is required in the EVA output; if absent, Export uses the date Export is pressed, per operator specification.
- Instruction Date retains its existing specified current-date default.
- VAT Status is optional.
- Mileage is optional.
- Mileage Unit is required exactly when Mileage is present.
- Any populated suggested value is usable; pressing Export confirms the values sent.
- If an edit removes a required detail or all eligible images from a Review Case, save the edit, automatically move the Case to Not ready, and show the existing Case status notification naming the missing requirements.

## Implementation

1. Merge current `origin/dev` normally into the task branch and resolve only ENG-016 conflicts; preserve unrelated staged user files and newer dev work.
2. Extend the existing Core Case completeness policy to evaluate the concrete required Case values and eligible-image condition. Use it for initial Review allocation, transitions/returns to Review, and edits to a Review Case.
3. On an invalidating edit, persist the new data and transition to Not ready in the same business operation/history boundary. Return a typed outcome containing the missing requirement labels; Web renders it through the existing `CaseStatus` TempData/status-card convention.
4. Collapse `MapForProduction` and `MapForOperatorExport` into one mapping. It preserves real provenance/status, permits populated suggestions, emits optional blanks, applies the Inspection Date default, and enforces only conditional Mileage/Mileage Unit consistency.
5. The surviving Export POST enforces lifecycle state Review server-side, then performs only technical package validation: authorization, current Case/version/replay integrity, readable supported images belonging to the Case, deterministic JSON/image bytes, and accepted mapping activation. It does not repeat completeness or custody policy.
6. Preserve deletion of duplicate hand-off UI/routes/MCP/ports/tables. Keep one Export POST, antiforgery, `Content-Digest`, per-export `ActionHistory`, and the once-per-Case `EvaFirstHandoffProxies` record.
7. Keep the pre-cutover destructive migration simple under ADR-0030: direct dead-schema removal, roll-forward recovery, no compatibility/rollback machinery.
8. Reconcile operator notes, FRD-07, capabilities, current architecture, ticket report, and PR description to the single Review/readiness and three-route model.
9. Run focused and full verification, simplification lenses, commit only ticket files, push normally, and require fresh green CI plus independent review.

## Acceptance

- No Case can enter or remain in Review without the required details and an eligible image.
- Invalidating a Review Case moves it to Not ready and visibly names the gap.
- Blank VAT and blank Mileage do not block; Mileage without Unit does.
- Suggested populated values export.
- Missing Inspection Date becomes the Export date.
- Direct Export outside Review fails server-side.
- Review Export is not blocked by Case/Audit custody or suggested status.
- Exactly one package route remains; first proxy, every-export history, replay, digest and package-shape semantics pass.
