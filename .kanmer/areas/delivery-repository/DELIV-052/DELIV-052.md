---
id: DELIV-052
type: ticket
title: Align the Kanmer verification receipt contract with Pegasus CI
status: backlog
area: delivery-repository
assignee: ''
profile: capture
labels: []
links:
  - DELIV-051
capture_evidence:
  - .github/workflows/ci.yml
  - get_status.delivery.verification
capture_actor: codex-mcp-client
archived: false
created: '2026-09-08T08:23:24.949Z'
updated: '2026-09-08T08:23:24.949Z'
---

Observed during [[DELIV-051]]: get_status resolves the default contract pr.yml / verify / push, while Pegasus uses .github/workflows/ci.yml (repository-check) and only main push plus pull_request triggers. No exact dev-merge receipt is expected from that default. Reconciled Kanmer 0.4.2 correctly falls back to missing-obligation verification. Determine the intended receipt contract in a separate scoped task; do not alter CI or board configuration as part of the instruction amendment.
