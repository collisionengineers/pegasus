# Plan — PLAT-018: correct two self-contradictory rules in the design authority

## Approach

Make the two smallest textual corrections in `docs/design/README.md`: remove only `queue` from the banned operator-copy enumeration, retaining the existing targeted ban on “queue mechanics”; then replace the broad-looking exception sentence with a direct reference to the closed approved necessary-copy list. This beats changing current UI labels, expanding the banned list with exceptions, or changing source code because the authority—not the labels or mechanics—is contradictory. The related markup work stays with [[MAIL-006]] and [[PLAT-019]].

## Governing docs

No PRD, FRD, or ADR is linked or modified. This ticket corrects repository design-authority conventions, which the repository’s documentation model assigns to the existing `docs/design/README.md`, explicitly outside the PRD/FRD/ADR taxonomy. No architectural decision is introduced.

## Steps

1. In the existing operator-copy banned-word list, remove only `queue`; retain every other banned term and the nearby rule against exposing queue mechanics.
2. Reword the no-explanatory-copy exception so it states that a consequence sentence is permitted only when it is one of the individually approved entries in the closed necessary-copy list above; do not alter that list.
3. Review the documentation-only diff against the ticket and research scope, confirming the two corrections resolve the contradictions and no other tracked file changes.

## Verification

Run `git diff --check` and inspect the path-limited diff for `docs/design/README.md`. Search the authority to confirm `queue` is absent from the banned-word enumeration while the approved `Queues` shell label and the “queue mechanics” prohibition remain. Confirm the exception explicitly points to the closed approved-copy list. Record those command outputs and diff inspection in the post-implementation report/proof after implementation and merge; no build or test run is warranted for this docs-only change.

## Risks / open questions

- **Risk:** Removing `queue` could be read as allowing implementation terminology in operator copy. **Mitigation:** preserve the explicit `queue mechanics` restriction and make no source-copy changes in this ticket.
- **Risk:** The exception wording could itself authorize new prose. **Mitigation:** state that the approved list is closed and leave its three entries unchanged.
- **Open questions:** None; the ticket records the operator direction and research found no unresolved dependency.

## Simplification pass — 2026-08-21

n/a — docs-only. The diff is the two planned textual corrections in one existing authority file; no abstraction, runtime path, test shape, or duplicate implementation exists to simplify.
