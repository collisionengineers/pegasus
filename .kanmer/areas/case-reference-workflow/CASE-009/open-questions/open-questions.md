# Open questions — CASE-009

- [x] Case Details must render a read-only list of Case-linked emails classified as Query; the existing manual action is removed. (Operator confirmed 2026-08-21.)
- [x] Which classification set fills the Queries panel? Resolved 2026-09-03 by the controller: the existing Inbox `Queries` destination set (`MailOperationalDestinationPolicy.Query`), selected through the existing policy — one list per concept, no new classification list.
- [x] Empty state? Resolved 2026-09-03 by the controller from the design authority: the Queries heading and table are absent when no qualifying linked email exists; no `No queries` label.

## Parked (explicitly deferred)

None.
