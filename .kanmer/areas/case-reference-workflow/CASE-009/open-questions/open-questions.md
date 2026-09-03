# Open questions — CASE-009

- [x] Case Details must render a read-only list of Case-linked emails classified as Query; the existing manual action is removed. (Operator confirmed 2026-08-21.)
- [ ] Which classification set fills the Queries panel: every message in the existing Inbox `Queries` destination (post-report `query`, `dispute`, `amendment-request` plus Billing `billing-query`, i.e. `MailOperationalDestinationPolicy.Query(Queries)`), or only the `PostReportEmails` family as D12 uses for the Engineer Report? (Research 2026-09-02; the plan selects through the existing policy either way, never a new list.)
- [ ] Empty state: the ticket says "a truthful empty state"; the design authority (`docs/design/README.md` §No explanatory copy: a read-only section with nothing recorded and no action is absent, not an empty-state panel) and CLAUDE.md say no empty-state panels in read-only view. The plan (2026-09-02) follows the design authority — the Queries heading and table are absent when no qualifying linked email exists, and the test proves that absence and the absence of every manual control. Confirm, or direct a single `No queries` label instead.

## Parked (explicitly deferred)

None.
