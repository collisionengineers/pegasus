# Plan — PR-017

## Approach
Add a mailbox-list method to the existing Deleted source/use case and select it only when the page folder is Deleted Items. The Graph implementation delegates to `IApprovedIntakeMailboxes`; the fallback returns none. Estimate: 4 files, under 100 lines.

## Governing docs
FRD-08 mailbox refinement is supplied from the canonical approved estate; retained Inbox history remains untouched.

## Steps
1. Extend the narrow existing port/use case and page caller.
2. Prove a zero-retained-row approved mailbox is selectable; simplify.

## Simplification pass — 2026-08-20

- Reuse: applied — mailbox choices come from the existing `IApprovedIntakeMailboxes` owner through the existing Deleted source port.
- Simplification: no second mailbox query service or persisted list.
- Efficiency: one approved-estate read for Deleted tabs.
- Altitude: Web selects the correct existing source by folder; authorization remains in Core.
