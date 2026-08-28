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

## Microsoft Learn and Azure extension — 2026-08-28

### Source routing

- Kanmer `get_sources` resolved no project-declared MCP, plugin, or
  `llms.txt` sources for this ticket's area and labels. At the operator's
  explicit request, this extension used the already-connected Azure MCP and
  Microsoft Learn MCP directly. They are technical evidence, not authority
  over the repository's linked FRDs, ADRs, code, or release rules.

### Concurrency and transaction guidance

- Microsoft Learn's [EF Core concurrency guidance](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
  confirms that application-managed concurrency tokens are appropriate when
  the application controls the protected unit. Pegasus already marks both
  `CaseWorkflowEntity.Version` and `ConcurrencyToken` as concurrency tokens.
  Adding SQL `rowversion` for this holder-kind correction would duplicate the
  existing protection and is not part of the fix.
- Microsoft's [transaction guidance](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
  and [SQL Server table-hint reference](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver17#arguments)
  support the existing shape: a short transaction, one workflow row selected
  with `UPDLOCK`, and serializable-range behavior from `HOLDLOCK`. The SQL
  locking guide specifically describes update locks as the way to avoid a
  shared-to-exclusive conversion race. Retain the current
  `SERIALIZABLE` + `UPDLOCK,HOLDLOCK` claim/renew/release boundary and extend
  the identity tuple inside it; do not broaden the hints or hold a transaction
  across an editing session.
- Microsoft documents [optimized locking](https://learn.microsoft.com/en-us/sql/relational-databases/performance/optimized-locking?view=sql-server-ver17#best-practices-with-optimized-locking)
  for Azure SQL Database and notes that explicit locking hints still take
  effect. That makes the existing single-row hint a deliberate exception, not
  a pattern to copy to other queries.
- EF's [connection-resiliency guidance](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency#execution-strategies-and-transactions)
  requires an explicitly started transaction to be replayed as one unit when
  retrying execution strategies are enabled. Pegasus currently calls
  `UseSqlServer` without `EnableRetryOnFailure`; conflicts and deadlocks
  surface. KANMER-005 must not add a partial retry around one command. A future
  retry change would replay the entire idempotent lease operation through
  `CreateExecutionStrategy` and is separate work.

### Migration guidance and refinement

- Microsoft's [migration guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing#customize-migration-code)
  supports adding the new column as nullable with no default, reviewing the
  generated migration, and using explicit migration SQL for the data
  transformation. The migration must not call the current `DbContext` or
  current entity types because they can drift from the historical schema.
- This refines and supersedes the earlier blanket-clear migration instruction.
  Add nullable `EditLeaseHolderKind` with no default. Backfill it only from the
  exact current `CaseEditLeaseOperations` row: matching case, operation key,
  holder subject, claim/renew operation kind, result version, token hash, and
  expiry. That row is durable evidence of the actor kind; GUID shape and a
  default of `Staff` are not. Clear the complete ephemeral lease tuple only
  for a retained holder that has no such exact match.
- Microsoft documents [check constraints](https://learn.microsoft.com/en-us/ef/core/modeling/indexes#check-constraints)
  as database invariant enforcement, but a paired-null holder constraint is
  intentionally not added in this release. Pegasus applies migrations before
  activating the new packages; the currently running old Web revision can
  still claim a lease without the new column. A constraint would therefore
  make that otherwise additive migration break the live old writer and would
  invoke ADR-0030's non-additive-release consequences. The new runtime instead
  treats a missing or unrecognized retained kind as an active, unidentified
  competing holder until expiry: it cannot be taken over or used for a write,
  and expiry permits the existing atomic clear/reclaim path.
- `Down` drops only the added column. Clearing an unmatched transient lease is
  intentionally not reversible; it discards no Case data, version, identity,
  or history. Migration tests must prove exact-match backfill, unmatched
  tuple clearing, the no-holder case, and new-runtime refusal/recovery for a
  direct-SQL partial or unknown-kind tuple.

### Read-only Azure evidence

- Azure MCP resolved the exact production database as `pegasus` on
  `pegasus-prod-sql-252ow37gij` in UK South. Its management status was
  `Online` on Standard S0. The SQL server was `Ready` and the Web Container
  App provisioning state was `Succeeded`.
- Direct read-only SQL at `2026-08-28T10:14:01Z` found six workflow rows, zero
  retained holders, zero active holders, and zero lease-operation rows. An
  earlier census in the same research session saw five workflow rows; one Case
  was created between reads while every lease count remained zero. There is
  still no retained lease evidence from which to reconstruct the report.
- The database has snapshot isolation `ON`, read-committed snapshot enabled,
  and accelerated database recovery enabled. Those database settings do not
  replace the explicit write lock protecting the claim decision.
- Production object grants already cover the added column. The Web runtime
  role has `SELECT`, `INSERT`, and `UPDATE` on `CaseWorkflows` and `SELECT` and
  `INSERT` on `CaseEditLeaseOperations`; the Worker role has `SELECT`,
  `INSERT`, and `UPDATE` on `CaseWorkflows` and `SELECT` on the operation
  table. Both roles are denied `DELETE`. Staff and Automation enter through
  the Web runtime role, so Azure RBAC and SQL grants neither distinguish nor
  own actor-kind exclusivity. No new grant migration is required, but the
  existing runtime-role migration tests and bootstrap census must stay green.
- For `2026-08-18T12:00:00Z`–`16:00:00Z`, Azure Monitor reported 648 Web
  requests, exactly one replica, zero restarts, SQL availability at 100%,
  zero failed connections, and zero deadlocks. This rules out those platform
  signals as an explanation for a holder rewrite; it does not prove the
  application invariant.
- Application Insights requests/exceptions/traces and Container App console
  logs stopped before noon that day despite continuing platform request
  metrics. There are therefore no correlated application records for the
  reported window. Resource Health and AppLens were also not usable for this
  topology: Container Apps were reported unsupported, while database/workspace
  Resource Health calls returned an authorization/provider-registration
  error. Those results are evidence limitations, not healthy-resource claims;
  no provider registration or other Azure write was attempted.
- Azure best-practice guidance favors managed identity, parameterized SQL,
  bounded retry semantics, and structured monitoring. The existing managed
  connection and parameterized EF/SQL paths remain. New lease telemetry or an
  alerting contract would be separate observability scope; correctness in this
  ticket is proved by the persisted invariant and synchronized real-store
  tests, never by the absence of Azure alerts.

### Planning consequences

- [[CASE-024]] remains in Review on PR 581 at `747ecc47`; CI is green, but four
  P1 review findings and two operator sign-offs remain unresolved. It now
  formally blocks KANMER-005. Execution waits for it to merge and starts from
  a freshly updated `origin/dev`, then extends its heartbeat and shared page
  handlers rather than the pre-merge copies.
- The complete new-claim identity is `(ActorKind, SubjectId, token)`. The Core
  lease returned by claim/renew/heartbeat carries a non-null `ActorKind`; the
  persisted column remains nullable only for migration/rollout compatibility.
  Query and Web projections must preserve an unidentified invalid state
  fail-closed instead of inferring actor kind from the subject.
- Cover both Staff/Automation directions, same-subject/different-kind, exact
  state preservation after rejected claims and writes, one-winner synchronized
  claims on separate contexts/connections, the holder's renew/release path,
  and the settled save-clears-lease behavior.
