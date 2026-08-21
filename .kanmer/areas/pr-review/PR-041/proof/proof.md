# Proof

**Shipped:** PR #477, merge `e4d56d9e` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

> A successfully moved message must leave Inbox and remain findable. PR #477 only adds an
> Inbox `NOT EXISTS` filter, so the moved item disappears from every list and search entry
> point and is reachable only by a retained direct URL.
>
> Do **not** create a second search store or folder taxonomy.

## Verified in the shipped code

The overlay went into the canonical query owner, not a new one:
`EfRetainedMailboxMessageStore` (the single retained-mail query store) resolves
`CurrentFolderType` onto the retained-mail record (`RetainedMail.cs:61`), and the existing
`MailLogicalFolders` / `MailLogicalFolderPolicy` vocabulary supplies the labels
(`:599`). There is one folder taxonomy and one search store; `grep` for a second
`IRetainedMailQueries` implementation or a parallel search store returns none.

The constraint in the finding is the part worth recording: the smallest fix was an overlay
onto the existing query, and that is what shipped. A second store would have satisfied the
functional bullet and broken the architectural one.

A moved message is excluded from Inbox by current location rather than by a `NOT EXISTS`
subquery, and included in its destination scope by the same value — one field driving both,
so the two cannot disagree and the message cannot be listed twice.

## Not claimed

Inbox exclusion, destination inclusion, paging and mailbox scoping are covered by the
shipped tests (`RetainedMailPersistenceTests`, 29 facts; `MailWorkspaceWebTests`). No live
message has been moved and re-found in production, and this proof does not claim one has.
