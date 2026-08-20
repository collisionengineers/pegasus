# Plan — MAIL-10

## Chosen approach

Add one thin exact-message Web caller over the existing association ports. The page searches with `ISearchCases`, reviews one canonical `IGetCase` summary, and only then confirms a reasoned mutation. On every POST it reloads the retained message and receipt, derives the authoritative receipt/current Case server-side, checks the reviewed versions, acquires the existing Case edit lease, and delegates to `ILinkIntake` or `IReverseIntakeLink`.

Relink is intentionally not a command: unlink the current Case, then perform a fresh search/review/reasoned link. This preserves two decisions and the existing transaction/history semantics.

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: exact-message association behavior and permanent history.
- `docs/design/README.md`: deliberate search, business-readable summary, reason, confirmation, accessibility, and return context.
- No ADR: existing Core/Infrastructure/Web boundaries carry the change.

## Steps

1. Create the ticket branch/worktree from exact `origin/dev` `708706b8` and take TICK-052.
2. Extend Mail Message with side-effect-free Case search and one selected canonical target summary, reusing `ISearchCases` and `IGetCase`.
3. Add narrow link/unlink POST handlers that re-resolve exact message/receipt/current association, validate reviewed versions, acquire the existing edit lease, and call the existing Core command.
4. Render separate reasoned confirmations; never offer direct active-to-active replacement.
5. Add focused authenticated Web tests plus proportional persistence regression coverage only where existing tests do not already prove it.
6. Run locked restore/Release build, focused and proportional tests, then the four simplification lenses. Apply behavior-preserving findings.
7. Update capability wording and write the PIR to the exact local evidence tier; push and open a PR to `dev` in Review.

## Boundaries

No live mailbox/Graph/Box/cloud/deployment/permission/production write; no new schema, persistence owner, transaction, action framework, or direct swap. Production correction remains a separately approved verification activity, not this implementation checklist.

## Simplification pass — 2026-08-20

- **Reuse:** Found the landed `UploadCaseDecision` in the exact ticket worktree after an earlier root-checkout lookup missed it. Replaced direct `ISearchCases` use with its existing bounded, normalized suggestion method. Reused `IGetCase`, `IGetIntake`, `IAcquireCaseEditLease`, `ILinkIntake`, `IReverseIntakeLink`, `OperatorLabels.CaseStage`, and `_ReasonDialog` unchanged.
- **Simplification:** No new service, result taxonomy, command framework, Core contract, EF store, table, migration, or active-to-active swap was introduced. The two explicit POST handlers remain separate because link and unlink have different authority predicates and consequences; a boolean/generic action handler would obscure those rules.
- **Efficiency:** Search stays bounded by the existing eight-result helper. The page loads a full Case only for the current or explicitly selected target, not for every search result. Mutations perform only the authoritative message/receipt/Case reloads required for freshness.
- **Altitude:** Web owns presentation/orchestration only. Core and the existing serializable EF transaction continue to own authorization, edit-lease enforcement, idempotency, current-association mutation, and append-only history.
- **Applied findings:** corrected the stale helper premise; reused the shared search helper; reused the shared lifecycle label; rejected terminal/archived selected targets before leasing; validated reason before any lease mutation; removed the unnecessary lease-key suffix.
- **Unapplied findings:** none.
