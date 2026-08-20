# Post-implementation report — MAIL-10

## Outcome

Implemented the local MAIL-10 slice on the exact retained-message page. An authorized staff user can search existing cases through the landed shared upload Case-search helper, review one canonical target summary, then give a reason and explicitly confirm a link. An existing association exposes only a separately reasoned unlink for its exact current Case. A correction is therefore an honest unlink followed by a fresh search, target review, reason and replacement link; there is no direct swap.

Every POST reloads message→receipt and the Case on the server, compares the reviewed receipt/Case versions, rejects a missing/current/different/stale association, acquires the existing edit lease, and delegates to the existing Core link/reverse use case and serializable EF transaction.

## Files changed

- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` — thin search/review/link/unlink orchestration and fail-closed freshness checks.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml` — current association, bounded search results, canonical target summary, and shared reason dialogs.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — exact authenticated Web journey plus roleless/stale no-write proof.
- `docs/capabilities.md` — local MAIL-10 implementation/evidence tier.
- `docs/current-architecture.md` — as-built exact-message association caller.

No Core, Infrastructure, schema, migration, permission, Graph/Box adapter, generic framework, or deployment file changed.

## Verification

- `dotnet restore ./Pegasus.slnx --locked-mode` — passed.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — passed, 0 warnings/errors.
- Exact new Mail tests — 2/2 passed.
- Full `MailWorkspaceWebTests` — 32/32 passed.
- `CaseAcceptanceReplayTests|CaseMatchIntegrationTests` — 11/11 passed.
- Full Core tests — 860/860 passed.
- Full Architecture tests — 98/98 passed.
- `git diff --check` — passed (line-ending notices only).

The exact Web journey uses an unclassified retained message, proving classification is not a link gate. It links, unlinks, then separately searches and links a replacement; the final local SQL state has one active reused association row and three immutable mutation-history records. A second test proves a roleless POST is forbidden and stale reviewed receipt state returns no-write failure.

## Simplification

The four-lens pass is recorded in the plan. Its material correction was to reuse the landed `UploadCaseDecision.SearchAsync` helper after the execution worktree proved the earlier root-checkout symbol lookup stale. The final diff adds no business-policy owner or generic action abstraction.

## Residual risk and qualification

Evidence is disposable local SQL and the real local Web pipeline. No live mailbox, Graph, Box, Azure/cloud, permission, deployment, production database write, or operator live acceptance was performed or authorized. Production verification remains separately exact-target approval-gated.

## Delivery references

- Commit: `d4c951f5`
- Pull request: #490 — https://github.com/collisionengineers/pegasus/pull/490
- Target: `dev`
- Disposition: open for independent review; not self-reviewed or merged.
