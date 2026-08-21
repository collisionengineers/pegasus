- [x] Replace count-all exception KQL with normalized operation-aware logic.
- [x] Configure the 15-minute window and five-minute failed-request branch.
- [x] Add distinct-operation and operationless minute-bucket persistence branches.
- [x] Preserve Sev1 severity and the existing action group.
- [x] Add infrastructure assertions for the alert contract.
- [x] Compile Bicep and run focused, Release, full non-corpus tests and simplification pass.
- [ ] Record the post-implementation report and open the reviewed PR to dev.
- [ ] After exact approval, deploy the alert rule and run historical/live read-only verification.
- [ ] Refresh operations documentation and record merged-main proof.

## Progress notes

2026-08-21: Bicep compilation, focused alert-contract test, and Release build passed. Simplification review caught and corrected an initially mis-targeted Web 5xx window edit. Concurrent full-suite execution produced unrelated shared-resource timeouts; no alert-path failure occurred.
