# ADR-0006: Provider-neutral intake with a contained QDOS policy

- Status: Accepted for the pre-release local intake slice
- Date: 2026-07-24
- Owners: Alex and the CollisionSpike v2 development team
- Supersedes: ADR-0005 decision 1 only

## Context

The first usable release must prove full functionality for one principal, QDOS.
The current local slice took that release cohort too far into the reusable intake
spine: generic receipt identity, multi-format reading, evidence, assets,
persistence, routes, queues, and future caller guidance were all QDOS-named.

QDOS-specific recognition and field extraction are intentional. A QDOS-shaped
transport and storage model is not. Case identity and workflow are uniform across
principals, and ADR-0001 requires provider extraction rules to remain isolated
behind a common contract.

No v2 database or deployment had entered live use when this decision was made.
The existing local database was disposable development evidence rather than a
compatibility boundary.

## Decision

1. `ProcessIntake` is the single Core intake use case. It owns source-occurrence
   identity, required original-source retention, reader outcomes, persistence,
   and the final pre-case processing result.
2. The multi-format reader, artifact store, receipt/query store, evidence,
   assets, typed instruction draft, Web route, feature flag, and queue language
   are provider-neutral.
3. One concrete `QdosInstructionExtractionPolicy` owns the accepted QDOS marker,
   label, precedence, typed-suggestion, and policy-version rules. It returns
   evidence-backed `Applicable`, `NotApplicable`, or `Indeterminate` assessment
   plus neutral field candidates. It does not categorise a mailbox item, accept
   a case, allocate a reference, or resolve another provider.
4. The QDOS policy runs only after a source is fully readable. Positive QDOS
   content evidence may create one `InstructionDraft` with
   `SuggestedPrincipalCode = "QDOS"`. QDOS in a filename or sender alone is not
   sufficient. Readable non-QDOS or indeterminate material has no principal
   suggestion and remains in `Needs sorting`; unsupported and incomplete reader
   outcomes retain their distinct fail-closed results.
5. Intake persistence uses provider-neutral tables and explicit stable stored
   codes with versioned JSON envelopes. CLR names are not durable values;
   unknown codes or envelope versions fail visibly. The selected extraction
   policy key and version are retained with the receipt.
6. Original source retention is required before a reviewable receipt is stored.
   A retention failure is retryable and does not claim custody in SQL. A later
   SQL failure may leave unreferenced content-addressed local bytes; an
   idempotent retry reuses them and this slice does not delete them.
7. Because the application is pre-release with no live v2 schema to preserve,
   the existing migration chain is replaced by one provider-neutral initial
   migration. Development SQLite uses a new default database path and a strict
   baseline guard; the prior local database is left untouched. Non-Development
   startup never applies migrations, and SQL Server migrations remain an
   explicit release/test operation.
8. The Development-only caller is `/Intake/Upload`, guarded by
   `Features:LocalIntake`. `/Intake/Qdos` is removed rather than retained as a
   second entry point.

No provider registry, rules engine, second policy, mailbox classifier, provider
table, dormant transport, or compatibility route is introduced.

## Consequences

- QDOS remains the sole automatic instruction-extraction policy and first-release
  acceptance cohort, but it is never a fallback principal.
- Generic transport, provenance, storage, and review contracts can be reused by
  later authorised Web, Worker, provider API, and MCP callers without copying the
  QDOS policy.
- Existing disposable local databases are not upgraded. Rollback restores the
  previous application binary and previous database path; neither database nor
  retained artifacts is deleted or down-migrated.
- ADR-0003 remains the accepted QDOS/PdfPig evidence record. ADR-0005 remains
  accepted for multi-format bounds, asset provenance, and OCR-candidate rules;
  only its `ProcessQdosIntake` ownership/name decision is superseded.

## Limits and deferred-capability impact

This decision does not implement another principal, mailbox categorisation,
Graph delivery, the Worker trigger, provider API, MCP, authentication, case
acceptance/reference allocation, Blob/Box custody, OCR execution, EVA changes,
DOC/MSG extraction, or Azure resources.

Neutral source identity, receipt provenance, instruction fields, and reader and
artifact ports preserve later mailbox coverage, WhatsApp coexistence, guided
capture, AI/vision, malware scanning, external accounts, and alternative
infrastructure adapters. `InstructionDraft` preserves a pre-case boundary for
later EVA replacement/API use, estimating and valuation, and Diminution or
Commercial cases. Each capability still requires its own approved policy,
caller, evidence, permissions or licence, and any necessary additive migration.
A second provider additionally requires approved extraction rules, genuine
evidence, and an explicit policy-selection decision; none is guessed here.
