# Proof — production, 2026-08-23

Tier: **production**. Release 26 (`7d6a948a`), against the database wiped clean
by [[PLAT-040]], so the case below was registered entirely by the new code.

## Both faults are gone

`QDOS26014` was created from a forwarded QDOS instruction and holds three
confirmed photographs registered as `Image` occurrences:

| Ordinal | Role | File | Bytes |
| ---: | --- | --- | ---: |
| 2 | Image | `2_clvoffsidejpeg-V1.jpeg` | 3,533,876 |
| 3 | Image | `3_clvrearjpeg-V1.jpeg` | 4,030,444 |
| 5 | Image | `clvdamagejpeg-V1.jpeg` | 4,495,351 |

The operator's Evidence-tab feedback is about **how images are presented** —
that clicking one opens a bare tab instead of a preview ([[DOCS-011]]), and
that the custody detail beside them is unwanted ([[DOCS-012]]). Both
observations require the images to be on screen. Before this fix every gallery
URL carried the `CaseDocuments.Id` and returned 404, so nothing rendered at
all; there was no presentation to have an opinion about.

The second fault, [[PLAT-039]]'s Box 401, is independently proved by the same
case's export succeeding — the two had to go together for anything to render,
and both did.

## What was measured before

On `ap.QDOS26012` under release 25, with the ids taken from the database:

| Request | Status |
| --- | --- |
| the id the page emitted (a `CaseDocuments.Id`) | **404** |
| the matching `DocumentOccurrences.Id` | **500** — reached Box, hit the token |

Both now resolve.

## Residual

The gallery is proved to render. It is *not* proved that every one of the three
images is byte-correct on screen, because that is a visual check and the
browser session had signed out; the archive-side equivalent — hash-verified
image content out of Box — was taken on `ap.QDOS26011` under [[CASE-019]] and
the same `OpenReadVersionAsync` path serves both.
