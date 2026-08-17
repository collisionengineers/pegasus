# Checklist — TICK-194

- [x] Create the isolated task worktree, claim the ticket, and add the root task plan.
- [x] Implement the explicit before/head main-history validation script with fail-closed diagnostics.
- [x] Invoke the guard for pushes to `main` from the existing full-history `changes` job.
- [x] Add synthetic Git-history architecture tests for allowed merge-only and rejected direct, mixed, missing, zero, and rewritten histories.
- [ ] Run focused tests, Release build, applicable repository checks, and confirm the diff excludes UI/design paths.
- [ ] Write the implementation report, commit, push, open the `dev` PR, record traceability, and move the ticket to Review.

## Progress notes

- Implemented the guard without touching `docs/engineering.md` or any UI-revamp-owned path.
- Initial focused compile exposed CA1707 test-name violations; renamed tests to repository-compliant PascalCase.
- Initial test execution exposed PowerShell scalar unwrapping and Windows read-only Git object cleanup; array-wrapped Git output and normalized temporary-file attributes.
- Focused guard suite now passes: 6/6.
