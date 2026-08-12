# Inbox effective forwarded sender

## Outcome

The Inbox uses the intake route's proven effective sender as the visible
sender for a normal forwarded message. It continues to preserve and display
the Collision Engineers mailbox that forwarded the message as provenance. A
message with no proven effective sender remains displayed exactly as its
retained Graph envelope supplied it.

Every definitive typed instruction, including a standalone Audit, creates its
normal Case/PO automatically. A missing original-report assessment leaves that
case `Not ready` and withholds only its `a.` or `ap.` Audit reference; it never
requires a staff confirmation before Case creation.

## Changes

1. Extend the retained-mail read projection with the effective sender already
   persisted on the matching mailbox intake route decision. Do not change the
   retained Graph envelope columns or create a new store/migration.
2. Render the effective sender as the Inbox row and message-detail `From`
   value. Where it differs from the retained envelope sender, show a clear
   `Forwarded by` provenance value. Thread entries remain envelope history in
   this narrow repair.
3. Remove the standalone-Audit confirmation gate from processing and the Core
   acceptance/persistence boundary. Preserve retained original-report evidence
   as the later Audit-reference rule, not a Case-allocation precondition.
4. Amend the canonical requirements, capability, architecture, and domain
   wording so none says a standalone Audit waits for confirmation before Case
   creation.
5. Add focused query and page assertions for: a forward uses the original
   sender and names the forwarder; direct mail has no forwarding label; and a
   retained message without a route decision remains unchanged.

## Verification

- Run focused Core, allocation, Web, and Infrastructure tests, then the Release
  build. The allocation test must prove an Audit receipt creates a Case with no
  standalone-evidence identifier.
- Inspect the final diff for the read-model join, presentation, and the narrow
  removal of the confirmation gate; no migration, mailbox mutation, receipt
  rewrite, or parser-policy change.
- Review the PR independently against this plan, require green CI, merge into
  `dev`, merge the approved release to `main`, build immutable artifacts, run
  preflight, apply only any pending migration, deploy matching Web and Worker
  artifacts, and read back health, version/SHA, worker activation, and the
  operator-visible forward display.
