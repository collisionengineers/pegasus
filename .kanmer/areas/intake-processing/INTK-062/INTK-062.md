---
id: INTK-062
type: ticket
title: Bound public upload bodies before multipart buffering
status: done
area: intake-processing
assignee: principal_delivery_audit
profile: fix
stageEntered:
  preparing: '2026-09-07T21:24:37.487Z'
  review: '2026-09-07T22:44:35.228Z'
  verifying: '2026-09-07T22:49:48.666Z'
  done: '2026-09-07T23:16:52.276Z'
labels: []
groups:
  - EPIC-014
links:
  - INTK-055
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - 7c8f6e50e7bf1297307dbd5a7dd2c1940a2f78f1
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/684'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: 7c8f6e50e7bf1297307dbd5a7dd2c1940a2f78f1
delivery_recorded_at: '2026-09-07T23:16:52.913Z'
archived: false
created: '2026-09-07T20:01:20.291Z'
updated: '2026-09-07T23:19:30.354Z'
---

## What

Correct PR675 review finding 2: apply the configured public-link file/submission transport limit to anonymous request bodies before antiforgery and multipart model binding buffer them. Preserve supported multipart overhead, reject unknown-token and oversized requests early through the existing public upload route.

## Acceptance

Focused HTTP tests demonstrate early bounded rejection and successful supported uploads. No new request framework, domain data or broad infrastructure. User authorizes v1 remediation, merge and deployment.

## Outcome

PR684 merged into dev at 7c8f6e50e7bf1297307dbd5a7dd2c1940a2f78f1 on 7 September 2026. Root exact merged restore/build and all12 focused transport/custody checks passed; final whole proof c7ea9e379933dea6. No deployment or live upload claim. No additional implementation follow-up is required; integrated release remains the remediation controller's responsibility.
