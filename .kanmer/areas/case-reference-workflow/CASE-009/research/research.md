# Research — CASE-009: Case query panel

## Question

Whether the Case Details query panel can be corrected as a presentation-only change, and whether linked emails classified as queries already populate it.

## Findings

- `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` renders a literal **Engineer queries** heading, disabled **Raise a query** button, and fixed empty-state text. Its nearby comment explicitly says raising, reply chains, and resolution are not allocated and nothing is stored or answered there.
- `CaseDetails` in `src/Pegasus.Core/Cases/CaseQueries.cs` has no query or linked-correspondence collection. The Case Details page therefore has no current port through which query emails could reach the panel.
- `docs/frd/frd-08-email-mailbox-and-background-processing.md` classifies post-report query, dispute, and amendment-request mail into the Queries application queue, but does not specify a Case Details list of those messages.
- `docs/frd/frd-12-operator-experience.md` requires Case detail/history journeys and directs UI behaviour to `docs/design/README.md`; it does not define this static panel.
- A read-only `rg` survey found no test that asserts this panel's current heading, button, or empty state.

## Implications

Renaming the heading to **Queries** and removing the inactive button is a one-view presentation correction. Rendering linked Query emails would require a new Core read model/port, persistence query, page-model wiring, UI behaviour, and acceptance tests; it is not present today and must not be claimed as preserved behaviour.

## Open questions

- See `open-questions`: decide whether CASE-009 is limited to the safe copy/control correction or also includes an actual linked-email query list.
