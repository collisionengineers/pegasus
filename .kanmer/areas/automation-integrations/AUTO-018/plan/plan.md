# Plan — AUTO-018 (2026-09-02, gpt-5.6-terra high)

## Diff estimate

ASSUMED — approximately 14 existing source/test files, one Core completion
contract, one EF persistence implementation, and one serialized migration
(`.cs`, Designer, and model snapshot). No Razor page, CSS, JavaScript, snapshot,
governing-document, package, AutoTrader, or scraper change is in scope.

## Starting state

VERIFIED — the detached checkout is clean at
`897db9530a45063e8f684f2800685afbfdced006`. AUTO-018 is `preparing`; its
research and file map exist, but its resolved gate still requires a plan,
checklist, and resolved/deferred question before it can enter `implementing`.

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

## Assumption pending operator answer

`MarketResearchCompletionTargetState => AiJobState.DraftReady;`

ASSUMED — this one Core-policy line preserves the existing Automation-only
completion and staff-only `Completed` rules. The retained document and
valuation row are a proposal, not accepted Engineer value or settlement input.
The operator question below decides the later staff closure interaction; it
does not block implementing the typed completion contract.

Wrapper note (Claude, 2026-09-02) — VERIFIED — the governing FRD-11 row
written by DELIV-041 (`docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md:270`)
gives MarketResearch the staff confirmation "None on the job — the entry is a
proposal on the Case", while its states section (`:275-285`) still lists only
Query response and Unidentified-queue pass as hand-completed kinds. Under the
`DraftReady` default a MarketResearch job therefore has no closure surface in
AUTO-018's owned paths and would stay non-terminal in the AI Job List until
the operator answers; under the alternative (Automation completes straight to
`Completed` for this kind only) the per-kind rule in
`AiJobPolicy.ValidateTransition` (`AiJobOperations.cs:155-162`) must be
narrowed for `MarketResearch` alone. Both are one Core line; the implementer
must apply the operator's answer before opening the PR.

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

- Add a typed MarketResearch completion command/result that carries the Case
  version, edit-lease token, job version, operation key, file metadata and
  bytes, guide month, recorded date/time, mileage, retail value, and trade
  value. Do not encode these values as JSON in generic result text.

- Define one Core-owned completion use case for this job kind. It validates
  `MarketResearch`, the taken Automation Actor, bounds and required fields, and
  retains the existing rule that normal job completion is `DraftReady`.
  Existing Estimate, Query response, Unidentified resolution, and queue-pass
  completion rules remain unchanged.

- Do not add a valuation note/comparable-adverts field. The retained findings
  document is the evidence artifact; adjustments, rationale, and versioned
  history are TICK-083 scope.

- Acceptance: an unknown kind is rejected; MarketResearch is Case-only; stale
  job version, missing/expired lease, invalid document/figures, or wrong actor
  is refused before any partial outcome; completion remains `Draft ready`.

### Step 2 — Extend the valuation domain without accepting AI output

- File: `src/Pegasus.Core/Assessment/Valuations.cs`.

- VERIFIED — reuse `ValuationDetails`, `ValuationPolicy`,
  `IValuationStore`, and the existing rule that only `EngineersValue` writes
  `assessment.values.engineer`:
  `src/Pegasus.Core/Assessment/Valuations.cs:8-40` and
  `src/Pegasus.Core/Assessment/Valuations.cs:118-147`.

- Add `AiMarketResearch` and a guide-month field to the one valuation details
  record, with validation appropriate to an entered month. Keep date, time,
  mileage, retail, and trade validation in the existing policy.

- Wrapper note (Claude, 2026-09-02) — VERIFIED — FRD-06 names the guide-month
  field "owned by `CASE-029`"
  (`docs/frd/frd-06-vehicle-and-engineering-evidence.md:218-220`), while
  `Valuations.cs` and the migration are AUTO-018's whole files. Split: AUTO-018
  adds the Core field, its persistence column and the policy rule that an
  `AiMarketResearch` row requires it; the column is nullable so existing
  Glass's/Cazana/Engineer's Value rows and the current staff dialog (which
  cannot yet enter it) are unaffected; CASE-029 owns the dialog entry and the
  "required for every entry" rule if the operator wants one. Record this in the
  post-implementation report so CASE-029 picks it up.

- Narrow Automation admission: Automation may persist only an
  `AiMarketResearch` valuation through the MarketResearch completion use case;
  staff retain existing valuation authority, and Automation cannot create or
  edit Glass's, Cazana, or Engineer's Value rows. Engineer's Value remains
  Engineer-confirmed only.

- Acceptance: an AI market row is valid evidence with guide month; it never
  writes, replaces, or clears the Engineer's Value assessment field.

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
  their lengths, money precision, guide-month representation, enum checks, and
  indexes using the existing model conventions:
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

### Step 4 — Expose the typed Automation Actor completion and labels

- Files: `src/Pegasus.Web/Mcp/AiJobMcpTools.cs` and
  `src/Pegasus.Web/Presentation/OperatorLabels.cs`.

- VERIFIED — reuse the existing `automation.jobs` MCP tool registration,
  resolved actor, expected job version, operation key, and MCP error
  translation in `AiJobMcpTools`:
  `src/Pegasus.Web/Mcp/AiJobMcpTools.cs:189-230`.

- VERIFIED — reuse the bounded base64 decoding and
  `DocumentSource.Automation` rules already used by document MCP ingress:
  `src/Pegasus.Web/Mcp/DocumentMcpTools.cs:58-171`.

- Add MarketResearch-specific fields to the existing complete-tool contract and
  route only that kind to the new Core completion port. Preserve the generic
  completion contract for every existing kind.

- Add only two labels in the canonical map: `Market research` for the job kind
  and `AI market research` for the valuation source. Reuse
  `OperatorLabels.AiJobs.Kind` rather than emitting enum text:
  `src/Pegasus.Web/Presentation/OperatorLabels.cs:1001-1035`.

- The Operations page itself is not changed. VERIFIED — it already renders
  kind, instruction, record link, state, and Action Log-backed job data through
  the shared label map: `src/Pegasus.Web/Pages/Operations/Index.cshtml:77-136`.

- Acceptance: a client with `automation.jobs` can list, claim, and complete a
  MarketResearch job; another client, missing scope, invalid payload, or stale
  version is refused; Action History attributes the result to the resolved
  Automation Actor rather than connector-supplied identity.

### Step 5 — Serialize and add the schema migration

- Files:
  `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_MarketResearchAiJob.cs`,
  its Designer, and
  `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`.

- VERIFIED — both current schema constraints are closed and must be altered,
  not merely updated in Core: `20260828084601_AiJobs.cs:48-52` and
  `20260829095336_CaseValuations.cs:36-39`.

- Wait for explicit availability of the capacity-one migration and
  `Presentation/OperatorLabels.cs` locks before editing either path. Refresh
  from `origin/dev` with `git merge --no-edit origin/dev`; do not rebase.

- Generate one migration that adds the persisted MarketResearch completion
  data, guide month, and expanded `AiJobs`/`CaseValuations` constraints. Retain
  existing data and preserve restrictive foreign keys and no-delete policy.

- VERIFIED — existing Web grants cover `AiJobs` and `CaseValuations`;
  `scripts/Invoke-AzureDatabaseBootstrap.ps1:353-404`. Do not change that
  script unless the generated migration itself changes grant SQL. In either
  case, run the exhaustive grant verification.

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
  scope, resolved actor, claim ownership, invalid base64/oversize/missing
  fields, expected-version refusal, kill-switch claim refusal, completion after
  a switch-off, operation-key replay, and Action History.

- Reuse the SQL valuation harness to prove custody/valuation/job atomicity,
  guide-month round-trip, source constraint, no Engineer's Value mutation, and
  production DI resolution. Add a narrow Operations rendering assertion only
  if the existing fake job list must be extended to compile or prove the new
  label; do not alter the page.

- Acceptance: one successful tool completion produces exactly one confirmed
  custody document and one AI-market valuation; duplicate completion produces
  neither duplicate; nothing becomes accepted automatically; Operations
  renders `Market research`; stopped automation rejects new claims.

## Dependencies outside this ticket

- [[CASE-029]] owns the Case Valuation-section button, section partial, page
  model, CSS, JavaScript, and Test UI snapshots. Its caller must create the
  MarketResearch job through `ICreateAiJob`; AUTO-018 does not modify those
  paths.

- [[TICK-083]] owns valuation adjustments, rationale, revaluation-history
  types, persistence, and UI. This ticket adds no equivalent fields.

- The default `DraftReady` decision requires a named staff closure surface if
  the job should later become `Completed`. The current Operations predicate
  exposes manual completion only for Query response and Unidentified-queue pass:
  `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs:442-446`. That
  Operations-page action is outside AUTO-018's owned paths and must be assigned
  to CASE-029 or a dedicated follow-up after the operator answers the question.

- Snapshot generation is not run for AUTO-018 because no routed Razor page is
  changed. If scope is formally amended to change one, CASE-029 must run
  `./scripts/Update-TestUiSnapshots.ps1`,
  `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`, and
  `./scripts/Test-UiCatalogue.ps1`.

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
never reach markup. Exact existing job-state labels remain unchanged. CASE-029
may show a disabled MarketResearch control only when it has the real handler
and a named record-state or permission condition; excluded capabilities,
including in-Pegasus AutoTrader lookup or scraping, are absent.

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
- Two wrapper notes added above (FRD-11 "None on the job"; FRD-06 guide-month
  ownership). No Codex claim was dropped.

## Simplification pass (dated, to be filled by the implementer)

`YYYY-MM-DD — pending implementation`: review the branch diff for reuse,
unnecessary abstractions, duplicated policy, avoidable fields, and scope drift;
record each finding and disposition here before opening the PR.
