---
id: PLAT-073
type: ticket
title: Provision and document the Linux-native WSL toolchain
status: backlog
area: platform-operations
assignee: ''
profile: chore
labels:
  - wsl
  - linux
  - tooling
groups:
  - EPIC-013
links: []
blocks:
  - PLAT-074
  - UIIMP-016
archived: false
created: '2026-09-04T11:58:34.774Z'
updated: '2026-09-04T11:58:57.715Z'
---

## What

Install the pinned offline and cloud development tools under Linux, remove Windows PATH dependencies, reconcile Kanmer v0.4.1, and align Doctor/runbook repair guidance.

## Why

The WSL checkout is native but currently resolves Windows tools and lacks most Pegasus prerequisites.

## Verification

- [ ] Both Doctor profiles and the canonical locked restore/build/test commands pass using Linux-native executables.

## Outcome
