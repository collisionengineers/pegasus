# Checklist — UIIMP-001

- [ ] Confirm [[UIIMP-002]] provides `docs/design/test-ui/index.html`.
- [ ] Add the validated `UiMode` parameter and reject unsupported action/control combinations.
- [ ] Preserve the existing Live startup path and add dependency-free Windows/Linux Test catalogue opening before lifecycle initialization.
- [ ] Add `scripts/Test-UiModes.ps1` coverage for defaults, validation, path resolution, opener selection, and no Live initialization in Test mode.
- [ ] Update `README.md` and `docs/runbook.md` with the two-mode contract.
- [ ] Run focused script/parser checks and canonical restore/build/tests.
- [ ] Inspect Release publish output and prove Test UI is absent.
- [ ] Record verification results for the post-implementation report and proof.

## Progress notes
