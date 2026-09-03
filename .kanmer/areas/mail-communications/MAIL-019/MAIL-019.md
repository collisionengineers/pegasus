---
id: MAIL-019
type: ticket
title: >-
  Post-release smoke asserts inbox intake liveness (active subscription, recent
  poll)
status: done
area: mail-communications
order: 2470
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-27T14:31:18.634Z'
  review: '2026-08-27T14:45:22.633Z'
  verifying: '2026-08-27T16:55:52.536Z'
  done: '2026-08-27T17:03:21.284Z'
labels:
  - release
  - smoke
groups:
  - EPIC-010
links: []
commits:
  - a7b44e327b7e7780874b9c0250c1fad5145f424c
  - be507fafe0de46ce54b23b25ac317d821558f330
prs:
  - '#573'
archived: false
created: '2026-08-27T10:06:22.829Z'
updated: '2026-09-03T09:06:56.831Z'
---

## Problem

Release 33/34 notes state the recovery timer was not proven; neither release detected that no Graph subscription existed and no poll ran.

## Required outcome

The release smoke reads (read-only) one `Active` row in `ApprovedMailboxSubscriptions` and an `ApprovedInboxPollStates.LastCompletedAtUtc` newer than the recovery interval, and fails the release verification otherwise.

## Outcome

Shipped as planned in PR #573 (https://github.com/collisionengineers/pegasus/pull/573),
merged into `dev` 2026-08-27 at be507fafe0de46ce54b23b25ac317d821558f330:
`scripts/Invoke-ProductionSmoke.ps1` gained the read-only inbox intake
liveness gate, named in `docs/runbook.md` and the release skill. Proof PASS
at the merge SHA, including a live read-only run against production. No
follow-up tickets; reaches `main` with the next release promotion.
