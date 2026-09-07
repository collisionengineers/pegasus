# Stream C implementation plan

## Objective

Complete the user-approved Stream C intake, directory, pre-case and shared
operator-surface work, then preserve it on the existing Stream C branch for
agent A. Maintain one open, unmerged PR targeting `dev`.

## Governing docs

Implement `docs/frd/frd-02-intake-and-source-identity.md` and the accepted
cross-stream decisions without changing protected operator notes. The complete
approved requirements remain in
`pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md`,
`DECISIONS.md`, `SHARED-CONTRACTS.md`, and `COORDINATION.md`.

## Starting state

- Common baseline: `3284f93fc3ea9fd3bbbea9405ec92dc7818378f2`.
- Ticket: `INTK-060`.
- Branch: `task/pegasus-v1-intake`.
- Worktree: `../pegasus-worktrees/v1-intake`.
- Stream A owns Foundation, global EF configuration, migrations, composition,
  shared test support, Graph runtime, storage, MCP and deployment.
- Stream B owns Case pages, engineering decisions, report generation/delivery
  and Glass's.
- Stream C owns its Core intake policies, adapters/store methods, C Razor pages,
  outer shell, navigation and shared Web assets.

The exact previous plan is preserved in
`scratch/plan-archive-part-1.md` through
`scratch/plan-archive-part-4.md`; concatenate their payloads in order.
Original SHA-256:
`62649b22a7e43d771820d36c4126a65867fc38d99b636c54a20cc5a6468f3a95`.

## Required changes

Execute C01–C08 and the residual acceptance table from the authoritative Stream
C plan. C09 integrates the owned work, verifies caller reachability, records
all failures and dispositions, and creates or updates exactly one open Stream C
PR. Preserve genuine-source provenance, distinct business roles, fail-closed
ambiguity, pre-case identity and existing production callers.

## Expected files

Only paths assigned to Stream C by:

- `pegasus_pack/astra_output/v1_implementation_plans/registers/file-ownership.csv`
- `pegasus_pack/astra_output/v1_implementation_plans/registers/file-ownership.json`
- the C01–C09 file map in
  `pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md`

## Do not modify

- `corpus/**`
- `.kanmer/**`
- `.worktrees/kanmer/**`
- Stream A or Stream B owned application paths
- protected operator notes
- cloud, mailbox, Box, Glass's or EVA state

## Constraints

No new dependency, generic workflow engine, duplicate business-rule owner,
second OCR vendor/runtime, fabricated evidence, compatibility layer, deployment,
external write, merge, reset, force-push or old-PR closure. Preserve existing
branches, commits and dirty work.

## Ordered steps

### Step 1 — C01 retained-instruction analysis

- Files: C01 paths in the authoritative Stream C plan.
- Reuse: `ProcessIntake`, `DurableIntake`,
  `ReconcileUnidentifiedDestinations`.
- Done: unresolved source-linked candidates persist and allocation remains
  withheld until accepted.
- Deviation stop: Foundation schema or ownership differs materially.

### Step 2 — C02 structured extraction and OCR

- Files: C02 paths in the authoritative Stream C plan.
- Reuse: existing source reader, external-work conventions and lookup port.
- Done: digital-first extraction and page-qualified OCR preserve provenance,
  ambiguity and recoverable failure.
- Deviation stop: a second OCR/runtime or duplicate policy is required.

### Step 3 — C03 principal profiles

- Files: C03 paths in the authoritative Stream C plan.
- Reuse: QDOS route/extraction owners and the single selector registry.
- Done: fourteen additional source-backed profiles and finite VRM alternatives
  are tested without guessed matches.
- Deviation stop: evidence or clean-target identity is unavailable.

### Step 4 — C04 intake and Triage consistency

- Files: C04 paths in the authoritative Stream C plan.
- Reuse: existing Triage contracts, stores and typed authorization.
- Done: attachment classification, case-type decisions and promotion paths fail
  closed consistently.
- Deviation stop: allocation can occur without a consistent typed result.

### Step 5 — C05 third-party report extraction

- Files: C05 paths in the authoritative Stream C plan.
- Reuse: document contracts, source reader and evidence components.
- Done: supported report families retain source-linked fields and unknown
  layouts never guess outcomes.
- Deviation stop: source role or provenance would be lost.

### Step 6 — C06 directories and principal administration

- Files: C06 paths in the authoritative Stream C plan.
- Reuse: existing organization/principal administration and address policies.
- Done: claimant, repairer, inspection location and claim source remain distinct
  with reviewed provenance.
- Deviation stop: unreviewed source data would become business truth.

### Step 7 — C07 pre-case queues, custody and uploads

- Files: C07 paths in the authoritative Stream C plan.
- Reuse: existing Triage/Image Intake stores, custody contract and upload-link
  semantics.
- Done: global T identity, append-only notes, fixed sessions, typed refusal and
  real-instruction promotion are wired.
- Deviation stop: normal Case/PO allocation occurs for pre-case material.

### Step 8 — C08 operator shell and shared surfaces

- Files: C08 paths in the authoritative Stream C plan.
- Reuse: existing Razor routes, shared partials, `OperationsSnapshot`,
  `site.css`, `site.js` and Lucide sprite.
- Done: Inbox, Search, Work Centre, notifications, correspondence and shared
  interactions use real typed callers with accessible static fallbacks.
- Deviation stop: Web duplicates Core policy or shared assets break Stream B's
  frozen interface.

### Step 9 — C09 integrate, verify and hand off

- Files: Stream C owned paths only.
- Reuse: the existing Stream C branch and its single PR.
- Done: all focused and canonical checks are recorded at the exact pushed SHA;
  every review finding is dispositioned; the PR targets `dev` and stays open.
- Deviation stop: ownership, dependency, source or caller evidence is
  inconsistent.

## Acceptance checks

All C01–C08 focused checks and residual acceptance in the authoritative Stream C
plan pass. Every changed production path has a reachable caller. Exact-head
review covers reuse, simplicity, runtime/query efficiency and abstraction
altitude. Failures remain recorded even after a later pass.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category=Corpus"
pwsh -File ./scripts/Test-MigrationGrants.ps1
pwsh -File ./scripts/Update-TestUiSnapshots.ps1 -Verify
pwsh -File ./scripts/Test-UiCatalogue.ps1
```

## Failure and deviation rules

A nonzero or unavailable command is not PASS. Do not weaken tests, hide
conflicts, fabricate provider/corpus evidence, cross an ownership boundary, or
discard concurrent work. Record the exact failure and retain a resumable branch.

## Stop condition

Stop when the existing Stream C branch and its single open PR contain all
authorized Stream C work, the exact pushed SHA and check outcomes are recorded,
and responsibility can transfer to agent A. Do not merge, deploy, mutate an
external system, close historical PRs, or start another ticket.
