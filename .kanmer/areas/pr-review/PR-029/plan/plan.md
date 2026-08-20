# Plan

Estimated diff: four existing files, about 80 lines.

1. Extend the existing detail query/use case with the same normalized optional search term used by list search and reuse the current match projection for the one detail row.
2. Add search mismatch to `OutsideListScope` in both GET and reload paths.
3. Prove an authenticated matching message can link to a nonmatching thread member that preserves search and renders outside-view state.
4. Run focused tests and four-lens/PIR updates.

## Governing docs

FRD-08 requires preserved search context and an honest state when a thread member is outside it; this plan reuses the existing detail and status owners.

## Simplification pass — 2026-08-20

- Reuse: the existing detail use case/query, summary match list, match mapper, and outside-view status are extended.
- Simplification: one optional term on the existing detail boundary replaces a separate membership query/service; the outside-scope condition is centralized for GET/reload.
- Efficiency: detail evaluates one retained row and its bounded match evidence.
- Altitude: authorization/term normalization remains Core, query evidence Infrastructure, presentation state Web.
