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
