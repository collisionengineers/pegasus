# Research — KANMER-005: cross-actor edit-lease exclusivity

*This records what the current lease paths do, the evidence available for the
reported incident, and the remaining cross-actor identity gap.*

## Question

Can a staff session or Automation Actor replace the other actor type's
unexpired case edit lease, and what concrete change and proof does KANMER-005
need without changing the settled save lifecycle?

## Findings

- The governing behavior is already explicit. `docs/frd/frd-01-case-identity-and-lifecycle.md:83-89` requires one server-owned lease, refusal for a
  missing, expired, or wrong-holder lease, no takeover path, and the same guard
  for Web and MCP Automation Actor callers.
  `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md:186-202`
  repeats that Automation writes use the staff application's lease and version
  guards. `docs/adr/0011-restrict-mcp-to-automation-actor.md:21-40` requires a
  durable Automation identity rather than staff impersonation.
- Staff and Automation claim, renew, and release calls converge on the existing
  Core seams in `src/Pegasus.Core/Lifecycle/CaseCommandSeams.cs` and the same
  `EfCaseWorkflowStore` methods registered by
  `src/Pegasus.Infrastructure/DependencyInjection.cs`. No production caller
  bypasses the shared claim engine.
- `EfCaseWorkflowStore.ClaimAsync` runs a serializable transaction, obtains the
  workflow-row mutation lock, and tests `CaseEditAuthority.IsHeld` before
  clearing stale lease fields or writing a new holder
  (`src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs:114-199`).
  SQL Server uses `UPDLOCK,HOLDLOCK` in
  `AcquireWorkflowMutationLockAsync`. The check is actor-kind agnostic, so a
  genuinely populated, unexpired lease cannot be replaced by a competing
  claim through this path.
- Renew and release take the same row lock and require the current version,
  holder subject, token, and unexpired lease before changing state
  (`EfCaseWorkflowStore.cs:201-342`). Holder-authenticated writes call
  `CaseMutationGuard.RequireLease`, while `Version` and `ConcurrencyToken` are
  persistence concurrency tokens. No staff-only or Automation-only bypass was
  found.
- A successful case mutation increments the workflow version and clears the
  lease in the same transaction through
  `src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs:70-86`. This is
  shared by the case, assessment, workflow, task, document, intake, repair,
  triage, and vehicle mutation stores. It is intentional rather than a lease
  overwrite.
- [[CASE-024]] is in Review on PR 581 and explicitly preserves that behavior:
  successful save ends editing immediately, and its persistence regression
  prevents a later heartbeat from resurrecting the cleared lease. It also
  centralizes the overlapping staff claim, release, and lease-state restoration
  handlers in `CaseMutationPageModel`.
- The user resolved the ticket's ambiguous “save and release” wording on
  2026-08-28: save continues to end the lease. After a rejected competing
  attempt, the holder may either save, which consumes the lease, or explicitly
  release without saving. A later release after a successful save is expected
  to fail because there is no longer a lease to release.
- The reported end state has a concrete, non-defective sequence: Automation
  claims; Automation saves and thereby clears its lease; staff then claims the
  free lease; Automation later calls `edit_end` and is rejected because staff
  is now the holder. This is an inference from the code and settled lifecycle,
  not a reconstruction of the original event.
- A read-only production census on 2026-08-28 found five
  `CaseWorkflows` rows, zero rows retaining a lease holder, zero active lease
  holders, and zero `CaseEditLeaseOperations` rows. Production therefore
  contains no retained evidence from which to reconstruct the reported event.
- The active holder identity is incomplete. `ActionActor` consists of
  `ActorKind` plus `SubjectId`, but `CaseEditLease` and
  `CaseWorkflowEntity.EditLeaseHolder` retain only the subject text.
  `CaseEditAuthority.RequireLease` compares the subject and token without the
  kind, and holder display infers Automation from “subject is not a GUID”.
  Operation-history rows do retain `ActorKind`. A GUID-shaped Automation client
  ID can therefore collapse onto a staff subject or be displayed as staff.
  Production currently configures the non-GUID subject
  `pegasus-automation`, so this is a real identity-model gap but not a credible
  explanation of the observed production sequence.
- Existing persistence coverage proves staff-versus-staff claim conflict and
  expiry, but no real-store test exercises staff versus Automation in both
  directions. Existing Automation ingress tests prove begin and write, while
  MCP refusal tests mostly use fakes. No test synchronizes cross-actor claims
  or asserts every retained field after a rejected cross-actor claim or write.

## Implications

- Reuse the existing serializable transaction, workflow-row lock, Core command
  seams, and mutation guard. KANMER-005 must not add a parallel lock or another
  lease implementation.
- Preserve save-clears-lease. Split the ticket's combined verification into
  rejected competitor then holder save, and rejected competitor then holder
  release without saving.
- Plan the concrete hardening around the complete live holder identity:
  persist and project `ActorKind` beside `SubjectId`, require both for holder
  operations, and stop deriving holder kind from GUID shape. The existing token
  check remains mandatory.
- Prove both actor directions against real persistence: rejected claim and
  forged write leave holder kind, subject, token, operation key, expiry, and
  version unchanged; the holder can still renew or release; a synchronized
  claim race has exactly one winner.
- Add Web evidence that an Automation-held case is read-only to staff and does
  not render a claim action, plus an Automation ingress scenario against a
  staff-held case. A same-subject/different-kind case pins the identity fix.
- Implementation must wait for [[CASE-024]] to merge and then use its shared
  `CaseMutationPageModel` handlers. Reworking the pre-CASE-024 page copies would
  create avoidable conflicts and duplicate the convention.
- The migration must not infer kind from a pre-existing subject. Clear the
  complete ephemeral lease tuple on any row that still holds one before
  enforcing holder-kind consistency; the research-time zero-holder census does
  not guarantee deploy-time emptiness.
- No new runtime, store, package, governing behavior, or architecture boundary
  is needed. A persisted holder-kind field belongs to the existing workflow
  schema and migration stream.

## Open questions

None. The only product choice discovered by research—whether save ends the
lease—was resolved in favor of the current behavior and is recorded in
`open-questions/open-questions.md`.
