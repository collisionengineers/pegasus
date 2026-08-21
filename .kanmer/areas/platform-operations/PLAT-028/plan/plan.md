# Plan — PLAT-028: Redesign Organizations and Principals with provider API controls

## Approach

After TICK-061 supplies Core commands/status, make Organizations the single Administration entry point and Organization detail the consolidated Organization/Principal workspace. Preserve separate create and immutable replacement actions. Add thin Principal credential controls and render a generated/reset secret only in the immediate response.

## Governing docs

- Modify FRD-04 narrowly to allow Administrators to manage Principal provider credentials while retaining prohibitions on cloud/release secrets and non-administrator access.
- Modify FRD-09 with Principal-owned lifecycle/pause behavior.
- Update the design authority only if the durable layout rule is genuinely new; otherwise follow existing page-economy/no-explanatory-copy rules.

## Steps

1. Integrate TICK-061 and update/link the governing FRDs, clearing `docs_todo` when those repo changes land.
2. Redesign the Organization list as the sole entry point using existing header/filter/table/status/form/responsive primitives.
3. Consolidate roles, Principals, create, and immutable replacement on Organization detail; remove the duplicate Principal-index destination with a safe redirect.
4. Add Administrator-only generate/reset/revoke/pause/resume handlers delegating to Core with expected version, reason, and operation key.
5. Render generated/reset text once in the immediate HTTPS response; persist or emit it nowhere else. Subsequent navigation shows status only.
6. Use labels, values, and at most one destructive consequence sentence; add no explanatory panels or provider self-service/staff-shell surface.
7. Add Razor/integration/browser tests for authorization, legacy workflows, lifecycle controls, replay/stale state, secret non-retention, keyboard/focus, axe, constrained width, and overflow.
8. Refresh current-state/design evidence after deployment and run simplification plus locked restore/build/focused/full tests.

## Azure decision

The existing Web Container App and Azure SQL carry this feature. Do not add an Azure Portal workflow, Key Vault per-Principal secrets, App Configuration, a second app, or another deployment unit. Live issuance is a separately approved external write.

## Verification

Administrator and non-administrator integration tests plus a real browser prove consolidated navigation, unchanged Principal invariants, safe action confirmations, once-only secret display, and accessible responsive behavior.

## Deferred

Multiple keys, provider self-service, live issuance, and generic credential/cloud administration remain out of scope.
