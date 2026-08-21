# Checklist — MAIL-007

- [x] `TrimProviderFooter` in `StaffForwardBodyCleaner` (fail-open rules)
- [x] Wired into `MailBodyPresentation.Present` + the store excerpt
- [x] Cleaner facts over real corpus shapes (10/10: trim keeps sign-off, signature-only unchanged, markerless unchanged)
- [x] Mail + intake suites green (Core 872/872; integration 69/69 incl. extraction coverage — classification untouched by construction and the coverage suite confirms)
- [x] Simplification pass: one new function + one regex beside the existing owners; no second cleaner; both display call sites share it. Nothing to trim from the diff.
- [ ] PR to dev (after DOCS-006's PR #499 merges); merge on green CI

## Progress notes

2026-08-21: committed 721cbcbc on the branch; validation fully green.
