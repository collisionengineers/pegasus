# Research — PR-017

The tab list currently comes only from retained rows. The canonical approved-estate owner is `IApprovedIntakeMailboxes`, already injected into the Deleted source. Extend the Deleted use case/source boundary with a mailbox-list operation and have the page use it only for Deleted scope; retained Inbox/Sent tabs remain owned by `IRetainedMailQueries`. Source: `RetainedMail.cs`, `ApprovedMailboxAdministration.cs`, `Index.cshtml.cs`.
