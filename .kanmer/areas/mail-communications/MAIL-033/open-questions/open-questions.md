# Open questions — MAIL-033

Nothing here blocks planning or implementation. Both entries below are deferred
sibling risks found while reading PR #641 against `GraphApprovedSources.cs`; each
is recorded so the reviewer sees a decision rather than an oversight.

## Parked (explicitly deferred)

- [ ] Should a sparse delta entry that omits `parentFolderId` also be tolerated? `GraphMailClient.ReadDeltaAsync` (`src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` lines 282–288) throws `UnauthorizedAccessException` for any non-removed item whose `ParentFolderId` does not equal the approved folder id exactly, so such an entry would stall the same cursor by a different route. Safe to defer: the production incident (24 identical exceptions, 2026-09-01 08:40–08:56Z) was `InvalidDataException` on `receivedDateTime`, which proves `parentFolderId` was present in every observed sparse payload; and the exact-folder assertion is a security boundary that must not be loosened on speculation. Reopens if a mailbox stalls with `UnauthorizedAccessException` from that line — then it is a new ticket, not a widening of this one.
- [ ] Could a message moved **into** the approved Inbox arrive sparse and be silently skipped? Recorded as an accepted, unconfirmed risk in the second commit of PR #641 (`c6842a8c`), whose message cites a `plan.md` that does not exist as a board document on MAIL-029 — so the provenance of that acceptance is this document from now on. Safe to defer: the alternative (fetch MIME for an entry with no received time to decide) is exactly the unnecessary MIME fetch this ticket's Approach forbids, and Graph re-emits a full representation for a genuinely new resource. Reopens if an operator reports a message present in Outlook but absent from the Inbox projection with no quarantine row — then a follow-up ticket, per the same commit.
