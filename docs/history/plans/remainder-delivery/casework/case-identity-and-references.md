# Case identity and references

> **Archive status — non-authoritative planning evidence.** Revalidate against current product, roadmap, architecture, operations, design, decisions, and code before use.

Pre-conversion status: **Ready V1 plan — settled identity invariants**

## Purpose

Give each accepted case one durable identity and the correct non-reusable principal/year reference, including Audit forms. This area owns reference policy and allocation; the principal and reference become immutable immediately on allocation, and this area does not decide whether raw intake is definitive.

## Feature coverage

Primary matrix IDs: `ACC-06`, `CASE-02`, `CASE-03`, `CASE-04`, `CASE-07`, `CASE-08`, `CASE-09`, and `CASE-10`. Their routes are [allocation and active identities](#allocate-and-represent-active-case-identities) and [immutable principal-code cutover](#replace-a-used-principal-code-through-an-immutable-cutover). Allocation remains owned by the [maturity map](../../feature-maturity-map.md); this list is a route, not implementation evidence.

## Authority and current boundary

- **Authority:** [source order](../../../../agent-guidance/source-of-truth.md), [questionnaire §§4–5](../../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md), [remaining requirements §4](../../../../product/v1-gap.md), and [open decisions](../../../../product/open-decisions.md).
- **Policy owner:** planned Core `CaseIdentity` policy/use case.
- **Current implementation:** there is no active case/reference allocator and v2 has not been deployed. The retired Development proof produced non-business test references only; its migration records those values for migration diagnostics, then deliberately removes the `CaseEntity`, counter, receipt links and allocation caller. Those test values are not issued case identities, do not reserve sequence numbers, and must not seed or constrain the future allocator.
- **Real callers:** `/Intake/Upload` is a development-only pre-case intake caller and creates no case/reference. Accepted-case UI, allocator and Worker/API/MCP callers are **planned**; the planned case-detail caller also owns the explicit `Created in error` replacement action.
- **Persistence/adapters:** the future accepted-case, principal/year counter, case type, original-report assessment, terminal reason and replacement-case relationship are planned relational data. No reference-alias correction model is authorised.
- **Dependencies:** definitive pre-case intake evidence from [intake and acceptance](intake-and-case-acceptance.md), principal configuration, staff action history and the single migration stream; Box/EVA consume the allocated identity later, while lifecycle supplies authorised transitions.
- **Replaces/consolidates:** the retired QDOS-specific receipt-store allocator has been removed. Add one shared allocator only with the accepted-case caller; do not restore the old path or create two counters/formatters.
- **Settled wrong-principal boundary:** never rewrite a case's allocated principal or reference. An authorised staff action marks the original terminal `Created in error`, records the reason in permanent action history, creates a replacement case through the same allocator under the correct principal, and links both cases. The original number and the replacement number are never reused. `Created in error` cannot reopen; do not add an alias, renumber, Box-rename or EVA-rewrite path.

## Shared failure and observability rules

No reference is allocated for a non-definitive intake, unknown principal, missing readable image-led registration, or standalone Audit without unambiguous original-report assessment. Sequence exhaustion is a visible terminal allocation failure: never wrap, widen or reuse. Every allocation and failed rule enters permanent action history with actor/correlation. A wrong-principal replacement preserves both immutable cases and their explicit relationship.

## Allocate and represent active case identities

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §§4–5 and remaining requirements §4.
- **Confirmed facts:** format is `{principal code}{YY}{three-digit shared sequence}`; Audit prefixes are `a.`/`ap.`; all case types share the principal/year sequence.
- **Decision required before implementation:** None for Inspection, standalone Audit and Inspection + Audit.

### Owner and dependencies

- **Policy/implementation owner:** Core `CaseIdentity` plus one SQL-backed allocation port.
- **Independent evaluator:** test engineer, with SQL Server concurrency evaluation separate from the implementer.
- **Prerequisites:** durable fully processed pre-case evidence, known principal/code, readable registration, unambiguous active case type, any required standalone-Audit assessment, trusted actor/action history, and one migration stream. The allocator contract is a dependency of atomic acceptance; it must not require an already accepted case.
- **Consumers/unlocks:** acceptance, case workspace, Box naming and EVA export.

### Caller, contract and change boundary

- **Real or intended caller:** planned `AcceptCaseDraft`; current `/Intake/Upload` stops at a pre-case typed draft and must not gain allocation behaviour.
- **Input/output:** principal, allocation calendar year, active case type and required assessment result produce the base reference, optional Audit display reference and identity action-history record.
- **Ordered decisions and failure behavior:** validate principal; allocate one shared sequence atomically; for standalone Audit require original report repairable/total-loss result; for Inspection + Audit allocate normal inspection first then later create its Audit reference/subfolder. Missing/ambiguous evidence retains pre-case source or blocks the later Audit reference.
- **Persistence/migration:** migrate minimal case/reference to stable case identity, principal code, type and allocation records; preserve unique constraints and serializable SQL behaviour.
- **Adapters/side effects:** emit a post-commit named-folder/export work item only; no direct Box/EVA mutation.
- **Operator surface and observability:** show base/reference type and allocation failure; show `Created in error`, the required reason and reciprocal replacement/original links without offering principal/reference editing.
- **Documentation affected:** describe implementation evidence after replacement; operator notes remain read-only.
- **Replaces/consolidates:** keep the provider-neutral intake store free of principal formatting, counters, or allocation decisions; `AcceptCaseDraft` becomes the sole allocation caller.

### Scope

- **Included:** principal codes, Inspection/Audit/Inspection + Audit identity, immediate identity immutability, atomic sequence allocation, exhausted-sequence outcome and wrong-principal replacement linkage.
- **Excluded:** Diminution/Commercial processing, estimate/valuation/invoice workflows, folder implementation and report transmission.

### Implementation checklist

- [ ] Move reference rules to one Core identity owner and one persistence allocator shared by all accepted case types.
- [ ] Persist assessment provenance; make API/UI contracts expose calculated values and never accept an arbitrary reference.
- [ ] Connect definitive pre-case source/draft evidence from the shared intake boundary to case acceptance without adding allocator/format logic to `/Intake/Upload` or another transport.
- [ ] Add one guarded `Created in error` replacement use case that closes the original, allocates a distinct replacement through the same transaction boundary, links both cases and refuses reopening or identity edits.

### Validation checklist

- [ ] Allocate `QDOS26001`, then other active case types share the next QDOS/year number; validate year rollover and 999 exhaustion.
- [ ] Repairable/total-loss standalone Audit emits `a.`/`ap.` only with unambiguous original-report finding; absent/ambiguous result allocates nothing.
- [ ] Inspection + Audit keeps normal inspection reference, then creates the appropriate later Audit reference in the same case.
- [ ] Prove an attempted principal/reference edit is refused immediately after allocation; wrong-principal handling leaves the original terminal `Created in error`, creates one linked replacement, and never reuses either number.
- [ ] Prove duplicate delivery, rollback and concurrent allocation against SQL Server; execute the `/Intake/Upload` real caller through the accepted transaction.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1`, recording exact result and its non-production limitation.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Shared QDOS/year cases | Monotonic, unique common sequence | SQL transaction test | Production contention |
| Standalone Audit with ambiguous report | Warning/pre-case outcome, no reference | negative domain/caller test | Operator assessment quality |
| Counter at 999 | Visible failure, no wrap/reuse/partial case | persistence test | Recovery action approval |
| Wrong principal discovered after allocation | original is terminal `Created in error`; one correctly numbered replacement is linked; neither identity changes or reopens | case-detail-to-Core transaction and negative edit/reopen tests | manual correction of external artefacts |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** explicit production migration/release approval; no Box/EVA write is authorised.
- **Rollout/activation:** apply one ordered migration, migrate caller, prove SQL transaction and operator-visible references before enabling further callers.
- **Rollback/recovery:** retain all identity/allocation history; revert application artifact only. Correct data with a forward action-history entry and linked replacement, never counter rollback.
- **Irreversible risk:** issued sequence values cannot be reused.

### Deferred-capability impact

- **Named capabilities:** Diminution/Commercial, EVA replacement/API, valuation/estimating and future provider APIs.
- **Stable seam retained:** stable case ID, principal code, case type, base reference, display/reference kind, immutable allocation history and explicit case-to-case replacement relationship are provider-neutral.
- **Future migration/replacement:** additional case-type semantics and external number mapping still need explicit policies/migrations.
- **Activation boundary:** direct product decision and accepted implementation slice for each deferred type/integration.
- **Deliberately absent:** no dormant per-type counter, external mapping service, finance numbering or EVA API adapter.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Identity owner and proof path are specified | Implementation, caller, deployment or acceptance |

## Replace a used principal code through an immutable cutover

**Evidence state:** Planned; no Administrator caller or replacement transaction exists.

### Owner, caller and contract

- **Requirement/decision:** [remaining requirements §4](../../../../product/v1-gap.md#4-case-model-and-lifecycle) makes a used principal code immutable. A legitimate change creates a new linked principal and atomically deactivates its predecessor.
- **Policy/implementation owner:** Core `PrincipalCodeReplacement` within the case-identity/reference capability; the [Administrator principal page](../identity-and-access/staff-identity-authorisation-and-action-history.md#administer-principals-and-live-operational-configuration) is the planned caller. This is distinct from wrong-principal case replacement: it changes prospective principal configuration and counter continuity, never a case's allocated identity.
- **Input/output:** authorised Administrator, predecessor principal/version, unique replacement code and required reason yield one active linked successor, one inactive predecessor, preserved counter continuity and a permanent action-history event. The caller never supplies the cutover year.
- **Ordered decisions and failure behavior:** refuse editing a code once any reference uses it; validate Administrator, predecessor/current version and replacement uniqueness; derive the cutover year inside Core from the injected trusted clock and the Europe/London business calendar; lock predecessor/successor/counter rows; create the linked successor and deactivate the predecessor atomically. The successor continues the predecessor's next sequence in that derived year and begins later years at `001`. If the predecessor cutover-year counter is exhausted at `999`, preserve that exhaustion visibly for the successor in that year; never wrap, widen, reset or reuse. Conflict, stale version, duplicate code or history-write failure leaves predecessor/counters unchanged.
- **Persistence/migration:** principals retain stable IDs plus predecessor/successor relationship, cutover year, inactive reason/time and concurrency version. The single principal/year counter owner records cutover continuity without copying historical case identities or creating a second sequence stream.
- **Operator surface and observability:** Administrator UI labels a used code read-only, requires reason and explicit replacement confirmation, displays the trusted current Europe/London cutover year and its next/exhausted state as read-only, previews later-year `001`, and links predecessor/successor history. Engineer/User/direct requests are denied before mutation; no request field may override the year.

### Scope, proof and rollback

- **Included:** used-code edit refusal, one linked successor transaction, atomic predecessor deactivation, cutover-year sequence continuity, later-year reset, concurrency/exhaustion behavior and permanent action history.
- **Excluded:** wrong-principal case correction, renumbering existing cases, reference aliases, multiple active successors, bulk principal migration, Box/EVA rename and provider credentials.
- [ ] Add the Administrator command through the existing principal page and the single Core/persistence owners; do not add a second allocator or settings store.
- [ ] Prove unused-code metadata edit remains separate, while a used-code edit is refused and offers only the replacement action.
- [ ] Transaction-test successful cutover, rollback on every pre-commit/history failure, duplicate/stale concurrent Administrators, concurrent case allocation at cutover and unique single-successor enforcement on SQL Server; prove the caller contract contains no writable year override.
- [ ] With an injected clock on both sides of the Europe/London year boundary, prove Core derives the applicable year consistently; predecessor next `057` yields successor `057` in that year, later-year first allocation is `001`, and exhausted `999` remains a visible no-allocation result in the cutover year without affecting later-year `001`.

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Administrator replaces used code | linked successor active, predecessor inactive, reason/history committed once | Web-to-Core SQL transaction test | correctness of operator-entered code |
| Concurrent replacement/allocation | one serial order; no duplicate successor, skipped/reused number or split active state | SQL Server concurrency test | production contention ceiling |
| Failure after validation | no principal/counter/action-history partial commit | fault-injection transaction test | disaster recovery |
| Cutover-year exhausted | successor retains visible exhausted state for that year; later year begins `001` | allocator boundary test | approval to widen reference format |

- **Rollout/activation:** ship migration and read-only used-code UI first; enable the replacement command after focused transaction/concurrency proof and Administrator walkthrough.
- **Rollback/recovery:** disable the command and retain both linked principals, counters and history. Once the successor exists, correct mistakes through another authorised forward replacement; never reactivate by rewriting issued identity or decrementing a counter.
- **Irreversible risk:** principal-code and sequence values become permanent business identifiers when issued; the transaction must not expose a partial cutover.

### Deferred-capability impact

- **Named capabilities:** more principals, provider APIs, predecessor import, Diminution/Commercial and external accounts.
- **Stable seam retained:** stable principal IDs, explicit predecessor/successor relation, immutable codes and one principal/year counter owner preserve future callers without reference rewriting.
- **Future migration/replacement:** imported or externally mapped principal identities require an approved mapping/cutover migration; they do not replace this allocator.
- **Activation boundary:** approved principal data and Administrator action for each real cutover; deferred capabilities need their own product/integration authority.
- **Deliberately absent:** no editable used code, alias table, parallel counter, automated mass cutover or external side effect.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Plan review | Owner, caller, transaction, negative cases, sequence semantics and rollback are specified | Implementation, database migration, caller execution, deployment or acceptance |
