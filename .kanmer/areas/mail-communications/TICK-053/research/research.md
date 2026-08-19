# Research — MAIL-11

## Question

How should Pegasus browse, paginate, search and view exact retained messages, attachments and scoped threads including read-only Deleted Items?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: ListRetainedMail/GetRetainedMail, EfRetainedMailboxMessageStore and /Inbox already provide paged list/detail/thread over retained Inbox/Sent data; missing work is scoped body/attachment-content search, Deleted Items retention/search, richer filters and accessibility/state preservation.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/RetainedMail.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.
