# Checklist — PR-057

- [x] Add accepted ADR-0031, carrying forward the retained Automation Actor/Send to AI boundaries while removing the separate EVA generate/status route.
- [ ] Mark ADR-0021 superseded, reconcile the ADR index, link ADR-0031 to PR-057 and clear `docs_todo`. (Implementation/index/docs_todo complete; ADR link awaits board repoRoot visibility.)
- [x] Reconcile MCP-06 plus present-tense FRD/current-architecture/design/operations citations, preserving historical ADR-0021 references.
- [x] Update active ADR citations in the four source/Razor comment locations without changing executable behavior.
- [x] Run the scope/citation search, Markdown placement/link validators and focused Automation MCP inventory test; write the post-implementation report.

## Progress notes

(append with `set_ticket_doc(doc: "checklist", append: true)`)


2026-08-25: implementation and focused verification completed at c86b803c. Simplification lenses: reused existing lock and batch-read conventions; removed obsolete switches and catch/retry path; no new abstraction; no deferred code finding.

2026-08-25 merged-state note: ADR-0031 is present in PR #539 merge commit `d973ead358f75736bdbdec3aa123d7d88a0083bd`, and the merged documentation/link checks are green. The checkbox remains open only because Kanmer validates refs against `C:\Users\PC\Documents\GitHub\pegasus`, whose checked-out tree does not yet contain merged `dev`; `link_doc` therefore still reports the path absent.
