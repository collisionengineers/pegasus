# Post-implementation report — MAIL-10

## Outcome

The exact retained-message page supports deliberate search/review/reasoned link, reasoned unlink, and a separate replacement search. PR-048..050 corrections add a lease-first confirmation boundary so exact successful final POSTs reach the existing Core fingerprint replay before any fresh-state rejection. Changed inputs under the same key still conflict. Definitive post-acquire failures release through the existing Case lease port with non-request cancellation; uncertain outcomes retain the same confirmation authority. Each Case-search result is one focusable link whose accessible name contains reference, registration, claimant and stage.

## Exact file inventory

- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` — prepare/final association orchestration, protected confirmation authority, direct Core replay path, and definitive-failure compensation.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml` — two-step link/unlink confirmations and one complete accessible Case-result anchor.
- `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml` — optional hidden fields used by the concrete link and unlink confirmation callers, keeping the lease token out of the URL.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — exact authenticated link/unlink replay, changed-input conflict, history cardinality, successful lease consumption, post-acquire failure release/immediate reacquisition, and accessible-name proof.
- `docs/capabilities.md` and `docs/current-architecture.md` — unchanged from the original MAIL-10 commit; the correction does not alter capability scope or as-built boundaries.

No Core, Infrastructure, EF model/store, schema, migration, Graph/Box/cloud/deployment/permission or generic action framework changed.

## Verification

- `dotnet restore ./Pegasus.slnx --locked-mode` — passed.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — passed, 0 warnings/errors.
- Exact replay journey — passed: exact link replay and exact unlink replay return success; changed reason under the link operation key conflicts; history counts remain one link and one unlink.
- Post-acquire stale-state compensation — passed: no association/history, lease token cleared, immediate reacquisition succeeds.
- Exact accessibility test — passed: one matching anchor contains the current Case reference, registration, claimant and stage.
- Full `MailWorkspaceWebTests` — 33/33 passed.
- `git diff --check` — passed (line-ending notices only).

## Simplification and qualification

The correction four-lens pass is recorded in the plan. The result remains a Web-only orchestration correction over existing ports and one existing shared partial. Evidence is disposable local SQL and the authenticated local Web pipeline. No external or live write was performed.

## Delivery references

- Original commit: `d4c951f5`
- Correction commit: `6b7c62a4`
- Pull request: #490 — https://github.com/collisionengineers/pegasus/pull/490
- Target/disposition: `dev`, open in Review for independent re-review; not self-reviewed or merged.
