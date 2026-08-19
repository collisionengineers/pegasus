# Plan — INTK-007

## Approach

First reconcile protected operator truth and every governing behavior that currently uses Needs sorting. Then add one Core-owned Unidentified aggregate with a dedicated atomic `U<n>` allocator, canonical reason taxonomy, immutable origin/group link, open/resolved history, queries, and authorized resolution. Migrate every legacy producer by meaning, backfill existing data deterministically, and replace all operator/MCP/search/count surfaces.

## Governing docs

Modifies all current authoritative references listed in files.md. Use kanmer-docs before implementation.

No ADR is required if this remains inside existing Core/EF/Web boundaries. Do not edit historical ADR-0006 solely for terminology. Create a new ADR only if implementation genuinely requires a new architectural boundary.

## Implementation steps

1. **Run the documentation prerequisite.**
   - Invoke kanmer-docs for INTK-007.
   - Update protected `operator-notes.md` using the explicit operator instruction recorded in this ticket.
   - Update PRD and FRDs so “Unidentified” has one definition and each old Needs sorting use is classified as Unidentified, Triage, Blocked intake, incomplete Audit, Image Intake, or INTK-006 Image-Only.
   - Define exact U-reference, group-origin, reason taxonomy, state, resolution destinations, authorization, replay, and history.
   - Update FRD-12/design for queue/detail/search/count behavior.
   - Update capabilities/index ownership and link every governing doc to the ticket; set `docs_todo: false`.
   - Run the stale-term search across current governing docs. If any normative occurrence is unclassified, stop before code.

2. **Create the Core Unidentified model.**
   - Add one reason enum with exactly the six codes in research.md.
   - Add `UnidentifiedState` with Open and Resolved only unless the updated FRD explicitly adds another state.
   - Add origin discriminant for single receipt versus submission group and validate exactly one non-empty origin id.
   - Add records for summary, detail, history entry, register request/result, resolve request/result, and exact query filters.
   - Add `UnidentifiedReferenceFormat.Create(long)` and `TryParse(string)`: positive sequence only, uppercase U, invariant digits, no padding, checked overflow, exact canonical round-trip.
   - Add store/query ports and register/resolve use cases under Core. Require staff authorization, expected version, bounded reason/detail, operation key, actor, and time provider.
   - Keep the taxonomy and validation in one Core location; persistence/UI/MCP must consume it.

3. **Implement reference allocation and persistence.**
   - Add `UnidentifiedItems`, `UnidentifiedSequences`, and `UnidentifiedHistory` entities/tables using existing naming/concurrency conventions.
   - Item columns: id, sequence, reference, origin kind, receipt id nullable, group id nullable, reason code, safe detail, state, created/resolved times, resolution target kind/id/reference nullable, version.
   - Constraints: sequence > 0; exact one origin; unique sequence; unique reference; unique single receipt origin; unique group origin; resolved fields consistent with state; bounded strings.
   - Sequence row starts at zero and allocates under the same serializable transaction that inserts the item. Never use `MAX + 1`.
   - Register is idempotent by origin and operation. Same replay returns existing item; conflicting request fails closed.
   - Resolve appends history and changes state/target in one transaction using expected version and operation replay.
   - Add ordered list/detail/exact-reference/origin queries. Default queue is Open ordered oldest-first then sequence.
   - Add only required Web/Worker SQL grants following adjacent migration conventions.

4. **Create the migration/backfill deliberately.**
   - Snapshot legacy NeedsSorting rows before transformation using only existing persisted keys/reasons.
   - Backfill grouped rows first: one item per distinct INTK-005 group, using the earliest received time and group id.
   - Backfill remaining ungrouped receipts one item per receipt.
   - Allocate sequences deterministically by `ReceivedAtUtc`, then stable receipt/group GUID tie-breaker so repeatable test fixtures get the same order.
   - Map legacy reasons to the six canonical codes with an explicit CASE/mapping table in the migration. Unknown non-empty reasons map to `NoUsableIdentification` with original safe detail preserved; known terminal technical failures map to `TechnicalProcessingFailure`.
   - Insert initial history entry for each migrated item.
   - Update/project legacy decision values according to the governing-doc choice; retain a compatibility parser only when required to read old rows during rolling deployment.
   - Prove rerun/idempotency where migration tooling permits and provide a Down path that removes new objects without pretending allocated references can be safely reused in production.

5. **Route every producer through one registration use case.**
   - Update `ProcessIntake` mappings for unreadable/corrupt, unsupported, no identification, conflicting identification, ambiguous destination, and exhausted technical failure.
   - Do not register while a work item is pending/retryable.
   - Update mail route/classification policies to return evidence; invoke Unidentified registration only at the durable destination boundary.
   - Update Triage paths exactly per FRD-03; do not duplicate a U item when material becomes valid Triage.
   - Update Image Intake automation: successful INTK-006 completed groups never enter Unidentified; only the documented terminal failure path does, once per group.
   - Make all producer replays resolve the existing U item by origin.

6. **Replace read models/counts/search.**
   - Change intake receipt/list/detail projections to include U-reference, reason label, state, and link.
   - Change retained-mail projection to the same item, not a separately synthesized status.
   - Replace dashboard/operations NeedsSorting counts with count of Open Unidentified items.
   - Add exact U-reference search using the Core parser and store index. Keep Case/PO, Audit, and Image Intake parsers separate.
   - Ensure resolved items remain searchable/detail-visible but leave the default Open queue.
   - Update any automation query/filter contracts to use canonical state/reason/reference.

7. **Build the operator queue/detail/resolution surface.**
   - Add `/Unidentified` list with exact U-reference, original filename/source/group member count, received timestamp, canonical reason, state, and one full-row detail link.
   - Add detail with all member receipts/files, source/custody links, safe detail, processing evidence, and immutable chronological history.
   - Add resolution form using antiforgery, authorized actor, expected version, operation key, required reason, and one destination choice allowed by the updated FRD.
   - On success show the permanent U-reference and linked destination. On replay show the existing result. On stale version show a non-destructive conflict and reload action.
   - Add navigation, dashboard, Operations, Upload status, Intake detail, Mail detail, and shared status-chip links/labels.
   - Use `OperatorLabels.cs` as the only label map. Never display internal word “intake” where operator notes prohibit it.

8. **Update MCP safely.**
   - Change schemas/results to expose U-reference, canonical reason code/label, safe detail, state, origins, and history.
   - Add exact lookup/list filters if within updated FRD capability.
   - If resolution is exposed, require existing automation actor authorization, expected version, operation key, and reason; call the same Core use case as Web.
   - Reject U-reference wherever a Case/PO/Audit/Image Intake reference is required.
   - Keep backward-compatible old input only if rolling deployment requires it; mark and test it as legacy, with no operator-facing output.

9. **Execute the semantic test matrix.**
   - New unreadable/corrupt, unsupported, no-id, conflicting-id, ambiguous-owner, and terminal-technical sources each get one correct U item.
   - One INTK-005 group gets one U item and all filenames/receipts.
   - Concurrent allocations produce unique increasing references.
   - Replay/retry produces the same U item; resolution never frees/reuses its number.
   - Search finds open and resolved exact U-reference and never confuses it with Case/Audit/Image Intake.
   - Resolution records actor/time/reason/target and preserves origin/reference/history.
   - Blocked intake, Triage, incomplete Audit, successful Image Intake, and INTK-006 Image-Only paths remain distinct.
   - Existing legacy records backfill once with deterministic reason/reference and no custody loss.
   - Dashboard/Operations counts equal open Unidentified query results.
   - Browser journey covers queue, detail, retained source, resolution, search, history, stale version, authorization, and keyboard use.

10. **Audit removal, verify, and simplify.**
   - Run the exact stale-term search from files.md.
   - Classify each permitted residual (historical ADR, migration compatibility, explicit compatibility test) in the post-implementation report; remove every other occurrence.
   - Run `dotnet restore`, Release build, focused Core/persistence/migration/web/MCP/browser tests, then full `dotnet test`.
   - Test migrations from clean database and a fixture containing representative legacy rows.
   - Perform four-lens simplification. Reject duplicate reason lists, generic reference/workflow frameworks, duplicate queue projections, and direct EF mutations outside the store.
   - Record dated findings/dispositions in this plan and complete the checklist/report.

## Verification

- Every qualifying retained source/group receives exactly one immutable U-reference and required canonical reason.
- U allocation is atomic, monotonic, concurrency-safe, replay-safe, and never reused.
- Grouped material retains all receipts, original filenames, custody, order, and one shared U item.
- Resolution preserves U-reference/origin/history and links the supported destination.
- No U-reference satisfies Case/PO, Audit, principal, or Image Intake identity.
- Every old producer/consumer is explicitly migrated or classified elsewhere.
- Current operator-facing UI, APIs/MCP, docs, tests, counts, filters, and design examples contain no stale Needs sorting wording.
- Release build, clean/upgrade migrations, and all tests pass.

## Risks and controls

- **Blind rename changes semantics:** mandatory docs classification and producer-by-producer matrix.
- **Reference collision/reuse:** dedicated transactional sequence plus unique constraints and no deletion.
- **Duplicate items on replay/group processing:** unique origin constraints and idempotent operation records.
- **Broken settled workflows:** explicit negative tests for Triage, Blocked, Audit, Image Intake, and Image-Only.
- **Migration data loss:** preserve receipts/reasons/custody; deterministic tested backfill.
- **Over-engineering:** focused aggregate inside Intake; no generic workflow/reference platform.

## Simplification pass — 2026-08-19

- Reuse: the new aggregate uses existing `ActionActor`, `TimeProvider`/UTC conventions, EF serializable sequence allocation, and the existing Core → Infrastructure → Web/MCP composition boundaries. No new project, runtime, or storage service was introduced.
- Simplification: one Core reason enum and one reference formatter/parser are consumed by EF, Web, and MCP; no second label taxonomy or generic workflow/reference framework was added.
- Efficiency: list and exact-reference queries remain database-side and indexed; queue ordering is `CreatedAtUtc, Sequence`; registration and resolution use one serializable transaction each.
- Altitude: the UI owns presentation only, MCP delegates to the same Core resolve command, and migration compatibility is isolated to the migration SQL. Residual old decision codes remain only for rolling compatibility and preserved Triage/Image Intake paths.
- Disposition: no behaviour-preserving simplification was identified beyond the implemented reuse and boundary corrections. The unchecked grouped-submission, retained-mail/Operations projection, and full stale-term audit work remains explicit scope for the follow-on integration/review rather than hidden behind a new abstraction.

## Review fixes — 2026-08-19 (takeover, claude-code)

Operator ruling (already given): "Confirmed — Unidentified replaces Needs sorting." The `NeedsSorting => "Unidentified"` mapping at `Message.cshtml.cs` codex flagged as contradicting the settled-distinct-meanings invariant is therefore correct, not reverted; the invariant itself (AGENTS.md/CLAUDE.md) was updated instead to record the supersession.

### Blockers

1. **Missing GRANTs** — `20260819115323_UnidentifiedWork.cs` created three tables with no GRANT. Added a provider-guarded block, then corrected it to per-object least privilege after coordinator review (see "Grant correction" below). Verified against a local copy of the not-yet-merged `Test-MigrationGrants.ps1` and against `Test-AzureDeploymentPlan.ps1 -Mode Local`'s grant-carrying-migration check.
2. **Retryable failures burning a U-reference** — `ProcessIntake.ExecuteCoreAsync`'s reader catch (`IntakeExceptionPolicy.IsRecoverable`) swallowed every recoverable reader exception, including transient ones, into a stored `TechnicalFailure` receipt on the first attempt, before `DurableIntake`'s own retry/terminal classification ever ran. Added `IntakeExceptionPolicy.IsTransientFailure` (the same taxonomy `ProcessQueuedIntake` already used privately, now shared — one list per concept) and threaded an `isFinalAttempt` flag from `ProcessQueuedIntake` (which already computes `workItem.AttemptCount >= RetryDelays.Length`) through `ExecuteRetainedAsync`. A transient reader fault now propagates unless this is the last attempt. Core tests: `FirstAttemptTransientReaderFailurePropagatesWithoutAllocatingUnidentified`, `FinalAttemptTransientReaderFailureIsTerminalAndAllocatesUnidentified`.
3. **Backfill defects** — line ~175 read `DecisionReason` only; fixed to `COALESCE(FailureReason, DecisionReason, ...)` matching `ProcessIntake`'s live source. Line ~184 seeded `REPLICATE('0', 64)` for every row; replaced with a real per-row `HASHBYTES('SHA2_256', ...)` fingerprint matching `EfUnidentifiedStore.Fingerprint`'s algorithm, using the runtime registration actor (`SystemWorker`/`intake-processing`) so a later reevaluation's freshly computed fingerprint can match and replay instead of throwing `UnidentifiedOperationConflictException`. Also truncated the backfilled history `Reason` to 500 chars (same defect as EfUnidentifiedStore.cs:101, below).

### The other 14 reviewer comments

| # | Location | Disposition |
|---|---|---|
| 1 | `ProcessIntake.cs` (image-only material excluded from registration before the image scan runs) | **Fixed.** `ProcessIntake` still skips image-only `NeedsSorting` material (unchanged), but `ProcessQueuedIntake.SynchronizeUnidentifiedAsync` (new) now registers it after `ImageIntakeAutomation.ApplyAsync` runs and confirms no confident registration was made, so it can never be silently absent from both queues. |
| 2 | `EfUnidentifiedStore.cs:174` (no destination-port validation before resolving) | **Fixed.** `ResolveUnidentified` (Core) now validates the target exists via the matching read port (`ICaseQueryStore`, `IImageIntakeQueries`, `ITriageQueries`, `IIntakeReceiptQueries`) before calling `store.ResolveAsync`, throwing `UnidentifiedResolutionTargetNotFoundException` (an `ArgumentException`, so the existing Web resolve-form error handling needs no new catch clause). `ExternalReference` stays unvalidated by design — free-form, no Core-owned port. |
| 3 | `Message.cshtml.cs:114` (mapping NeedsSorting to Unidentified) | **Confirmed correct by the operator, not reverted.** See ruling above. |
| 4 | `ProcessIntake.cs:256` (retryable failures) | **Fixed — Blocker 2.** |
| 5 | `EfUnidentifiedStore.cs:148` (weak replay-fingerprint comparison) | **Fixed.** `ResolveAsync`'s replay check now also compares `TargetKind` and `TargetReference`, not just `TargetId` and `Reason`. |
| 6 | `Unidentified/Details.cshtml.cs:63` (unstable operation key) | **Fixed.** `LoadAsync` generates `OperationKey` once per GET (only when not already bound) and the view carries it as a hidden field, so a retried POST resubmits the same key. |
| 7 | `Unidentified/Details.cshtml:25` (no retained source evidence) | **Fixed.** For a Receipt-origin item, `DetailsModel` now loads the receipt via the existing `IGetIntake` use case and the view shows filename, retained asset records, and decision evidence, with a link to the full `/Received/{id}` page for custody detail. |
| 8 | `ProcessIntake.cs:243` (stale U-items not reconciled on reevaluation) | **Fixed.** `ProcessQueuedIntake.SynchronizeUnidentifiedAsync` resolves an existing open item to the Case or Image Intake that now exists (via the new `IUnidentifiedStore.GetByOriginAsync` and `IResolveUnidentified`) once a reevaluated receipt reaches `CaseCreated` or `ImageIntakeRegistered`. Advisory/non-blocking like image automation itself. |
| 9 | `ProcessIntake.cs:268` (wrong reason mapping, several codes unreachable) | **Fixed.** `MapUnidentifiedReason` now derives `ConflictingIdentification` from `CaseMatchDecision.Ambiguous`, `AmbiguousOwnershipOrDestination` from `MailClassificationDecision.Ambiguous`, and `UnreadableOrCorruptContent` from the reader's `intake_limit_exceeded` evidence signal, instead of collapsing everything to `NoUsableIdentification`. Core test: `AmbiguousCaseMatchRegistersUnidentifiedWithConflictingIdentification`. |
| 10 | `EfUnidentifiedStore.cs:101` (history column truncation) | **Fixed.** Both the runtime `RegisterAsync` and the migration backfill now truncate `SafeDetail` to `UnidentifiedValidation.MaximumReasonLength` (500) before writing `UnidentifiedHistory.Reason`. |
| 11 | `UnidentifiedMcpTools.cs:55` (accepts an invalid numeric enum) | **Fixed.** Added the same `Enum.TryParse` + `Enum.IsDefined` guard `CaseMcpTools` already uses. |
| 12 | `Migrations/...:175` (backfill reads the wrong reason column) | **Fixed — Blocker 3.** |
| 13 | `UnidentifiedMcpTools.cs:52` (state filter defaults to all) | **Fixed.** Defaults to `UnidentifiedState.Open`. |
| 14 | `ProcessIntake.cs:277` (wrong timestamp source) | **Fixed.** Registers with `receipt.ReceivedAtUtc` instead of `ProcessedAtUtc`. |
| 15 | `Migrations/...:184` (all-zero replay fingerprint) | **Fixed — Blocker 3.** |
| 16 | `Unidentified/Details.cshtml:35` (raw enum + raw UTC rendered) | **Fixed.** History rows now route through `OperatorLabels.UnidentifiedState` and `OperatorLabels.OfficeTime` inside a `<time datetime>` element, matching the rest of the app's convention. Also fixed the same defect at the Origin-kind line (`Details.cshtml:22`), which used a raw `UnidentifiedOriginKind` — added `OperatorLabels.UnidentifiedOriginKind`. |

### Grant correction — 2026-08-19 (coordinator review)

The first grant pass gave both runtime roles the identical SELECT/INSERT/UPDATE matrix on `UnidentifiedItems`/`UnidentifiedSequences` and SELECT/INSERT+DENY on `UnidentifiedHistory`. The coordinator asked for per-object least privilege evidenced against actual callers; re-derived:

- `UnidentifiedItems`: Worker SELECT/INSERT/UPDATE (Register is Worker-only in production via `ProcessQueuedIntake` -> `ProcessIntake.ExecuteRetainedAsync`; the UPDATE is `SynchronizeUnidentifiedAsync`'s own resolve call). Web SELECT/UPDATE, no INSERT — nothing in `Pegasus.Web` calls `IRegisterUnidentified`.
- `UnidentifiedSequences`: Worker only — only `RegisterAsync`'s allocation touches it, and that's Worker-only.
- `UnidentifiedHistory`: unchanged, both roles — both Register and Resolve write a row, and both roles reach both operations.

Migration SQL and `scripts/Invoke-AzureDatabaseBootstrap.ps1`'s census were updated together.

## Simplification pass — 2026-08-19 (takeover, claude-code)

- **Reuse:** the retryable-failure fix reused `ProcessQueuedIntake`'s own transient-fault taxonomy instead of inventing a second one — moved it into `IntakeExceptionPolicy.IsTransientFailure` (Core) so both callers share one list. The destination-port validation reused the existing read ports (`ICaseQueryStore`, `IImageIntakeQueries`, `ITriageQueries`, `IIntakeReceiptQueries`) rather than adding a new lookup abstraction. The reason-mapping fix reused fields already on the persisted receipt (`CaseMatchDecision`, `MailClassificationDecision`, `Evidence`) rather than adding a new field to carry an assessment-time classification through to the receipt. The stale-item reconciliation reused `IResolveUnidentified`/`GetByOriginAsync` rather than adding a second write path. The retained-source-evidence fix reused the existing `IGetIntake` use case and linked to the existing `/Received/{id}` page rather than duplicating custody-chain rendering.
- **Simplification:** no new abstraction was introduced for a single caller — `IntakeExceptionPolicy.IsTransientFailure`, `GetByOriginAsync`, and `BuildUnidentifiedRegistrationRequest`/`IsUnidentifiedEligible` (extracted as `internal static` on `ProcessIntake`) each have two real callers (`ProcessIntake` + `ProcessQueuedIntake`, or the two `SynchronizeUnidentifiedAsync` call sites).
- **Efficiency:** `GetByOriginAsync` is a single indexed `SingleOrDefaultAsync` against the existing unique `OriginKind`/`OriginId` index, not a client-side filter over `ListAsync`.
- **Altitude:** `SynchronizeUnidentifiedAsync` and the image-only fallback stay advisory/non-blocking like the image automation they sit beside — a failure there never changes the receipt's own recorded decision, matching the existing convention for that call site.
- **Disposition:** no further behaviour-preserving simplification identified. The still-unchecked checklist items (grouped-submission/INTK-005 integration, mail/retained-mail/Operations projection completion, migration tests against a legacy fixture, concurrency/replay integration tests, `dotnet test` full-suite run) remain explicit scope for follow-on work, not hidden behind new abstraction.

## Additional fix found via new test coverage — 2026-08-19

Added `tests/Pegasus.IntegrationTests/UnidentifiedPersistenceTests.cs` (no persistence-level test file existed for `EfUnidentifiedStore` before this session) covering the history-truncation, replay-fingerprint, and destination-validation fixes above. Writing the destination-validation test caught a real bug in `ProcessQueuedIntake.SynchronizeUnidentifiedAsync` (this session's own reconciliation fix, comment #8): it called `IResolveUnidentified.ExecuteAsync` with `ActionActor.SystemWorker("intake-processing")`, but `UnidentifiedValidation.ValidateResolve` requires `RequireStaffOrAutomation` (Staff or Automation only — registration is the one that also accepts SystemWorker). Every reconciliation attempt would have thrown `UnauthorizedAccessException`, silently swallowed by the same method's own `IntakeExceptionPolicy.IsRecoverable` catch — the reconciliation feature would never have actually worked. Fixed by using `ActionActor.Automation("intake-processing")` for that call, matching how `UnidentifiedMcpTools`'s own automation actor is constructed (`AutomationActorResolver` also produces `ActionActor.Automation`).
