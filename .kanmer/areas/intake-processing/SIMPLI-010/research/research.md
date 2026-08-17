# Research — SIMPLI-010: legacy `draft_ready` intake code

## Question

What does the persisted `draft_ready` value mean in the current product, where is it consumed, and what must be true before its compatibility path can be changed or removed?

## Findings

- `draft_ready` is not a current Core decision, operator state, or business state. `IntakeDecision` contains `CaseCreated`, `NeedsSorting`, `BlockedIntake`, `Unsupported`, `OcrRequired`, `TechnicalFailure`, and `ImageIntakeRegistered`; the removed `DraftReady` decision originally named the wait for staff to press “Accept and allocate case reference” (`src/Pegasus.Core/Intake/IntakeContracts.cs:39-65`).
  - The binding FRD says receipt is not case creation and requires all identity-critical gates before allocating a reference (`docs/frd/frd-02-intake-and-source-identity.md:8-20,59-80`).
  - The design explicitly gives `DraftReady` no operator label and says there is no decision meaning “a human has not pressed the button yet” (`docs/design.md:409-447`).

- Current code never writes `draft_ready`. `EfIntakeReceiptStore.ToCode(IntakeDecision.CaseCreated)` writes `case_created`; the only non-test runtime references to the old value are compatibility reads/filters and Operations mapping (`src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs:1173-1200`; repository search: `rg -n "draft_ready" src --glob '!**/*.Designer.cs' --glob '!**/*ModelSnapshot.cs'`).
  - No executable EF migration contains `draft_ready`; there is therefore no schema/data migration that rewrites old rows to `case_created`.
  - Eleven integration-test files still seed `draft_ready` directly, mostly as legacy fixture data for downstream persistence/workflow scenarios. This preserves broad read compatibility but is not a focused proof of an unlinked legacy receipt’s allocation semantics.

- Every persisted `draft_ready` row is parsed as `IntakeDecision.CaseCreated` (`src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs:1185-1199`).
  - Filtering the receipt list by `CaseCreated` deliberately matches both `case_created` and `draft_ready` (`src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs:192-203,273-279`).
  - Consumers no longer expose the legacy spelling: MCP serializes the parsed decision as `case_created` and then reports allocation separately as `ready_for_allocation`, `pending`, `failed_recoverable`, `failed_blocked`, or `case_created` when a Case id exists (`src/Pegasus.Web/Mcp/IntakeMcpTools.cs:188-207`).
  - Operations treats both `case_created` and `draft_ready` as a successfully processed intake item, not as proof of successful allocation (`src/Pegasus.Infrastructure/Persistence/EfOperationsStore.cs:563-568`).

- The processing decision and Case existence are intentionally separate.
  - `CaseCreated` means a definitive instruction is eligible for typed allocation; it can coexist with no Case while allocation is pending, recoverably failed, or blocked. Current architecture states that only the actual Case intake link proves allocation (`docs/current-architecture.md:219-231`; `docs/design.md:411-429`).
  - `IntakeReceipt.CurrentCaseId` is derived from the accepted/manual association projections, not from the decision code (`src/Pegasus.Core/Intake/IntakeContracts.cs:362-381`; `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs:281-313,515-561`).
  - Tests explicitly prove a receipt can expose processing decision `case_created` with no Case id after a recoverable allocation failure, and later expose allocation status `case_created` only after the Case link exists (`tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs:971-1025`).

- Parsing `draft_ready` as `CaseCreated` has executable consequences, not just display consequences.
  - `IntakeDecisionPolicy.CanBecomeCase` admits both `CaseCreated` and `NeedsSorting`; every other decision is refused (`src/Pegasus.Core/Intake/IntakeDecisionPolicy.cs:16-40`; `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs:193-207`).
  - Automatic allocation runs when `CurrentCaseId` is null and the parsed decision is `CaseCreated`, using the persisted case type and suggested Principal (`src/Pegasus.Core/Intake/IntakeAllocation.cs:213-249`).
  - Completed-work replay deliberately re-drives automatic allocation for a completed receipt with no Case. Because `draft_ready` parses as `CaseCreated`, an unlinked legacy row reached through this replay path is eligible for the same idempotent allocation attempt (`src/Pegasus.Core/Intake/DurableIntake.cs:620-665`).
  - Missing case type or Principal does not silently mint a Case: it produces a durable bounded allocation failure; sequence exhaustion is blocked, and concurrency/unexpected failures have their own dispositions (`src/Pegasus.Core/Intake/IntakeAllocation.cs:371-505`).

- History explains why the alias exists and why its meaning changed.
  - Commit `9393c983` removed the manual acceptance gate and removed the `DraftReady` enum. Its commit message says old rows should remain readable rather than require a data migration.
  - That initial change interpreted old `draft_ready` rows as `NeedsSorting`. Commit `379d7ddd` later changed the mapping to `CaseCreated` eligibility when durable allocation recovery was introduced, explicitly making the allocation/link projection the proof of Case existence. This was a behavioral compatibility change, not merely a rename.
  - Current docs and code agree on the post-`379d7ddd` model: `case_created` supersedes `draft_ready`; both represent the same processing eligibility, and neither proves a Case exists (`docs/current-architecture.md:225-231`; `docs/design.md:425-447`).

## Implications

- This ticket must not replace `draft_ready` with a new domain enum or operator label. The live model already removed that state; what remains is a persistence-compatibility alias.
- Removing the parser/filter alias without first normalizing retained data would make old rows fail closed as unknown decision codes and would remove them from `CaseCreated` filters. That would affect receipt detail/list callers, Operations, MCP, acceptance eligibility, and recovery.
- Blindly rewriting every `draft_ready` row to `case_created` would preserve today’s parser result but could erase useful evidence about which rows predate automatic allocation. More importantly, a rewrite alone would not answer whether each old row has a Case link, a durable allocation attempt, sufficient frozen allocation inputs, or a legitimate manual-review requirement.
- The safe normalization unit is therefore not “decision string only.” Production inspection must classify legacy rows by at least: Case intake link/current association, latest allocation attempt and disposition, completed evaluation/work identity, persisted case type, suggested/active Principal, and Audit evidence where applicable.
- A legacy row with an existing Case link is already settled; the link remains authority. An unlinked row with a completed evaluation is potentially allocation-recoverable. An unlinked row missing identity-critical inputs must remain without a Case and surface a bounded failure/manual-review state rather than be bulk-promoted.
- Compatibility removal should come only after data is normalized and a focused test proves the chosen treatment of unlinked legacy rows. Existing fixtures show old rows remain readable, but no focused test was found that seeds an unlinked `draft_ready` receipt and proves its list, allocation, replay, and failure behavior end to end.
- SIMPLI-009 overlaps the same processing/replay boundary. Implementation sequencing must avoid independently changing `ProcessQueuedIntake` or completed-work replay semantics in both branches.

## Open questions

- How many `IntakeReceipts.Decision = 'draft_ready'` rows exist in each deployed database, and how many have a Case intake link?
- Of unlinked rows, which have completed evaluations, allocation attempts, accepted case type, active Principal, and (for Audit) qualifying standalone evidence?
- Is historical `draft_ready` provenance required after normalization, or is immutable event/history evidence sufficient to rewrite the decision code?
- Should normalization be a one-time reviewed data migration, a bounded operational repair with before/after evidence, or continued read compatibility until all retained legacy rows age out?
- Which files SIMPLI-009 will change around `ProcessQueuedIntake`, replay, and allocation projection must be coordinated before SIMPLI-010 implementation begins?

## Clarification recorded after review

- “Parser” in this research means only the persistence-code mapper `EfIntakeReceiptStore.ParseDecision`; it is not the EML/PDF/document extraction parser.
- The intended end state is complete removal of `draft_ready`: no deployed rows, runtime mapping, filter alias, Operations branch, documentation promise, or ordinary current-schema test fixture. The compatibility mapping remains only until deployed rows are classified and normalized safely.
- Repository guidance does not preserve legacy compatibility indefinitely. It prohibits compatibility shims for unreleased behaviour, while the SIMPLI-010 source plan requires production-data inspection, migration/normalization, deployment verification, and only then removal of compatibility reads (`docs/current-architecture.md:602-613`; `docs/temp-plans/simplify/simplify.md:400-410,759-787`).

## Correction — no retained data compatibility requirement

The earlier production-normalization analysis was wrong for this repository and is superseded by this section.

- Pegasus is a clean-room application and starts with fresh application data; predecessor cases and application state are not migrated or preserved (`AGENTS.md`; `docs/current-architecture.md:342-346`).
- There are no Cases or retained `draft_ready` rows that this task must preserve. No production inspection, data classification, repair, normalization migration, deployment sequencing, or live readback is required.
- `draft_ready` is compatibility code for an unreleased/pre-release implementation. Repository guidance rejects compatibility shims for unreleased behaviour (`docs/current-architecture.md:602-613`).
- The correct end state is direct deletion of every `draft_ready` runtime branch, comment, documentation promise, and test-fixture value. Historical behavior remains available in git history; tests of unrelated old migrations do not need to preserve this literal.
- The two migration-test fixtures previously described as immutable historical evidence are not protected. Remove their `draft_ready` values as well; retain only whatever minimal fixture data those unrelated tests require.

## Verified facts — 2026-08-17 read-only production check (claude-code)

Run as `digital@collisionengineers.co.uk` (the server's Entra administrator; no grant or firewall change made) against `pegasus-prod-sql-252ow37gij.database.windows.net` / `pegasus`, `SELECT` only, via `Invoke-Sqlcmd -AccessToken`:

| Metric | Count |
| --- | --- |
| `IntakeReceipts` total | 10 |
| `IntakeReceipts WHERE Decision = 'draft_ready'` | **0** |
| `IntakeWorkItems` total | 10 |
| `IntakeWorkItems WHERE State = 'dispatched' AND LeaseToken IS NULL` | **0** |
| … and `DueAtUtc < now − 1 h` | 0 |
| `IntakeWorkItems` by state | completed 9, failed 1 |

Consequences for this ticket:
- The `draft_ready` alias can be deleted directly (plan step 2); no normalisation, migration, or repair. The ticket's "consolidate only after production-data inspection" line is met by this check — record the table above in proof.
- No stranded unleased `dispatched` rows exist today, so the [[SIMPLI-009]] "repair stranded dispatched work" line has no data to repair. The lost-queue-message gap (a `dispatched` row nothing re-dispatches) is still a real design hole; keep the small `FindNextDispatchCandidateAsync` stale-`dispatched` re-dispatch in scope as resilience, not repair — or, if the plan judges it separate, file it and say so.
