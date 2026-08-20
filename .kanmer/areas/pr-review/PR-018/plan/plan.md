# Plan — PR-018

## Approach
Add nullable `AttachmentOrdinal` to `IntakeSearchDocument` and its existing unmerged table. Projection ordinals follow canonical attachment asset order; retained and Deleted detail map by ordinal, while display match labels may retain filenames. Estimate: 8 existing/generated files, about 120 hand-written lines.

## Governing docs
FRD-08's per-attachment searchable/unsupported disclosure becomes exact without adding a second projection or identity scheme.

## Steps
1. Carry and persist ordinal in the existing projection/migration.
2. Correlate retained/Deleted searchability by ordinal and prove duplicate filenames; simplify.

## Simplification pass — 2026-08-20

- Reuse: applied — canonical reader attachment descriptors, the single search projection, existing retained attachment ordinals, and the unmerged migration are extended.
- Simplification: one nullable ordinal carries exact identity; no second parser, projection, table, or backfill.
- Efficiency: ordinal sets replace filename sets and preserve the existing query shape.
- Altitude: identity is produced by Core's projection contract, persisted by Infrastructure, and only labelled by Web.
