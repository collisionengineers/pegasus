# Plan — AUTO-018 (2026-09-02, gpt-5.6-terra high; corrected 2026-09-03 after cross-model review)

## Diff estimate

ASSUMED — approximately 15 existing source/test files, one Core completion
contract, one EF persistence implementation, and one serialized migration
(`.cs`, Designer, and model snapshot). No Razor page, CSS, JavaScript, snapshot,
governing-document, package, AutoTrader, or scraper change is in scope; the one
Web page-model change is the existing AI Job List completion predicate
(Step 4b).

## Starting state

VERIFIED — the detached checkout is clean at
`897db9530a45063e8f684f2800685afbfdced006`. AUTO-018 is `preparing`; its
research and file map exist, and its open question is resolved, so plan and
checklist complete the resolved gate for `implementing`.

VERIFIED — FRD-11 defines `MarketResearch` as a Case-Valuation action completed
by the external Cowork connector with a retained findings document and an
`AI market research` valuation; it is proposal-only and does not create an
AutoTrader integration: `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md:251-285`.

VERIFIED — existing AI completion moves a taken job to `DraftReady`, while
`Completed` remains staff-only: `src/Pegasus.Core/AiWork/AiJobOperations.cs:63-76`
and `src/Pegasus.Core/AiWork/AiJobOperations.cs:458-471`.

## Governing rules and boundaries

- VERIFIED — reuse the Core-owned, pull-based ledger, Automation Actor,
  expected-version, operation-key, action-history, and kill-switch mechanics;
  do not introduce a queue, timer, or second ledger:
  `docs/adr/0035-ai-job-ledger.md:48-72`.

- VERIFIED — the MCP caller must resolve the Automation Actor, apply the
  `automation.jobs` scope, reject invalid input, and prove an actual caller and
  history rather than merely registering a tool:
  `docs/frd/frd-10-mcp-automation-and-actor-boundary.md:57-96`.

- VERIFIED — the new valuation is source-labelled evidence only; it cannot
  become `assessment.values.engineer`, and valuation adjustments, rationale,
  and revaluation history remain TICK-083:
  `docs/frd/frd-06-vehicle-and-engineering-evidence.md:215-228`.

- VERIFIED — one named Core use case owns each business policy; Web translates
  MCP input and Infrastructure persists it:
  `docs/engineering.md:72-87` and `docs/engineering.md:95-103`.

- VERIFIED — the design permits no explanatory copy, requires only relevant
  populated UI, canonical labels, exact state labels, and an absent rather than
  inert control for excluded capability:
  `docs/design/README.md:654-673` and `docs/design/README.md:705-729`.

## Settled state decision (open question resolved 2026-09-03)

`MarketResearchCompletionTargetState => AiJobState.DraftReady;`

The controller resolved the ticket's one open question from D35 ("proposal
only, never accepted automatically"): Automation completion of a
`MarketResearch` job ends at `DraftReady`, and the job becomes `Completed`
through the **existing** staff confirmation on the AI Job List
(`OperatorLabels.AiJobs.CompleteJob`). There is no alternative
Automation-to-`Completed` path, no new closure surface, and no narrowing of the
staff-only `Completed` rule in `AiJobPolicy.ValidateTransition`
(`AiJobOperations.cs:155-162`). D44 is satisfied: the existing confirmation is
the job ledger's own closure act, not a review flag, checkbox, dialog, or
review-history event, and none is added.

VERIFIED — that existing confirmation is currently offered for two kinds only
(`CanCompleteByHand`, `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs:442-446`),
so the resolution requires the one-token extension in Step 4b; without it a
MarketResearch job would sit in `Draft ready` forever.

## Ordered implementation

### Step 1 — Add the MarketResearch Core contract and policy

- Files: `src/Pegasus.Core/AiWork/AiJobs.cs`,
  `src/Pegasus.Core/AiWork/AiJobOperations.cs`, and a narrowly named new Core
  file under `src/Pegasus.Core/AiWork/` only if needed for the completion port.

- VERIFIED — extend the closed `AiJobKind` catalogue, subject mapping, result
  mapping, creation validation, and Case eligibility using `CreateAiJob`,
  `AiJobPolicy`, `IWorkAiJob`, and the existing command/record patterns:
  `src/Pegasus.Core/AiWork/AiJobs.cs:13-88`,
  `src/Pegasus.Core/AiWork/AiJobs.cs:155-224`, and
  `src/Pegasus.Core/AiWork/AiJobOperations.cs:30-45`.

- Case eligibility: VERIFIED — reuse the existing concrete predicate
  `AiJobPolicy.IsEligibleEstimateCaseState`
  (`src/Pegasus.Core/AiWork/AiJobOperations.cs:218`, `ReportPreparation` or
  `PostReport`) for `MarketResearch`. The Valuation section is Engineer
  workbench work on the same lifecycle boundary as the Estimate job, and D30
  makes Engineer sections read-only once Complete. Do not add a second
  eligibility list. If implementation finds a state where the two must differ,
  stop and raise it as an operator question rather than forking the predicate.

- Add a typed MarketResearch completion command/result that carries the Case
  version, edit-lease token, job version, operation key, file metadata and
  bytes, recorded date/time, mileage, retail value, and trade value. Do not
  encode these values as JSON in generic result text. Guide month is **not** in
  this contract (see Dependencies — D40 gives it to CASE-029).

- Define one Core-owned completion use case for this job kind. It validates
  `MarketResearch`, the taken Automation Actor, bounds and required fields, and
  retains the existing rule that normal job completion is `DraftReady`.
  Existing Estimate, Query response, Unidentified resolution, and queue-pass
  completion rules remain unchanged.

- Do not add a valuation note/comparable-adverts field. The retained findings
  document is the evidence artifact; adjustments, rationale, and versioned
  history are TICK-083 scope.

- Acceptance: an unknown kind is rejected; MarketResearch is Case-only and
  refused outside the eligible states; stale job version, missing/expired
  lease, invalid document/figures, or wrong actor is refused before any partial
  outcome; completion remains `Draft ready`.

### Step 2 — Extend the valuation domain without accepting AI output

- File: `src/Pegasus.Core/Assessment/Valuations.cs`.

- VERIFIED — reuse `ValuationDetails`, `ValuationPolicy`,
  `IValuationStore`, and the existing rule that only `EngineersValue` writes
  `assessment.values.engineer`:
  `src/Pegasus.Core/Assessment/Valuations.cs:8-40` and
  `src/Pegasus.Core/Assessment/Valuations.cs:118-147`.

- Add `AiMarketResearch` to the one `ValuationSource` vocabulary. Date, time,
  mileage, retail and trade validation stay exactly as the existing policy has
  them; no new per-entry field is added by this ticket.

- Narrow Automation admission: Automation may persist only an
  `AiMarketResearch` valuation through the MarketResearch completion use case;
  staff retain existing valuation authority, and Automation cannot create or
  edit Glass's, Cazana, or Engineer's Value rows. Engineer's Value remains
  Engineer-confirmed only.

- Acceptance: an AI market row is valid evidence; it never writes, replaces, or
  clears the Engineer's Value assessment field; `StaffAuthorization.Require`
  still refuses Automation on the staff save/edit paths.

### Step 3 — Persist one replay-safe completion transaction

- Files: `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs`,
  `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfAiJobStore.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs`, and a
  narrowly named new completion store under
  `src/Pegasus.Infrastructure/Persistence/`.

- VERIFIED — reuse the serializable, optimistic AI-job transition and history
  pattern in `EfAiJobStore`: `src/Pegasus.Infrastructure/Persistence/EfAiJobStore.cs:88-225`.

- VERIFIED — reuse the existing valuation mapping and the early return that
  leaves non-Engineer sources out of `assessment.values.engineer`:
  `src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs:23-91` and
  `src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs:200-253`.

- VERIFIED — reuse, rather than copy, the document custody validation,
  content-hash, content-store compensation, source `Automation`, semantic role
  `Other`, operation-key replay, and Case mutation guard:
  `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs:23-131`.

- Implement the one specialised EF persistence operation so a successful
  completion writes exactly one custody occurrence/version, one
  `AiMarketResearch` valuation, the `DraftReady` job result/pointer, and their
  attributable histories in one serializable Case/job transaction. Refactor
  only the necessary internal custody helper so the existing document command
  and this operation share the same hash, write, rollback, and replay logic.

- Persist the typed MarketResearch result fields needed to return an exact
  replayed result and identify the retained document and valuation. Configure
  their lengths, money precision, enum checks, and indexes using the existing
  model conventions:
  `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs:229-271`.

- Add the concrete completion store and Core port registrations in
  `src/Pegasus.Infrastructure/DependencyInjection.cs`; reuse the scoped
  `EfAiJobStore`, `EfValuationStore`, and `EfDocumentCustodyStore` registration
  style at `src/Pegasus.Infrastructure/DependencyInjection.cs:338-359` and
  `src/Pegasus.Infrastructure/DependencyInjection.cs:459-471`.

- Acceptance: replay of the same operation key returns the original document,
  valuation, and job result; a changed replay payload is rejected; a stale Case
  version or lost lease leaves no document, valuation, or transitioned job;
  an internal failure compensates any newly written content artifact.

### Step 4 — Expose the typed Automation Actor completion and the kind label

- Files: `src/Pegasus.Web/Mcp/AiJobMcpTools.cs`,
  `src/Pegasus.Web/Mcp/AutomationMcpErrors.cs` (shared limit only),
  `src/Pegasus.Web/Mcp/DocumentMcpTools.cs` (reads the moved constant), and
  `src/Pegasus.Web/Presentation/OperatorLabels.cs`.

- VERIFIED — reuse the existing `automation.jobs` MCP tool registration,
  resolved actor, expected job version, operation key, and MCP error
  translation in `AiJobMcpTools`:
  `src/Pegasus.Web/Mcp/AiJobMcpTools.cs:189-230`.

- Add one **new named tool** — `pegasus_ai_job_complete_market_research` —
  beside `pegasus_ai_job_complete`, rather than hanging nine
  MarketResearch-only optional parameters on the generic completion contract.
  It resolves `AutomationMcp.JobsScope` and routes only this kind to the Core
  completion port; `pegasus_ai_job_complete` is unchanged and continues to
  refuse a MarketResearch job.

- VERIFIED — the input helpers to reuse are
  `AutomationMcpErrors.DecodeContent`, `RequireFileName` and `RequireMediaType`
  (`src/Pegasus.Web/Mcp/AutomationMcpErrors.cs:98-160`), not `DocumentMcpTools`
  itself. VERIFIED — the 10 MiB document limit is currently the private
  `DocumentMcpTools.MaximumDocumentBytes`
  (`src/Pegasus.Web/Mcp/DocumentMcpTools.cs:75`); move that one constant to a
  shared home both tools read (`AutomationMcpErrors`) so there is exactly one
  document-size rule, and do not copy the number.

- Scope and lease workflow, stated explicitly: VERIFIED —
  `pegasus_case_edit_begin` resolves `AutomationMcp.CasesScope`
  (`src/Pegasus.Web/Mcp/CaseMcpTools.cs:250-268`), so the Cowork connector
  holds `automation.jobs` for the job tools **and** `automation.cases` to claim
  and release the case edit lease it presents on completion. No jobs-only lease
  mechanism is invented and the Core Case-mutation guard is never bypassed.

- Add only one label in the canonical map: `Market research` for the job kind,
  reusing `OperatorLabels.AiJobs.Kind`
  (`src/Pegasus.Web/Presentation/OperatorLabels.cs:1001-1035`). VERIFIED — no
  `ValuationSource` label map exists in the Web layer and AUTO-018 renders no
  valuation, so the `AI market research` **source** label is not added here:
  a single source label with no caller would be an unwired orphan and half a
  vocabulary. CASE-029 adds the whole source label map with the Valuation UI
  that consumes it.

- Acceptance: a client with `automation.jobs` (and a lease claimed under
  `automation.cases`) can list, claim, and complete a MarketResearch job;
  a client missing `automation.jobs`, a client with no/expired lease, an
  invalid payload, or a stale version is refused — each proved separately;
  Action History attributes the result to the resolved Automation Actor rather
  than connector-supplied identity.

### Step 4b — Offer the existing staff confirmation for the new kind

- File: `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs`.

- VERIFIED — `CanCompleteByHand` (`:442-446`) currently returns true for
  `QueryResponse` and `UnidentifiedQueuePass` only, and `ReviewAction`
  (`:416-434`) returns null for any other kind. Add `MarketResearch` to
  `CanCompleteByHand` so the settled resolution's "existing staff confirmation"
  actually reaches the operator. No new control, label, dialog, flag, or
  history event is added, `ReviewAction` is untouched (the proposal is read on
  the Case, which CASE-029 renders), and the markup is unchanged.

- Ownership note: `Pages/Operations/Index.cshtml.cs` is not a shared-lock path
  and is not claimed by ENG-035, DOCS-017 or PLAT-068. This one-token change is
  the wiring the resolved open question requires and is recorded here as an
  explicit, minimal scope extension beyond the ticket's approximate owned
  paths.

- Acceptance: a `Draft ready` MarketResearch job offers `Complete job` to
  staff; pressing it moves the job to `Completed` and changes no valuation,
  document, or assessment field.

### Step 5 — Serialize and add the schema migration

- Files:
  `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_MarketResearchAiJob.cs`,
  its Designer, and
  `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`.

- VERIFIED — both current schema constraints are closed and must be altered,
  not merely updated in Core: `20260828084601_AiJobs.cs:48-52` and
  `20260829095336_CaseValuations.cs:36-39`.

- Shared-lock protocol (not an ownership transfer): `Persistence/Migrations/**`
  has capacity one for the whole wave and PLAT-068 also needs it. Do not open
  the file until the lock is explicitly free; then refresh the lane with
  `git merge --no-edit origin/dev` (never a rebase) and generate this migration
  **on top of** whatever snapshot is then on `origin/dev`, so the timestamps
  stay ordered and the snapshot has one author at a time. If PLAT-068's
  migration lands after this one is generated, regenerate rather than
  hand-merge the snapshot.

- Generate one migration that adds the persisted MarketResearch completion data
  and the expanded `AiJobs`/`CaseValuations` check constraints. Retain existing
  data and preserve restrictive foreign keys and no-delete policy. No guide
  month column is added by this ticket.

- VERIFIED — existing Web grants cover `AiJobs` and `CaseValuations` and no new
  table is introduced, so the approved permission matrix is unchanged:
  `scripts/Invoke-AzureDatabaseBootstrap.ps1:353-404`. Do not change that
  script unless the generated migration itself changes grant SQL. In either
  case, run the exhaustive grant verification in the same diff as the
  migration.

- Update the applied-migration census in
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`; it
  currently pins both relevant migrations:
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:107-120`.

- Acceptance: a fresh LocalDB migration creates the new columns and accepts
  only valid enum values; existing rows survive; runtime grants remain exactly
  the approved matrix.

### Step 6 — Prove Core, persistence, MCP, and Operations rendering

- Files: `tests/Pegasus.Core.Tests/AiWork/AiJobTests.cs`,
  `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs`,
  `tests/Pegasus.IntegrationTests/AutomationAiJobIngressTests.cs`,
  `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`,
  `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`, and
  `tests/Pegasus.IntegrationTests/OperationsWebTests.cs`.

- VERIFIED — extend the existing AI harness rather than introducing another
  fake: `tests/Pegasus.Core.Tests/AiWork/AiJobTests.cs:232-330`.

- VERIFIED — extend the valuation tests and recording store to cover the closed
  source vocabulary and Engineer's Value isolation:
  `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs:19-79` and
  `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs:115-178`.

- Reuse `AutomationMcpTestSupport` and the existing ingress tests to prove
  scope (missing `automation.jobs` refused, and a completion presenting no or
  an expired case edit lease refused — the two failures proved separately),
  resolved actor, claim ownership, invalid base64/oversize/missing fields,
  expected-version refusal, kill-switch claim refusal, completion after a
  switch-off, operation-key replay, and Action History.

- Reuse the SQL valuation harness to prove custody/valuation/job atomicity,
  source constraint, no Engineer's Value mutation, and production DI
  resolution. In `OperationsWebTests.cs`, prove the list renders
  `Market research` and that a `Draft ready` MarketResearch job offers the
  existing `Complete job` action and completes to `Completed` (Step 4b); the
  page markup itself is unchanged.

- Acceptance: one successful tool completion produces exactly one confirmed
  custody document and one AI-market valuation; duplicate completion produces
  neither duplicate; nothing becomes accepted automatically; Operations
  renders `Market research` and closes the job only by the staff act; stopped
  automation rejects new claims.

## Evidence tier and activation (read before writing proof)

VERIFIED — `pegasus_ai_job_create` accepts `UnidentifiedQueuePass` only
(`src/Pegasus.Web/Mcp/AiJobMcpTools.cs:85-110`) and `AiJobPolicy` refuses
Automation creation of any other kind (`AiJobOperations.cs:225-232`), so a
MarketResearch job is created by staff through `ICreateAiJob` alone — and that
production caller is the Valuation-section button, which the ticket body itself
assigns to CASE-029. AUTO-018 therefore ships:

- proven at the highest tier available to it: the Core contract and policy, the
  replay-safe persistence transaction, the MCP claim/complete path exercised
  through the real Automation ingress and production DI, the migration, and the
  staff closure action;
- **not** activated end-to-end until CASE-029 merges its creation caller. The
  post-implementation report and `proof.md` must say exactly that, name
  CASE-029 as the activating ticket, and must not claim the operator-facing
  capability is delivered.

## Dependencies outside this ticket

- [[CASE-029]] owns the Case Valuation-section button, section partial, page
  model, CSS, JavaScript, Test UI snapshots, the `ValuationSource` operator
  label map, and — per D40 and
  `docs/frd/frd-06-vehicle-and-engineering-evidence.md:218-220` — the
  **guide month** field, its column, its dialog entry and any "required for
  every entry" rule. AUTO-018 adds none of them. When CASE-029 adds guide
  month it also extends the MarketResearch completion contract if the connector
  is to supply one; until then a MarketResearch valuation row carries no guide
  month. Record this hand-off in the post-implementation report.

- [[TICK-083]] owns valuation adjustments, rationale, revaluation-history
  types, persistence, and UI. This ticket adds no equivalent fields.

- [[PLAT-068]] also needs `Persistence/Migrations/**`; Step 5 states the
  serialization protocol.

- Snapshot generation is not run for AUTO-018 because no routed Razor page's
  markup is changed (Step 4b changes a page-model predicate only). If a
  snapshot diff appears, stop: it means the scope moved into CASE-029's UI.

## Commands and verification order

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Test-MigrationGrants.ps1
```

## Design rules

No explanatory copy is added. All operator text uses
`Presentation/OperatorLabels.cs`; raw enum names and internal result fields
never reach markup. Exact existing job-state labels remain unchanged, and the
existing `Complete job` label is reused rather than re-worded. CASE-029 may
show a disabled MarketResearch control only when it has the real handler and a
named record-state or permission condition; excluded capabilities, including
in-Pegasus AutoTrader lookup or scraping, are absent.

## Stop condition

The implementation branch has a PR open to `dev`, the post-implementation
report records command exit codes and evidence-tier results, and AUTO-018 is in
Review. Do not merge, release, or begin a neighbouring ticket.

## Wrapper check (Claude, 2026-09-02)

Produced by gpt-5.6-terra (effort high) reading the detached `origin/dev`
checkout `.worktrees/research` at `897db953` (clean before and after the run);
the Claude wrapper owns the board writes and re-read every cited range in the
same checkout:

- FRD-11 `:251-285` (MarketResearch row, states), ADR-0035 `:48-72`, FRD-06
  `:215-228`, `docs/engineering.md:72-80`, design README `:654-673`/`:705-729`
  — all say what the plan cites.
- `AiJobOperations.cs:63-76`, `:155-162`, `:458-471`; `AiJobs.cs:155`
  (`CompleteAiJobCommand`); `Valuations.cs:8-40`, `:118-147`;
  `EfAiJobStore.cs:88`; `EfDocumentCustodyStore.cs:23-40`;
  `DependencyInjection.cs:338-359`, `:459-471`;
  `Operations/Index.cshtml.cs:442-446` (`CanCompleteByHand`);
  `AiJobMcpTools.cs:189-230`; `DocumentMcpTools.cs:75-81`;
  `AiJobTests.cs:232` (`Harness`); `ValuationTests.cs:19-30`;
  `AutomationMcpTestSupport` used by `AutomationAiJobIngressTests.cs:7` —
  confirmed.
- Migration census `IntakePersistenceIntegrationTests.cs:107-117` ends at
  `20260829212237_GrantProviderSubmissionAcceptRecovery`; grants script
  `Invoke-AzureDatabaseBootstrap.ps1:353-404` covers `AiJobs` and
  `CaseValuations` — confirmed.
- No Codex claim was dropped.

## Simplification pass (dated, to be filled by the implementer)

`YYYY-MM-DD — pending implementation`: review the branch diff for reuse,
unnecessary abstractions, duplicated policy, avoidable fields, and scope drift;
record each finding and disposition here before opening the PR.

## Resolutions (2026-09-03)

- Controller (D35 "proposal only"): the MarketResearch job lands in the existing review state (`DraftReady`) and is completed by the existing staff confirmation on the AI Job List; no new closure surface and no change to the Completed-is-staff rule. Applied throughout this plan; Step 4b is the wiring it requires.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

gpt-5.6-sol read the plan, checklist, ticket, decisions D29–D46 and the
repository independently at `origin/dev` `897db953` (verdict: REQUEST CHANGES —
five blockers, two should-fixes). Every finding was re-verified against the
same checkout by the wrapper before disposition; the checkout was clean after
the run. Claude added two findings of its own (C1, C2).

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | Resolution, 1/4/6, checklist | Plan still carried the unresolved fork ("pending operator answer", the Automation-to-`Completed` alternative) and said Operations is unchanged, but `CanCompleteByHand` (`Operations/Index.cshtml.cs:442-446`) offers `Complete job` to `QueryResponse`/`UnidentifiedQueuePass` only, so the settled "existing staff confirmation" would never appear. | **Fixed.** Fork deleted and replaced by "Settled state decision"; new Step 4b adds `MarketResearch` to `CanCompleteByHand` with an ownership note and an `OperationsWebTests` assertion; checklist item 1 rewritten. No review flag, checkbox or dialog is introduced (D44). |
| 2 | blocker | 1, 2, 3, 5, 6 | AUTO-018 absorbed guide month although D40 and `frd-06:218-220` name CASE-029 its owner. | **Fixed.** Guide month removed from the completion contract, `Valuations.cs`, persistence, migration and tests; the hand-off (including extending the completion contract if the connector should supply one) is recorded under Dependencies. Conduct rule 2 — never absorb another ticket's scope — outranks the earlier convenience argument that AUTO-018 owns the file and the migration; CASE-029 is a later wave and edits both after this merges. |
| 3 | blocker | 4, 5 | File allocation not disjoint: `Persistence/Migrations/**` (with the shared snapshot) is also PLAT-068's, and `Presentation/OperatorLabels.cs` sits outside AUTO-018's listed paths. | **Partly fixed, partly rejected.** Rejected the ownership framing: EPIC-012 declares both paths capacity-one **shared locks**, not single-lane property, and a schema change may not ship without its migration (conduct rule 16). Fixed the mechanics: Step 5 now states the lock protocol (wait for the lock, merge `origin/dev`, generate on the merged snapshot, regenerate rather than hand-merge if PLAT-068 lands after) and keeps migration plus grant verification in one diff. |
| 4 | blocker | Dependencies, verification | No production creation caller: `pegasus_ai_job_create` accepts `UnidentifiedQueuePass` only and the Valuation button is CASE-029's, so "job appears in Operations" cannot be proven end-to-end. | **Fixed as an explicit, documented deferral.** New "Evidence tier and activation" section states what AUTO-018 proves, that the capability is not activated until CASE-029 merges its caller, and requires the report and `proof.md` to say so and make no delivery claim. Rejected moving the caller into AUTO-018: the ticket body assigns it to CASE-029, which owns those files (conduct rules 1 and 2). |
| 5 | blocker | 1, 4, 6 | Authorization contract inconsistent: completion needs a case edit lease, but `pegasus_case_edit_begin` requires `automation.cases` (`CaseMcpTools.cs:250-268`) while the plan claimed `automation.jobs` alone suffices. | **Fixed.** Step 4 states the two-scope workflow explicitly, invents no jobs-only lease, and never bypasses the Core Case-mutation guard; Step 6 proves a missing `automation.jobs` scope and a missing/expired lease as separate refusals. |
| 6 | should-fix | 1, 6 | "Case eligibility" undefined and untested; the existing concrete helper is `AiJobPolicy.IsEligibleEstimateCaseState` (`AiJobOperations.cs:218`). | **Fixed.** Step 1 reuses that predicate by name for `MarketResearch`, forbids a second eligibility list, requires every state tested, and makes a genuine divergence an operator question rather than a silent fork. |
| 7 | should-fix | 4 | `DocumentMcpTools` cited as the reusable helper; the real helpers are `AutomationMcpErrors.DecodeContent`/`RequireFileName`/`RequireMediaType`, and the 10 MiB limit is private to `DocumentMcpTools`. | **Fixed.** Step 4 names the three helpers and moves the single `MaximumDocumentBytes` constant to `AutomationMcpErrors` so both tools read one rule (one list per concept); the number is not copied. |
| C1 | should-fix | 4 | Claude: the plan added an `AI market research` **valuation-source** label, but no `ValuationSource` label map exists in the Web layer and AUTO-018 renders no valuation — a lone source label would be an unwired orphan and half a vocabulary (conduct rule 14; one list per concept). | **Fixed.** Only the job-kind label `Market research` is added; the source label map moves to CASE-029 with the UI that consumes it. |
| C2 | should-fix | 4 | Claude: hanging ~nine MarketResearch-only optional parameters on the shared `pegasus_ai_job_complete` contract overloads a tool every other kind uses. | **Fixed.** Step 4 adds a separate named tool `pegasus_ai_job_complete_market_research`, matching the repository's one-tool-per-operation MCP convention; the generic tool is unchanged and still refuses the new kind. |

No finding required a new operator question: each was answerable from the
ticket body, D35/D40/D44 or the repository. D45 (no damage type) and D46 (crop)
touch nothing in this plan, and no new package or explanatory copy is proposed.
