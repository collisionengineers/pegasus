# Page 27 — Replace principal: alteration plan

Source: `src/Pegasus.Web/Pages/Administration/Principals/Replace.cshtml`.
Review: `../review.md`. Standards: `../../ui-standards-and-review.md`.

## Review summary

The right layout (predecessor left, successor form right) buried under immutability
narration: a five-clause lede about rows, references and lineage; a raw sequence-lineage
GUID and a "Version: 0" integer in the detail list; a duplicated status chip; and the
"bounded selector" dev-speak repeated with different wording than its sibling page. The
consequence of the action belongs in one sentence at the confirm button.

## Changes

1. **Remove the lede.** Old: *"The existing code, principal row, cases, references, and
   reference ownership will not be edited. Replacement disables this predecessor, links a
   new active successor, and continues the same sequence lineage."* New: nothing at the
   top; one consequence sentence at the button (change 6).
2. **Remove raw internals from the Predecessor panel.** Old rows: *"Version: 0"* and
   *"Sequence lineage: 911df17b-234e-47f3-bcbf-e72958947310"*. New: both gone (standards
   §4.4 — no GUIDs, no internal version integers). The panel shows Organisation, Status
   (chip), Allocated cases — with **Allocated cases promoted to second place**, since it is
   the fact with decision weight.
3. **One status rendering.** The floating page-heading "Active" chip is removed; the chip
   lives in the Predecessor panel only.
4. **Rewrite the selector-overflow note** to match the Create page exactly: **"Showing the
   first 50 organisations — search in Organisations to find one that is not listed."**
   Rendered under the successor organisation select, only when more Work Provider
   organisations exist.
5. **Successor code hint** matches Create: **"Letters and numbers only — saved in
   capitals."**
6. **One consequence sentence at the confirm button.** New line directly above the button:
   **"ALPHA1 stops taking new work immediately; its existing cases and references stay with
   ALPHA1."** Button label kept as the honest double effect, personalised: **"Disable
   ALPHA1 and create successor"**.
7. **Reason hint.** Under "Reason for replacement": **"Recorded permanently against both
   principals."**
8. **Redesign the already-replaced/disabled state.** Old: *"This principal has no
   replacement action because it is disabled or already linked to a successor."* New: two
   distinct cards — disabled: **"ALPHA1 is disabled. A disabled principal cannot be
   replaced."**; already linked: **"ALPHA1 has already been replaced."** with a link
   **"View its successor"**. The successor form is absent in both (standards §4.9).
9. **One heading stack.** Eyebrow and back link replaced by breadcrumb "Administration /
   Replace principal"; H1 stays "Replace ALPHA1".
10. **Designed success state.** After replacement, return to Principals with a status card:
    "ALPHA1 disabled — replaced by BETA2." Both rows visible with their new state chips.
11. **Remove the blank-body defensive wrapper behaviour** from the design: an unknown
    principal renders the styled not-found page, never an empty body (standards §4.6).

## Dependencies

- Same filtered-overflow fix as page 26: the "more organisations exist" flag must count
  Work Provider organisations only.
- The already-linked state card needs the successor's identity (code + link target) exposed
  to the page model; today only `SuccessorId` presence is known to the view.
- Styled not-found page (shared dependency, owned by page-18-error work).
- Success status card on the Principals list reuses `TempData["AdministrationStatus"]`.

## Open questions

- Should replacement require the operator to retype the predecessor code as confirmation
  when Allocated cases is large? Standards §4.8 says one confirmation only — the plan keeps
  a single button and treats the reason field as the deliberate step; flag for operator
  decision if stronger friction is wanted.
- Where does the operator land after replacement — Principals list (plan assumes) or the
  successor's detail? Needs a decision with the page-25 work.
