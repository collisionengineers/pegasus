# Plan — TICK-061: Provider credential lifecycle

## Approach

Extend the existing Principal administration Core owner and EF transaction/history conventions with one credential per Principal. Generate a clear secret once, persist only its framework verifier in existing Azure SQL, and expose a verification port for the first real endpoint in TICK-058. PLAT-028 consumes the lifecycle commands and status projection. Do not compose a dormant authentication scheme.

## Governing docs

- Modify `docs/frd/frd-09-provider-and-intermediary-routes.md` with one-time issue/reset, revocation, and submission-only pause/resume behavior.
- ADR-0004 already owns principal-scoped opaque provider credentials and remains accepted. This behavioral refinement does not need a new ADR and must not reserve ADR-0030.
- Update capabilities/open decisions only where API-02 retirement or API-04 wording is stale.

## Steps

1. Update FRD-09/capability wording without changing ADR-0004 or allocating an ADR number.
2. Add Core status, generate/reset/revoke/pause/resume commands, Administrator authorization, expected-version/reason/operation-key rules, and a verification result that separates authenticated identity from permission to submit.
3. Add one EF row per Principal in existing Azure SQL with immutable client ID, password-hasher output only, lifecycle/version timestamps, atomic permanent history, replay/conflict behavior, and migration.
4. Return clear text only from generate/reset command results; prevent storage in entities, history, logs, telemetry, URLs, TempData, or configuration.
5. Leave Web authentication composition to TICK-058, where the handler and first endpoint ship together; expose only the Core verification port here.
6. Add Core/persistence/migration/architecture tests for one-time issue, immediate reset invalidation, revoke/reissue, pause/read distinction, authorization, concurrency/replay, history, hash-only storage, and cross-Principal isolation.
7. Run the simplification lenses, locked restore, Release build, focused/full tests, and record the post-implementation report.

## Azure decision

Reuse Azure SQL and the Web managed identity. Do not put provider clear secrets in Key Vault: Pegasus never needs to recover them, and Key Vault would create a second lifecycle owner. Add no Azure resource or cloud write in this ticket.

## Verification

Tests inspect persisted state and history to prove no clear secret survives, and prove the verification port returns Principal/client identity only for valid non-revoked credentials while pause remains a submission authorization result. Architecture tests prove no provider endpoint or authentication scheme is activated by this ticket.

## Risks / deferred

Multiple simultaneous credentials and live issuance remain deferred. TICK-058 and PLAT-028 remain blocked until this backend contract merges.
