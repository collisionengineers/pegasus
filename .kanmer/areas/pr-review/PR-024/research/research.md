# Research

## Verified

`EfRetainedMailboxMessageStore.ListAsync` admits a row when any receipt search document text matches. That includes the root document (`AttachmentFileName == null`). `AddSearchMatchesAsync` only turns retained `BodyPlainText` into `MessageBody` and only turns named projection rows into attachment-content matches. A staff-forward or other canonical/display-body difference can therefore return a row with no visible `Matches` entry.

## Implication

Retained body search must be owned by the retained display body, because that is what the authenticated page displays. Restrict the receipt projection admission predicate to attachment rows. Deleted search may continue using its canonical root document because it renders that same canonical body. No schema/parser/store/backfill change is needed.
