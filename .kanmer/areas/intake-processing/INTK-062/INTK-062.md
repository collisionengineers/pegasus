---
id: INTK-062
type: ticket
title: Bound public upload bodies before multipart buffering
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels: []
groups:
  - EPIC-014
links:
  - INTK-055
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-09-07T20:01:20.291Z'
updated: '2026-09-07T20:01:20.291Z'
---

## What

Correct PR675 review finding 2: apply the configured public-link file/submission transport limit to anonymous request bodies before antiforgery and multipart model binding buffer them. Preserve supported multipart overhead, reject unknown-token and oversized requests early through the existing public upload route.

## Acceptance

Focused HTTP tests demonstrate early bounded rejection and successful supported uploads. No new request framework, domain data or broad infrastructure. User authorizes v1 remediation, merge and deployment.

## Outcome
