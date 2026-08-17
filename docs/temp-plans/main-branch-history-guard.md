# Main branch history guard

Kanmer ticket: `TICK-194`

## Scope

Detect, in the existing `repository-check` workflow, any push to `main` whose
new first-parent history contains a non-merge commit. This is a post-push
detection control; GitHub branch-protection configuration and recovery are out
of scope.

## Owned files

- `.github/workflows/ci.yml`
- `scripts/Test-MainBranchHistory.ps1`
- `tests/Pegasus.ArchitectureTests/MainBranchHistoryGuardTests.cs`
- `docs/temp-plans/main-branch-history-guard.md`

The task must not change `src/Pegasus.Web/**`, UI browser/snapshot tests,
`design/**`, `.stitch/**`, or the documentation cleanup owned by `KANMER-002`.

## Implementation

1. Validate explicit before/head commits, rejecting zero or unavailable
   revisions and non-ancestor history.
2. Walk the new first-parent segment and require exactly two parents for every
   mainline commit.
3. Run the validator immediately after the full-history checkout for `main`
   push events.
4. Exercise allowed and rejected histories in temporary Git repositories.

## Acceptance and verification

- A merge-only append exits successfully.
- Direct, mixed, missing, zero-before, and rewritten histories fail with stable
  diagnostics.
- Focused architecture tests and the Release build pass.
- The final diff contains no UI-revamp-owned path.
