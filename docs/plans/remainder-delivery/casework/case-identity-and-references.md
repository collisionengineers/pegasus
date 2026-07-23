# Case identity and references

## Purpose

Give each accepted case one durable identity and the correct non-reusable principal/year reference, including Audit forms. This area owns reference policy and allocation; principal correction is withheld pending its report-sent authority and this area does not decide whether raw intake is definitive.

## Authority and current boundary

- **Authority:** [source order](../../../agent-guidance/source-of-truth.md), [questionnaire §§4–5](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md), [remaining requirements §4](../../remaining-requirements.md), and [open decisions](../../open-decisions.md).
- **Policy owner:** planned Core `CaseIdentity` policy/use case.
- **Current implementation:** there is no active case/reference allocator. The retired Development proof is preserved as migration audit evidence but its `CaseEntity`, counter, receipt links and allocation caller have been removed.
- **Real callers:** `/Intake/Qdos` is a development-only pre-case intake caller and creates no case/reference. Accepted-case UI, allocator, Worker/API/MCP and principal correction are **planned**; correction has no task/caller until the sent-report decision is settled.
- **Persistence/adapters:** the future accepted-case, principal/year counter, case type and original-report assessment model is planned relational data. Alias/correction confirmation schema is withheld with the correction command.
- **Dependencies:** [intake and acceptance](intake-and-case-acceptance.md), staff audit, Box/EVA plans; lifecycle supplies authorised transitions.
- **Replaces/consolidates:** the retired QDOS-specific receipt-store allocator has been removed. Add one shared allocator only with the accepted-case caller; do not restore the old path or create two counters/formatters.
- **Withheld decision boundary:** principal correction, alias issuance and Box/EVA confirmation work are not emitted as an implementation task until [authoritative sent-report evidence and time](../../open-decisions.md#authoritative-sent-report-evidence-and-time) settles whether a correction is still permitted. Do not add a provisional UI flag, schema or caller.

## Shared failure and observability rules

No reference is allocated for a non-definitive intake, unknown principal, missing readable image-led registration, or standalone Audit without unambiguous original-report assessment. Sequence exhaustion is a visible terminal allocation failure: never wrap, widen or reuse. Every allocation and failed rule is audited with actor/correlation. The future correction path must preserve searchable history but is not implemented from this task.

## Allocate and represent active case identities

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §§4–5 and remaining requirements §4.
- **Confirmed facts:** format is `{principal code}{YY}{three-digit shared sequence}`; Audit prefixes are `a.`/`ap.`; all case types share the principal/year sequence.
- **Decision required before implementation:** None for Inspection, standalone Audit and Inspection + Audit.

### Owner and dependencies

- **Policy/implementation owner:** Core `CaseIdentity` plus one SQL-backed allocation port.
- **Independent evaluator:** test engineer, with SQL Server concurrency evaluation separate from the implementer.
- **Prerequisites:** definitive accepted draft, principal configuration/admin and one migration stream.
- **Consumers/unlocks:** acceptance, case workspace, Box naming and EVA export.

### Caller, contract and change boundary

- **Real or intended caller:** planned `AcceptCaseDraft`; current `/Intake/Qdos` stops at a pre-case typed draft and must not regain allocation behaviour.
- **Input/output:** principal, allocation calendar year, active case type and required assessment result produce the base reference, optional audit display reference and identity audit record.
- **Ordered decisions and failure behavior:** validate principal; allocate one shared sequence atomically; for standalone Audit require original report repairable/total-loss result; for Inspection + Audit allocate normal inspection first then later create its audit reference/subfolder. Missing/ambiguous evidence retains pre-case source or blocks the later Audit reference.
- **Persistence/migration:** migrate minimal case/reference to stable case identity, principal code, type and allocation records; preserve unique constraints and serializable SQL behaviour.
- **Adapters/side effects:** emit a post-commit named-folder/export work item only; no direct Box/EVA mutation.
- **Operator surface and observability:** show base/reference type and allocation failure; no principal-correction or alias action is exposed while its decision is withheld.
- **Documentation affected:** describe implementation evidence after replacement; operator notes remain read-only.
- **Replaces/consolidates:** delete QDOS constant/formatting/counter decision from `EfQdosIntakeStore` in the same slice.

### Scope

- **Included:** principal codes, Inspection/Audit/Inspection + Audit identity, atomic sequence allocation and exhausted-sequence outcome.
- **Excluded:** Diminution/Commercial processing, estimate/valuation/invoice workflows, folder implementation and report transmission.

### Implementation checklist

- [ ] Move reference rules to one Core identity owner and one persistence allocator shared by all accepted case types.
- [ ] Persist assessment provenance; make API/UI contracts expose calculated values and never accept an arbitrary reference.
- [ ] Migrate `/Intake/Qdos` through case acceptance and remove its private allocator/format logic.

### Validation checklist

- [ ] Allocate `QDOS26001`, then other active case types share the next QDOS/year number; validate year rollover and 999 exhaustion.
- [ ] Repairable/total-loss standalone Audit emits `a.`/`ap.` only with unambiguous original-report finding; absent/ambiguous result allocates nothing.
- [ ] Inspection + Audit keeps normal inspection reference, then creates the appropriate later Audit reference in the same case.
- [ ] Prove duplicate delivery, rollback and concurrent allocation against SQL Server; execute the migrated `/Intake/Qdos` real caller.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1`, recording exact result and its non-production limitation.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Shared QDOS/year cases | Monotonic, unique common sequence | SQL transaction test | Production contention |
| Standalone Audit with ambiguous report | Warning/pre-case outcome, no reference | negative domain/caller test | Operator assessment quality |
| Counter at 999 | Visible failure, no wrap/reuse/partial case | persistence test | Recovery action approval |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** explicit production migration/release approval; no Box/EVA write is authorised.
- **Rollout/activation:** apply one ordered migration, migrate caller, prove SQL transaction and operator-visible references before enabling further callers.
- **Rollback/recovery:** retain all identity/allocation history; revert application artifact only. Correct data with an audited forward action, never counter rollback.
- **Irreversible risk:** issued sequence values cannot be reused.

### Deferred-capability impact

- **Named capabilities:** Diminution/Commercial, EVA replacement/API, valuation/estimating and future provider APIs.
- **Stable seam retained:** stable case ID, principal code, case type, base reference, display/reference kind and immutable allocation history are provider-neutral; a future settled correction slice can add alias history without replacing the allocator.
- **Future migration/replacement:** additional case-type semantics and external number mapping still need explicit policies/migrations.
- **Activation boundary:** direct product decision and accepted implementation slice for each deferred type/integration.
- **Deliberately absent:** no dormant per-type counter, external mapping service, finance numbering or EVA API adapter.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Identity owner and proof path are specified | Implementation, caller, deployment or acceptance |
