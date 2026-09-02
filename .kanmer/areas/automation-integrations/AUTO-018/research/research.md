# Research — AUTO-018 (2026-09-02, gpt-5.6-terra high, wrapper-checked)

Produced by gpt-5.6-terra (effort high) reading the detached `origin/dev`
checkout `.worktrees/research` at `cad00be9`; the Claude wrapper owns the
board writes and re-checked a sample of the VERIFIED claims against the main
checkout (see "Wrapper check" at the end).

## Basis and verification

- **VERIFIED** — this is a clean, detached `origin/dev` checkout at
  `cad00be9`; `git status --short`, `git rev-parse --verify HEAD`, `git log -1
  --oneline`, and `dotnet --list-sdks` found no local changes and SDKs
  `10.0.204` and `10.0.303`.
- **VERIFIED** — `Test-Path` confirmed `AGENTS.md`, `CLAUDE.md`, `docs/index.md`,
  FRD-06, FRD-10, FRD-11, ADR-0035, the design README, capabilities, and all
  four supplied mockup paths exist.
- **VERIFIED** — `git log --all` confirms AUTO-011 merged as `658a7984` and
  ENG-027 as PR #621 / `450b9234`.
- **VERIFIED** — `rg -n -i 'MarketResearch|market-research' src tests docs`
  returned no current product implementation. The only AutoTrader result is an
  open-decision reference; there is no scraper or adapter.

## Current behaviour

### AI-job Core and persistence

- **VERIFIED** — `src/Pegasus.Core/AiWork/AiJobs.cs:13-88` defines the closed
  `AiJobKind` catalogue: `Estimate`, `UnidentifiedResolution`, `QueryResponse`,
  and `UnidentifiedQueuePass`; states are `Queued`, `Taken`, `DraftReady`,
  `Completed`, `Failed`, `Cancelled`, and `Expired`.
- **VERIFIED** — the existing result shape is exactly
  `AiJobResult(Kind, Reference, Text)` (`AiJobs.cs:55-63`). The persisted
  `AiJobRecord` has only `ResultKind`, `ResultReference`, and `ResultText`
  (`AiJobs.cs:65-88`); there is no document pointer, retail value, trade value,
  guide month, mileage, note, or separate display detail.
- **VERIFIED** — current kind-to-result mapping is Estimate → `Estimate`;
  Unidentified resolution and queue pass → `ProposedResolution`; Query response
  → `DraftReply` (`AiJobOperations.cs:38-45`). Result reference is capped at
  200 characters and result text at 4,000 (`AiJobOperations.cs:20-24`,
  `191-216`).
- **VERIFIED** — the current MCP complete payload is `jobId`,
  `expectedVersion`, `resultKind`, optional `resultReference`, optional
  `resultText`, and `operationKey` (`AiJobMcpTools.cs:189-230`). It calls
  `IWorkAiJob.CompleteAsync`, which only transitions the ledger to
  `DraftReady` (`AiJobOperations.cs:458-471`).
- **VERIFIED** — Automation may take, progress, complete, fail, and release;
  staff alone may cancel or confirm (`AiJobOperations.cs:151-172`). A staff
  confirmation is currently the only `DraftReady` → `Completed` path.
- **VERIFIED** — claims and progress are stopped by `ISendToAiControl`, while
  release, fail, and complete remain available to avoid stranding held work
  (`AiJobOperations.cs:396-405`, `407-421`, `440-494`). The lease is 30
  minutes (`AiJobOperations.cs:20-28`).
- **VERIFIED** — `EfAiJobStore` persists the ledger, uses serializable
  transactions and optimistic versioning, and writes `ai_job_created`,
  `ai_job_taken`, `ai_job_progress`, `ai_job_released`, `ai_job_draft_ready`,
  `ai_job_failed`, `ai_job_cancelled`, `ai_job_completed`, and lease-expiry
  history (`EfAiJobStore.cs:14-18`, `69`, `149`, `172-202`, `310-339`).
- **VERIFIED** — `AiJobs` has database check constraints generated from the
  Core enums (`AssessmentModelConfiguration.cs:229-266`), while the existing
  migration pins the four old kinds and three old result kinds
  (`20260828084601_AiJobs.cs:48-52`). Adding a kind or result kind therefore
  requires a migration, not only an enum change.

### Automation Actor and MCP ownership

- **VERIFIED** — the real ownership is:
  `AiJobMcpTools.cs` is the MCP adapter; `AutomationMcp.cs` owns the
  `automation.jobs` scope; `AutomationMcpExtensions.cs` registers the tools;
  `AutomationActorResolver.cs` resolves `ActionActor.Automation(clientId)`;
  `EfAiJobStore.cs` owns ledger persistence; and
  `DependencyInjection.cs:352-359` composes them. The ticket body's
  "`src/Pegasus.Infrastructure/**` Automation Actor tools" therefore resolves
  to `src/Pegasus.Web/Mcp/**` for the tools and
  `src/Pegasus.Infrastructure/Persistence/**` for the stores.
- **VERIFIED** — MCP registration is gated: `Program.cs:285`, `734`, and
  `1101` only compose/map the ingress when valid Automation MCP configuration
  is present. It is not evidence of an active Cowork caller.
- **VERIFIED** — the resolved actor is the Automation Actor, not connector
  supplied identity (`AutomationActorResolver.cs:26-72`), and MCP calls record
  attributed action history (`AutomationActorResolver.cs:101-236`).
- **VERIFIED** — the Operations list currently renders Job as kind plus
  `job.Instruction`; it has no separate detail field. Its columns are Job,
  Record, Started by, Created, State, and Action
  (`Pages/Operations/Index.cshtml:77-140`). Existing draft-ready actions cover
  Estimate, Query response, and Unidentified resolution only
  (`Index.cshtml.cs:416-446`).

### Valuation record

- **VERIFIED** — `ValuationSource` currently contains only `Glasses`, `Cazana`,
  and `EngineersValue` (`Core/Assessment/Valuations.cs:8-25`). A new
  `AiMarketResearch` source is required.
- **VERIFIED** — `ValuationDetails` currently has source, local date, local
  time, mileage, retail, and trade (`Valuations.cs:27-40`); it has no guide
  month or research note. The ticket must extend the record for the D35/D40
  guide-month requirement.
- **VERIFIED** — both save and edit require case expected version, edit-lease
  token, operation key, reason, and an `ActionActor`
  (`Valuations.cs:51-70`). `EfValuationStore` applies the case mutation guard,
  serializable transaction, replay protection, and `valuation_created` /
  `valuation_updated` ActionHistory events (`EfValuationStore.cs:23-91`,
  `94-160`).
- **VERIFIED** — current valuation policy calls
  `StaffAuthorization.Require`; it does not accept the Automation Actor
  (`Valuations.cs:118-132`). AUTO-018 must permit Automation only for the new
  AI source, not generally.
- **VERIFIED** — only `EngineersValue` writes
  `assessment.values.engineer`; every other source writes no assessment field
  (`Valuations.cs:134-147`; `EfValuationStore.cs:189-223`). Therefore
  "proposal only" can mean an `AiMarketResearch` row is retained evidence but
  can never set or replace Engineer's Value, accepted value, or settlement
  input.
- **VERIFIED** — the current SQL source check is limited to the three existing
  values (`20260829095336_CaseValuations.cs:36-39`). The new source and guide
  month require a serialized migration, model snapshot, and migration-list
  test update.

### Document custody

- **VERIFIED** — canonical case-document retention is
  `IAddCaseDocument` / `AddCaseDocumentCommand`
  (`Core/Documents/DocumentContracts.cs:113-129`) implemented by
  `EfDocumentCustodyStore` (`EfDocumentCustodyStore.cs:12-177`).
- **VERIFIED** — it records immutable content hash, version, occurrence,
  semantic role, source, actor, operation key, custody state, case version,
  and edit lease. The external content write is compensated if the database
  transaction fails (`EfDocumentCustodyStore.cs:23-176`).
- **VERIFIED** — `pegasus_document_add` already accepts base64 bytes, enforces
  a 10 MiB limit, requires the case version and edit lease, resolves the
  Automation Actor, and supplies `DocumentSource.Automation`
  (`DocumentMcpTools.cs:58-171`).
- **VERIFIED** — available semantic roles are OriginalSource, Instruction,
  Image, Correspondence, EngineerReport, AuditReport, and Other
  (`DocumentContracts.cs:5-24`). There is no MarketResearch document role.
  The smallest current-fit category is `Other`, with
  `DocumentSource.Automation`.

## Mockup

- **VERIFIED** — `22-case-engineer.js:21-43` shows the Valuation-section
  primary "AI market research" action, sparkles icon, disabled state while the
  case has an open research job, and "Researching the market" spinner.
- **VERIFIED** — it creates a `market` job with a Case reference and detail
  `{registration} · {make} {model} · {mileage|mileage unknown}`; completion
  adds source `ai`, current guide month, mileage, retail, trade, date, and a
  comparable-adverts note (`22-case-engineer.js:30-40`).
- **VERIFIED** — the mockup uses a dashed `valuation-card--ai`, displays retail,
  trade, guide month, mileage, date, and note, and adds an AI-attributed history
  line plus toast (`22-case-engineer.js:22-40`).
- **VERIFIED** — mockup labels name `market` "Market research" and `ai`
  "AI market research" (`03-labels.js:61`, `100`). The notes repeat that it
  returns a dashed source card with retail, trade, and adverts
  (`Pegasus_UI_v2_notes.md:54-56`).

## Gaps and reuse

- **VERIFIED** — reuse `CreateAiJob`, `WorkAiJob`, `AiJobPolicy`,
  `AiJobMcpTools`, `AutomationActorResolver`, and `EfAiJobStore`; do not build
  a queue, timer, AutoTrader adapter, or scraper.
- **VERIFIED** — reuse `IAddCaseDocument`, `DocumentSource.Automation`,
  `AutomationMcpErrors.DecodeContent`, case edit leases, and document custody
  replay semantics for the findings bytes.
- **VERIFIED** — reuse `IValuationStore` / `EfValuationStore` validation,
  replay, ActionHistory, and the rule that only `EngineersValue` changes the
  accepted Engineer field.
- **VERIFIED** — reuse `OperatorLabels.AiJobs.Kind` and add the two new labels
  only in `Presentation/OperatorLabels.cs`; that shared-lock file is required.
- **ASSUMED** — the completion contract should be a typed
  MarketResearch-specific payload, not encoded JSON in the current
  reference/text fields. Current constraints and the required document plus
  figures make the existing generic pointer inadequate.
- **ASSUMED** — the findings document should use existing semantic role `Other`
  and source `Automation`; adding a new document category has no current
  requirement.
- **VERIFIED** — this ticket cannot land its Valuation button, section partial,
  CSS, or snapshot. CASE-029 owns that caller and UI surface.

## Risks

- **VERIFIED** — D35 requires an automated document and valuation write, while
  the current complete operation only makes a job `DraftReady`. The design must
  add one Core-owned, replay-safe completion operation; separate independent
  document, valuation, and ledger calls can leave partial completion after a
  failure.
- **VERIFIED** — the current ledger permits Automation only to `DraftReady`;
  changing completion to a terminal state must remain per-kind and must not
  weaken Estimate, Query response, or Unidentified human-review rules.
- **VERIFIED** — document and valuation writes both require the same case
  version and edit lease. A case changed after the job is taken must fail
  closed and leave a recoverable, observable job state.
- **VERIFIED** — both `Presentation/OperatorLabels.cs` and
  `Persistence/Migrations/**` are capacity-one shared locks. A migration is
  necessary because the source checks are fixed in deployed schema.
- **VERIFIED** — the mockup's CAP HPI, AutoTrader, and Vehicle data sources are
  not scope. AUTO-018 adds only AI market research and performs no external
  valuation lookup inside Pegasus.

## Open questions for the operator

- [ ] Does a successful MarketResearch completion become `Completed`
  immediately after retaining its evidence row, or remain `DraftReady` until a
  named staff review action? D35 says "proposal only," but current AI-job
  semantics reserve `Completed` for staff consumption.

## Wrapper check (Claude, 2026-09-02)

Spot-checked against the main checkout `C:/Users/PC/Documents/GitHub/pegasus`
(same `origin/dev` head):

- `AiJobOperations.cs:151-172` — confirmed: non-Automation actors are refused
  for Taken/Queued/DraftReady/Failed transitions; Cancelled/Completed are
  staff-only; `CompleteAsync` (`458-471`) transitions to `DraftReady`.
- `DocumentMcpTools.cs` — confirmed: `MaximumDocumentBytes = 10 * 1024 * 1024`
  (line 75), tool `pegasus_document_add` (line 81), `DocumentSource.Automation`
  (line 153).
- `Valuations.cs:118-132` — confirmed `StaffAuthorization.Require(actor,
  StaffAccessRight.PerformCasework)`; `20260829095336_CaseValuations.cs:38`
  pins `CK_CaseValuations_Source` to `Glasses`, `Cazana`, `EngineersValue`;
  `20260828084601_AiJobs.cs:48-49` pins the four kinds and three result kinds.
- Dropped one Codex claim: "`CLAUDE.md` is empty" — it is 34,500 bytes in the
  research checkout (`wc -c`); AGENTS.md carries the same content.
- The research checkout was clean after the run (`git status --porcelain`
  empty).
