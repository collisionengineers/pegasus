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

## PR-048..050 correction plan — 2026-08-20

### Governing docs

FRD-08 still owns exact-message reasoned association and permanent history. `docs/design/README.md` requires the whole visible search result to be one keyboard/accessibility target. No governing-doc behavior or ADR changes.

### Steps

1. Reuse the Intake Details lease-first convention: add separate prepare-link and prepare-unlink POSTs that verify the server-bound receipt/current target and acquire the existing Case lease.
2. Carry the exact lease token, reviewed versions, and one association operation key into the final reason dialog. On final POST, resolve message→receipt server-side and call the existing Core command without pre-empting its replay/fingerprint ordering.
3. On a definitive association refusal after preparation, quietly release through `IReleaseCaseEditLease` with `CancellationToken.None`; preserve uncertain/cancelled outcomes for same-confirmation recovery.
4. Put registration, claimant, and stage inside each existing Case-result anchor.
5. Add exact authenticated SQL/Web tests for successful link/unlink replay, same-key changed-input conflict, post-acquire failure compensation/immediate retry, successful lease consumption, and accessible result text.
6. Run focused/proportional verification, repeat four lenses, update PIR/traceability, push the replacement head, and leave Review.

No Core/EF/schema/framework/swap/external-write scope is introduced.
