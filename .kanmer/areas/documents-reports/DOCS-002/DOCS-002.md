---
id: DOCS-002
type: ticket
title: Record the Web Container App as the integrated renderer execution boundary
status: done
area: documents-reports
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-19T09:13:28.723Z'
  review: '2026-08-19T09:17:43.343Z'
  verifying: '2026-08-19T09:20:45.991Z'
  done: '2026-08-19T09:22:46.310Z'
taken_at: '2026-08-19T09:14:51.544Z'
branch: task/docs-002-renderer-web-boundary
worktree: ../pegasus-worktrees/docs-002-renderer-web-boundary
labels:
  - now
  - renderer-integration
groups:
  - EPIC-004
links:
  - SIMPLI-014
  - PLAT-007
blocks:
  - TICK-215
refs:
  - docs/adr/0028-run-integrated-renderer-in-web-container-app.md
commits:
  - 169bcd5bbe1e334a52dbb18725d1ae46c6e8f6ab
  - 4d1bff3db4ed16692e7646ea07e7f4491365defd
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/413'
deployment: n/a
archived: false
created: '2026-08-19T09:13:24.531Z'
updated: '2026-08-19T09:23:04.833Z'
---

## What

Write and link the thin ADR selecting the existing Pegasus Web Container App as the production Chromium/report-rendering execution boundary, with the existing Flex Consumption Worker unchanged and no separate renderer service/job.

## Why

TICK-215 research established a durable technical choice not fully decided by ADR-0015 or ADR-0025. Repository governance requires the choice to be recorded before implementation planning.

## Approach

- Allocate the next stable ADR id after verifying the index/frontmatter set.
- Record one decision only: in-process rendering in the existing Web Container App because it is the existing custom-container boundary capable of carrying pinned Chromium/native/font dependencies.
- Link ADR-0025 and FRD-11; record consequences, including synchronous/durable operation constraints and the separately approval-gated Azure proof in PLAT-007.
- Update the ADR index and link the new ADR to TICK-215/SIMPLI-014/PLAT-007 as appropriate.

## Verification

- [ ] ADR frontmatter/index are valid and use the next permanent id.
- [ ] The decision creates no new project, runtime, service, queue consumer, or deployment unit.
- [ ] TICK-215 can resume planning against the linked ADR.

## Outcome

ADR-0028 and its ADR-index row shipped through [PR #413](https://github.com/collisionengineers/pegasus/pull/413), merged to `dev` at `4d1bff3db4ed16692e7646ea07e7f4491365defd` on 2026-08-19. The ADR is linked to [[TICK-215]], [[SIMPLI-014]], and [[PLAT-007]]. TICK-215 may now be planned; implementation and Azure/runtime proof remain with those owning tickets. No deployment was performed.
