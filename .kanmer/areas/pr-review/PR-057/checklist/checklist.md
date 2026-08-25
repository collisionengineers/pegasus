# Checklist — PR-057

- [ ] Add accepted ADR-0031, carrying forward the retained Automation Actor/Send to AI boundaries while removing the separate EVA generate/status route.
- [ ] Mark ADR-0021 superseded, reconcile the ADR index, link ADR-0031 to PR-057 and clear `docs_todo`.
- [ ] Reconcile MCP-06 plus present-tense FRD/current-architecture/design/operations citations, preserving historical ADR-0021 references.
- [ ] Update active ADR citations in the four source/Razor comment locations without changing executable behavior.
- [ ] Run the scope/citation search, Markdown placement/link validators and focused Automation MCP inventory test; write the post-implementation report.

## Progress notes

(append with `set_ticket_doc(doc: "checklist", append: true)`)


2026-08-25: implementation and focused verification completed at c86b803c. Simplification lenses: reused existing lock and batch-read conventions; removed obsolete switches and catch/retry path; no new abstraction; no deferred code finding.
