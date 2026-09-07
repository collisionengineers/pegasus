---
id: CASE-048
type: ticket
title: Estimate version compare view on the Case Estimate section
status: backlog
area: case-reference-workflow
assignee: ''
profile: capture
labels:
  - pegasus-v1
  - stream-b
  - deferred
links:
  - CASE-047
capture_evidence:
  - 'https://github.com/collisionengineers/pegasus/pull/674'
  - >-
    https://github.com/collisionengineers/pegasus/commit/3da60bd0c270111d5168dc17246dc831882108ea
  - CASE-047 plan/b08-estimate-controls-report
capture_actor: claude-code
capture_disposition: already-fixed
capture_result: CASE-047
capture_decided_at: '2026-09-07T14:30:41.447Z'
capture_decided_by: codex-mcp-client
archived: true
created: '2026-09-07T05:33:08.813Z'
updated: '2026-09-07T14:30:41.447Z'
---

Deferred from [[CASE-047]] (Stream B, B04). The v1 pack's B04 brief lists "duplicate, compare, immutable versions" for repair estimates. v1 delivers immutable versions on their own tabs with Duplicate, Discard and Use as Current on the Case Estimate section; no side-by-side compare exists. A compare view needs a design decision first (which versions, which lines and totals, printed vs raw values) in `docs/design/README.md`; none is recorded there today, and the B09 fresh review (CASE-047 `plan/b09-review-record` item 7) found the omission unrecorded. Nothing in v1 depends on it: the accepted-estimate successor rule and the reconciliation evidence stay in Core.
