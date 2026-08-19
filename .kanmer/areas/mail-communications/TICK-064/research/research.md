# Research — MAIL-23

## Question

How should Pegasus own one canonical mapping from the detailed taxonomy to operational destination and designated Outlook folder recommendation?

## Verified findings

- FRD-08 is the governing behavioural owner and EPIC-006 requires UI, infrastructure and Automation callers to reuse one Core implementation.
- Current repository state: MAIL-02 research proves no authoritative exhaustive mapping exists; classification categories deliberately carry neither queue nor folder, so this should be a separate Core policy consumed by MAIL-05, UI-14 and MCP.
- The previous-implementation material added to MAIL-01–04 is useful reference evidence for durable identity, fail-closed routing and append-only history, but its taxonomy/folder tree is not Pegasus authority.
- Repository implementation and local verification are activated by the operator's EPIC-006 instruction. Real Outlook, Graph or cloud mutation remains separately approval-gated.

## Implications

Reuse `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` and the existing caller/store conventions. Keep exact-message identity, classification, operational routing, folder recommendation, Case association and transport mutation as separate facts and commands. Fail closed on missing identity, ambiguity, stale versions, unauthorized actors or unsupported mailbox state.

## Acceptance direction

Focused Core tests prove policy and validation; integration tests prove persistence/concurrency and the real Web caller; no deployment or external write is claimed by local evidence.

## Folder hierarchy clarification — 2026-08-19

The prior evidence supports more Outlook destinations than the three application queues. Recovered destination purposes are: instructions, audits, diminution, new clients, case queries, enquiries, billing, pre-instructions, no-action, images, cancellations, case updates and other. These are reference evidence, not yet canonical Pegasus folder identities or exact names.

The plan must model operational queue and Outlook folder separately: a message can appear in the aggregate Other queue while receiving a specific designated folder such as Billing, Case updates or No action. MAIL-23 therefore needs an exhaustive detailed-classification → (operational destination, approved folder identity) table confirmed by the operator.
