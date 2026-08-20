# Research — PR-028

## Question

What must the MAIL-004 PIR inventory and verification section contain?

## Findings

- `git diff --name-only origin/dev...480f19fe` contains exactly 23 files: 3 governing docs, 1 bootstrap script, 2 Core files, 6 Infrastructure files plus migration/designer/snapshot, 3 Web files, and 6 test files.
- The existing PIR groups those files and does not enumerate each path.
- PR-026/027 will add `AzureSqlRuntimeRoleMigrationTests.cs` and may change the final file count, so the definitive inventory must be generated from final reviewed-head diff after fixes.
- Verification claims must name exact filters/results and distinguish local/test evidence from deployment and live Outlook evidence.

## Implication

Replace MAIL-004's grouped inventory after all fixes with one row per final diff path and precise command evidence.
