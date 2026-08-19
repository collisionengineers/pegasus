# Research — canonical repair specification

## Question

How should one canonical repair specification feed assessment and Audit/Inspection reports while preserving source provenance?

## Corrected operator authority

The 2026-08-19 operator correction recorded in [[TICK-205]] supersedes the earlier dual-role premise:

- Audit and Inspection use the same physical report.
- Audit differs only in internal workflow/reference identity: normal Case/PO plus `a.{Case/PO}` or `ap.{Case/PO}`.
- Audit does not require conservative/maximised repair specifications, a dual-specification aggregate, or uplift.
- One canonical accepted repair specification is shared.

TICK-205's older plan/PIR/proof are stale evidence. Its later Outcome and resolved open question are the governing correction. [[PR-011]] identifies every TICK-093 layer that must be reconciled.

## Findings

1. Current accepted scope still requires stable specification identity/version, ordered existing estimate lines, source route/artifact/version/hash, accepted calculation inputs/totals, Engineer acceptance, immutable accepted versions, reasoned supersession, exact-version retrieval, and explicit `LegacyUnresolved` migration.
2. Purpose/role columns and `OrdinaryAssessment`/`Audit`/`Ordinary`/`Conservative`/`Maximised` vocabulary exist only to support the now-rejected branching model. One canonical specification needs none of them.
3. The minimal uniqueness rules are one current accepted version per case, one monotonically increasing version number per case, and one creation operation key per case.
4. The existing estimate-line vocabulary remains the single calculation truth. Its one Core-derived names-only mapping to new parts, repairs, and additional operations remains authorized for the shared report path.
5. The PR is unmerged, so its branch-owned migration may be amended safely. Shared migrations already on `dev` must not be rewritten.

## Implications

- Remove Audit-only and generic purpose/role types from Core requests, aggregate records, policy, persistence entity/schema/migration/snapshot, queries, tests, and FRD-06.
- Retain one case-scoped draft/accepted/correction history and the exact accepted-version query needed by [[TICK-092]].
- Do not add Audit-specific data, uplift, templates, rendering, or presentation.
