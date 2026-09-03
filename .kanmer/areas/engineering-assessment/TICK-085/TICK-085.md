---
id: TICK-085
type: ticket
title: Complete Glass's repair-estimate import from a representative export
status: backlog
area: engineering-assessment
order: 1140
assignee: ''
profile: feature
labels:
  - capability
  - EXT-12
  - requires-live-approval
  - now
  - evidence-required
  - samples-supplied
groups:
  - HZN-002
  - EPIC-009
  - EPIC-011
links:
  - ENG-002
blocks: []
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
archived: false
created: '2026-08-12T15:05:40.242Z'
updated: '2026-09-03T15:15:29.297Z'
---

## What

Complete Glass's repair-estimate import from the five representative calculation PDFs supplied in the adjacent `pegasus-work-pack`.

## Why

EXT-12 is allocated to Now / 0.1.0-alpha.1. [[ENG-002]] delivered the Audatex custody/import landing, and the earlier blocker was the absence of genuine Glass's evidence. That evidence now exists: four samples have usable embedded text and one visually valid sample (YL69YFO) has an unusable embedded character map and must use the approved Azure OCR path.

## Evidence supplied

- `1046012231790__VX21TZD calculation sheet.pdf`
- `1313339771083__LT72PYX Calculation....pdf`
- `1710254173321__Calculation ML23 OXR.pdf`
- `1952640666665__YL69YFO CALCULATION SHEET.pdf`
- `2228602993671__CalculationPDF.pdf`

The work pack records immutable SHA-256 values, structural observations and reviewed oracles. It is local evidence and must not be copied into tracked source or tests.

## Approach

- Reuse the existing EXT-12 custody, source-version and draft repair-specification path.
- Add explicit source/format routing so a Glass's document is never accepted by the Audatex parser.
- Reconcile the assessment-relevant set only: source/version, vehicle and claim identifiers, Body/Auxiliary/Paint lines, rates, time, materials, notes and printed totals. Addresses and diagrams remain in the source PDF.
- Use embedded deterministic extraction for the four readable files.
- Use [[TICK-041]] and [[PLAT-065]] for the YL69YFO Azure Document Intelligence fallback.
- Reject the whole import on missing, ambiguous, low-confidence or internally inconsistent evidence.
- Leave estimate-to-report comparison/savings with EXT-09 and keep direct vendor-service links out of scope.

## Verification

- [ ] All five supplied Glass's samples match their reviewed oracles.
- [ ] Retained source, parsed version, draft lines and OCR result where applicable are hash/provenance-backed.
- [ ] Repeated-page totals and continuation sections are not double-counted.
- [ ] Unsupported, ambiguous or low-confidence variants fail closed without partial lines.
- [ ] No direct Glass's API or launch control is introduced.

## Outcome
