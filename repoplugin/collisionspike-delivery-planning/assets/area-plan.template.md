# <Owned area>

## Purpose

<Operator/business outcome and why this area owns it.>

## Authority and current boundary

- **Authority:** <relative links to requirements and accepted decisions>
- **Policy owner:** <Core feature or named target policy>
- **Current implementation:** <existing owner and at most three anchor paths/symbols>
- **Real callers:** <called today versus intended and absent>
- **Persistence/adapters:** <authoritative data and external translations>
- **Dependencies:** <upstream area/task links>
- **Replaces/consolidates:** <implementation to migrate or remove>

## Shared failure and observability rules

<Typed failures, operator-visible outcomes, action events, telemetry and retry boundaries shared by this area.>

## <Task outcome>

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** <relative links>
- **Confirmed facts:** <facts only>
- **Decision required before implementation:** None | <canonical open-decision link and affected behavior>

### Owner and dependencies

- **Policy/implementation owner:** <one owner>
- **Independent evaluator:** <required role/evidence>
- **Prerequisites:** <links>
- **Consumers/unlocks:** <links or callers>

### Caller, contract and change boundary

- **Real or intended caller:** <entry point and current state>
- **Input/output:** <business input and observable result>
- **Ordered decisions and failure behavior:** <policy, unknown outcome, actor and action history>
- **Persistence/migration:** <authoritative data and transaction>
- **Concurrency/edit ownership:** <exclusive lease, optimistic version, idempotency/reconciliation, recovery, or concrete reason not applicable>
- **Adapters/side effects:** <boundary translation and idempotency>
- **Permission/scope guard:** <authority, grant type, exact resources, broader/additive grants excluded, pre-client validation and zero-client-call denial; or not applicable>
- **Operator surface and observability:** <UI, permanent action history and content-safe telemetry>
- **Documentation affected:** <authoritative or guidance documents; operator notes remain read-only>
- **Replaces/consolidates:** <old path removed in the same slice>

### Scope

- **Included:** <bounded behavior>
- **Excluded:** <deferred, unsupported or separately approved behavior>

### Implementation checklist

- [ ] <Outcome-oriented implementation step>
- [ ] <Real-caller wiring and operator-visible result>
- [ ] <Migration/cleanup with one owner>

### Validation checklist

- [ ] <Literal positive case>
- [ ] <Contradiction, negative or failure case>
- [ ] <Persistence, parallel-actor, stale-version, concurrency or replay case>
- [ ] <Actual caller and genuine-input evidence where relevant>
- [ ] `pwsh ./scripts/Invoke-RepoCheck.ps1` with exact result and limitations

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| <case> | <result> | <test/caller/operator evidence> | <limitation> |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** <none or exact target>
- **Rollout/activation:** <ordered safe sequence>
- **Rollback/recovery:** <recoverable action and retained data>
- **Irreversible risk:** None | <risk and required decision>

### Deferred-capability impact

- **Named capabilities:** <relevant deferrals>
- **Stable seam retained:** <identity, provenance, contract, data or adapter>
- **Future migration/replacement:** <work still required later>
- **Activation boundary:** <evidence, scale, licence, product decision or approval>
- **Deliberately absent:** <no dormant code, service, queue, table, endpoint, dependency or flag>

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Sequence and criteria exist | Implementation, caller, deployment and acceptance |
