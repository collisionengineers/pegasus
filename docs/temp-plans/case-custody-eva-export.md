# Case custody and EVA export

## Goal

Complete the existing Case custody and manual EVA handoff paths for an already
allocated Case/PO. The implementation must retain the Case when custody fails,
provide an authenticated and reasoned staff recovery path, store Case and Audit
material under the exact business-readable Box hierarchy, and generate the
accepted deterministic EVA archive only from a Review-stage Case whose required
custody is confirmed.

The detailed acceptance contract is the Generator/Evaluator-agreed Sprint 02
contract in Traycer artifact
`autobuild/qdos-functional-recovery/sprint-02/contract/index.md` (exchange 4,
2026-08-11). That contract is normative for exact hierarchy, failure taxonomy,
idempotency, archive grammar, UI language, frozen tests and evaluation gates.

## Scope and ownership

- Keep business policy in `src/Pegasus.Core`: custody retry authorization,
  failure/replay dispositions, Review-only EVA eligibility, evidence selection,
  deterministic bundle composition and download command rules.
- Extend the existing persistence and outbox seams under
  `src/Pegasus.Infrastructure/Persistence` rather than adding a new store,
  runtime or migration stream. Persist retry attempts, operation keys, leases,
  creation-owner markers, evidence ordinals/versions and EVA revision/download
  history atomically where the agreed contract requires it.
- Repair the existing production Box HTTP adapters under
  `src/Pegasus.Infrastructure/Custody`. Final Case/Audit folder and evidence
  names stay business-readable. Interrupted Case/Audit creation uses the
  predeclared `.pegasus-create-{CreationOwnerToken}` staging protocol, exact
  binding verification and an ETag-guarded same-parent rename. No live Box
  operation is part of this task.
- Route real `RecordEngineerFinding`, Worker external-work, Case-detail and
  Automation composition callers through the shared Core owners. Do not create
  parallel custody or EVA policy in Web, Worker or Infrastructure.
- Extend the existing Case detail surface with truthful custody state,
  staff-only reasoned retry, EVA blockers, generation history and authenticated
  reasoned archive download. Never render raw UUIDs, hashes, remote receipts,
  internal workflow versions or staging markers.
- Reconcile only the existing canonical documentation whose current-state
  claims change. Production activation, Box migration, EVA API delivery,
  named-Engineer assignment, external receipt and operator drag/drop acceptance
  remain explicitly unproved.

## Hard boundaries

- No Outlook, Box, Azure, deployment, credential, production database or EVA
  network operation.
- No new project, runtime, top-level directory, store, queue, deployment unit,
  generic retry framework or accepted ADR.
- No changes to `corpus/`, protected packages under
  `workspaces/ai-centre/skills/`, proposed ADR-0024/PR #365 or another task's
  worktree.
- Initial custody processing and source replay never redispatch a persisted
  processing failure. Only a valid human staff command with reason, rendered
  workflow version, lease and idempotency key may re-enter custody.
- Automation/System actors cannot invoke human custody retry. Automation EVA
  remains composition-gated and is regression proof only.
- EVA is refused outside Review and when required custody, accepted mapping,
  current evidence, eligible images or exact workflow version is missing.

## Implementation sequence

1. Inventory the current Core contracts and all real Web/Worker/Automation
   callers, then move any business decision presently inside
   `EvaHandoffStore` into one shared Core use case.
2. Extend the existing EF model and migration stream for the agreed custody
   attempt/creation marker/evidence and EVA operation history without changing
   immutable Case/PO or principal identity.
3. Implement response-loss-safe Case/Audit Box creation, retained-source and
   managed-document/version paths, exact bindings, replay verification and
   fail-closed conflict handling against the in-memory HTTP boundary.
4. Implement lease-safe Worker processing and reasoned staff custody recovery,
   including cancellation rethrow, terminal redelivery/poison behavior and
   once-only later-Audit custody allocation from `RecordEngineerFinding`.
5. Implement Core-owned Review/custody/evidence eligibility, exact 13-key JSON,
   all eligible images, deterministic UTF-8 manifest/archive and once-per-Case
   first-handoff proxy plus revision/download idempotency.
6. Wire the Case detail POST/download routes and operator-safe projection, then
   reconcile composition and current-state documentation.
7. Add the exact frozen tests in existing classes and run the agreed gates at
   the clean candidate commit.

## Verification

- `dotnet restore Pegasus.slnx --locked-mode`
- `dotnet build Pegasus.slnx --configuration Release --no-restore`
- Use `dotnet test --list-tests` to prove every exact frozen FQN from the agreed
  contract exists once, then run the selected Core (11), Architecture/Worker
  (2), serialized SQL/Web (14), and Browser (1) inventories with the contract's
  exact commands and counts.
- Run the applicable broader repository test shards and architecture lane.
- Run EF's pending-model-change check and verify the new migration against both
  fresh schema and the prior QDOS-allocation schema.
- Exercise the named 1440x900, 100% zoom evaluator journey with keyboard and
  JavaScript-disabled paths using local fixtures only; screenshots are evaluator
  evidence, not product source.
- Confirm `git diff --check`, a clean worktree, exact local/remote candidate SHA,
  evaluator `CONTRACT PASS` plus `RUBRIC PASS`, and green PR
  `repository-check` before merge authority is requested.

## Review questions

The independent reviewer must answer both repository workflow questions:

1. Did this plan miss anything implied by the claimed Case custody and EVA
   export task?
2. Did the implementation miss anything in this plan or widen beyond it?

## Delivery state

Implementation and bounded local verification are complete. The frozen Core,
Architecture/Worker, serialized SQL/Web and Browser inventories pass at their
contracted counts; the complete Core, Architecture, three-shard non-Browser and
two-thread Browser regressions also pass, and EF reports no pending model
change. Independent evaluator review, pull-request CI, production Box/deployment
proof and operator acceptance remain separate and pending.
