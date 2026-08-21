# Post-implementation report

**Branch:** `task/qdos26008-regressions` · **PR:** #505 · **Commit:** `ad1ba223`

## What was built

`MaximumPhotographSideRatio = 3.0` and `IsPhotographShaped`, applied in
`InstructionEvidenceImages.Select`. It fails open: an image whose dimensions were not
recorded is judged on the existing rules, because hiding a real photograph is the worse
error.

## The measurement, as the ticket required

The ticket said the threshold must be corpus-measured and recorded, not invented.

| | Side ratio |
| --- | --- |
| The two false positives on QDOS26008 (1990×437 PNG, 2214×248 JPEG) | 4.55, 8.93 |
| The nine genuine photographs on the same receipt (709×~650) | 1.09 – 1.15 |
| Other banners across the corpus | 3.19, 3.30, 9.08 |
| Widest corpus image that might be a photograph | 2.22 |

3.0 sits in open space between 2.22 and 3.19. The same 1990×437 letterhead appears in five
unrelated reports.

## Why the plan's first three steps were dropped

The plan proposed, in order: exclude inline-classified images, exclude attachments
carrying a `cid`, then apply a photograph test. Only the third was implemented.

The measurement is why. Both false positives clear the 40 KB floor by a wide margin and
one is a **JPEG**, so neither a byte rule nor a format rule separates them from evidence —
and the `cid` rule fails exactly in the case the ticket called out, a signature whose
`cid` was stripped by forwarding. Shape separates them on its own, so the other two rules
would have been machinery that changed no outcome.

## Nothing new is captured

`WidthInSamples` and the bounding box were already recorded on every asset. This reads
what was there.

## Evidence

- `Pegasus.Core.Tests` — 908 passed, covering both measured banners excluded, the measured
  photographs kept, and an image with no recorded dimensions still admitted
- Live: the evidence gallery with photographs and no letterhead — Phase 6

## Deferred, and named

The inline-image classification is written twice in the MIME reader
(`MimeKitPdfPigOpenXmlIntakeSourceReader.cs:862` and `.DocMsg.cs:234`) — the exact drift
that produced this defect. Behaviour-preserving cleanup outside this fix's blast radius,
so it is on [[PLAT-032]]'s roster rather than silently skipped.
