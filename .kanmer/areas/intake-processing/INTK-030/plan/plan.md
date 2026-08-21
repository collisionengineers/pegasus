# Plan

Committed in `ad1ba223`. The threshold was **measured**, not invented — the ticket
required that explicitly.

## Root cause

The floor for an embedded image to count as a photograph was its byte size, so QDOS26008
put two pieces of letterhead art in front of the operator before any real photograph.

## The measurement

From production, the two false positives are a 110,783-byte PNG at **1990×437** and a
77,972-byte JPEG at **2214×248**. Both clear the 40 KB floor by a wide margin and one is
a JPEG, so neither a higher floor nor a format rule would have separated them from
evidence. The nine genuine photographs on the same receipt are all 709×~650.

What separates them is **shape**:

| | Side ratio |
| --- | --- |
| Letterhead banners on QDOS26008 | 4.55:1, 8.93:1 |
| Genuine photographs on QDOS26008 | 1.09:1 – 1.15:1 |
| The same 1990×437 letterhead across the corpus | five unrelated reports |
| Other corpus banners | 3.19, 3.30, 9.08 |
| Widest thing that might be a photograph | 2.22 |

A 3.0 limit sits in open space between 2.22 and 3.19.

## The change

Apply `IsPhotographShaped` in `Select`. It **fails open**: an image whose dimensions were
not recorded is judged on the existing rules, because hiding a real photograph is the
worse error.

## Acceptance

- The two measured banners are excluded. ✅
- The nine measured photographs are kept. ✅
- An image with no recorded dimensions is still admitted. ✅
- Live: evidence shows photographs and no signatures — Phase 6.

## Simplification pass

2026-08-21. One predicate on data already captured; no new capture, no new store, no new
classification vocabulary. The inline-image classification duplicated between
`MimeKitPdfPigOpenXmlIntakeSourceReader.cs:862` and `.DocMsg.cs:234` — the exact drift
that produced this defect — is **not** fixed here: it is behaviour-preserving cleanup
outside this fix's blast radius, and is on [[PLAT-032]]'s roster. Named rather than
silently skipped.
