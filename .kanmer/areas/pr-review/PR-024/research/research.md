# Research

## Verified

`EfRetainedMailboxMessageStore.ListAsync` admits a row when any receipt search document text matches. That includes the root document (`AttachmentFileName == null`). `AddSearchMatchesAsync` only turns retained `BodyPlainText` into `MessageBody` and only turns named projection rows into attachment-content matches. A staff-forward or other canonical/display-body difference can therefore return a row with no visible `Matches` entry.

## Implication

Retained body search must be owned by the retained display body, because that is what the authenticated page displays. Restrict the receipt projection admission predicate to attachment rows. Deleted search may continue using its canonical root document because it renders that same canonical body. No schema/parser/store/backfill change is needed.

## Final re-review refresh — 2026-08-20

The previous root-projection exclusion fixed empty match labels but raw retained `BodyPlainText` still differs from the body detail renders after `StaffForwardBodyCleaner`. The existing receipt-owned root search document can be normalized once from the canonical reader plus its route decision, then serve as both retained body-search input and displayed detail body. For an attached original, the effective sender's source label identifies the nested body; for an inline forward the existing cleaner removes the wrapper. Historical rows without this projection retain the existing cleaned-detail fallback and are not reconstructed. Sources: `ProcessIntake`, `IntakeSearchProjection`, `EfRetainedMailboxMessageStore`.
