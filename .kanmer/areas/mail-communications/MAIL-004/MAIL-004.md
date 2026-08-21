---
id: MAIL-004
type: ticket
title: Configure the approved Outlook category catalogue in email administration
status: done
area: mail-communications
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-20T09:52:23.990Z'
  review: '2026-08-20T12:06:42.628Z'
  verifying: '2026-08-21T14:17:10.016Z'
  done: '2026-08-21T14:53:13.619Z'
taken_at: '2026-08-20T11:39:48.912Z'
branch: task/mail-004-outlook-category-catalogue
worktree: ../pegasus-worktrees/mail-004
labels:
  - mail-workspace
  - administration
  - outlook-categories
  - operator-requested
groups:
  - EPIC-003
  - EPIC-006
links:
  - TICK-054
  - MAIL-002
blocks:
  - TICK-054
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-12-operator-experience.md
  - docs/capabilities.md
  - docs/design/README.md
commits:
  - ec8bb958
  - 480f19fe
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/473'
deployment: production
archived: false
created: '2026-08-20T09:47:32.793Z'
updated: '2026-08-21T14:53:13.619Z'
---

## What

Let authorised staff configure the approved Outlook category catalogue from Pegasus email administration. MAIL-13 consumes this catalogue for exact-message category assignment instead of accepting arbitrary category strings.

## Why

The approved category names are not yet settled and no canonical configuration owner exists. Categories may also support retained-email search and Case-linking workflows, but those callers must be proven rather than assumed.

## Approach

- Research the smallest administrator-owned category catalogue and its synchronization/validation boundary with the approved mailbox.
- Prove concrete callers for MAIL-13 and, if supported by governing behaviour, email search/linking; do not build speculative search metadata or a generic mailbox-rule editor.
- If research cannot establish a real use case beyond unsupported speculation, record that evidence and close/archive this ticket without dormant code.

## Verification

- [ ] Administration can list and maintain the approved category catalogue without exposing Graph identifiers.
- [ ] MAIL-13 accepts only configured approved categories and preserves unrelated message categories.
- [ ] Search/linking integration is implemented only when a concrete caller and accepted behavior are proven.

## Outcome
