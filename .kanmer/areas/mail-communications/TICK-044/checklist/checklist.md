# Checklist — MAIL-02

- [x] Revalidate prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [x] Author and review FRD-08's exhaustive classification criteria/method/evidence/destination/folder catalogue before policy code.
- [x] Implement the minimal Core contract/policy with fail-closed validation.
- [x] Keep the destination deterministic rather than persisting duplicate state; record downstream UI-14/MAIL-23 callers.
- [x] Preserve one Core business-rule owner for later retained-mail and Automation callers.
- [x] Add focused acceptance tests for every taxonomy member, reasoned Other, Ambiguous/Unclassified, and Triage routing.
- [x] Run `dotnet restore` and `dotnet build --configuration Release`.
- [x] Run focused tests and the relevant full Core suite.
- [x] Run and record the four-lens simplification pass.
- [x] Update governing/current-state documentation only to the evidence tier actually reached.
- [x] Write the post-implementation report with commands, results, residual risks and deployment qualification.
