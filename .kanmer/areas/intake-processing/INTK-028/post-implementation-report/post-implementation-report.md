# Post-implementation report

**Branch:** `task/qdos26008-regressions` · **PR:** #505 · **Commit:** `1a86f5db`

## What was built

`QdosInstructionExtractionPolicy` version 4 → 5:

- the speedo rule matches `Speedo:` anywhere on the line, not only at line start;
- vehicle, speedo **and** registration values are cut at the next column label through one
  shared `ReportColumnCutPattern` constant;
- `IsReportFragment` removed entirely;
- the third-party-row guard moved up to the whole line.

## Departures from the plan

**Registration was added to the scope.** It was not on the original list. Once the
column-cut rule existed it was obvious that registration had the same defect — it was
followed by `Registered:`/`Type:`/`Trans:` on its own line and so had never parsed either.
Fixing it separately would have meant a second pass over the same three lines.

**`IsReportFragment` was deleted, not widened.** The plan said "add a content tell so a
report named anything else still gets the report grammar". The operator then explained
that the original report comes from a different third-party engineering firm every time,
named however that firm's system named it. A filename test cannot be widened into
correctness. The report grammar now runs over every fragment and is written so only a
report can satisfy it, with its facts appended last so the instruction letter still
outranks. Generalised into [[INTK-031]].

## What the tests caught

Two failures, both real:

1. `ThirdPartyRowsNeverFeedClaimantFields` — the first registration rule matched
   `TP Registration:` mid-line and fed the third party's registration into the claimant's
   field. This is why the guard moved to the whole line. An existing test caught a safety
   regression in new code.
2. `AVehicleLineOutsideAReportContributesNothing` encoded the filename gate that had just
   been removed. Rewritten, and its real guard moved into a dedicated test rather than
   dropped with it.

## A survey that was wrong, and was not believed

A corpus survey run with PyMuPDF reported `Speedo:` at line start in 6 of 6 reports,
apparently refuting the whole diagnosis. It was not accepted: the application reads PDFs
with **PdfPig**, and a production query showed PdfPig stores the line as
`Vehicle: TOYOTA NOT RECORDED Colour: Black Speedo: 72850 Miles`. The survey had used the
wrong extractor.

## Evidence

- `Pegasus.Core.Tests` — 908 passed, including the real multi-column line, the no-digits
  abstention, comma-thousands, and a report whose file name says nothing
- Full integration suite: recorded before merge
- Live replay of a real QDOS audit instruction: Phase 6

## Not done

`docs/principal-rules-and-mappings/qdos.md` was **not** updated — that path does not exist
in this repository. The rule change is recorded in the policy's own version notes.
