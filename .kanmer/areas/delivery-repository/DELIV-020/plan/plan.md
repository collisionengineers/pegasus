# Plan

1. Update `docs/current-architecture.md` and `docs/operations.md` using the release 30 Azure read-back and smoke evidence already collected. Reuse the existing release-table and production-snapshot conventions; add no new documentation structure.
2. Review the documentation-only diff for accuracy and scope, commit it on `task/release-30-docs`, open a PR to `dev`, and merge after green CI.

Acceptance: both required living snapshots identify release 30, exact source SHA/image digest/revision, migration head, Worker activation, smoke result, and the selective reset boundary. Simplification pass: n/a — docs-only.
