# Change: Adopt Azure Workflow repository standard

```yaml
id: 2026-07-27-azure-workflow-onboarding
type: onboarding
status: complete
risk: standard
created: 2026-07-27
updated: 2026-07-27
issue: none
pull_request: https://github.com/collisionengineers/collisionspike_v2/pull/2
baseline: 8c3919c81bf4117cbd8f4e4aa2e85ac29ce1f8ce
target_release: unallocated
roadmap_horizon: Now
mode: development
supersedes: none
superseded_by: none
```

## Summary

Convert the existing Azure-oriented repository from its removed local plugin
suite to the portable Azure Workflow standard without changing product rules,
application behavior, data, or Azure resources. The result must retain material
repository truth, expose one documentation spine and work taxonomy, adapt
the supplied Collision Engineers design foundation without duplicating its
marketing system, and end as a green independently reviewed pull request.

## Scope

### Included

- Canonical repository, product, capability, roadmap, architecture, operations,
  design, decision, change-record, and agent-mistake routes.
- Exact conversion of all 213 stable feature identities into one capability
  inventory while retaining their controlled source and plan owners.
- Azure Workflow agent routes, issue forms, pull-request template, proportional
  repository checks, and removal of dead local-plugin validation.
- Four `type:*` labels and one linked user-owned GitHub Project with portable
  Status, Priority, and Horizon fields.
- Adapted Collision Engineers brand essentials and exact master-logo asset.
- Consolidation of 22 fragmented operator-note files into 17 indexed canonical
  files across business process, product requirements, and systems/integrations,
  with old-to-new provenance and retained-authority assertions.
- Conversion of all 55 former `docs/plans/` artifacts: 14 active authorities
  moved to product, design, and testing owners, and 41 superseded artifacts
  retained under an explicitly non-authoritative history archive.

### Excluded

- Product implementation, schema/API changes, a selected `0.1.0-alpha.1` shell, synthetic
  operational examples, feature issue generation, and release allocation.
- Azure reads, deployments, credentials, resource mutations, and live-state
  claims.
- The supplied marketing website, document/letterhead system, photography,
  signatures, font bundle, previews, WhatsApp treatment, and mobile navigation.

## Authorities, current state, and constraints

- Authorities: current user direction, [operator notes](../operator-notes.md),
  canonical [product requirements](../requirements.md), retained
  [questionnaire](../history/product/project-discovery-questionnaire.md) and
  [feature-source](../history/product/feature-versioning-worksheet.md) evidence,
  accepted historical ADRs, current code/tests/IaC, and supplied design evidence
  in the order declared by [the documentation owner](../index.md).
- Current implementation: the only mutating product entry point is the
  Development-only Web `/Intake/Upload` route calling the Core intake policy;
  the Worker has no trigger or Core caller. Onboarding does not change either.
- Constraints: Windows/PowerShell 7, immutable `corpus/`, preservation of every
  material operator statement, one Core policy owner, one documentation/work
  owner per concern, no generated status ledger, and explicit approval for
  every Azure read/write.
- Baseline: local `main` at `8c3919c`, one commit ahead of `origin/main`; that
  preceding commit removes obsolete repository-local MCP declarations and is
  part of the pull-request ancestry.
- Conflicts: none. The user explicitly selected Azure Workflow and explicitly
  directed design adaptation from `collision-engineers-design-dev` with excess
  removed, then granted Azure Workflow full authority over repository
  documentation and organization, including operator notes. Existing unresolved
  product questions remain out of scope.

## Acceptance criteria

- One discoverable portable authority spine and exact Azure Workflow routes.
- All 213 capability IDs, outcomes, horizons, release values, and owner links
  validate without inventing delivery state.
- Required issue forms, PR sections, change record, ADR, and proportional CI
  are structurally enforced by the repository-owned check.
- Design authority retains only shared application essentials and one
  checksum-matched master logo; the 60-file source pack is absent from the final
  tree.
- `docs/plans/` no longer exists as a parallel work database; every former
  artifact has one recorded canonical destination or historical destination,
  and all 213 capability rows link to current product-area owners.
- Existing application, corpus, Azure/IaC, operator meaning, and product
  behavior remain unchanged.
- GitHub labels and Project fields read back exactly, and the exact pull-request
  head passes CI and independent review.

## Plan

1. Inventory repository/GitHub/toolchain state and reconcile authority and
   feature identities.
2. Establish the portable docs/design/decision/change-record spine and route
   agents from `AGENTS.md`.
3. Replace dead workflow checks with proportional Docs/Full validation while
   preserving the existing application harness as the Full lane.
4. Configure and read back the bounded GitHub taxonomy and delivery Project.
5. Consolidate the fragmented operator-note tree under explicit user authority,
   repair every incoming link, record source-to-destination provenance, and add
   retained-authority assertions.
6. Convert the former plan tree into current product, design, and testing
   authorities plus a marked history archive, preserving artifact-count parity
   and replacing plan-pack ownership across all capability rows.
7. Verify invariants, commit scoped paths, publish a draft pull request, wait
   for exact-head checks, independently review, and remediate required findings.

## Data, failure, and recovery

- Data/schema: none; no domain model, persistence, migration, API, or external
  contract changes.
- Failure behavior: unknown/mixed CI diffs select Full validation; malformed
  capability, issue-form, ADR, change-record, link, route, or path state fails
  closed. Missing LocalDB prevents local Full completion rather than weakening
  the check.
- Recovery/rollback: the pre-onboarding baseline is `8c3919c`; scoped commits
  can be reverted through a reviewed pull request. The source design bundle
  remains recoverable from intermediate commit `9af3733`, while the retained
  logo is independently checksum-proven.

## UI/UX contract

No runtime UI is changed. `design/` adapts the supplied foundation for a future
internal case-management surface: exact gear-C logo, Collision red, warm
charcoal/ink neutrals, system UI sans, 4px rhythm, 2px corners, border-first
depth, visible focus, and Lucide-only icons. It excludes marketing/document
layouts, imagery, signatures, fonts, web motion, WhatsApp, and mobile-product
patterns. Current CSS differences are recorded, not silently declared aligned.

Deferred-capability impact: `0.1.0-alpha.1` shell selection and every `Next`/`unallocated` and `Later`/`unallocated` UI
capability remain deferred. Existing stable capability IDs and the approved
design-to-runtime seam are preserved; no dormant dependency or alternate UI is
built. Activation requires a selected UI change, caller/source mapping,
accessibility/responsiveness proof, and reviewed runtime reconciliation. The
only copied binary is the exact source logo, so no irreversible design choice is
introduced.

## Azure impact

None. No Azure read, mutation, deployment, credential use, or live-state claim
was authorized or performed. The dated inventory remains evidence only; a
future operation must use `$azure-workflow:operate-azure-repository` with exact
scope and separate approval.

## Decisions and conflicts

- This onboarding replaced the repository-local plugin workflow. This completed
  change record, rather than a durable ADR, retains that migration provenance.
- Rejected: restoring the removed plugin suite, keeping a second task database,
  generating one issue per capability, importing the complete design bundle,
  treating a registered Worker as a caller, or claiming dated Azure inventory
  is current.
- Current .NET currency was checked against Microsoft on 2026-07-27: .NET 10 is
  active LTS and Functions 4.x supports .NET 10 isolated at dependency minimums
  below the repository's Worker packages. This is drift-prone operations
  evidence, not a product decision.
- GitHub Actions release readback on 2026-07-27 found `actions/checkout@v7`
  and `actions/setup-dotnet@v6` current. They replace v4 pins after exact-head
  CI reported the Node 20 deprecation; behavior remains the same Full gate.
- Unresolved onboarding decisions: none.

## Implementation

- Status: complete. Repository/GitHub conversion, operator consolidation, and
  plan-tree conversion are implemented, verified, published, and independently
  reviewed with no required findings.
- Deviations: none.
- Recovery actions: the first Project-link attempt using literal `@me` was
  rejected by the CLI; retry with explicit owner `collisionengineers` succeeded.
  GitHub protected the built-in Status field from deletion, so its options were
  updated in place through the supported GraphQL mutation. The first Project
  item-add command returned success without an item; direct supported GraphQL
  registration succeeded and read back PR 2 as `In review`, `P2 Normal`, `Now`.
  The first CI attempt was blocked before any step by private-repository account
  billing. The user explicitly authorized public visibility; readback confirmed
  `PUBLIC`, and rerun attempt 2 reached the Full validation step.
  Full CI on `4ac1cf2` passed but warned that both v4 JavaScript actions target
  deprecated Node 20; current-major remediation was applied before review.
  CI on `4bbe176` was cancelled as obsolete after the user expanded onboarding
  scope to include conversion of the complete `docs/plans/` tree.

## Verification

| Check | Scope | Expected | Observed |
| --- | --- | --- | --- |
| Docs repository check | structure, docs, forms, routes, records | green | green after plan conversion: 153 Markdown files, 1,101 local links, 213 exact feature triples, 41 archived plan artifacts, 21 assertions |
| Design reduction | supplied pack versus retained authority | one exact logo; no duplicate system | source/copy SHA-256 `E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2`; source pack removed from final tree |
| Operator-note consolidation | 22 original files and all incoming links | every material statement retained in a smaller indexed authority | green: 17 indexed targets, old-to-new source map, 22 capability rows, lifecycle/type/term/mailbox assertions, and no legacy link remains |
| Plan-tree conversion | 55 original artifacts and all incoming links | no active `docs/plans/`; 14 current owners plus 41 marked historical artifacts | green: exact 55-artifact parity, five product-area owners, design/testing routes, archive markers, and all 213 capability ownership links converted |
| GitHub readback | labels, Project 3, PR 2, visibility | exact standard taxonomy and authorized public repository | green: four labels; Status 5, Priority 4, Horizon 3; PR item `In review`/`P2 Normal`/`Now`; visibility `PUBLIC` |
| Full repository check | application, tests, Bicep, corpus boundary | green or explicit environment blocker | blocked before restore by missing `sqllocaldb`; corpus correctly reported not run |
| Focused application verification | restore, Release build, Core/integration/architecture tests, Bicep | green | green: 0 build warnings/errors; 28/28 Core, 83/83 non-corpus integration, 30/30 architecture; Bicep compiled |
| CI rerun after public visibility | pre-consolidation PR head `9207565` | green | green: Full validation completed in 4m 6s after the billing-blocked first attempt |
| post-consolidation CI | PR head `4ac1cf2` | green | green: Full validation completed in 4m 43s; one Node 20 action deprecation warning remediated in the next head |
| final Full CI | published plan-conversion head `c2c67ac` | green without the Node 20 warning | green: run `30232655439`, Full validation completed in 4m 55s |

## Independent review

- Plan review: incorporated through onboarding inventory and user design direction.
- Candidate PR review: clean at `c2c67ac`; all 163 changed paths reviewed,
  zero required findings, stable-evidence fingerprint
  `630b7bc0813d2c3251f32482dec9c77daf6b1588d5b0ee18cc3d1ea80c283fe2`.
- Final exact-head confirmation: required after this outcome-only record update;
  it is published as external PR review evidence so it does not invalidate the
  commit it reviews.
- Remediation rounds: none.

## Documentation and work tracking

- Documentation impact declared before implementation: product/capabilities,
  roadmap, architecture, operations, design, operator notes, questionnaire,
  linked references, former plans, testing guidance, decision, change, routing,
  and repository-entry owners are affected. Supplied reference content remains
  evidence; only links from first-party reports are repaired.
- Agent mistake entries: none.
- Product/capabilities: [product requirements](../requirements.md) and [current capability inventory](../capabilities.md).
- Design system/assets: [adapted design authority](../../design/README.md) and exact [master logo](../../design/brand/logos/logo_no_margin.png).
- Roadmap/release: [capability allocation summary](../capabilities.md#allocation-summary); no release allocations changed.
- Architecture/workflow: [architecture](../architecture.md) and [engineering guidance](../engineering.md).
- Operations: [operations](../operations.md), proportional CI, and repository-owned checks.
- GitHub issue/Project/milestone: no issue or milestone created; draft [PR 2](https://github.com/collisionengineers/collisionspike_v2/pull/2) is registered in linked [Project 3](https://github.com/users/collisionengineers/projects/3).

## Outcome

Azure Workflow onboarding is complete at the pull-request endpoint: the full
repository and documentation conversion is published in draft PR 2, Full CI is
green, and independent candidate review found no required findings. The final
outcome-only commit receives a separate exact-head confirmation before handoff.
No application, data, operator meaning, corpus, IaC, or Azure behavior changed.

## Blocker or follow-ups

- Blocker: none.
- Follow-ups: none.
