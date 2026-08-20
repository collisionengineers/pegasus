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
