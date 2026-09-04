---
id: DELIV-047
type: ticket
title: Make Linux the authorised Pegasus release workstation
status: preparing
area: delivery-repository
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-09-04T18:51:30.385Z'
labels:
  - release
  - linux
  - azure
groups:
  - EPIC-013
links: []
blocks:
  - DELIV-048
refs:
  - docs/adr/0007-direct-terminal-azure-deployment.md
archived: false
created: '2026-09-04T11:58:34.797Z'
updated: '2026-09-04T18:51:30.385Z'
---

## What

Change the authorised release route to Linux-built artifacts and a Linux terminal after local equivalence proof, leaving current releases available until cutover.

## Why

Web and Worker already deploy to Linux and the release scripts partially support a Linux migration bundle, but documentation and defaults remain Windows-bound.

## Verification

- [ ] A Linux exact-SHA release passes artifact, deployment, smoke and current-state documentation gates; Windows-only release commands are retired.

## Outcome
