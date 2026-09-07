---
id: MAIL-036
type: ticket
title: Preserve the wipe-time email cutoff across Graph replay
status: done
area: mail-communications
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-07T20:18:52.479Z'
  review: '2026-09-07T20:44:41.973Z'
  verifying: '2026-09-07T20:50:44.730Z'
  done: '2026-09-07T21:05:06.369Z'
labels: []
groups:
  - EPIC-014
links:
  - MAIL-031
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 1d972f05c0f10c2ecf804f271a4fd3155242f1ef
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/678'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: 1d972f05c0f10c2ecf804f271a4fd3155242f1ef
delivery_recorded_at: '2026-09-07T21:05:53.241Z'
archived: false
created: '2026-09-07T20:01:20.357Z'
updated: '2026-09-07T21:07:03.670Z'
---

## What

Preserve an explicit processing cutoff when Pegasus intake/mail test data is wiped. Old emails must stay excluded when delta tokens reset, queued notifications arrive late or retained identities have been removed. Preserve mailbox onboarding/configuration and reuse its start boundary where appropriate; one effective oldest admissible receive time applies to both polling and direct notification intake.

## Acceptance

Existing wipe workflow records a current cutoff; focused old/new-message tests after wipe and delta reset show old mail excluded and newly received/forwarded mail accepted. No live wipe is implied by implementing this change. The operator's current request supersedes old replay-from-onboarding behavior.

## Outcome

PR [678](https://github.com/collisionengineers/pegasus/pull/678) integrated on dev at 1d972f05c0f10c2ecf804f271a4fd3155242f1ef. Independent review and exact-merge focused proof PASS. Notifications now process only the named message; future wipes preserve a monotonic cutoff. No live wipe or deployment performed. [[EPIC-014]] retains actual webhook delivery and deployed lifecycle acceptance.
