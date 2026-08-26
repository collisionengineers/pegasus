---
id: TICK-199
type: ticket
title: Retire .infisical.json or document its active owner
status: done
area: delivery-repository
order: 1900
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-20T03:58:44.652Z'
  review: '2026-08-20T03:59:49.882Z'
  verifying: '2026-08-20T04:03:53.658Z'
  done: '2026-08-20T12:48:40.858Z'
labels:
  - now
  - source-now
links: []
docs_todo: true
commits:
  - 2d5bc5ad
prs:
  - '442'
deployment: n/a
archived: false
created: '2026-08-12T15:08:04.949Z'
updated: '2026-08-26T14:34:46.219Z'
---

## What

Resolve `.infisical.json` to one explicit repository state: remove it if it has no supported consumer, or document its active owner, caller, configuration contract, and verification route if it remains required.

## Why

An unexplained credential-tool configuration file can become stale authority or be removed while still supporting a real workflow. The repository needs one evidenced answer rather than a perpetual verification-only ticket.

## Approach

- Trace every repository, CI, runbook, deployment, and local-tool reference to `.infisical.json`.
- Identify the actual executable consumer and supported workflow, if any.
- If unused, remove the file and stale references in the owning task; if used, document its owner and exact verification command in the canonical documentation.
- Do not read, rotate, or mutate credentials or external Infisical state without separate exact-target approval.

## Verification

- [ ] No supported caller is left undocumented or broken.
- [ ] The file is either absent with stale references removed, or retained with an explicit owner and exercised local verification route.
- [ ] No secret value is printed, copied, or committed.

## Notes

- Source: the retired pre-Kanmer tracker repository-hygiene item.


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.
