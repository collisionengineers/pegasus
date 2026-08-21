# Plan — PLAT-028: Redesign Organizations and Principals with provider API controls

## Approach

After TICK-061 provides Core credential commands, make Organizations the single Administration entry point and its detail page the consolidated Organization/Principal workspace. Preserve separate create and immutable replacement actions, remove the duplicate Principal index destination, and add thin credential controls per Principal. A one-time secret is rendered only in the immediate POST result; it is never persisted in UI state.

## Governing docs

- **Modifies `docs/frd/frd-04-parties-accounts-and-access.md`**: narrow the Administrator prohibition so provider credential generation/reset/revocation/pause/resume is allowed, while cloud/release secrets and non-administrators remain excluded. Explicitly authorized by the operator on 2026-08-21.
- **Modifies `docs/frd/frd-09-provider-and-intermediary-routes.md`**: add the accepted Principal-owned administration workflow and pause semantics.
- Update `docs/design/README.md` to place these administrator controls on the consolidated Principal surface while provider clients continue to receive no staff shell.

## Steps

1. Integrate TICK-061 projections/commands, update FRD-04/FRD-09/design authority, and clear the ticket's governing-doc debt.
2. Redesign the Organization list as the sole entry point using existing page-header, filter, table, status, form, and responsive primitives; remove explanatory empty-state panels.
3. Redesign Organization detail to show roles and its Principals together, with clear create and immutable replacement actions and credential status/actions on each Principal.
4. Retire the separate Principal index navigation and provide a safe redirect to Organizations; preserve create/replace URLs or replace them with equivalent consolidated routes without changing Core behavior.
5. Add Administrator-only generate/reset/revoke/pause/resume handlers delegating to TICK-061 commands with expected version, reason, operation key, and concise destructive confirmation.
6. Render generated/reset clear text exactly once in the immediate response, with no TempData/session/URL/log/database copy; refresh/back navigation shows status only.
7. Add Razor/integration/browser tests for existing workflows, authorization, stale/replay behavior, each credential action, one-time secret non-retention, no provider/staff leakage, keyboard/focus flow, axe, constrained width/200% equivalent, and no document overflow.
8. Refresh current architecture/design evidence, run the simplification lenses, locked restore, Release build, focused/full tests, and record visual/test evidence in the post-implementation report.

## Verification

Authenticated integration tests cover Administrator versus Engineer/User and exercise create/update/replace plus all credential controls. A real browser proves the consolidated information architecture, one-time secret view, confirmations, keyboard order, axe, and responsive/no-overflow behavior. Screenshots use repository fixtures only.

## Risks / open questions

- The clear secret must not cross a redirect or durable UI store; tests inspect response, logs where available, and subsequent reload.
- TICK-061 blocks implementation.
- Multiple keys, provider self-service, live issuance, and generic credential/cloud administration remain deferred.
