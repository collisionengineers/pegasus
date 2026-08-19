# Checklist — DOCS-002

- [x] Add thin ADR-0028 selecting the existing Web Container App and rejecting separate/Worker execution.
- [x] Update the ADR index and validate frontmatter, stable id, and links.
- [ ] Link the ADR to owning tickets; record docs-only simplification and verification evidence.

## Progress notes

- 2026-08-19: Added ADR-0028 and its accepted index row. `git diff --check` passed; documentation links resolved across 223 Markdown files. Simplification pass: n/a — docs-only; the diff contains one technical decision and one derived index row, with behaviour, implementation, sizing, deployment, and cloud changes left to their owning documents/tickets.
