---
id: KANMER-002
type: ticket
title: Repo Plan doc cleanup and organization into kanmer
status: done
area: kanmer-meta
order: 210
assignee: codex
profile: chore
stageEntered:
  preparing: '2026-08-17T04:44:28.890Z'
  implementing: '2026-08-17T04:49:08.745Z'
  review: '2026-08-17T04:53:38.922Z'
  verifying: '2026-08-17T05:21:56.612Z'
  done: '2026-08-18T12:22:55.153Z'
labels: []
links: []
commits:
  - c95e24a7
  - a70c2ddf
  - 6e827d19
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/379'
deployment: n/a
archived: false
created: '2026-08-14T12:30:37.742Z'
updated: '2026-09-01T14:44:31.840Z'
---

Cleanup on:

reference

design folder repo root -> move to docs/design, move design.md into design, add or update links from design to relevant assets/files. Update links from other repo files.

temp-plans - confirm either covered by tickets and implementation or needs adding as kanmer tickets, then folder to be retired, all links in repo removed

cleanup on artifacts folder - at least one planning folder in here that is likely already implemented

Check for empty directories - check if still linked in the repo and if so, clear them

## Outcome

Shipped via PR #379 (merged 2026-08-17T05:21:47Z, `6e827d19`; on `main` since #394): design material moved under `docs/design`, `docs/temp-plans` retired into Kanmer ticket documents, artifacts/planning leftovers and empty directories cleared, links repointed. Verified on `main` `f1e116c6`: 222 Markdown files link-clean; no `docs/temp-plans`, `docs/design.md` or `docs/docs/design` in the tree. The stray local branch `KANMER-002-repo-doc-cleanup` on this workstation was deleted at closeout. Closed out 2026-08-18.
