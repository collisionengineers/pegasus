# Plan — ENG-018

1. Remove `EvaMappingAcceptance`, activation checking, and its operator-facing hand-off error from the one Core mapping owner. Keep the mapping key/version as descriptive export-history metadata.
2. Remove the acceptance dependency from Infrastructure and Web composition and delete the three unused Bicep environment values. Add no fallback, feature flag, migration, or compatibility path.
3. Update affected tests and add a regression proving a Review case can reach bundle creation without EVA activation configuration. Preserve lifecycle, image-byte, authorization, replay, action-history, and first-send-proxy validation.
4. Reconcile FRD-07 and current-state docs with the single Export action.
5. Run focused tests, canonical Release build/test, inspect the diff through reuse/simplification/efficiency/altitude lenses, then open the PR for independent review.
