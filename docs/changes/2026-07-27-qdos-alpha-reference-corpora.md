# Change: Deliver provider-aware email interpretation and QDOS alpha

```yaml
id: 2026-07-27-qdos-alpha-reference-corpora
type: feature
status: in_progress
risk: high
created: 2026-07-27
updated: 2026-07-27
issue: https://github.com/collisionengineers/collisionspike_v2/issues/3
pull_request: pending
baseline: b2f40a2b68b5b1a906ff2e736fa43653006dba61
target_release: 0.1.0-alpha.1
roadmap_horizon: Now
mode: development
supersedes: none
superseded_by: none
```

## Summary

Deliver the two coupled `Now` outcomes: a durable provider/intermediary-specific
instruction foundation and the first end-to-end QDOS alpha. Delivery first
builds and accepts the complete application offline through LocalDB, Azurite,
the actual Functions host, local mailbox/custody adapters, local HTTPS
authentication/MCP, and genuine working-copy evidence. Only after that gate may
approved live adapters, Azure reconciliation, deployment, and acceptance begin.

## Scope

### Included

- Normalize all 88 supplied provider records and all 1,638 exact
  provider/location relationships, preserving source provenance, frequency,
  special non-physical values, missing values, and anomalies.
- Separate provider identity from direct-provider and intermediary email-route
  identities. A provider may be reached through both routes.
- Add explicit code-versioned Core policies for evidence-backed direct-provider
  and intermediary routes, organized by stable identity and mirrored by tests.
- Reconstruct a proved original sender for Collision Engineers staff forwards
  while retaining the outer message as transport provenance.
- Activate genuine-evidence policies only; QDOS direct identity is the exact
  `@qdosassist.co.uk` suffix. Do not create empty policies for spreadsheet rows.
- Build and review a genuine provider-instruction evidence corpus covering each
  of the 88 provider identities and every intermediary discovered in that work.
  The foundation identifies all evidenced routes from the outset; only QDOS is
  allowed to proceed to case creation in the alpha.
- Implement the live `instructions@collisionengineers.co.uk` Worker caller and
  complete every active QDOS Inspection, standalone Audit, and Inspection +
  Audit path through acceptance, immutable identity, Box custody, work/review,
  EVA JSON/image handoff, report evidence, lifecycle, observability, recovery,
  and acceptance.
- Implement the selected Operations-first V1 staff shell and all states needed
  by the QDOS workflow.

### Excluded

- Case creation or workflow activation for non-QDOS providers without their own
  genuine corpus, policy evidence, and later activation decision.
- Generic rules engines, expression languages, database-authored predicates,
  admin rule editors, universal case-match ordering, placeholder policies, or a
  second classifier tied to one mailbox.
- V2 all-mailbox management, folder moves, general correspondence matching,
  provider API activation, DOC/MSG/OCR expansion, AI classification, WhatsApp
  automation, and every Later/Not planned capability.
- Live-service client construction, Azure/IaC mutation, cloud reads/writes,
  deployment, predecessor retirement, or production cutover before the complete
  offline acceptance gate and their own exact-target approvals.

## Authorities, current state, and constraints

- Operator behavior: [intake and work instructions](../operator-notes/business-process/intake-and-work-instructions.md), [inspection address](../operator-notes/business-process/inspection-address.md), [case types and references](../operator-notes/business-process/case-types-and-references.md), and [case lifecycle](../operator-notes/business-process/case-lifecycle.md).
- Product/release: [product index](../product/index.md), [capabilities](../product/capabilities.md), [V1 gap](../product/v1-gap.md), and [roadmap](../roadmap.md).
- Architecture: [architecture](../architecture.md), [Decision 0011](../decisions/0011-separate-direct-provider-and-intermediary-email-policies.md), and ADR-0006's preserved neutral transport/storage decisions.
- Design: [V1 requirements](../../design/product/requirements.md), [UI specification](../../design/product/ui-spec.md), and selected [Operations-first direction](../../design/references/directions/operations-first.md). The adapted `collision-engineers-design-dev` essentials remain the visual authority; its excess is not reintroduced.
- Supplied evidence: `providers-worked-on.xlsx` SHA-256 `555a3f3ba5b81ce54af491b22fd49724d49d77b01f5b3c0a0fa8b758a03b4a33`; duplicate provider workbooks SHA-256 `25f7e2c6893f741a743f5c22fdf619032dc63d6b7aa92d24b3f842cc04e40e5f`; job sheet SHA-256 `a52b5df2a131c1b00866f478ebba20150070a3af25915acd8c05a41b2d0b983b`.
- Current caller: Development-only `POST /Intake/Upload` calls Core
  `ProcessIntake`. The Worker is telemetry-only. The current reader records the
  root sender but suppresses nested-message sender evidence, and the one
  QDOS-specific extraction policy creates only a draft, not a case/reference.
- `corpus/` is ignored, immutable, untrusted test input. No corpus item enters
  Git or a PR; generated evaluation output belongs under `artifacts/`.
- The four-project modular monolith remains. This plan adds no project, runtime,
  store, migration stream, or deployment unit.

## Acceptance criteria

- Import verification reports exactly 88 unique stable provider codes and
  exactly 1,638 unique provider/location relationships from 17,737 source
  cases, including 1,555 physical, 66 `Image Based Assessment`, and 17
  `Not supplied` relationships; 410 unmapped case IDs, 74 physical links with
  missing postcode, and anomalous labels remain visible review evidence rather
  than guessed or discarded.
- Every selectable physical inspection location is canonical and linked to at
  least one provider. `Image Based Assessment` is an explicit special choice;
  `Not supplied` and anomalous non-address labels are not selectable physical
  locations. Frequency ranks suggestions only.
- Direct-provider and intermediary route identities are distinct from provider
  identity. Tests prove that the same provider can be determined from its own
  direct policy and from an intermediary's separate policy without sharing
  message-specific predicates.
- A direct route identifies its provider from the normalized source sender—the
  proved original sender for a Collision Engineers staff forward, otherwise the
  direct sender—then uses extracted attachment/body/subject evidence to
  determine type and case. An intermediary route identifies the intermediary,
  then its own policy determines provider, type, and case from the extracted
  evidence.
- A staff-forwarded message proves and records original sender, retains outer
  transport provenance, and fails closed when the forward chain is malformed or
  ambiguous. An intermediary message is never evaluated as direct-provider mail.
- Case association precedence belongs to the applicable route policy. There is
  no global ordering; a CE Case/PO is not preferred and is used only as a
  route-approved lowest fallback.
- Each first successful evaluation stores route-policy key/version and evidence;
  retries/replays reuse it. Zero or multiple applicable routes and contradictory
  provider/type/case evidence produce `Needs sorting` with no case/reference.
- A source sender matching both direct-provider and intermediary traits is
  explicitly tested as multiple applicable routes and produces `Needs sorting`.
- No policy exists without genuine examples and positive, negative, ambiguous,
  forward/intermediary, retry, and holdout evidence. Spreadsheet presence alone
  creates reference identity, not executable policy.
- All 88 provider identities have an operator-reviewed evidence disposition:
  proved direct route, proved intermediary route(s), both, or confirmed no
  current email-instruction route. Every proved route has an executable policy;
  absence of evidence blocks that route rather than producing a guessed policy.
- Non-QDOS route policies are exercised by the genuine-input evaluator and may
  identify the provider/type/case evidence at live intake, but the alpha
  activation gate prevents them from creating a case or allocating a reference.
- `instructions@collisionengineers.co.uk` is the sole live alpha mailbox caller.
  Only definitive QDOS instructions create cases. All three active QDOS case
  types complete the settled V1 path with staff identity/authorization,
  immutable references, Box custody, Operations-first UI, EVA handoff, exact
  report evidence, terminal history, and accepted recovery evidence.
- Full repository validation, independent implementation review, operator
  acceptance, management acceptance, and separately authorized live Azure
  validation are green before `0.1.0-alpha.1` is accepted.

## Capability evidence index

This immutable index accounts for all 128 `Now` capability IDs. The delivery
steps are the owning implementation/evidence slices; this table records
allocation, not a claim that pending evidence has passed. Each row later records
local proof, live proof where required, or the exact release blocker without
removing the capability.

| Capability IDs | Delivery steps | Required evidence owner |
| --- | --- | --- |
| `OPS-10` | 12, 13 | approved isolated Azure Development deployment and direct-terminal release evidence |
| `OPS-22` | 4, 10 | genuine-input graphical evaluator and cohort/holdout evidence |
| `OPS-01`, `OPS-02`, `OPS-03`, `OPS-04`, `OPS-05`, `OPS-06`, `OPS-07`, `OPS-08`, `OPS-09`, `OPS-11`, `OPS-13`, `OPS-14`, `OPS-20`, `OPS-24` | 1, 5, 8, 10, 12, 13 | offline platform/caller/concurrency proof followed by approved Azure, resilience, capacity, deployment, and recovery proof |
| `OPS-23`, `OPS-25` | 13 | operator journey and Collision Engineers management release approval |
| `EVAL-01`, `EVAL-02`, `EVAL-03`, `EVAL-04`, `EVAL-05` | 4, 10 | local evaluator UI, persisted review evidence, cohort/holdout |
| `MAIL-20`, `MAIL-21`, `MAIL-22` | 4, 10, 11 | shared Core taxonomy/route evidence, local caller proof, then approved Graph parity |
| `MAIL-14`, `MAIL-15`, `MAIL-16` | 6–8, 10, 11 | exact local Sent evidence/linking and approved automatic matcher holdout, then Graph parity |
| `MAIL-18` | 6, 9, 10 | Core chaser policy and authenticated copyable Web output |
| `ACC-01`, `ACC-02`, `ACC-03`, `ACC-04`, `ACC-05`, `ACC-06`, `ACC-07`, `ACC-08`, `ACC-09`, `ACC-10`, `ACC-11` | 3, 9, 10 | Identity/OpenIddict, authorization, history, authenticated browser/MCP |
| `INT-01`, `INT-02`, `INT-03`, `INT-08`, `INT-09`, `INT-10`, `INT-11`, `INT-12`, `INT-13`, `INT-17`, `INT-18`, `INT-19`, `INT-20`, `INT-21`, `INT-22`, `INT-23`, `INT-24`, `INT-25`, `INT-26`, `INT-27`, `INT-29`, `INT-30` | 4–10 | shared evaluator, durable receipt/outbox/Worker, acceptance and negative recovery smoke |
| `TRI-01`, `TRI-02`, `TRI-03`, `TRI-04`, `TRI-05`, `TRI-06`, `TRI-07`, `TRI-08`, `TRI-09` | 4, 6, 8–10 | approved matcher evidence, Core transitions, Worker Sent evidence, UI/MCP |
| `CASE-01`, `CASE-02`, `CASE-03`, `CASE-04`, `CASE-07`, `CASE-08`, `CASE-09`, `CASE-10`, `CASE-11`, `CASE-12`, `CASE-13`, `CASE-14`, `CASE-15`, `CASE-16`, `CASE-17`, `CASE-18`, `CASE-19`, `CASE-20`, `CASE-21`, `CASE-24`, `CASE-25`, `CASE-26`, `CASE-27`, `CASE-28`, `CASE-29`, `CASE-30` | 6–10 | Core/persistence contract, local adapters, Worker, UI/MCP, lifecycle smoke |
| `UI-01`, `UI-02`, `UI-03`, `UI-04`, `UI-05`, `UI-06`, `UI-07`, `UI-08`, `UI-09`, `UI-11`, `UI-13` | 9, 10 | authenticated Razor Pages caller and Playwright/accessibility acceptance |
| `DOC-01`, `DOC-02`, `DOC-03`, `DOC-04`, `DOC-05`, `DOC-06`, `DOC-07`, `DOC-08` | 6, 7, 9–11 | Core custody contract, local adapter/UI smoke, then Box parity/live proof |
| `EXT-01`, `EXT-02`, `EXT-03`, `EXT-14`, `EXT-18` | 7, 10, 11 | local replay/export contract and operator smoke, then approved live parity |
| `MCP-01`, `MCP-02`, `MCP-03`, `MCP-04` | 3, 9, 10, 13 | OpenIddict actor enforcement and real Streamable HTTP caller |
| `DATA-01`, `DATA-02` | 2, 10 | deterministic manifest/package/migrations and exact count/hash proof |

Count assertion: **128 distinct IDs; no duplicate and no omitted `Now` row**.

## Plan

1. **Reference data — Infrastructure persistence and explicit release-owned
   migration.** Add normalized `Provider`, `InspectionLocation`, and
   `ProviderInspectionLocation` models/mappings. Use a reviewed EF migration
   with deterministic seed rows generated from the pinned workbook, including
   source hash, frequency, match basis, and review status. Add a repeatable
   verification command that derives counts from the preserved workbook and
   compares them with generated seed input and a migrated database. Never read
   the workbook during application startup.
   Provider and location reference identities are database-owned; sender traits,
   intermediary identities, and route-to-provider predicates remain in the
   code-versioned policy catalog and are not seed/configuration tables.
2. **Instruction evidence — Core contracts and source reader.** Extend neutral
   evidence to represent transport sender, normalized source sender, proved
   original forwarded sender, subject, body, attachment/document content, and
   extraction completeness.
   Update the existing Infrastructure MIME reader to expose nested forwarded
   sender evidence without losing occurrence/provenance or relaxing limits.
3. **Route policy — Core single owner.** Replace the one-policy assumption with
   an explicit catalog of direct-provider and intermediary policies. Keep route
   policy classes in discoverable stable-identity folders; shared orchestration
   selects exactly one route and records key/version/evidence. Persist route
   kind, stable policy key/version, resolved provider code,
   classification/case outcome, and supporting evidence on the receipt's
   versioned evaluation record. Each route owns its provider/type/case rules and
   precedence. Remove the superseded selector path rather than retain
   compatibility in development mode.
4. **Evidence activation — evaluator and tests.** Build an operator-reviewed
   evidence inventory for all 88 provider identities and every discovered
   intermediary outside Git. Acquire genuine examples for every current email
   route and record an explicit no-current-email-route disposition only when
   confirmed by the owner. Implement every proved direct/intermediary policy;
   unresolved coverage blocks release rather than creating placeholders. Prove
   QDOS direct sender `@qdosassist.co.uk`, staff forwards, every evidenced QDOS
   intermediary route, conflicting routes, negatives, ambiguity, policy-version
   pinning, and untouched holdouts. Do not infer sender domains from provider
   names or generate operational email fixtures.
5. **Case acceptance — Core and persistence.** Add the case/reference,
   instruction-type, immutable principal, lifecycle, action-history, concurrency,
   and idempotency models/use cases required by the accepted QDOS flow. Invoke
   them only after complete route-policy determination and settled Audit gates.
   Reuse one shared acceptance path for Worker and manual staff resolution.
6. **External adapters — Infrastructure.** Implement Graph polling/custody,
   Box case-file custody, vehicle enrichment, EVA JSON/image handoff, exact
   Outlook report/Triage evidence, SQL persistence, and bounded retry/outbox
   behavior behind existing Core ports. Each external effect has idempotency,
   visible terminal failure, telemetry, and a documented recovery action.
7. **Real callers — Worker and Web.** Add the one approved `instructions@`
   Worker trigger/caller and authenticated Web operator callers. Worker invokes
   the same Core policy as manual intake and never creates a case directly.
   Implement CollisionSpike accounts/roles and Operations-first Intake, Triage,
   Case, document, administration, error, stale, retry, and accessibility states.
8. **Azure/release — existing IaC and explicit operations route.** After a fresh
   inventory and exact-target approval, finish immutable packages, migration
   bundle, identities/RBAC, configuration, health/smoke evidence, backup/restore,
   15-minute RPO/four-hour RTO proof, cutover, and previous-package recovery.
   Do not apply migrations at startup or perform Azure work under this plan PR.
9. **Acceptance and documentation.** Run the full repository and live caller
   evidence, update canonical product/architecture/design/operations docs in the
   implementation PR, obtain independent exact-head review, then obtain
   operator and management acceptance before release.

## Data, failure, and recovery

- Data/schema: additive normalized provider/location/reference and later
  case/workflow schema under the existing Infrastructure migration stream.
  Provider codes become immutable after first case use; activation is separate
  from reference presence. Persisted intake evaluation stores route kind,
  immutable stable policy key/version, resolved provider code,
  classification/case outcome, and evidence. Intermediary identity and sender
  traits are code-owned policy keys/predicates rather than database-authored
  entities or mapping tables. A direct route resolves one provider; an
  intermediary policy may resolve multiple providers, and a provider may be
  reached through multiple route policies.
- Import integrity: all source rows are accounted for by imported, special,
  unmapped, or review-needed counts. A hash/count mismatch aborts migration
  preparation and release; it never partially guesses mappings.
- Failure behavior: unreadable/incomplete extraction, unknown or competing
  route, uncertain provider/type/case, policy-version mismatch, custody failure,
  or external dependency ambiguity remains visible pre-case and allocates
  nothing. After allocation, ordinary idempotent retries preserve the principal,
  reference, policy version, and external-effect identity.
- Recovery: correct reference-data generation or policy code forward and rerun
  verification. Roll back application to the prior immutable package only when
  schema compatibility permits; schema recovery is a tested forward fix or
  database restore, never automatic down-migration. No case or reference is
  deleted or reused.

## UI/UX contract

Operations-first is selected. The authenticated desktop shell lands on exact
office outcomes and links each count to its exact filtered queue. Intake shows
route, provider/type/case evidence and provenance in operator language, never
internal parser/policy names. Unknown, conflicting, retrying, stale, denied,
empty, loading, and dependency-unavailable states are distinct and recoverable.
All planned V1 keyboard, focus, semantic, forced-colour, reduced-motion,
1024px/200%-zoom, and role boundaries apply. The existing adapted CE logo,
colour/type/geometry/icon rules are reused; no upstream marketing, mobile,
document, imagery, or animation excess returns.

## Azure impact

Delivery materially affects the intended Web, Worker, SQL, Storage, Key Vault,
Application Insights, and Log Analytics topology. Offline delivery performs no
Azure read or write and does not modify IaC. After offline acceptance, a fresh
inventory, explicit subscription/resource targets, spending boundary,
identity/RBAC proof, deployment approval, health/smoke validation, and recovery
approval are required before each exact live operation. No new Azure service or
deployment unit is planned.

## Decisions and conflicts

- Selected: Operations-first V1 shell; classification policy is code-versioned
  Core behavior; version is pinned on first successful evaluation and retained
  on retries/replays.
- Corrected: intermediary rules are independent route rules, not a resolver
  wrapper around direct-provider rules. A provider may have both routes.
- Rejected: mailbox destination as business classification, QDOS-only
  architecture, one generic provider policy for intermediary mail, universal
  case-association order, CE Case/PO as a preferred key, rules engine/admin
  editor, empty policy scaffolds, and historical frequency as an `always` rule.
- Evidence gap and release gate: supplied workbooks identify 88 providers and
  historical provider/location relationships but do not prove complete sender
  or message traits. Only 15 provider codes had any inspected job-sheet domain
  clue; QDOS's exact domain is owner-supplied. Genuine policy evidence and an
  operator-reviewed route disposition must be acquired for the complete
  provider corpus before alpha acceptance; it is never inferred.
- No unresolved architecture or product decision remains for implementation to
  begin. Each policy's exact predicates remain an evidence deliverable and
  activation gate, not permission to guess them during implementation.

## Implementation

- Status: active; implementation approved on 2026-07-27.
- Current stage: Step 2 provider/location reference data.
- Full implementation plan: [QDOS alpha implementation plan](2026-07-27-qdos-alpha-implementation-plan.md).
  This change record remains the owner of current status, evidence, blockers, and
  outcome.
- Deviations: workflow-specific repository/doctor/documentation wrapper scripts
  were removed by direct owner instruction; verification uses owning executables.
- Recovery actions: none.
- Live boundary: no cloud/vendor read, write, credential, deployment, or
  predecessor-retirement operation is authorized by this activation.
- Step 2 authoring code is implemented in `scripts/Build-ProviderReferenceData.ps1`.
  It uses the ignored dependency cache
  `artifacts/reference-data-tools/` and ignored atomic staging
  `artifacts/reference-data-staging/`, then targets the committed package
  `src/CollisionSpike.Infrastructure/ReferenceData/provider-reference-data.v1.json`
  and manifest
  `src/CollisionSpike.Infrastructure/ReferenceData/provider-reference-data.v1.manifest.json`.
  This is authoring-code evidence only; no generated package, manifest,
  migration, count, review, test, or acceptance result is claimed here.
- Step 2 precondition: every supplied workbook must be closed and no
  `~$*.xls*` lock may remain under
  `docs/reference/workproviders-and-repairers/`. The authoring command rejects
  a lock before dependency installation, source reading, or output writing.
  The current observed external blocker is
  `docs/reference/workproviders-and-repairers/~$providers-worked-on.xlsx`;
  therefore neither committed output is claimed as generated. Source
  workbooks and CSVs remain immutable, and every candidate remains
  `Unreviewed` and unavailable as a runtime selector.
- Direct operator invocation is `pwsh ./scripts/Build-ProviderReferenceData.ps1`
  from the repository root after the lock check. The command is offline and
  makes no cloud/vendor calls; completion would not by itself prove review,
  selector activation, migration, tests, release, or alpha acceptance.


## Verification

| Check | Scope | Expected | Observed |
| --- | --- | --- | --- |
| planning documentation validation (historical) | planning documentation | valid record, links, source roles, design and roadmap consistency | passed: 155 Markdown files, 1,115 local links, 213 feature triples, 41 archived artifacts, 21 assertions |
| scoped diff inspection | planning branch | documentation/work tracking only; no corpus or supplied-reference mutation | passed before publication; only canonical documentation and this record changed |
| fresh plan review | complete record and canonical updates | no missing decision, contradiction, hidden compatibility, or unexecutable step | passed after one pre-publication remediation batch; no remaining blocker/required finding |
| GitHub `validate` | each published exact head | proportional Docs lane succeeds | PR #4 owns current result; prior exact-head runs `30236008712` and `30236209099` passed |
| direct Release build and non-corpus tests | current delivery branch | owning executables pass without workflow wrappers | passed: Architecture 30/30, Core 28/28, Integration 85/85; no failures or skips |
| tool-neutral activation | active guidance and scripts | issue #3 is active; no active plugin route or workflow-specific validation/doctor wrapper remains | passed; issue title/body updated and obsolete wrappers deleted |
| offline platform source/caller smoke | standard tools, LocalDB, current Web, Azurite, actual Functions host | explicit migration only; isolated DevelopmentOffline; ready local services; no cloud/vendor client | passed: `npm ci`; LocalDB migration applied twice idempotently; Web HTTPS live/ready/intake returned 200; Azurite Blob/Queue listeners and Functions 4.12.1 host lock were observed; host correctly reported no trigger at this checkpoint |
| Development HTTPS workstation trust | current-user certificate store | trusted certificate for ordinary browser use | certificate and HTTPS host proved; Windows trust confirmation is still required during clean-operator setup and is not claimed complete on this workstation |
| Step 2 provider-reference authoring | supplied workbooks, lock guard, deterministic package/manifest outputs | no `~$` lock; immutable inputs; all candidates `Unreviewed`; exact package/manifest and counts only after a successful run | blocked before authoring: `docs/reference/workproviders-and-repairers/~$providers-worked-on.xlsx` is present; no generation, count, migration, review, test, or acceptance evidence claimed |

| genuine corpus evaluation | later delivery | route-specific cohorts/holdouts and failures prove accepted predicates | not run — implementation excluded |
| live Azure/caller/acceptance evidence | later approved operation | migration, callers, external effects, recovery, operator and management acceptance | not run — implementation excluded |

## Independent review

- Plan review: passed in fresh context after correcting ADR supersession,
  persisted route identity, V1-gap ownership wording, normalized sender wording,
  and the direct/intermediary trait-collision test.
- Candidate PR review: exact head
  `ce0135ede23101af320846a135d97c1ee05c7146` returned one required finding:
  the product index still called the selected V1 UI authority
  direction-neutral. Corrected in the next head.
- Second PR review: exact head
  `9a8ffe7cb992c024bb2ba1655368a2fdbe3db6fb` confirmed the first finding was
  fixed and returned one required finding: this record still described the
  already-created PR and completed validation/publication/CI as pending.
- Final exact-head review: GitHub review evidence owned outside this commit;
  required after every tracked change so the record never self-certifies the
  head that contains it.
- Remediation rounds: one pre-publication documentation batch and two PR-review
  documentation batches.

## Documentation and work tracking

- Documentation impact declared before implementation: operator intake routing;
  product index/areas/open decisions/capabilities; roadmap; architecture and
  Decision 0011; design requirements/direction/reference map; later operations
  and implementation handoff updates when callers exist.
- Agent mistake entries: none; the intermediary-model correction was caught
  during planning before publication or implementation and does not qualify.
- Product/capabilities: `docs/product/` and both `Now` outcomes target
  `0.1.0-alpha.1`.
- Design system/assets: Operations-first selected; adapted CE assets unchanged.
- Roadmap/release: both active `Now` outcomes target `0.1.0-alpha.1`.
- Architecture/ADR: Decision 0011 supersedes ADR-0006's single-policy selection
  and no-provider-registry/table limits while preserving its neutral
  intake/storage principles.
- Operations: offline setup, live preflight, teardown, migration, deployment,
  backup/restore, and rollback procedures are updated with their owning slices.
- GitHub issue/Project/milestone: [issue #3](https://github.com/collisionengineers/collisionspike_v2/issues/3), delivery pull request pending, Project status `In progress`, Priority `P1 High`, Horizon `Now`, milestone `0.1.0-alpha.1`.

## Outcome

Implementation is active on the approved delivery branch. The planning PR is
historical evidence; the current change record owns offline proof, named live
blockers, approved live evidence, and the final release outcome.

## Blocker or follow-ups

- Current blocker: none for offline implementation.
- Step 2 external authoring blocker: the observed
  `docs/reference/workproviders-and-repairers/~$providers-worked-on.xlsx`
  Office lock requires the authoring command to fail before dependency
  installation, source reading, or output writing. Generation and all
  package/count/migration/review/test/acceptance evidence remain unclaimed.
- Live blockers: genuine route/Triage/report holdouts, selected VRM engine,
  accepted DVLA/DVSA and EVA contracts, exact Graph/Box targets and scopes,
  refreshed Azure inventory, teardown approvals, isolated v2 target, operator
  acceptance, and management approval remain mandatory before release.
