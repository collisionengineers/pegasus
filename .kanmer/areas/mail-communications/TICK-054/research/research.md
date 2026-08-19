# Research — MAIL-13

## Question

How should Pegasus perform separately authorised exact-message Outlook state/category/flag/delete mutations through Core-owned commands with durable attribution?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: The current workspace is intentionally read-only and GraphApprovedSources is an intake reader; no mutation port exists. FRD-08 says UI-10 itself does not change read state, so this later capability must remain a separate action surface.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/RetainedMail.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.
