---
id: DOCS-016
type: ticket
title: 'Pin the EVA export''s Work Provider reporting with an assertion, not a trace'
status: backlog
area: documents-reports
assignee: ''
profile: chore
labels:
  - test-coverage
  - eva
groups:
  - EPIC-011
links:
  - AUTO-013
refs:
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
archived: false
created: '2026-08-29T22:16:36.958Z'
updated: '2026-08-29T22:16:36.958Z'
---

## What

Assert that the EVA export reports the Work Provider for a case created through
the Provider API. Today it does — but only by inspection.

## Why

[[AUTO-013]]'s first acceptance clause is "a case created through the Provider
API carries its Work Provider in the snapshot, proven by a persistence test,
**and the EVA export reports it**". The persistence half is pinned. The export
half is not: every assertion the ticket added is on the `CaseDataProjection`
returned by `ICaseDataQueries`, and none touches `EvaCaseEvidenceReader.Build`
or `EvaOperatorExport.UnrecordedFields`.

Independent verification traced the path end to end and confirmed it genuinely
works:

- `EvaHandoffStore.CreateAsync:66` reads the same `caseDataQueries` projection.
- `EvaCaseEvidenceReader.Build:49` passes `caseData.Provider.WorkProviderCode`
  into `FromCaseField`.
- `Accepted()` takes the `Fact` because `CaseDataValue.IsAccepted` covers
  `Fact`, so `FromCaseValue` returns `EvaEvidenceStatus.Accepted`.
- "Work Provider" therefore leaves `CaseEvaMapping`'s `UnrecordedFields` list.

**So this is a missing regression pin, not a broken behaviour.** It is filed
because the failure mode is silent: a future change to `Accepted()`, to the
`Fact`/`Confirmed` precedence, or to `NotableWorkProvider` would return the
export to reporting Work Provider as unrecorded for provider-API cases **with
every existing test still green** — which is the exact defect AUTO-013 exists to
close, quietly reintroduced.

## Approach

Extend the existing EVA export coverage rather than building a parallel harness.
`tests/Pegasus.Core.Tests/Qdos/CaseOperatorExportTests.cs` and
`QdosBoundaryContractTests.cs` already exercise `UnrecordedFields`; neither
currently touches Work Provider.

Assert both directions:

- a provider-API case whose snapshot carries `work_provider_code` exports it as
  `Accepted` and **not** in `UnrecordedFields`;
- a case with no work provider fact still reports it as unrecorded, so the
  assertion cannot pass vacuously.

## Verification

- [ ] An assertion fails if `work_provider_code` stops reaching the export
- [ ] The test is on the export path, not on `CaseDataProjection`
- [ ] Both directions covered, so it cannot pass vacuously

## Notes

Raised by the adversarial verification of [[AUTO-013]] (2026-08-29) as a `low`
finding, and disposed as "deferred with reason" rather than fixed in the lane —
the behaviour is correct and traced, and building the export fixture was more
than that lane's remaining scope on the eve of release 37.
