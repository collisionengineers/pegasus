# Research — UI-10

## Question

How should Pegasus compose the full accessible email-management workspace from the canonical retained-mail queries and Core actions owned by the MAIL tickets?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: /Inbox list and exact-message pages already exist with paging, mailbox/folder filters, freshness and retained detail/thread; this ticket should integrate filters, preview, navigation and action surfaces rather than duplicate domain policies.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/RetainedMail.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.
