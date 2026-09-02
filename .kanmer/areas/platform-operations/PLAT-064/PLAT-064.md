---
id: PLAT-064
type: ticket
title: Add administrator-initiated staff password reset
status: backlog
area: platform-operations
assignee: ''
profile: feature
labels: []
groups:
  - EPIC-011
links:
  - PLAT-027
refs:
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-12-operator-experience.md
deployment: not-deployed
archived: false
created: '2026-09-01T14:40:44.973Z'
updated: '2026-09-01T14:40:44.973Z'
---

## What

Let an Administrator set an entered temporary password for an existing staff account and require that user to change it at the next successful sign-in.

## Why

The prototype exposes Reset password, but production currently has only account creation and self-service/forced password change. The reset must reuse the current local identity policy rather than invent email delivery.

## Approach

- Reuse the existing password validation, hashing and `MustChangePassword` behavior.
- Require Administrator authorization, antiforgery, optimistic concurrency and an idempotent operation key.
- Never retain or redisplay the submitted temporary password; record the actor, target, time and outcome in permanent history.
- Do not send a password or reset link by email.

## Verification

- [ ] A valid reset invalidates the old password and forces a new password after the next sign-in.
- [ ] Weak, stale, replayed and unauthorized requests fail safely.
- [ ] Disabled-account behavior and existing attribution remain unchanged.
- [ ] History records the reset outcome without recording the password.

## Outcome
