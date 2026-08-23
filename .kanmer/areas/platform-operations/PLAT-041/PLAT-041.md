---
id: PLAT-041
type: ticket
title: 'Resolve the Box case folder once per export, not once per image'
status: backlog
area: platform-operations
assignee: ''
profile: fix
labels:
  - qdos26014
  - found-during-qa
  - performance
  - box
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T15:19:04.949Z'
updated: '2026-08-23T15:19:04.949Z'
---

## What the operator saw

> *"The export seems to take too long though, around 10ish seconds."*
> — `QDOS26014`, three photographs.

## Measurements

| Case | Images | Export |
| --- | ---: | ---: |
| `ap.QDOS26011` (2026-08-23, release 25) | 8 | ~25 s |
| `QDOS26014` (2026-08-23, release 26) | 3 | ~10 s |

Roughly linear in image count at ~3 s each, with a fixed overhead — which is
the signature of per-image work, not per-export work.

## Cause

`EvaHandoffStore.LoadEligibleImagesAsync` calls
`contentStore.OpenReadVersionAsync` once per eligible image, and
`BoxDocumentContentStore.OpenReadVersionAsync` begins by calling
`ResolveCaseFolderAsync` — which walks Box from the approved root to the case
folder. That walk is repeated for every image in the archive, so an eight-image
export makes roughly four times the Box round trips it needs.

The same resolution also runs on every Evidence-tab thumbnail, so the gallery
pays it per image too.

## Shape of the fix

Resolve the case folder once per export and reuse it for every image in that
archive. The obvious candidates:

- pass the resolved folder through the read call for a batch, or
- give `BoxDocumentContentStore` a short-lived per-case folder-id memo.

A memo has a correctness question worth answering before choosing it — a case
folder id is durable (`Cases.CustodyRootRemoteId`), so it is a reasonable thing
to hold, but a stale id after a Box-side move must fail loudly rather than read
the wrong folder. Prefer the explicit batch route unless the memo proves
simpler.

## Not urgent, and worth saying so

Nothing is broken; the archive is correct and hash-verified. This is a
proportionality ticket: three images should not cost ten seconds, and the cost
grows with the thing operators will do most. Filed rather than folded into the
Box work so it is scheduled on its own merits.
