# Post-implementation report — TICK-093

## Delivered

- Added one Core-owned immutable repair-specification aggregate with closed purpose/role/state/source vocabularies, stable identity and version, ordered existing estimate lines, source provenance, accepted calculation inputs/totals, Engineer acceptance, and supersession lineage.
- Added explicit start-draft, accept, correct, exact-version, and current-accepted persistence operations. They reuse the existing edit lease, expected case version, operation key, actor/reason, action history, and replay conventions.
- Kept the existing assessment edit surface compatible through the current ordinary draft. Accepted versions are immutable; corrections clone into a new draft and supersede without overwrite.
- Added a SQL migration that retains every legacy estimate line as an explicit ordinary `LegacyUnresolved` draft and fabricates neither source provenance nor acceptance.
- Updated FRD-06 narrowly and added the one Core-derived names-only mapping to the three rendererref1 display sections.
- Added focused policy, SQL lifecycle, correction, Audit-role coexistence, idempotency/history, exact-query, schema, and pre-migration fixture coverage.

## Scope and downstream ownership

This ticket proves the shared assessment aggregate only. It does not add report rendering, templates, Audit wording, percentage uplift, provider integration, or FRD-11 behavior. [[TICK-092]] owns projection of an exact accepted version into a render snapshot. [[TICK-098]] remains responsible for Audit presentation after representative template approval. [[SIMPLI-014]] owns the integrated renderer.

## Plan deviations

- The planned deterministic calculation contract originally allowed compatible accepted inputs/totals but did not authorize a VAT formula. Independent simplification found that the first implementation inferred a 20% business rule. The formula was removed; Core now validates non-negative recorded inputs and `Total = Labour + Parts + PaintMaterials + SpecialistOther + Vat`, while preserving the VAT-registration flag and calculation-policy version as provenance.
- The canonical full integration project remained active without failures beyond a proportional 25-minute local ceiling. It was stopped rather than looped indefinitely. Isolated PR CI is the authoritative full-suite completion gate before merge.
- A newer `dev` migration landed after the PR first opened. It was merged non-rebase; the only conflict was the additive ordered migration-manifest list, resolved by retaining both migrations. The combined schema/model snapshot builds and the focused schema/lifecycle/migration suite passes.

## Verification

- `dotnet restore --locked-mode` — passed.
- `dotnet build --configuration Release --no-restore` — passed, zero warnings/errors before the final additive dev merge; passed again after resolving the merge.
- Core Assessment tests — 41/41 passed.
- Focused assessment, lifecycle, legacy-migration, and combined migration-manifest SQL tests — 8/8 passed on final merged state.
- Architecture tests — 96/96 before the renderer merge; canonical merged-state run passed 97/97.
- Canonical merged-state Core suite — 639/639 passed.
- Canonical merged-state integration run — no failed assertions through the 25-minute ceiling; PR CI pending authoritative isolated completion.
- `git diff --check` — passed.
- Scope inspection against current `origin/dev` — only Assessment/Core/persistence/FRD-06 and owned tests; no Reports, renderer, template, FRD-11, or package-lock diff.

## Simplification

Independent four-lens review passed after applying both findings: remove the unauthorized VAT formula and remove an unused private draft helper. Reuse, abstraction level, test/operational efficiency, and scope otherwise required no change.

## Deployment

Not deployed. This PR targets `dev`; no cloud or `main` write was performed.
