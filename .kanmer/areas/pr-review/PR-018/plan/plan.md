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

## Re-review completion plan

1. Reuse the existing display-reader attachment enumeration but replace its nameless `continue` with a deterministic `Unnamed attachment N` label, preserving every occurrence in order.
2. Add a Search content column to the existing retained attachment table using `RetainedMailAttachment.IsSearchable`.
3. Prove a nameless attachment before a named attachment does not shift the named occurrence, and prove per-row disclosure in authenticated detail.
4. Run focused checks and update four-lens/PIR evidence.

Estimated incremental diff: four existing files, under 100 lines.

## Governing docs

FRD-08's honest per-attachment searchable/unsupported disclosure is rendered without changing retention history.

## Re-review simplification pass — 2026-08-20

- Reuse: the display reader now materializes its existing attachment enumeration once for names and rows; the existing IsSearchable value is rendered through one shared label helper.
- Simplification: preserved nameless occurrences with a deterministic display label; no second identity scheme, parser, store, or backfill.
- Efficiency: one materialized attachment list replaces two enumerations.
- Altitude: parsing stays in Infrastructure and wording stays in Web presentation.

## Final completion plan

Estimated incremental diff: two existing files, under 50 lines.

1. Reuse the canonical reader's existing unsupported-attachment descriptor path for attached `TextPart` entities while retaining the early return for ordinary body text.
2. Extend the existing cross-reader occurrence test with attached `text/plain` before the later named part.
3. Run focused tests and record the final four-lens/PIR evidence.

## Governing docs

FRD-08's exact per-attachment disclosure is preserved with the existing ordinal and one parser/store.

## Final simplification pass — 2026-08-20

- Reuse: attached text uses the canonical reader's existing unsupported attachment descriptor path and ordinal list.
- Simplification: one condition distinguishes attached text from ordinary body text; no text parser, identity, schema, or store was added.
- Efficiency: the part is described without decoding/indexing it a second time.
- Altitude: MIME occurrence identity remains wholly in the canonical Infrastructure reader.
