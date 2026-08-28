---
id: PLAT-055
type: ticket
title: Restore the EVA client secret from Infisical after duplicated Key Vault value
status: review
area: platform-operations
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-08-28T14:03:00.859Z'
  review: '2026-08-28T14:22:45.644Z'
taken_at: '2026-08-28T14:06:40.544Z'
branch: task/plat-055-restore-eva-secret
worktree: ../pegasus-worktrees/plat-055-restore-eva-secret
labels:
  - production
  - eva
  - key-vault
  - credential-remediation
links:
  - TICK-077
refs:
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
deployment: production
archived: false
created: '2026-08-28T14:01:57.910Z'
updated: '2026-08-28T14:22:45.644Z'
---

## What

Restore production Key Vault secret `eva-client-secret` from Infisical key `eva_api_client_secret`. The current Key Vault value was duplicated during entry.

## Why

EVA token authentication returned HTTP 401 for trace `00-5f00f120eff5dedf6d6bfd977a7eb2ae-4e772536a57a259c-00`. Key Vault access, RBAC, version resolution and synchronization are healthy; the stored secret material is wrong.

## Boundaries

This corrects the existing test credential only. It does not perform [[ENG-019]]'s live-key swap, change code, or submit a case to EVA.

## Verification

- New Key Vault version equals Infisical `eva_api_client_secret` without exposing the value.
- Web and Worker references point to the new version.
- Runtime secret synchronization succeeds.
- Token authentication succeeds; no instruction is submitted.

## Outcome
