# Change: Allocate every planned Pegasus capability

```yaml
id: 2026-07-29-version-all-planned-capabilities
type: task
status: in_review
risk: medium
created: 2026-07-29
updated: 2026-07-29
issue: https://github.com/collisionengineers/pegasus/issues/19
pull_request: https://github.com/collisionengineers/pegasus/pull/20
baseline: 7f9f088150ff04d8336a38a27e25804dac412d8a
target_release: multiple
roadmap_horizon: Now, Next, Later, Not planned
mode: development
supersedes: none
superseded_by: none
```

## Required outcome

Assign one exact first-introduction Semantic Version target to every planned
Pegasus capability and retain every permanent boundary as deliberately
unallocated. Mirror all 229 stable identities to the existing user-owned
[Pegasus Delivery Project 3](https://github.com/users/collisionengineers/projects/3)
without treating draft planning cards, milestones, or project status as
activation, implementation, deployment, or acceptance evidence.

The accepted execution-plan input has SHA-256
`6226cdd8a19d87a5f2ddc5047c3919184c676aee53e9073e5690b8aeb5e62fbe`.
The accepted documentation predecessor is exact commit
`7f9f088150ff04d8336a38a27e25804dac412d8a`.

## Affected owners

- [Capability inventory](../capabilities.md): exact ID-to-release allocation and
  permanent-boundary records.
- [Product requirements](../requirements.md): ordered release/dependency
  narrative and release-content summaries, without becoming a second
  allocation ledger.
- [Operations](../operations.md): release sequencing, activation discipline,
  and evidence-state separation.
- [Product traceability](../../design/product/traceability-matrix.md): mirror of
  canonical horizon/target allocations plus separate development-evidence
  qualifiers.
- [Repository policy](../../scripts/Test-RepositoryPolicy.ps1): exact release
  set/count and cross-owner consistency gates.
- GitHub issue #19, the twelve repository milestones, and Pegasus Delivery
  Project 3: actionable planning state and readback evidence.

## Accepted plan

1. Re-read only the accepted documentation head and compute the release mapping
   from the centralized 229-row capability owner.
2. Allocate the 31 `Next` and 41 `Later` capabilities across the exact eleven
   post-alpha targets while retaining all 128 `Now` rows at
   `0.1.0-alpha.1` and all 29 permanent boundaries as `unallocated`.
3. Mirror the ordered release sequence into requirements and operations,
   keep the capability inventory's horizon summary authoritative, and synchronize the design matrix.
4. Create or update only the exact twelve open, dateless release milestones;
   assign the active alpha delivery issue and this allocation issue to the
   existing alpha milestone.
5. Synchronize all 229 IDs into user Project 3 with one draft card per ID,
   required fields/options, and separate planned-sequence and permanent-boundary
   views.
6. Prove exact repository counts, milestone/project readback, local restore,
   Release build, focused/full tests, and caller-independent exact-head review
   before presenting a stacked pull request.

## Execution scope update

Current user direction on 2026-07-29 stopped further GitHub Project
presentation work and directed effort to alpha delivery. Before that direction,
the Project API mirror had already been populated with all 229 keyed draft
items and exact capability fields. The two saved views exist but their
grouping, filters, displayed fields, and visual samples were not configured or
accepted; they are not a completion gate or evidence for this change.

Repository allocation remains the only product authority. No subsequent alpha
claim may depend on Project presentation. No further Project mutation or
readback was performed after the direction changed.

### AI-05 allocation update

Direct user direction on 2026-07-29 redefined `AI-05` from image/vision assistance into automatic advisory image readiness assessment. When activated, it assesses each current Case image set on addition, replacement, or removal for a registration overview, damage close-up, and the applicable reflection criterion. It may run before market valuation, does not return an AI Proposal or call `AI-09`, and has no Case allocation, lifecycle, eligibility, or chase effect.

The internal capability, requirements, and traceability owners carry 31 `Next` and 41 `Later` identities, with planned target counts of 7 for `0.2.0` and 13 for `1.0.0`.

The existing GitHub Project mirror is historical planning state at its prior
readback and is no longer current for `AI-05`. The user has not authorised a
subsequent external Project mutation or readback.

## Exact release sequence

| Order | Target | Purpose | Planned capability count |
| ---: | --- | --- | ---: |
| 01 | `0.1.0-alpha.1` | Unchanged QDOS-alpha target scope | 128 |
| 02 | `0.2.0` | Provider expansion and intake fidelity after QDOS acceptance | 7 |
| 03 | `0.3.0` | Four-mailbox classification, association, folder actions, email workspace and email MCP | 19 |
| 04 | `0.4.0` | Principal-scoped provider API and post-report query/dispute casework | 5 |
| 05 | `0.5.0` | Extended case types and staff/outbound communication channels | 5 |
| 06 | `0.6.0` | Individually approved operator AI assistance | 5 |
| 07 | `0.7.0` | Optional direct EVA API coexistence before replacement | 1 |
| 08 | `1.0.0` | Pegasus-owned engineering record/workbench and transfer of EVA authority | 13 |
| 09 | `1.1.0` | Deterministic report and fee-note rendering | 6 |
| 10 | `1.2.0` | Targeted report distribution, accounts/invoicing and management information | 5 |
| 11 | `1.3.0` | Vendor-neutral AI work requests, Engineer-reviewed query proposals and staff-selected AI Assessor | 3 |
| 12 | `1.4.0` | Conditional capture and domain outcomes after direct promotion decisions | 3 |

Permanent boundaries: 29 IDs, `Not planned / unallocated`, no release milestone
or activation issue.

## Deferred-capability impact

- `Next` and `Later` identities retain their stable IDs and canonical outcomes;
  the exact target records ordering only. No schema, service, route, flag,
  credential, UI placeholder, issue, adapter, deployment unit, or dormant code
  is created for a deferred capability.
- The preserved seam is the stable capability ID linked to one canonical release
  target and owner. Activation still requires its own accepted issue/change
  scope, direct decisions where named, implementation, caller evidence, and
  release gates.
- `Not planned` remains a permanent product boundary. Its 29 IDs receive no
  milestone, target, activation issue, implementation lifecycle state, or
  planned release-sequence placement; each retains one keyed boundary draft.
- This change makes no irreversible product, data, schema, external-system, or
  runtime choice. It only fixes an auditable planning sequence.

## External writes

| Target | Write and evidence |
| --- | --- |
| GitHub CLI credential | The active `collisionengineers` credential was explicitly authorised and refreshed with `project` scope after the initial Project read failed for missing `read:project`; `gh auth status` then reported `project`. |
| Repository issue #19 | Existing task issue remained assigned to milestone `0.1.0-alpha.1` (milestone number 1) and was added to Project item `PVTI_lAHOERCoDM4BeiWXzg0exlk`. |
| Repository issue #3 | API preflight and final card-sync readback preserved its one unkeyed Project item `PVTI_lAHOERCoDM4BeiWXzg0K22I` and its existing milestone, title, body, Status `Ready`, Priority `P1 High`, and Horizon `Now` values. |
| Repository milestones | Preserved `0.1.0-alpha.1` and created open, dateless milestone numbers 2–12 for `0.2.0` through `1.4.0`; paginated readback matched all twelve exact titles and descriptions. |
| Project fields | Selected `Capability ID` `PVTF_lAHOERCoDM4BeiWXzhZIots`, `Pegasus Target release` `PVTSSF_lAHOERCoDM4BeiWXzhZIpx0`, `Pegasus Horizon` `PVTSSF_lAHOERCoDM4BeiWXzhZIovg`, `Pegasus Delivery status` `PVTSSF_lAHOERCoDM4BeiWXzhZIot0`, and existing `Priority` `PVTSSF_lAHOERCoDM4BeiWXzhY7yPU`. |
| Project field correction | An empty preferred `Target release` field `PVTSSF_lAHOERCoDM4BeiWXzhZIotw` was created with only the twelve planned releases before the required `unallocated` option was rechecked. No item was assigned to it. It was left untouched and the compatible prefixed fallback above was selected, rather than perform an unauthorised destructive field deletion. |
| Project capability items | Sequential GraphQL writes created and keyed 229 draft items. Paginated readback proved 229 unique unarchived IDs: Horizon `128/32/40/29`, Delivery status `128 In progress / 72 Triage / 29 Not planned`, exact release counts `128/8/19/5/5/5/1/12/6/5/3/3`, and `29 unallocated`; no Priority value or milestone was inferred on any draft. |
| Project views | Created `Release sequence` `PVTV_lAHOERCoDM4BeiWXzgLIBfw` and `Permanent boundaries` `PVTV_lAHOERCoDM4BeiWXzgLIBno`. Their layout shells exist, but filters/grouping/display fields and visual verification were explicitly skipped under the later user direction and are not claimed as complete. |

No Azure, deployment, mailbox, Box, EVA, provider, WhatsApp, AI, account, case
data, or other external-service operation was performed by this change.

## Verification and evidence

- Immutable allocation input and pushed branch head:
  `19b69aaec49c9cef357d8d6f435ca34f2c258fb3`.
- `pwsh -NoProfile -File ./scripts/Test-RepositoryPolicy.ps1`: passed with
  229 unique capabilities, 200 exact allocations across 12 releases, 29
  permanent unallocated boundaries, and exact horizon/matrix parity.
- `pwsh -NoProfile -File ./scripts/Test-RepositoryLanguage.ps1`: passed.
- `dotnet restore ./Pegasus.slnx`: passed.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`: passed.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter
  "Category!=Corpus"`: 35 Architecture, 62 Core, and 87 Integration tests
  passed.
- Temporary fail-closed synchronization readback proved exact field option
  order, 229 unique draft IDs, zero missing/extra/duplicate IDs, 29 permanent
  boundary drafts, exact status/horizon/target counts, zero Priority
  exceptions, no draft milestones, exact immutable draft bodies, and unchanged
  issue #3 content/fields.
- Saved Project view presentation and visual samples are deliberately
  unverified under the 2026-07-29 user scope update. They are not repository,
  implementation, deployment, or acceptance evidence.
- Independent review of head `8bd432828ad1bff9d84842168236ff17ef25cafd`
  required removal of stale planned/unallocated labels from current owners,
  conditional-allocation wording for the three `1.4.0` rows, replacement of the
  deleted roadmap owner, and a drift guard. The findings were remediated in the
  current pull request; repository policy and language checks passed afterward.
- Follow-up exact-head review found one stale provider-API sentence in the
  current requirements owner and an incomplete current-authority guard. The
  sentence now delegates target ownership to the capability inventory, and the
  guard rejects non-boundary `unallocated` labels across every mutable
  allocation-bearing owner.

## Outcome

In review in pull request 20. The repository allocation is complete. GitHub
planning state is not implementation, deployment, live verification, operator
acceptance, or release evidence.
