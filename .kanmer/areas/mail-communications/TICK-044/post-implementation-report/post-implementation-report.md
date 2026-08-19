# Post-implementation report — TICK-044

## Summary

Delivered FRD-08's canonical MAIL-02 catalogue and a versioned Core mapping that preserves every known detailed classification as an operationally distinct typed result. Receiving work, Queries and Triage carry their exact category; other known categories use `DetailedClassification` carrying the exact `MailCategory`; only a reasoned genuinely novel classification uses `Other`. Ambiguous/unclassified outcomes fail closed to Needs sorting. Outlook folder types remain separate MAIL-23 policy and no external mutation occurs.

## Changes

| File | Change | Why |
|---|---|---|
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Added exhaustive criteria/evidence/method/destination/folder catalogue and corrected every known destination to its named detailed view | Makes FRD-08 the single behavioural owner and resolves MAIL-001 |
| `docs/capabilities.md` | Updated MAIL-02 wording/evidence status | Records typed local implementation without claiming deployment |
| `docs/current-architecture.md` | Added the typed Core mapping owner | Keeps as-built state accurate |
| `MailClassificationContracts.cs` | Added canonical subtypes for every confirmed example | Prevents known mail being hidden at family level or in Other |
| `MailOperationalDestinationPolicy.cs` | Added versioned destination result carrying exact category | Preserves distinct known classifications and reserves Other for novel ones |
| `QdosMailClassificationPolicy.cs` | Emits canonical `triage-request` subtype | Keeps Triage routing typed and route-independent downstream |
| Core classification/QDOS tests | Exhaustive mapping, Other-reservation and fail-closed assertions | Proves every known family/subtype is non-Other and abstentions remain Needs sorting |

## Governing docs

The operator explicitly authorised the in-repo catalogue and confirmed that every known example must be a separate classification. FRD-08 was amended in place. The correction implements the checked TICK-044/TICK-057 decision and MAIL-001 acceptance without creating a competing policy document or ADR.

## Risks / follow-ups

UI-14 and MAIL-23 remain the concrete consumers of the typed result. This ticket adds neither duplicate persistence nor Outlook mutation. Historical decisions are not silently reinterpreted. No live mailbox/cloud operation ran.

## Verification hand-off

Run `dotnet restore`, Release build, full Core tests, and the focused taxonomy/destination/QDOS filter. Latest implementation evidence: Release build 0 warnings/errors; full Core suite 616/616; focused suite 78/78. Verify specifically that every non-Other taxonomy member maps to ReceivingWork, Queries, Triage, or DetailedClassification with the exact category; only reasoned novel Other maps to Other; Ambiguous/Unclassified map to NeedsSorting with no category.
