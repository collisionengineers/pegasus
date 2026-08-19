# Post-implementation report — TICK-044

## Summary

Delivered the canonical MAIL-02 catalogue and a versioned, pure Core mapping from every settled detailed classification to Receiving work, Queries, Other, Needs sorting, or Triage. Known examples now have explicit subtype identities, ambiguity/unclassified outcomes fail closed, and Outlook folder types remain a separate MAIL-23 concern with no external mutation.

## Changes

| File | Change | Why |
|---|---|---|
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Added exhaustive criteria, evidence/method, destination, and folder catalogue | Makes FRD-08 the single behavioural owner requested by the operator |
| `docs/capabilities.md` | Updated MAIL-02 evidence status and canonical anchor | Records local implementation without claiming caller/deployment |
| `docs/current-architecture.md` | Added the Core mapping policy owner | Keeps the as-built snapshot current |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | Gave every confirmed example a canonical subtype | Prevents named classes being hidden inside family-level or Other buckets |
| `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` | Added versioned fail-closed destination mapping | Establishes one Core owner without duplicate persistence |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs` | Emits `triage-request` for the accepted QDOS tell | Lets routing consume a named category rather than provider predicate internals |
| `tests/Pegasus.Core.Tests/Intake/Classification/*` | Exhaustive taxonomy and destination tests | Covers every registered subtype/family, Sent, Other, Ambiguous and Unclassified |
| `tests/Pegasus.Core.Tests/Intake/Qdos/QdosMailClassificationPolicyTests.cs` | Updated Triage assertions | Proves the existing route emits the canonical subtype |

## Governing docs

The operator explicitly requested an in-repo breakdown and authorised the category/folder decisions. FRD-08 was therefore extended in place; no competing document or ADR was created. The implementation preserves FRD-08's separation of classification, application destination, Triage, and Outlook folder movement, plus its append-only correction and fail-closed rules. Design behaviour and Outlook mutation are untouched.

## Risks / follow-ups

UI-14 and MAIL-23 are the two concrete downstream callers; this ticket deliberately does not add a partial retained-mail projection, database column, or Outlook mutation. Existing historical pre-instruction decisions without the new subtype remain valid classification history; a later explicit approved re-evaluation may enrich them, but this change never silently rewrites history. No live mailbox or cloud operation ran.

## Verification hand-off

On merged `dev`/promotion target, run:

- `dotnet restore`
- `dotnet build --configuration Release --no-restore` — expect zero warnings/errors
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --no-restore` — expect all Core tests green
- focused filter `FullyQualifiedName~MailTaxonomyTests|FullyQualifiedName~MailOperationalDestinationPolicyTests|FullyQualifiedName~QdosMailClassificationPolicyTests`

Implementation evidence: Release build succeeded with 0 warnings and 0 errors; full Core suite passed 615/615; focused suite passed 78/78.
