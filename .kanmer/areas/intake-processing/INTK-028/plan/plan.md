# Plan

Committed in `1a86f5db`. Proportional to a one-file policy change.

## Root cause, confirmed against production

The speedo rule was anchored to the start of a line (`^speedo\s*:`) while the reader lays
the report's columns out as a single line. Production held it verbatim for QDOS26008:

```
Vehicle: TOYOTA NOT RECORDED Colour: Black Speedo: 72850 Miles
```

The neighbouring vehicle rule already knew this — it cut its value at
`colour|speedo|reg no|reg`. The two rules had drifted.

A corpus survey run with PyMuPDF reported `Speedo:` at line start 6/6 and appeared to
refute this. It was **not** accepted: the app reads PDFs with PdfPig, and querying
production showed PdfPig stores the line as above. The survey had used the wrong
extractor.

## The change

1. Match `Speedo:` anywhere on the line, not only at line start.
2. Cut vehicle, speedo **and** registration values at the next column label, through one
   shared `ReportColumnCutPattern` constant — the drift that caused this cannot recur.
3. Registration gets the same treatment: it was followed by `Registered:`/`Type:`/`Trans:`
   on its own line and so never parsed either.
4. Remove `IsReportFragment` entirely. Naming the report by file name never worked
   because a different firm writes it each time.
5. Move the third-party-row guard up to the whole line, since these rules now read labels
   mid-line.
6. Policy version 4 → 5.

## What the tests caught

The first draft of the registration rule matched `TP Registration:` mid-line and fed the
third party's registration into the claimant's field. The existing guard test
`ThirdPartyRowsNeverFeedClaimantFields` failed and produced the whole-line guard. A
second test encoded the filename gate that had just been removed; it was rewritten and
its real guard moved into a dedicated test rather than deleted.

## Acceptance

- The real multi-column line yields `72850` miles cited to the report. ✅
- `Speedo: Miles` with no digits still abstains. ✅
- Comma-thousands parses. ✅
- A report whose file name says nothing is still read. ✅
- Third-party rows never feed claimant fields. ✅
- No other provider's extraction shifted — full Core suite, 908 passed.

## Simplification pass

2026-08-21. The shared `ReportColumnCutPattern` constant *is* the simplification: three
rules had three copies of the same label list. `IsReportFragment` deleted rather than
widened — dead policy, not a smaller one. No findings deferred.
