# Changes

- Replaced the count-all AppExceptions query with normalized signature and operation-aware branches.
- Alerts immediately for exceptions correlated to failed requests in five minutes.
- Alerts for a signature across three distinct correlated operations or three operationless minute buckets in 15 minutes.
- Preserved Sev1, evaluation frequency, threshold, auto-mitigation, and the existing action group.
- Added an architecture contract test and compiled Bicep successfully.

# Governing docs

FRD-12/OPS-08 remains actionable while recovered-success noise is removed. ADR-0002's Azure Monitor/action-group boundary is unchanged.

# Verification

Bicep compilation passed, focused contract test passed, and Release build passed. Concurrent full-suite execution produced unrelated shared-resource timeouts.

# Risks and follow-ups

Exact live KQL schema/replay verification and alert deployment remain approval-gated.

# Verify on merged main

Compile Bicep, run the alert contract/full architecture tests, and perform read-only historical KQL comparison before deployment approval.
