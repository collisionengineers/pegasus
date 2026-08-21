# Proof

**Shipped:** PR #486 (`task/tick-051-mail-09-automatic-case-association`), merge `708706b8`
**Deployed:** `git merge-base --is-ancestor 708706b8 4111ad29` → **true** (Release 16, active revision `…--4111ad291779`).

## The finding

The PIR claimed the real live and completed-replay caller *"without executable evidence
that the queued processor actually invokes it"*. The tests exercised the use case and EF
store directly — one tier below the claim.

## Verified in the shipped code

Caller-level evidence now exists at the queued-intake tier:

- `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs:656` drives a real QDOS
  instruction through the queued-intake path with a MAIL-09 claim number, so the assertion
  is about what `ProcessQueuedIntake` does, not about the use case in isolation;
- `tests/Pegasus.IntegrationTests/CaseMatchIntegrationTests.cs:194-218` asserts the written
  association carries `AssociateRetainedMailWithCase.PolicyKey` and `PolicyVersion` —
  proving the association downstream allocation reads is the one MAIL-09 wrote, which is
  the "visible to downstream allocation" bullet;
- `tests/Pegasus.Core.Tests/Intake/CaseMatching/AutomaticMailCaseAssociationTests.cs` keeps
  the policy-level cases.

The "only while unassociated" ordering is enforced in the code the tests drive:
`EfIntakeMutationStore.AutoLinkAsync` refuses a receipt that already has an accepted case
link or any association history, so a second attempt cannot overwrite a prior one.

## Evidence tier, stated exactly

Local and CI only. `docs/capabilities.md` records for this capability: *"No live mailbox,
provider, deployment, or cloud write was performed."* That remains true — the code is
deployed, but no production association has been exercised against a live mailbox, and this
proof does not claim one has. The finding's own constraint was "keep all evidence local;
perform no production association or external write", and that was honoured.
