# Research — canonical repair specification

## Question

How should one canonical repair specification feed the approved assessment reports while preserving source provenance?

## Findings

1. Current assessment data stores replace-all `EstimateLines` with description, quantity, unit cost, grouping/position, confirmation actor/time, but no repair-spec identity/version/route provenance or explicit rendererref1 sections.
2. Rendererref1 assessment reports require three names-only sections: new parts, repairs, and additional operations. Monetary totals are derived separately from labour/parts/paint/specialist inputs; part numbers and per-line prices do not appear in the report.
3. FRD-06/ENG-01 requires one canonical specification per accepted purpose/version with provenance for Glass's, Audatex PDF, or approved AI proposal. Automation values remain unconfirmed until Engineer review.
4. TICK-205 resolves Audit as a deliberate exception with two role-labelled immutable accepted specification versions. That does not justify two ordinary assessment specs.
5. A versioned Core aggregate should preserve source route/artifact/version, accepted mapping, line category/order, confirmation, supersession, and deterministic totals/input snapshot. Rendering consumes an immutable accepted version; it never parses or owns estimating-source policy.
6. Existing estimate-line vocabulary and confirmation operations should be evolved/reused rather than creating a parallel renderer-only list.

## Implications

- Extend the existing Core assessment/estimate model into a versioned canonical repair specification with source provenance and accepted status.
- Map its accepted lines to rendererref1's three display sections; Core owns category vocabulary and ordering.
- Keep calculation inputs/totals separate from names-only report presentation while binding both to the same accepted specification version.
- Corrections create a new version; render snapshots retain the exact version used.
