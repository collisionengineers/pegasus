# Research — accepted structured source record

## Question

What existing Core data can be the single accepted source for deterministic report generation, and what is missing?

## Findings

1. Pegasus already has separate accepted/confirmed case data and assessment projections. Case data models retain fact/suggestion/confirmed source information and completeness evaluation; assessment fields and estimate lines retain confirmation actor/time and expose `AssessmentPolicy.EvaluateReadiness`.
2. Assessment readiness already names report-content requirements including history check, engineer name/qualifications/signature, agreed fee, vehicle/economic fields, and rejects unconfirmed fields/estimate lines. This is the existing policy to extend, not duplicate.
3. Current assessment projection is a flexible keyed field/estimate-line model shaped to rendererref1 paths, but it is not yet a versioned report payload snapshot. It lacks a durable accepted-source version/hash tied to a render attempt and artifact.
4. Core lifecycle already separates Report preparation, report approval, exact Sent evidence, and Post-report. Generation must fit inside Report preparation and must not imply approval/sending.
5. Rendererref1 requires more than generic “complete”: exact outcome-specific fields, case/principal/report reference data, incident facts, assessment method/location, repair specification categories, photos, engineer identity/signature, and fee. Readiness must be template/outcome-specific and use accepted values only.
6. One render input snapshot should compose current accepted case data + confirmed assessment + current document/photo custody identities at one version boundary. Copying data into a second editable report record would create conflicting owners.
7. Corrections create a new accepted input snapshot/report version; earlier snapshots/artifacts remain immutable.

## Implications

- Reuse and extend `AssessmentPolicy.EvaluateReadiness` and accepted case-data projections as the single policy/data source.
- Add a Core report-input snapshot/identity that records source aggregate versions and deterministic payload hash at request time; it is derived, not independently editable.
- Fail closed if any required value is missing, unconfirmed, ambiguous, stale, mismatched to outcome, or lacks custody.
- DOCS-001 owns atomic creation/idempotency of render request/reference from this snapshot.
