---
id: PLAT-037
type: ticket
title: Switch on the accepted EVA mapping in production
status: implementing
area: platform-operations
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-22T19:43:53.566Z'
taken_at: '2026-08-22T19:49:20.437Z'
branch: task/qdos26011-regressions
worktree: ../pegasus-worktrees/qdos26011-regressions
labels:
  - qdos26011
  - config
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-22T19:42:25.382Z'
updated: '2026-08-22T19:49:20.437Z'
---

## Why

`CaseEvaMapping.IsSwitchedOn` requires an accepted mapping key, version and evidence reference. `Pegasus.Web` reads them from `Eva:AcceptedMapping:Key`, `:Version` and `:EvidenceReference`. Verified 2026-08-22: the live Container App `pegasus-prod-web-252ow37gij` declares no `Eva*` environment variable at all, so `EvaBundleSchema.ValidateSource` throws before it looks at any case data.

Operator direction, 2026-08-22: *"Yes its supposed to be switched on."*

## Scope

Set on the production Web Container App, as part of the release that carries [[CASE-019]]:

| Setting | Value |
| --- | --- |
| `Eva__AcceptedMapping__Key` | `qdos-eva-13-field-mapping` |
| `Eva__AcceptedMapping__Version` | `1` |
| `Eva__AcceptedMapping__EvidenceReference` | the FRD that defines the mapping |

The values are not secrets and carry no credential, so they are plain environment variables rather than Key Vault references.

This switches on the **mapping**, which is what the bundle writer validates. It does not switch on external EVA delivery: no adapter is configured and no hand-off proxy is written.

## How to verify

After the release, a case export on QDOS26011 succeeds and its `provenance.json` records `mapping.key qdos-eva-13-field-mapping`, `mapping.version 1` and the evidence reference.
