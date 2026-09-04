---
id: INTK-049
type: ticket
title: Resolve machine-read UK registration character ambiguity through DVLA/DVSA
status: preparing
area: intake-processing
order: 70
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-28T20:37:55.677Z'
labels:
  - vehicle-registration
  - ocr
  - dvla-dvsa
  - character-ambiguity
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-28T20:37:48.985Z'
updated: '2026-09-04T11:33:07.327Z'
---

# Resolve machine-read UK registration character ambiguity through DVLA/DVSA

## What

When a vehicle registration originates from vehicle-image recognition or
document OCR and contains a supported ambiguous character, derive only
structurally valid UK registration candidates and use DVLA/DVSA evidence to
resolve the reading. The initial supported confusion map is `O` ↔ `0` and
`I` ↔ `1`; adding another pair requires real corpus or production evidence.

The policy covers the GB current, prefix, suffix and dateless formats and
Northern Ireland registrations accepted by the UK provider route. Republic of
Ireland and other European formats are outside this ticket.

Resolution remains bounded, deterministic, provenance-preserving and fail
closed. Preserve the raw machine reading and every attempted candidate/result.
Accept a registration only when exactly one candidate has viable provider
evidence and every other candidate conclusively returns not found. Do not
silently choose when no candidate or multiple candidates are viable, or while
any candidate is unresolved.

This behavior applies only to machine-read registrations from the two named
routes. It must not reinterpret a staff-confirmed registration, an ordinary
embedded-text instruction value, case search, or the existing confirmed-case
image matching rules.

## Why

OCR and image recognition can confuse visually similar letters and digits.
Looking up only the literal read can miss the real vehicle or enrich the wrong
identity even though the approved DVLA/DVSA sources can disambiguate it.

## Acceptance

- Vehicle-image and document-OCR reads containing `O`, `0`, `I` or `1`
  produce at most eight distinct, structurally valid UK candidates in stable
  order, with the valid original first.
- The supported confusion map has one Core owner and contains only `O` ↔
  `0` and `I` ↔ `1`.
- Provider evidence for every attempted candidate retains registration, source,
  typed outcome, response identity and time.
- Exactly one `Current`, `Stale` or `Partial` candidate becomes the
  resolved registration only after every other candidate is conclusively
  `NotFound`.
- Zero or multiple viable candidates, and any failed, throttled, unavailable or
  otherwise unresolved candidate, withhold automatic resolution without
  creating or enriching the wrong case.
- GB current, prefix, suffix and dateless registrations and Northern Ireland
  registrations are in scope; Republic of Ireland and other European formats
  are not sent through this UK-only correction policy.
- Staff-confirmed, embedded-text instruction and ordinary Case registrations
  keep exact-registration behavior. Existing confirmed-case image matching,
  including its plate-furniture rule, is unchanged.
- Durable intake-owned work is idempotent and does not enqueue a candidate
  already attempted for the same source evidence and machine reading.
- Core and integration tests cover O/0, I/1, mixed and multiple positions,
  supported formats, invalid/foreign shapes, no-match, ambiguous-match,
  retry/unavailable, provenance and route scope.
- [[TICK-041]] supplies the real document-OCR caller before this ticket is taken
  for implementation; no partial or dormant document path is shipped.

## Outcome
