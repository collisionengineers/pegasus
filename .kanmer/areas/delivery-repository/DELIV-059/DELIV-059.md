---
id: DELIV-059
type: ticket
title: Restore release-39 history to the canonical operations record
status: done
area: delivery-repository
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T16:08:50.581Z'
  review: '2026-09-08T16:39:10.872Z'
  verifying: '2026-09-08T16:48:54.739Z'
  done: '2026-09-08T17:02:07.874Z'
labels:
  - documentation
  - release-evidence
  - corrective
links:
  - DELIV-054
  - DELIV-048
  - DELIV-047
refs:
  - docs/index.md
  - docs/engineering.md
  - docs/adr/0007-direct-terminal-azure-deployment.md
commits:
  - 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/710'
deployment: n/a
delivery_state: integrated
delivery_branch: dev
delivery_sha: 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c
delivery_recorded_at: '2026-09-08T17:03:06.503Z'
archived: false
created: '2026-09-08T16:07:49.662Z'
updated: '2026-09-08T17:04:13.212Z'
---

## What

Restore the missing dated release-39 facts and failures from PR #676 to current dev's canonical docs/operations.md, preserving the current source-versus-deployment documentation boundary.

## Why

The approved next-corrective-deployment plan D1 explicitly requires release-39 evidence to survive before PR #676 is disposed as superseded. [[DELIV-054]] / PR #703 already integrated its useful ZIP correction, while current operations describes release 38 and its linked historical ledger omits release 39. PR #676 must not be merged unchanged or closed as wholly superseded while its unique operational record is missing. [[DELIV-048]] portable release support and [[DELIV-047]] historical Linux work remain separate retained claims, not scope to reopen.

## Approach

- Research exact PR676 source/artifact/migration/Worker replacement/reset records against original retained evidence; record any unverified claim as such, not fresh observation.
- Change only current docs/operations.md with the dated historical record, retaining the rejected original ZIP, non-identical replacement/provenance deviation, partial migration sequence and separately authorized reset.
- Do not restore obsolete Linux-only guidance, stale source architecture, credentials, new commands, live status assumptions, or deployment permissions.
- After independent review and integration, hand root the exact documentation SHA needed alongside PR703 for an explicit PR676 superseded disposition. This ticket does not itself authorize closing foreign claims or deploying.

## Verification

- [ ] Historical statements trace to exact retained source/receipt and preserve failures and scope limitations.
- [ ] Current architecture/source descriptions and release procedures stay unchanged.
- [ ] Relevant documentation link/placement checks and independent semantic review pass; no dotnet build/test for prose-only edits.
- [ ] Exact merged-SHA proof precedes Done.

## Outcome

- PR [#710](https://github.com/collisionengineers/pegasus/pull/710) was squash-merged into `dev` on 2026-09-08 as `9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c`.
- Final schema-2 exact-merge proof is PASS: the corrected identity/diff check, 140-file Markdown-link check, and exact merge-range placement check passed. Its original wrapper-only absolute-path join error is retained as a plan failure and not relabelled as a source failure.
- Documentation-only work: integrated to `dev`; deployment is `n/a`. No .NET, package, cloud, migration, reset, promotion or production operation occurred.
- The merge restores only the qualified historical release-39 documentation record. It does not close, change, or otherwise dispose PR #676; that separately authorised root action remains outside this ticket.
