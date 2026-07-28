# P80 — conformance, release and CollisionSpike integration

## Scope

Own programme-wide verification systems, packaging evidence and the production-caller boundary. A green build alone is not release evidence.

## Owned units

- `EXT-QA-001` unit, specification-conformance, semantic-differential and genuine-data harness.
- `EXT-QA-002` security, fuzz/property and hostile-input regression system.
- `EXT-QA-003` performance, memory, expansion, nesting and concurrency evidence.
- `EXT-PKG-001` dependency review, SBOM, packaging, versioning, update and rollback.
- `EXT-INT-001` CollisionSpike Infrastructure adapter and caller-backed cohort/holdout evidence.

## Required outputs

- Deterministic offline repository checks and separately authorised opt-in oracle, corpus and performance lanes.
- Manifested fixtures with licences, source hashes, feature tags and expected outcomes.
- Per-format semantic comparators and explained deviation records.
- Reproducible host/toolchain/corpus manifests and retained local diagnostics without sensitive content.
- Package/dependency/licence/security review, SBOM, schema/version support and rollback plan.
- Framework-dependent library/CLI smoke packages plus separately gated per-RID self-contained, single-file or Native AOT variants when authorised.
- CollisionSpike adapter tests proving engine-neutral translation and policy ownership remains in Core.

## Exit evidence

The declared release subset passes unit, conformance, differential, security, fuzz, resource, performance and deterministic retry gates. Operator-reviewed cohorts and independent holdouts pass without silent identity-critical truncation. The real intended caller reaches the library before `Called`; authorised review is required before `Accepted`.
