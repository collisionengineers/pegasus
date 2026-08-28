---
id: INTK-049
type: ticket
title: Resolve OCR O/0 registration ambiguity through DVLA/DVSA lookup
status: preparing
area: intake-processing
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-28T20:37:55.677Z'
labels:
  - vehicle-registration
  - ocr
  - dvla-dvsa
  - edge-case
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-28T20:37:48.985Z'
updated: '2026-08-28T20:37:55.677Z'
---

# Resolve OCR O/0 registration ambiguity through DVLA/DVSA lookup

## What

When a vehicle registration originates from vehicle-image recognition or document OCR and contains `O` or `0`, use DVLA/DVSA evidence to check the opposing `O`/`0` combinations rather than treating the first machine reading as definitive.

The resolution must remain bounded, deterministic, provenance-preserving, and fail closed: accept a corrected registration only when provider evidence identifies exactly one viable combination. Preserve the machine-read registration and every attempted candidate/result as evidence. If no combination or multiple combinations are viable, do not silently choose one.

This behavior applies only to machine-read registrations from the two named routes. It must not reinterpret a staff-confirmed or ordinary instruction-extracted registration.

## Why

OCR and image recognition commonly confuse the letter `O` with the digit `0`. Looking up only the literal read can miss the real vehicle or enrich the wrong identity even though the approved DVLA/DVSA sources can disambiguate it.

## Acceptance

- Vehicle-image and document-OCR registrations containing `O` or `0` produce a bounded set of distinct opposing combinations for lookup.
- The original candidate is checked and retained; provider evidence for all attempted candidates retains registration, source, outcome, response identity, and time.
- Exactly one viable provider-backed candidate becomes the resolved registration used by the case workflow.
- Zero or multiple viable candidates withhold automatic resolution and expose an honest review/unavailable outcome without creating or enriching the wrong case.
- Staff-confirmed and non-OCR instruction registrations keep the existing exact-registration behavior.
- Reconciliation is idempotent and does not repeatedly enqueue combinations already attempted for the case and machine reading.
- Core and integration tests cover single-position, multiple-position, no-match, ambiguous-match, retry/unavailable, provenance, and route-scope cases.

## Outcome
