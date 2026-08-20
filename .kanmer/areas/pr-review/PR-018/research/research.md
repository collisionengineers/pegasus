# Research — PR-018

Searchability currently collapses projection rows to filenames. Both retained attachments and search documents have stable ordinals within their owning message/receipt, but the search document row does not persist attachment ordinal. Add nullable attachment ordinal to the one projection and correlate by ordinal; null continues to identify the message body. This requires updating the existing unmerged migration, not a second migration. Source: `IntakeSearchProjection.cs`, `PegasusDbContext.cs`, `EfRetainedMailboxMessageStore.cs`.
