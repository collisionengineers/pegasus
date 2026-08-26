# Plan

1. Update the existing current-architecture topology verification sentence from release 31 to release 32.
2. Add release 32 to the existing operations release table and evidence list using the exact deployed SHA, digest, revision, unchanged migration head, and observed strict smoke results.
3. Verify the docs diff, record docs-only simplification, commit, review, merge to dev, and promote the exact documentation SHA so both branches remain aligned.

## Simplification pass — 2026-08-26

n/a — docs-only. The change edits the two existing canonical current-state documents and adds no new document or duplicate release record.

## Review disposition — 2026-08-26

Applied both findings: release 32 now records the full exact SHA, image digest and manifest hash, and current architecture now states immediate publication by the committing caller with the one-minute Worker timer limited to interrupted-work recovery.
