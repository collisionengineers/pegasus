# Plan — TICK-061: Provider API credential lifecycle

## Approach

Build the credential lifecycle first because API-01 and API-03 cannot have a real authenticated caller without it. Extend the existing Principal administration policy and EF transaction/history conventions with one credential per Principal; use framework password hashing and a thin Web authentication handler. This reuses the existing Core owner and avoids a parallel client registry. PLAT-028 consumes the commands later for the redesigned UI.

## Governing docs

- **Modifies `docs/frd/frd-09-provider-and-intermediary-routes.md`** with the operator-authorized v1 lifecycle: one Principal credential, one-time generated/reset secret, immediate rotation, revocation, and submission-only pause.
- **New ADR**: add ADR-0030 to supersede ADR-0004's provider portion, preserving the separate staff MCP decisions while removing mandatory transient processing-status exposure and recording Principal-owned administration.
- Update `docs/capabilities.md` to retire API-02 and clarify API-01/API-03/API-04; update ADR indexes and open decisions. This authorization was explicitly given on 2026-08-21.

## Steps

1. Update the governing documents and add ADR-0030; link it to this ticket before implementation completes.
2. Add Core credential status, generate/reset/revoke/pause/resume commands, one Principal-owned port, Administrator authorization, expected-version/reason/operation-key rules, and provider-client verification that distinguishes authentication from submission permission.
3. Add one EF credential row per Principal, framework password-hasher output only, lifecycle/version timestamps, transactionally append-only administration events, replay/conflict handling, and the migration.
4. Add a provider authentication scheme in Pegasus.Web using HTTP Basic client ID/secret over HTTPS, producing a Principal/client actor without accepting staff cookies; revoked/unknown/invalid credentials return the same authentication failure.
5. Compose the ports and authentication scheme without enabling any provider endpoint or issuing a live credential; API-01/API-03 supply the real callers.
6. Add Core, persistence, authentication, migration, and architecture tests for one-time return, hash-only storage, immediate reset invalidation, revocation/reissue, pause/read distinction, concurrency, replay, history, and cross-Principal denial.
7. Run the simplification lenses over the branch diff, then locked restore, Release build, focused tests, and full tests; record results in the post-implementation report.

## Verification

Core tests prove lifecycle and authorization. SQL integration tests inspect that clear secrets never persist and history is atomic. Web tests authenticate valid/invalid/revoked credentials and prove pause is an authorization capability rather than authentication failure. Architecture tests prove Web/Infrastructure dependency direction and no provider endpoint is activated prematurely. Proof records the locked build/test output after merge.

## Risks / open questions

- Secret exposure through logs, URLs, TempData, or entities is prevented by returning clear text only from the generate/reset command result and by explicit negative tests.
- API-01 and PLAT-028 remain blocked until this merges.
- Multiple concurrent credentials and live issuance remain deferred to named tickets/approval.
