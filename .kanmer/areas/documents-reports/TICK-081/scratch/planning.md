## 2026-08-20 — plan replacement

Replaced the stale zero-diff plan/checklist from the completed research. The new plan:
- records [[TICK-082]] as a structured blocker;
- assigns the single shared-caller/durable implementation to [[DOCS-001]] so no overlapping report branch is created;
- requires one Core service and one Infrastructure adapter for every activated generated document type;
- requires Audit and Inspection to enter that same service and share physical presentation while preserving reference provenance;
- requires a thin next-free shared-caller ADR plus FRD-11 behavior before code;
- defines exact local verification, independent review, exact-SHA main/deployment controls, and merged/deployed proof.

ADR-0028 was added to TICK-081's governing refs. TICK-081 remains Preparing and untaken; no application code, branch, worktree, release, or cloud state was changed.
