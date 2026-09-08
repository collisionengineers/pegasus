---
id: DELIV-055
type: ticket
title: Restore the self-contained release migration host recipe
status: done
area: delivery-repository
order: 40
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T13:28:18.420Z'
  implementing: '2026-09-08T13:30:29.628Z'
  review: '2026-09-08T14:24:56.601Z'
  verifying: '2026-09-08T14:54:07.503Z'
  done: '2026-09-08T15:12:10.746Z'
labels:
  - release
  - corrective
links: []
refs:
  - docs/adr/0007-direct-terminal-azure-deployment.md
commits:
  - a1f0bfe260ea05df531df6e0ca3109141e7697da
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/705'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: a1f0bfe260ea05df531df6e0ca3109141e7697da
delivery_recorded_at: '2026-09-08T15:14:00.893Z'
archived: false
created: '2026-09-08T13:25:29.949Z'
updated: '2026-09-08T15:14:38.664Z'
---

## What

Repair the release skill's database-migration reference after documentation PR #702 deleted its required runbook host-environment recipe.

## Why

The current reference points to the absent Release artifacts and bootstrap heading. The bundle constructs the current Production Web host, which requires additional nonblank settings. This is a focused documentation correction supporting [[PLAT-046]], not approval to change deployment order or run migrations.

## Approach

- Document current required process environment in the existing migration reference using approved azd non-secret values and inert host-only placeholders where supported.
- Preserve manifest-bound native bundle invocation, PreMigration and database-bootstrap ordering.
- No source/runtime/schema/permission changes, actual secret material, cloud writes or alternate deployment route.
- Update unmanaged AGENTS.md only as needed to point to the one procedure owner; coordinate after DELIV-053 to avoid overlap.

## Verification

- [x] Semantic mapping to current Web host required-key/config validation and deployment inputs.
- [x] No dangling runbook dependency, secrets or change to release ordering.
- [x] Independent documentation review and serialized documentation checks.

## Outcome

PR #705 squash-merged into `dev` as
`a1f0bfe260ea05df531df6e0ca3109141e7697da`. The author commit
`91a53a15353f442f5d3dad00fe9216f6561b692f` remains provenance; the merged
SHA is the reachable integration record. Schema-2 proof records PASS for the
140-file relative-link check and authoritative non-executing parse of exactly
five PowerShell fences, with no embedded recipe executed.

No application build/test, migration, bootstrap, Azure write, promotion,
release package, or deployment occurred. [[PLAT-046]] containment remains
outside this ticket; delivery is integrated on `dev`, not deployed.
