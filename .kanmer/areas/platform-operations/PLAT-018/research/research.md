# Research — PLAT-018: correct contradictory design-authority rules

## Question

Does `docs/design/README.md` genuinely contradict itself about `queue` and about when consequence guidance is permitted, and can the correction remain limited to that authority document?

## Findings

- **Verified:** The banned-words list at `docs/design/README.md:410–418` includes `queue`, while the approved shell at `:464` mandates the `Queues` navigation label and the rules at `:483` require metrics to link to filtered queues.
- **Verified:** The already-narrower rule at `:170` prohibits exposing “queue mechanics” in operator copy. It preserves the intended prohibition without banning the ordinary work-list label.
- **Verified:** Current operator-visible uses are legitimate list-label uses: `Pages/Shared/_Layout.cshtml:74`, `Pages/Triage/Index.cshtml:5,26,31`, `Pages/Triage/Details.cshtml:16`, `Pages/Shared/_MetricCard.cshtml:16`, and the accessible caption at `Pages/Triage/Index.cshtml:267`. Other `queue` matches include internal identifiers, route parameters, comments, and CSS classes, already outside the copy rule’s scope.
- **Verified:** The approved necessary-copy list at `README.md:400–408` contains three specific sentences. The no-explanatory-copy rule at `:431–435` calls a “single consequence sentence” the exception without explicitly tying it back to that closed list.
- **Verified:** [[PLAT-019]] removes unapproved shared reason-dialog copy and [[MAIL-006]] rebuilds the Inbox message page. Their work is intentionally separate: neither should be folded into this authority correction.
- **Verified:** This is a repository/design-authority convention change, not a product-behaviour or technical decision. PLAT-018 has no PRD, FRD, or ADR reference by design; its `docs_todo` declaration supplied the Backlog exit gate.

## Implications

Delete only `queue` from the banned-word enumeration; leave the narrower `queue mechanics` rule intact. Reword the exception sentence to point exclusively to the approved necessary-copy list and state that it is closed. No code, tests, styles, markup, governing documents, or related-ticket edits belong in this ticket.

## Open questions

None. The operator direction recorded in the ticket is sufficient; no product or technical decision remains to be made.
