# Plan — ENG-018

1. Remove `EvaMappingAcceptance`, activation checking, and its operator-facing hand-off error from the one Core mapping owner. Keep the mapping key/version as descriptive export-history metadata.
2. Remove the acceptance dependency from Infrastructure and Web composition and delete the three unused Bicep environment values. Add no fallback, feature flag, migration, or compatibility path.
3. Update affected tests and add a regression proving a Review case can reach bundle creation without EVA activation configuration. Preserve lifecycle, image-byte, authorization, replay, action-history, and first-send-proxy validation.
4. Reconcile FRD-07 and current-state docs with the single Export action.
5. Run focused tests, canonical Release build/test, inspect the diff through reuse/simplification/efficiency/altitude lenses, then open the PR for independent review.

## Simplification pass — 2026-08-25

- Reuse: retained the existing Review lifecycle check, mapper, bundle schema, image loader, action-history writer and first-send proxy.
- Simplification: deleted the activation record/check, configuration plumbing, Azure settings, nullable mapper output and always-empty mapper blocker list. No replacement abstraction was introduced.
- Efficiency: export now proceeds directly from reviewed case evidence to mapping and image loading, with no configuration lookup or duplicate readiness branch.
- Altitude: operator-visible behavior remains one Export action; internal `EvaHandoffStore` and proxy names remain because renaming storage concepts provides no behavior and would widen the change.
- Disposition: all in-scope findings applied; no deferred finding.
