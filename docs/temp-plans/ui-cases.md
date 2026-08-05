# UI Cases — task plan

Branch `task/ui-cases`. Pages 4/5 (Cases and Search) and 12 (case detail).

## The list

- **Search is retired into Cases.** Both ran the identical Core query and
  differed only in which filters they exposed, so two nav items led to one
  capability — and they disagreed about what a query failure meant (Cases 503,
  Search nothing). `/Search` redirects, carrying the keyword through.
- The lede narrated browser mechanics and is gone.
- Eleven stacked filter inputs become **one line** — keyword, stage, principal
  — with the rest behind "More filters", opened automatically when one is in
  use.
- **Case stage renders through the operator label map**, so `NotReady` and
  `PostReportComplete` never reach markup. The engineer column stops printing
  a GUID.

## The case container

One container: a dark header band carrying the reference, principal,
registration, claimant and case type, with the stage chip; an action bar; and
**Overview · Evidence · History** as tabs. It was a five-screen vertical stack
of sibling panels. The lede narrated concurrency mechanics and is gone.

- **Export is right-aligned behind a rule, disabled outside Review with the
  condition on the control** — and the rule is a **Core precondition**, not a
  greyed button. `IExportCaseDocuments` had no stage condition at all, so the
  operator's rule existed nowhere and any caller could take the bundle at any
  stage. `CaseNotInReviewException` is the boundary.
- **Provenance is an icon with a one-word tooltip**, replacing an 18-row
  three-column Fact/Suggestion/Confirmed grid in which every populated cell
  also carried a source label, a policy key and a version integer. Only
  populated fields render.
- **Evidence is one home** for files and vehicle images. The images had their
  own section under the banned term "Image intakes", beside a sentence saying
  an association could be "reasonedly reversed", which is not a word.
- History renders labelled events instead of snake_case codes, and drops the
  version arrows.

## Recorded, not done quietly

The Lucide sprite is a checksummed sixteen-glyph asset and the design
authority records that no glyph was added, removed or redrawn. Seven
provenance words share those sixteen glyphs, so **E-mail** and **AI** lean on
their tooltips rather than a distinct envelope and spark. Adding two glyphs
needs operator authorisation to re-checksum the sprite; it is recorded here
rather than done without it.

"AI" also has no persisted distinction from a plain document read — both are
`IntakeEvidence` — so it is derived from the reader identity on the source
label and falls back to Extracted rather than guessing.

## Verification

- Core 441/441, architecture 73/73, integration 399 passed / 0 failed
- A new test proves the export precondition refuses a Not-ready case at the
  Core boundary, not merely in the markup
