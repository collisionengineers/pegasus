# Research — CASE-009: Case query panel

## Question

What Case Details must show for query correspondence and whether that behaviour already exists.

## Findings

- `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` renders a literal **Engineer queries** heading, disabled **Raise a query** button, and fixed empty-state text. Its nearby comment explicitly says raising, reply chains, and resolution are not allocated and nothing is stored or answered there.
- `CaseDetails` in `src/Pegasus.Core/Cases/CaseQueries.cs` has no query or linked-correspondence collection. The Case Details page therefore has no current port through which linked query emails could reach the panel.
- `docs/frd/frd-08-email-mailbox-and-background-processing.md` classifies post-report query, dispute, and amendment-request mail into the Queries application queue.
- `docs/frd/frd-12-operator-experience.md` requires Case detail/history journeys and directs UI behaviour to `docs/design/README.md`; it does not define this static panel.
- A read-only `rg` survey found no test that asserts this panel's current heading, button, or empty state.

## Implications

The confirmed scope is both the presentation correction and a read-only Case Details list of emails linked to that Case and classified as a Query. It needs a new Core read model/port, persistence query, page-model wiring, UI behaviour, and acceptance tests. Query creation, replying, resolving, manual association, and mailbox mutation remain outside this ticket.

## Open questions

None; the operator confirmed the linked-email list is in scope.
