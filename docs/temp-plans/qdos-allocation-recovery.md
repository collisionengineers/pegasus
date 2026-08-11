# QDOS allocation and recovery

Task line: Restore QDOS allocation and recovery on
`task/qdos-allocation-recovery`.

The binding implementation contract is the agreed Sprint 01 contract in the
Traycer epic. This repository plan records the PR-sized change and its local
proof without importing the frozen ADR-0024 or PR #365 work.

## Change

- Persist the typed QDOS case type produced by classification and consume that
  fact for automatic allocation without parsing human-readable evidence.
- Keep standalone Audit fail closed until same-receipt original-report
  evidence and an unambiguous assessment are confirmed; repair current
  `draft_ready` eligibility readers while preserving legacy read mapping.
- Persist a bounded, operator-safe allocation attempt and failure history,
  log failures, and prevent completed-work replay from silently allocating
  after an earlier failed attempt.
- Add the permission-checked, reasoned, idempotent staff retry that reuses the
  failed command and resolves to one immutable Case/PO.
- Make actual Case/link identity plus allocation state authoritative for the
  touched Received, retained-mail, Upload, create-case, MCP, and count
  projections; remove allocation success as a Triage gate while preserving
  independent pre-Case Triage idempotency.
- Add the one SQL migration and focused Core, SQL integration, Web,
  architecture, and browser tests named by the agreed contract. Update only
  current-state documentation made false by the implemented caller proof.

## Boundaries

- Do not copy, depend on, or modify ADR-0024, PR #365, revision `ae9a6d2`, or
  the stable-mailbox-identity worktree.
- Do not change Graph mailbox identity, polling baseline, retained-thread
  grouping, mailbox administration, general Inbox/UI defects, negative
  mileage validation, Image intake routing, or unrelated NOW items.
- Do not perform Outlook, Box, Azure, credential, deployment, production, or
  genuine-mailbox operations. Do not add a project, runtime, store, queue,
  rules engine, generalized recovery framework, or background business retry.

## Verification

Run the exact contract gates:

1. `git diff --check`
2. `dotnet restore ./Pegasus.slnx --locked-mode`
3. `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
4. List and run the named focused Core filters for
   `QdosMailClassificationPolicyTests`, `QdosMailRoutePolicyTests`,
   `DefinitiveIntakeCaseTypeTests`, and `AllocateDefinitiveIntakeTests`.
5. List and run the named non-browser integration filters for
   `QdosAllocationRecoveryTests`, `IntakeAllocationConsumerTests`, and
   `CaseCreateWebTests`, with one integration thread.
6. List `QdosAllocationRecoveryBrowserTests`, then run the non-corpus browser
   lane with two threads.
7. Run `Pegasus.ArchitectureTests` in Release without rebuilding.
8. Scan the final source and base diff for the contract tripwires, protected
   paths, every direct `CaseCreated` consumer, and forbidden completed-work
   allocation re-entry.

The independent Evaluator also performs the agreed 1440x900 visual,
JavaScript-disabled, and keyboard exercises using test-owned fixtures. Passing
local gates is caller proof only; it is not deployment, live-service proof, or
operator acceptance.
