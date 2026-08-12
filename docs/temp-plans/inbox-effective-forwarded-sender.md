# Inbox effective forwarded sender

## Outcome

The Inbox uses the intake route's proven effective sender as the visible
sender for a normal forwarded message. It continues to preserve and display
the Collision Engineers mailbox that forwarded the message as provenance. A
message with no proven effective sender remains displayed exactly as its
retained Graph envelope supplied it.

An Audit creates automatically only when the retained email has two separate
document attachments: the Audit instruction and the original report. The
report must say exactly one literal outcome: `repairable` creates `a.` and
`total loss` creates `ap.`. A missing, conflicting, or unclear report is
`Needs sorting`; no Case/PO or reference is created and no staff confirmation
is used as a gate.

## Changes

1. Extend the retained-mail read projection with the effective sender already
   persisted on the matching mailbox intake route decision. Do not change the
   retained Graph envelope columns or create a new store/migration.
2. Render the effective sender as the Inbox row and message-detail `From`
   value. Where it differs from the retained envelope sender, show a clear
   `Forwarded by` provenance value. Thread entries remain envelope history in
   this narrow repair.
3. Classify an Audit report only from a separate retained document attachment;
   record its literal outcome as immutable system evidence before allocation.
   Missing, conflicting, or unclear reports remain `Needs sorting`.
4. Preserve an Inspection and Audit as an ordinary Inspection Case/PO. Its
   later EVA report keeps the existing manual Box-subfolder process; Pegasus
   creates no later-Audit custody work.
5. Amend the canonical requirements, capability, architecture, and domain
   wording so none says an Audit waits for staff confirmation or that an
   Inspection-and-Audit EVA report creates an automatic folder.
6. Add focused query, policy, processing, custody, and page assertions.

## Verification

- Run focused Core, allocation, Web, and Infrastructure tests, then the Release
  build. The allocation test must prove an Audit receipt creates a Case only
  with system-recorded original-report evidence and the correct `a.`/`ap.`
  reference.
- Inspect the final diff for the read-model join, presentation, evidence
  binding, and the absence of automatic Inspection-and-Audit folder work; no
  mailbox mutation or receipt rewrite.
- Review the PR independently against this plan, require green CI, merge into
  `dev`, merge the approved release to `main`, build immutable artifacts, run
  preflight, apply only any pending migration, deploy matching Web and Worker
  artifacts, and read back health, version/SHA, worker activation, and the
  operator-visible forward display.
