# Post-implementation report — TICK-058 (API-01)

> **Status, 2026-08-29: PR-open, review-blocked.** Not "landed-pr-ready".
> The three confirmed-live P1s are closed — two fixed in this PR, one deferred
> to [[AUTO-012]] — but no independent review has run against the current head.
> See § Round 2 for what round 1 of this report got wrong.

## What changed, and why the contract was rewritten

The contract accepted on 2026-08-28 took `multipart/form-data` files plus a
`providerReference` and nothing else, then relied on the Principal's instruction
extraction policy to read the business values back out of the submitted
documents. That policy recognises QDOS only. QDOS arrives by e-mail, so the
route had no caller; and for any other Principal its own accepted text conceded
the outcome — "retained for sorting rather than allocated" — which is to say
API-01 could not create a case for the providers it exists for.

The operator replaced it the same day: **one endpoint, taking a declared JSON
instruction with its files inline**. A provider integrating over HTTP already
holds the fields and states them; nothing is read back out of a document.

## Shape

`POST /api/provider/v1/submissions`, `application/json`. Credential, feature
gate, rate limit, idempotency header and status codes are unchanged from the
ingress already built; only the request shape and what happens to it changed.

**One submission is one intake receipt.** The retained source is the request
exactly as it arrived and the submitted files are that receipt's attachments —
the shape an e-mail instruction already has. This was the design decision the
plan did not anticipate and the operator settled: retaining each file as its own
receipt would scatter one instruction across many, and a declared Audit could
not then find its original report among its own evidence.

**One substitution, not a second pipeline.** `ProcessIntake.AssessAsync` returns
a declared assessment for the `provider_api` channel and never routes,
classifies or extracts. Allocation, Triage creation, custody, action history and
the durable Worker path are untouched and carry a declared instruction exactly
as they carry an extracted one.

## Decisions recorded

| Decision | Where it lives |
| --- | --- |
| `auditreport` = Inspection + Audit, and carries no incoming report or verdict — only a standalone `audit` does | FRD-01 § Case types; `ProviderInstructionKinds` |
| A declared Audit verdict derives the `a.`/`ap.` reference | FRD-01 amended; `DeclaredAuditReportAsync` |
| `triage` opens a Triage and allocates no Case/PO | FRD-03 amended |
| The body's `principal` is a cross-check, never a selector — mismatch is 403 | FRD-09; `DeclaredPrincipalMatches` |
| `YourRef` is `claimNumber`, one field | FRD-09 |
| `AddCaseNote` admits **one** kind beside Staff — `ActorKind.Provider`, on its own right. Automation stays denied | `AddCaseNote`; TICK-058 open questions |
| Envelope 30 MiB decoded / 42 MiB body | `IntakeEnvelopeLimits` — **still wants operator confirmation** |

## Corrections made against the approved plan

- **No acceptance actor-guard change** to `AcceptIntake` or
  `EfCaseAcceptanceStore`. `AttemptAutomaticAsync` already allocates as the
  system worker, so neither needed widening and a Provider arm in either would
  have had no caller. Provider attribution lives on the submission's own action
  history and on the case note the provider wrote, which is where FRD-09 puts
  it. The open-questions item that claimed all three were widened has been
  corrected and the unimplemented half parked with this reason.
- **`inspection.deadline` dropped from the wire schema.** The snapshot asserts
  the accepted deadline equals the draft's inspection date; the deadline is the
  inspection date, as it is for the mail route.
- **`auditreport` carries no verdict** — the plan had both Audit kinds
  requiring one.

## Defects found and fixed on the way

1. **`IntakeEvidenceSource` had two persisted code maps, already drifted**
   (`EfIntakeReceiptStore`, `InspectionAddressResolutionStore`). Receipts wrote
   the new member and the address-resolution snapshot then refused to read it
   back, failing allocation with an unclassified fault whose safe reason says
   only "The case could not be created." One owner now
   (`IntakeEvidenceSourceCodes`). Latent on `dev` before this ticket.
2. **The scaffolded migration re-added `CaseWorkflows.EditLeaseHolderKind`** —
   a migration merged from `dev` carries an earlier timestamp than this branch's
   own, so the branch's last Designer snapshot predates it and the diff saw the
   column as missing. Removed by hand, reason recorded in the migration.
   Follow-up raised as **DELIV-032**; it will recur on any branch that merges
   `dev` and then scaffolds.

## Round 2 — corrections to this report (2026-08-29)

An adversarial verifier re-ran the branch and refuted four claims. All four
stand; each is corrected here and in the code.

1. **An undisclosed behaviour change.** Commit 2804ebb6 deleted the
   `AddCaseNote` guard `if (request.Actor.Kind != ActorKind.Staff)` outright,
   which admitted `ActorKind.Automation` as well as `ActorKind.Provider`, and
   replaced the existing negative test `AnAutomationActorCannotWriteAnOperatorNote`
   with `AnAutomationActorMayWriteANote` — an inverted expectation, not a new
   one. The only recorded authority widened the guard by `ActorKind.Provider`
   alone. **Round 1 of this report did not mention any of it.** The production
   guard is now narrowed to `Staff or Provider` and the original negative
   assertion is restored byte-for-byte (only a doc comment added above it);
   `git diff origin/dev` over `tests/` now shows additions only.
2. **The outcome label was wrong.** Round 1 recorded "landed-pr-ready" while
   its own dispositions named a CONFIRMED merge blocker. Corrected at the head
   of this document.
3. **The BOM claim was scoped narrow.** Round 1 said the BOM question was
   settled after restoring `docs/capabilities.md`. Commit 2804ebb6 stripped the
   UTF-8 BOM from **seven** further files, left stripped and unmentioned:
   `src/Pegasus.Core/Intake/IntakeAllocation.cs`,
   `src/Pegasus.Infrastructure/DependencyInjection.cs`,
   `src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs`,
   `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`,
   `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs`,
   `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`,
   `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`.
   Two of them (`DependencyInjection.cs`, `PegasusDbContext.cs`) are edited by
   other EPIC-011 lanes this wave. All seven are restored; no documentation or
   CI job requires the strip — `Test-MigrationGrants` and
   `Test-MarkdownPlacement` neither read nor assert a preamble on `.cs` files.
   A byte comparison of every changed file against `origin/dev` now reports no
   file whose BOM state changed.
4. **A ticked open question the code did not implement.** Corrected — see
   § Corrections made against the approved plan and the open-questions document.

## Evidence (round 2, 2026-08-29)

- `dotnet build ./Pegasus.slnx --configuration Release`: **succeeded, 0
  warnings, 0 errors**.
- `Pegasus.Core.Tests --filter "FullyQualifiedName~ProviderApi|FullyQualifiedName~AddCaseNote"`:
  **23 passed, 0 failed, 0 skipped**.
- `Pegasus.IntegrationTests --filter "(FullyQualifiedName~ProviderApi|FullyQualifiedName~IntakePersistenceIntegrationTests|FullyQualifiedName~CaseNotePersistence)&Category!=Browser&Category!=Corpus"`
  (SQL, real HTTP through the composed host): **21 passed, 0 failed, 0
  skipped**, 1 m 30 s.
- `scripts/Test-MigrationGrants.ps1`: **passed** — 85 migration files, every
  created table granted or exempted.
- `scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD`: **passed**.
- Full suite, Browser category and the snapshot/catalogue scripts were **not**
  run here; the orchestrator owns them.

Round 1's figures (Core 1110 passed; `ProviderApiSubmissionTests` 8 passed;
`Test-MigrationGrants` 84 files) were re-run by the verifier and matched — they
are superseded, not withdrawn.

## What this is not

**No provider has called this**, in any environment. The feature gate is off,
no credential has been issued, and TICK-058 carries `requires-live-approval`:
issuing a live credential needs exact-target approval first. The evidence above
is a green build and an exercised in-process caller — not a deployed feature.

## Round 3 — dev merge and unit-test repair (2026-08-29)

Commit `1688504a` merged `origin/dev`. The only conflict was the deletion of
`AssessmentDamageAndCopyWebTests.cs`; the deletion was accepted because ENG-025
deleted it on `dev`, its replacement `AssessmentCopyWebTests.cs` exists on both
heads, and `AssessmentWorkspaceTestData.cs` carries the widened claimant data.

Commit `79a4aaf9` repaired the latent activity-listener isolation defect in
`ImmediateIntakeDispatchTests`. `ExecuteCommittedAsync` creates one publication
span; the second observed span came from a parallel test through another
`ActivitySource` with the same process-wide name. The test now roots the call in
its own activity and observes that trace only. `Assert.Single` and both existing
tag assertions remain; parent, publication-path and status assertions were
added. The focused class passed 5/5 and the whole Core test project passed
1152/1152 three consecutive times.

This evidence was recorded in the plan and checklist but was omitted here. The
round-3 hand-off label `pr-ready` was therefore also wrong. The accurate status
remained **PR-open, review-blocked**.

## Round 4 — verifier remediation (2026-08-29)

The verifier's CI finding was correct: run `33245767986` on `79a4aaf9` was
cancelled when `sql-integration (1)` exceeded its 20-minute job cap, leaving its
345 assigned tests incomplete. The exact shard then passed locally: 345 passed,
0 failed, 0 skipped, 9m45s test duration and exit 0. No timeout, shard rule,
production behaviour or test assertion was changed to obtain that result.

Commit `8ef4775c` removes the three-copy `Provider API` literal. The appended
`OperatorLabels.ProviderSubmissionApi` nested class owns the source label and
provenance icon, and the three required existing switch arms reference it. The
provider snapshot integration test now pins the enum, persisted-code and
provenance production mappings; no assertion was removed, weakened, skipped or
inverted.

The first two remediation builds failed, both with exit 1, while those new
assertions were corrected. The final
`dotnet build ./Pegasus.slnx --configuration Release` succeeded with 0 warnings
and 0 errors. The focused `ProviderApiSubmissionTests` filter passed 9/9.

Fresh Actions run `33254911537` on exact head `8ef4775c` completed
**success**. Hosted `sql-integration (1)` ran all 345 assigned tests: 345 passed,
0 failed, 0 skipped, 9m26s test duration. Every job in that run succeeded.
This closes the major CI finding without changing a timeout or suppressing a
test.

The shared-switch finding is rejected with its remaining merge surface
accepted: the enum, persisted-code and provenance production callers must each
handle the new source or throw/render unknown, the arms are append-only and
unreordered, and a parallel wrapper would violate the one-owner rail. The
pre-existing three `ActivitySource("Pegasus.Core.Intake")` declarations remain
an out-of-lane informational defect; no new ticket was created.

Status remains **PR-open, review-blocked**. Independent review has not run on
this head. The ticket has not been merged, proved on `main`, deployed or called
by a provider; the feature gate remains off.

## Verification-plan remediation — 2026-09-02

The exact-SHA implementation, canonical suite, migration-grant check, and local
deployment-plan check passed. Verification stopped because the historical
Markdown-placement command used mutable `origin/dev` as its base.

The plan now binds that check to merge commit
`0d985c9e0b3284f211f824d387e2f36460c0c826` and its immutable first parent
`23b0c564c81bf8a0665bc5a65f3f54d88010f835`. No repository file, production
behavior, test, dependency, deployment configuration, or assertion changed.
The fresh remediation worktree is clean at
`cad00be9d42dbeaee9edf34c2d24de222d7ddb9d`; no new commit or PR exists.
Independent review must attest the corrected verification boundary before a
fresh verifier appends to the retained failed proof.
