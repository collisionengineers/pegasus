# Research — PR-017

The tab list currently comes only from retained rows. The canonical approved-estate owner is `IApprovedIntakeMailboxes`, already injected into the Deleted source. Extend the Deleted use case/source boundary with a mailbox-list operation and have the page use it only for Deleted scope; retained Inbox/Sent tabs remain owned by `IRetainedMailQueries`. Source: `RetainedMail.cs`, `ApprovedMailboxAdministration.cs`, `Index.cshtml.cs`.

## Re-review refresh — 2026-08-20

The production path is correct, but the direct adapter test is not authenticated Web caller evidence. The existing Web test-host override convention can inject one `IDeletedMailSearchSource` fake and prove the approved zero-retained-row mailbox is rendered/selectable through `/Inbox`. This evidence is shared with [[PR-025]].
