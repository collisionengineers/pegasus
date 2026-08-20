# Plan — PR-017

## Approach
Add a mailbox-list method to the existing Deleted source/use case and select it only when the page folder is Deleted Items. The Graph implementation delegates to `IApprovedIntakeMailboxes`; the fallback returns none. Estimate: 4 files, under 100 lines.

## Governing docs
FRD-08 mailbox refinement is supplied from the canonical approved estate; retained Inbox history remains untouched.

## Steps
1. Extend the narrow existing port/use case and page caller.
2. Prove a zero-retained-row approved mailbox is selectable; simplify.
