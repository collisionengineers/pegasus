# Task plan: delete-traceability-matrix

## Goal

Delete `design/product/traceability-matrix.md` outright (operator decision
2026-08-03). The matrix restated rules whose owners already exist: the
capability inventory owns per-ID canonical owner and activation/boundary,
design/README.md owns the UI principles, route hierarchy, and the
deferred/not-planned absence rules, and requirements.md owns role and scope
prose. Nothing replaces it — no new table is added anywhere.

## Changes

- Delete `design/product/traceability-matrix.md`.
- Repoint or remove its inbound links:
  - `design/README.md` "durable product-design owners" list (drops the
    matrix entry);
  - `design/README.md` deferred-allocation sentences (two) that cited the
    matrix as the ID-by-ID mirror — the capability inventory is named as the
    sole allocation owner instead;
  - `design/product/requirements.md` traceability sentence — same
    replacement;
  - `design/product/ui-spec.md` if it links the matrix.

## Verification

- `scripts/Test-DocumentationLinks.ps1` passes (no dangling links to the
  deleted file).
- The PR is Markdown-only, so `repository-check` must run only `changes` +
  the documentation link check with `qdos-pressure` skipped — this is the
  live proof of the docs-only CI path.
