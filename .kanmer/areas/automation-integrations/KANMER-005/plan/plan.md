# Plan — KANMER-005: enforce exclusive editing leases between staff and Automation Actors

*This plan hardens the existing shared lease around complete actor identity; it
does not replace the lease or reinterpret the reported production event.*

## Objective

Make the one case edit lease retain and enforce holder identity as
`(ActorKind, SubjectId, token)` across Core, SQL persistence, Web, and MCP so a
Staff actor and an Automation actor can never be treated as the same holder,
while preserving the current atomic claim lock, CASE-024 heartbeat, and
save-clears-lease lifecycle.

## Starting state

- `EfCaseWorkflowStore.ClaimAsync` already serializes claims with one short
  SQL transaction and `UPDLOCK,HOLDLOCK`; it refuses every unexpired lease
  before replacing any holder. Renew, heartbeat after [[CASE-024]], release,
  and writes use the same persistence owner and Core guard.
- The reported takeover is not reproducible through the current claim path.
  The reliable before-fix defect is narrower: the live row and Core lease
  retain only `SubjectId`, so an Automation actor deliberately given the same
  subject text and valid token as a Staff actor passes the holder comparison,
  and a GUID-shaped Automation subject is displayed as Staff. The incident's
  final state is also consistent with Automation saving, thereby clearing its
  lease, before Staff legitimately claims and Automation later calls
  `edit_end`.
- Production read-only evidence at `2026-08-28T10:14:01Z` found six workflow
  rows, no retained or active holders, and no lease-operation rows. Azure
  metrics for the reported date showed one Web replica, no restart, SQL at
  100% availability, no failed connections, and no deadlocks. Application
  telemetry ended before the incident window, so the event cannot be
  reconstructed.
- `ActionActor` already owns the canonical `ActorKind` plus `SubjectId`
  identity. `CaseEditLeaseOperations` already records both dimensions and
  request hashes already include them. These existing structures are reused;
  no second identity type, actor list, port, or lease implementation is added.
- [[CASE-024]] formally blocks this ticket. PR 581 remains open at `747ecc47`
  with unresolved P1 findings and operator sign-offs. It owns the heartbeat
  and shared Details/Assessment handler base that this change must extend.

## Governing docs

- **Meets** `docs/frd/frd-01-case-identity-and-lifecycle.md`: the existing
  single server-owned lease remains the sole edit authority; Web and MCP use
  the same guard; wrong-holder, expired, and stale-version attempts fail
  closed without takeover.
- **Meets** `docs/frd/frd-10-mcp-automation-and-actor-boundary.md`: MCP keeps
  its resolved Automation identity and invokes the same Core claims and
  mutations as Web. No caller-provided actor data or separate policy path is
  introduced.
- **Meets**
  `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`:
  Automation direct writes continue through the same lease, version, replay,
  and Core command safeguards as Staff saves.
- **Meets** `docs/adr/0011-restrict-mcp-to-automation-actor.md`: persisted
  holder kind makes Automation durably distinct from Staff rather than
  inferring or impersonating identity from subject shape.
- **Meets**
  `docs/adr/0031-automation-actor-contract-without-eva-export-tools.md`: the
  accepted direct-write inventory and scopes remain unchanged. No EVA tool,
  MCP schema expansion, or new automation authority is added.
- No linked governing document is modified and no new ADR is required. If
  implementation needs changed lease behavior rather than enforcement of
  these accepted rules, stop and return to `kanmer-docs` with operator
  authorization.

## Technical guidance

The following Microsoft sources are non-governing implementation guidance:

- [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
  supports the existing application-managed `Version` and `ConcurrencyToken`;
  no extra `rowversion` is added.
- [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
  and [SQL Server table hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table?view=sql-server-ver17#arguments)
  support retaining the existing bounded transaction and update lock.
- [EF connection resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency#execution-strategies-and-transactions)
  requires whole-transaction replay if retry execution strategies are ever
  enabled; this ticket adds no partial retry.
- [EF migration customization](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing#customize-migration-code)
  supports a nullable no-default column plus reviewed migration SQL rather
  than runtime entity access.

## Required changes

1. Extend `CaseEditAuthority` with one typed holder-match function that returns
   true only when retained kind and subject exactly equal the supplied
   `ActionActor`. Reuse it in `RequireLease` and every Web self-holder check.
   `RequireLease` keeps the existing expiry, token-hash, and conflict order;
   a missing or undefined retained kind cannot authorize a holder operation.
2. Add non-null `ActorKind HolderKind` to `CaseEditLease`, because every lease
   issued or replayed by the new runtime has a proven actor. Add nullable
   `ActorKind? HolderKind` to `CaseEditLeaseSnapshot`, because an additive
   rollout can temporarily expose a row written by the old Web revision
   without the new column populated.
3. Change `IDescribeCaseEditAuthorityHolder` to receive the recorded kind.
   Automation is described only when the kind is `Automation`; Staff account
   lookup occurs only when the kind is `Staff` and the subject is a valid
   non-empty GUID. Null, unsupported, or malformed identities render the
   existing unnamed-holder state and never expose the subject identifier.
4. Add nullable `EditLeaseHolderKind` (`nvarchar(40)`, no default) to
   `CaseWorkflowEntity`. Claim writes `Actor.Kind`; renew and CASE-024
   heartbeat preserve and return the proven kind; replay checks the live tuple
   and operation row against both kind and subject; release and every write
   require the complete identity; `CaseMutationGuard.ClearLease` clears the
   kind with the existing token, hashes, holder, key, and expiry.
5. Preserve atomic exclusion for malformed rollout rows. Claim continues to
   reject any unexpired tuple based on server expiry before considering actor
   identity. Query projections retain an active snapshot even when kind is
   null/unknown; operations mark such a tuple unknown, and Web remains
   read-only. No actor can write, renew, heartbeat, replay, or release it.
   Once expired, the existing locked clear/reclaim path replaces the whole
   tuple with a complete new identity.
6. Generate one `CaseEditLeaseActorIdentity` EF migration after rebasing on
   the post-CASE-024 `origin/dev`. Its `Up` adds the nullable column, then
   backfills kind only from an exact claim/renew operation matching case,
   operation key, holder subject, result version, token hash, and expiry.
   Rows with a holder but no exact match have the entire ephemeral lease tuple
   cleared. `Down` drops the new column; it cannot restore an unmatched
   transient lease, but no Case data, version, identity, or history is lost.
7. Do not add a default or paired-null check constraint in this release. The
   old Web revision remains live while the migration is applied and would
   write a null kind. Keeping the schema additive avoids ADR-0030's documented
   write outage and rollback gap; runtime fail-closed checks enforce the
   transition safely.
8. Do not add GRANT SQL. Production and committed migration evidence show
   object-level Web and Worker grants already cover the new column. Keep the
   existing CaseWorkflows Web/Worker `SELECT, INSERT, UPDATE`, operation-table
   permissions, role membership, and `DELETE` denies unchanged, and run the
   runtime-role migration census.
9. After [[CASE-024]] merges, update its shared `CaseMutationPageModel` and
   Details/Assessment restoration/display callers to use full identity. An
   Automation-held or unidentified active lease leaves Staff read-only and
   renders no claim action; a Staff-held lease is unavailable to Automation.
   Update the Triage linked-Case holder display for the same typed comparison.
10. Keep the external MCP tool names, scopes, descriptions, result schemas,
    authentication, and audit contract unchanged. Exercise
    `pegasus_case_edit_begin`, an Automation write, renew/heartbeat where
    applicable, and `pegasus_case_edit_end` through the real ingress and the
    shared persistence owner.
11. Add regression evidence in four layers: Core identity/display unit tests;
    real SQL persistence tests in both actor directions plus a synchronized
    separate-context race; old-schema migration/backfill/recovery tests; and
    Web/MCP caller tests. Separate holder-continuation scenarios prove
    (a) save succeeds and consumes the lease and (b) release succeeds when no
    save occurred. Release after a successful save remains an expected
    refusal.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `src/Pegasus.Core/Workflow/CaseEditAuthority.cs` | One typed identity matcher, complete lease authorization, and kind-driven holder description. |
| Modify | `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs`; `src/Pegasus.Core/Cases/CaseQueries.cs` | Add holder kind to issued lease and active snapshot contracts. |
| Modify | `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs`; `CaseWorkflowModelConfiguration.cs`; `CaseMutationGuard.cs` | Persist, parse, authorize, and clear holder kind. |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs`; `EfCaseQueryStore.cs`; `EfOperationsStore.cs` | Carry complete identity through the existing lock, replay, heartbeat, and projections. |
| Add/generated | `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseEditLeaseActorIdentity.cs`; matching `.Designer.cs` | Add/backfill/clear the column and provide schema rollback. Review generated SQL; do not hand-copy a model. |
| Modify/generated | `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Record the nullable length-40 column. |
| Modify | `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs`; `Pages/Cases/Details.cshtml.cs`; `Pages/Cases/Assessment/Index.cshtml.cs`; `Pages/Triage/Details.cshtml.cs` | Reuse CASE-024's merged handlers and compare/display typed holder identity. |
| Modify | `tests/Pegasus.Core.Tests/Workflow/CaseEditAuthorityTests.cs` | Pin identity and display policy. |
| Modify | `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`; `CaseWorkflowMigrationTests.cs`; `IntakePersistenceIntegrationTests.cs` | Prove atomic behavior, migration transform/down shape, and the exact migration census. |
| Modify | `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`; `CaseEditModeWebTests.cs`; `AutomationAssessmentIngressTests.cs` | Prove real Web and MCP actor-direction behavior and preserve CASE-024 lifecycle. |
| Inspect/test | `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Prove existing object grants cover the changed table; modify only if evidence shows the census is stale. |

## Do not modify

- `docs/operator-notes.md`, the five linked governing documents, or any product
  behavior. This is enforcement of an accepted rule.
- `CaseMutationGuard.Complete`, save-clears-lease, the five-minute duration,
  CASE-024's heartbeat interval, heartbeat failure behavior, or Assessment
  edit-mode design.
- The lock query, isolation level, transaction lifetime, concurrency-token
  model, or SQL retry configuration.
- MCP inventory, public result schema, OAuth/client registration, scopes,
  operation-key format, Automation subject configuration, or audit/history
  policy.
- Runtime roles, membership, infrastructure, Azure resources, deployment
  state, monitoring/alert definitions, current-state docs, or release records.
- Any unrelated mutation store, new package, project, runtime, top-level
  directory, or `corpus/` content.
- [[CASE-024]]'s branch/worktree or unresolved review findings. Consume only
  its merged `dev` result.

## Constraints

- [[CASE-024]] must be Done/merged and the typed blocker must be clear before
  `kanmer-execute` takes KANMER-005. Create the ticket worktree from freshly
  fetched `origin/dev`; do not resume or copy the current shared checkout.
- Use Windows and PowerShell 7 for the whole evidence run. Paths and recorded
  commands remain repo-root-relative with forward slashes.
- Core remains the single owner of holder identity and lease policy.
  Infrastructure stores strings and performs fixed-time token comparison;
  Web and MCP only call the shared policy.
- The migration uses historical column/table names and explicit SQL only. It
  must not resolve the current `DbContext`, current entity classes, or a
  fabricated actor kind.
- The nullable column is a rollout compatibility boundary, not permission to
  accept incomplete identity. All new claims populate it; all incomplete
  active rows are unavailable; all clear paths clear it.
- No dependencies are added. No cloud or production write is authorized by
  this implementation plan.
- Structured lease telemetry is useful diagnostic follow-up but is not
  correctness evidence and is outside this defect's scope.

## Ordered steps

1. Confirm PR 581 has merged to `dev`, CASE-024's review findings and sign-offs
   are disposed, the typed blocker is clear, and its merged heartbeat/save
   tests are green. Obtain KANMER-005's execution packet, take exactly its
   recorded branch/worktree, fetch `origin/dev`, and verify the worktree starts
   at the current merged base.
2. Add failing Core tests for same-subject/different-kind refusal, null/unknown
   holder kind, kind-driven Automation/Staff description, and unchanged
   expiry/token conflict ordering. Then implement the one Core matcher and
   update the two lease contracts.
3. Add the entity/model field and thread it through claim, renew, CASE-024
   heartbeat, replay, release, `CaseMutationGuard`, Case queries, and Operations
   projections. Reuse the existing row lock and token helpers; ensure every
   clear path clears kind and every new lease result carries it.
4. Generate the EF migration from the current model. Customize `Up` with the
   exact-operation backfill and unmatched-tuple clear, implement the
   column-only `Down`, review the SQL, update the committed migration census,
   and add predecessor-to-current migration tests plus partial/unknown-row
   runtime recovery tests.
5. Adapt CASE-024's merged `CaseMutationPageModel`, Details, Assessment, and
   Triage holder checks to the Core matcher and typed descriptor. Preserve the
   current operator copy and page economy; add no new explanation or controls.
6. Extend real persistence tests with Staff-held/Automation-competes and
   Automation-held/Staff-competes claim and write cases. Assert rejected
   operations leave kind, subject, plaintext token, token/request hashes,
   operation key, expiry, version, and concurrency token unchanged. In
   separate scenarios prove holder renew, save-consumes-lease, and release
   without save.
7. Add a synchronized claim race using separate contexts and SQL connections.
   Assert exactly one success and one conflict, persisted kind/subject/token
   belong to the winner, and no concurrency result is swallowed. Add the
   same-subject/different-kind valid-token negative case.
8. Extend Web and real Automation ingress tests: Automation-held Details and
   Assessment stay read-only to Staff with no claim action; Staff-held MCP
   begin/write is refused; a rejected competitor cannot prevent the holder's
   next valid action. Keep MCP response schemas unchanged.
9. Run the focused Core, persistence, migration, runtime-role, Web, and MCP
   tests. Fix production code or truthful test setup; never weaken an
   assertion, suppress a conflict, or omit a failing result.
10. Run the locked restore, Release build, and full non-Corpus solution test
    with exit codes. Inspect the branch diff for generated artifacts, secrets,
    machine paths, migration safety, and unauthorized scope.
11. Run the required simplification pass over this branch's diff using
    independent reuse, simplification, efficiency, and altitude lenses. Apply
    behavior-preserving findings and append a dated findings/dispositions
    section to this plan.
12. Commit logical slices, write the post-implementation report with exact
    command results and limitations, record reachable commits/PR on the
    ticket, open the PR to `dev`, and hand off for independent
    `kanmer-review`.

## Acceptance checks

- A live lease issued by the new runtime persists and returns the exact
  `ActorKind`, `SubjectId`, and token of its holder. Same subject text with a
  different kind is a competitor, even when a test deliberately presents the
  valid token.
- With Automation holding, Staff cannot claim or write; with Staff holding,
  Automation cannot claim or write. Every rejected attempt leaves every lease
  and concurrency field byte-for-byte/logically unchanged.
- After a rejected competitor, the real holder can renew; in one scenario it
  saves successfully and the full lease tuple clears, and in a separate
  scenario it releases successfully without saving. A later release after
  save is refused.
- A synchronized Staff/Automation claim race on separate SQL connections has
  exactly one winner, one surfaced conflict, and a persisted identity matching
  the winner.
- An unexpired null/unknown-kind rollout row cannot be claimed or used for a
  mutation and remains visible as unavailable; after expiry, the existing
  locked path clears it and writes one complete new tuple.
- The migration backfills an exact historical claim/renew row, clears an
  unmatched transient tuple, leaves a no-holder row null, migrates down to the
  immediate predecessor schema, and leaves the committed chain with no
  pending migration. The irreversible unmatched clear is recorded as
  transient lease loss only.
- Production callers are exercised: Staff Details/Assessment shared handlers,
  Triage linked-Case claim/display, and MCP
  `pegasus_case_edit_begin`/write/end all reach the registered
  `EfCaseWorkflowStore`/`CaseMutationGuard` owner.
- Existing Web and Worker table-level permissions pass
  `AzureSqlRuntimeRoleMigrationTests`; no new grant, role, identity, secret,
  package, or runtime dependency appears in the diff.
- CASE-024 heartbeat and save-clears-lease regressions remain green. The MCP
  public contract and operator-visible copy remain unchanged.
- Every focused and canonical command exits `0`; the post-implementation
  report records commands, exit codes, migration/permission evidence, and the
  inability to reconstruct the historical incident.

## Commands

Run from KANMER-005's recorded ticket worktree in PowerShell 7 on Windows:

```powershell
gh pr view 581 --json state,mergeCommit,statusCheckRollup,headRefOid

dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore

dotnet ef migrations add CaseEditLeaseActorIdentity `
  --project ./src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj `
  --startup-project ./src/Pegasus.Web/Pegasus.Web.csproj

dotnet build ./Pegasus.slnx --configuration Release --no-restore

dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj `
  --configuration Release --no-build `
  --filter "FullyQualifiedName~CaseEditAuthorityTests"

dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj `
  --configuration Release --no-build `
  --filter "FullyQualifiedName~CaseWorkflowPersistenceTests|FullyQualifiedName~CaseWorkflowMigrationTests|FullyQualifiedName~IntakePersistenceIntegrationTests|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests|FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~CaseEditModeWebTests|FullyQualifiedName~AutomationAssessmentIngressTests"

dotnet test ./Pegasus.slnx --configuration Release --no-build `
  --filter "Category!=Corpus"
```

After migration generation, rerun the Release build before every test command
that uses `--no-build`. The final recorded gate is the canonical locked
restore, Release build, and full non-Corpus test sequence above; generation
itself is not verification.

## Failure and deviation rules

- Stop before taking the ticket if [[CASE-024]] is unmerged, still blocks the
  ticket, or its final merged contract differs from this plan. Refresh
  research/files/plan against the merged SHA rather than editing its branch.
- If a current-code reproduction shows a real active-lease overwrite through
  the shared claim path, stop and record the new path/evidence. Do not hide it
  behind the holder-kind change or add a second lock.
- If the migration cannot remain additive without a constraint, default,
  downtime, production data change, or ADR-0030 release exception, stop for a
  revised plan and exact operator authorization.
- Stop on unknown actor values, partial lease behavior not covered above, a
  grant mismatch, generated SQL that touches non-lease data, or an unexpected
  CASE-024 overlap. Surface the inconsistency; do not infer an actor kind.
- Stop on any failed test or command. Preserve the first failure and later
  rerun evidence; never delete/weaken assertions, swallow a concurrency
  result, or call an inconclusive check a pass.
- A new dependency, port, runtime, MCP schema, telemetry contract, governing
  change, cloud write, deployment, or unrelated refactor is scope expansion.
  File or route it separately rather than absorbing it.
- Preserve all unrelated user/agent changes and use only the ticket's recorded
  worktree. Do not stash, reset, clean, merge, or stage another ticket's work.

## Stop condition

Stop after the implementation is complete in KANMER-005's ticket worktree,
all focused and canonical checks pass, the simplification findings are
dispositioned in this plan, the post-implementation report and reachable
commits are recorded, and a PR targeting `dev` is open for independent review.
Do not merge the PR, deploy, write proof, or start another ticket.
