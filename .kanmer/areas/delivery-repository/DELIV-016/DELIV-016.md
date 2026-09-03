---
id: DELIV-016
type: ticket
title: 'Releases 17-20: ship the QDOS26009 operator fixes and repair case custody'
status: done
area: delivery-repository
order: 1050
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-08-22T05:50:10.977Z'
  implementing: '2026-08-22T05:50:13.115Z'
  review: '2026-08-22T05:54:34.706Z'
  verifying: '2026-08-22T05:54:38.780Z'
  done: '2026-08-22T05:54:43.067Z'
labels:
  - release
  - deployment
links: []
docs_todo: true
deployment: production
archived: false
created: '2026-08-22T05:01:55.880Z'
updated: '2026-09-03T09:06:47.637Z'
---

## Why

Four production releases in one run, none of which had a delivery ticket. This
is that ticket, filed retrospectively so the estate's release record is on the
board rather than only in `docs/operations.md`.

| Release | Source | Carried |
| --- | --- | --- |
| 17 | `71911734` | QDOS26008 regression remediation — MOT history read, intake latency, unlink projection, evidence banners, case-document registration |
| 18 | `1f3be493` | QDOS26009 operator fixes — Notes tab, one audit identity, completeness, vocabulary, MOT gap-fill, Origin label; audit custody coverage |
| 19 | `42125b34` | Custody failures name their own exception type; Web instrumented for Application Insights |
| 20 | (this one) | The Worker's missing grant on the case-document tables — [[DOCS-008]] |

## Route

Exact-SHA atomic fast-forward `dev` → `main`, per
`docs/engineering.md#branches-and-delivery`. Image pushed with `oras cp` from
the OCI archive (no Docker on the workstation), Web via `azd provision`, Worker
via `az functionapp deployment source config-zip` — never
`azd deploy worker --from-package`, which triggers an Oryx rebuild that
crash-loops the host. The full route is written up in the repository release
skill (`.agents/skills/pegasus-release`, `.codex/…`).

Release 20 is the first of the four with a **schema change**
(`20260822044425_GrantWorkerCaseDocuments`), so it also needs `efbundle` after
the promotion.

## Proof

The successful deploy, verified by me: `Invoke-ProductionSmoke.ps1` green
against the exact source revision, the migration applied and read back from
`sys.database_permissions`, and the current-state documents refreshed in the
same task.
