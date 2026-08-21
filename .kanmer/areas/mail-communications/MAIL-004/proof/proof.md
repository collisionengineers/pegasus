# Proof — MAIL-004 (verified on deployed release 16, 2026-08-21)

Type: visual + test-output. Deployment evidence bundle: [[DELIV-015]] proof.

- Deployed at release 16 (`4111ad29`, PR #473 squash `4d00c3b7`); migration `20260820114412_ApprovedOutlookCategoryCatalogue` applied to production, and the live grant readback shows web SELECT/INSERT/UPDATE + DELETE-denied on `ApprovedOutlookCategories` with **no Worker grant** — the least-privilege shape the ticket specified.
- Live production render: `/Administration/MailCategories` serves the standard Administration pattern (back link, eyebrow, CURRENT CATEGORIES empty state, ADD AN APPROVED CATEGORY labelled form with Display name / Active state / required Reason) with no Graph identifiers anywhere. Populating the catalogue is the operator's configuration act.
- PR-026's visual gate: local rendered desktop/200%-zoom inspection performed and recorded on this ticket's scratch (2026-08-21) — add flow produced the saved-entry card and status notice, required-Reason validation fired, no horizontal overflow at 1280 px or 512 px, axe-clean; the design-authority record was closed in-branch (f9876cfe).
- Focused evidence (PR-027's expansion) merged with the branch: Core catalogue policy tests, persistence conflict/replay/history tests, authenticated Web admin tests, and the role-matrix migration tests — green in the merge CI.
- MAIL-13 consumption remains a separate undelivered capability (its own gates); this ticket delivered exactly the catalogue and the Core Active-name resolver seam.
