# Change: Allocate every planned Pegasus capability

```yaml
id: 2026-07-29-version-all-planned-capabilities
type: task
status: in_progress
risk: medium
created: 2026-07-29
updated: 2026-07-29
issue: https://github.com/collisionengineers/pegasus/issues/19
pull_request: pending
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
- [Roadmap](../roadmap.md): horizon summary only; no exact release ledger.
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
2. Allocate the 32 `Next` and 40 `Later` capabilities across the exact eleven
   post-alpha targets while retaining all 128 `Now` rows at
   `0.1.0-alpha.1` and all 29 permanent boundaries as `unallocated`.
3. Mirror the ordered release sequence into requirements and operations,
   preserve the roadmap as horizon-only, and synchronize the design matrix.
4. Create or update only the exact twelve open, dateless release milestones;
   assign the active alpha delivery issue and this allocation issue to the
   existing alpha milestone.
5. Synchronize all 229 IDs into user Project 3 with one draft card per ID,
   required fields/options, and separate planned-sequence and permanent-boundary
   views.
6. Prove exact repository counts, milestone/project readback, local restore,
   Release build, focused/full tests, and caller-independent exact-head review
   before presenting a stacked pull request.

## Exact release sequence

| Order | Target | Purpose | Planned capability count |
| ---: | --- | --- | ---: |
| 01 | `0.1.0-alpha.1` | Unchanged QDOS-alpha target scope | 128 |
| 02 | `0.2.0` | Provider expansion and intake fidelity after QDOS acceptance | 8 |
| 03 | `0.3.0` | Four-mailbox classification, association, folder actions, email workspace and email MCP | 19 |
| 04 | `0.4.0` | Principal-scoped provider API and post-report query/dispute casework | 5 |
| 05 | `0.5.0` | Extended case types and staff/outbound communication channels | 5 |
| 06 | `0.6.0` | Individually approved operator AI assistance | 5 |
| 07 | `0.7.0` | Optional direct EVA API coexistence before replacement | 1 |
| 08 | `1.0.0` | Pegasus-owned engineering record/workbench and transfer of EVA authority | 12 |
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

| Target | Intended write | Evidence |
| --- | --- | --- |
| Repository issue #19 | Create and assign to `0.1.0-alpha.1` | Pending readback |
| Repository issue #3 | Preserve assignment to `0.1.0-alpha.1` | Pending readback |
| Repository milestones | Preserve/create the exact twelve open dateless release milestones | Pending readback |
| User Project 3 | Field, option, view, and 229 draft-item synchronization | Pending readback |

No Azure, deployment, mailbox, Box, EVA, provider, WhatsApp, AI, credential,
account, data, or other external-service operation is authorised by this change.

## Verification and evidence

Pending implementation, local validation, exact GitHub API/UI readback, green
exact-head pull request checks, and independent review.

## Outcome

In progress. Allocation records and GitHub planning state are not implementation,
deployment, live verification, operator acceptance, or release evidence.
