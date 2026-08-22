# Plan

Committed in `3d7f87d6`. Eight strings; this plan is deliberately short.

## The rule applied

Remove the word from what an operator reads. Where it sat in a label, the label loses it.
Where it sat in a sentence, the sentence goes — the approved necessary-copy list is closed,
so nothing is written to replace it, and the three Administration occurrences were
explanatory copy the design authority bans anyway.

## Checked before changing

No test asserted any of the eight strings — grepped first, having learned that lesson on
[[MAIL-010]], where CI caught two tests pinned to copy that had just been deleted.

## Acceptance

- A scan of `src/Pegasus.Web/Pages/**/*.cshtml` for the word returns only Razor comments
  and code identifiers. ✅
- `Pegasus.Web` builds clean. ✅
- Live: the case and Administration pages — Phase 6.

## Simplification pass

2026-08-22. Removes text and adds none. No findings deferred.
