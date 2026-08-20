# Post-implementation report — UI-14

## Outcome

The retained Inbox offers one labelled native selector for Receiving work, Queries, reasoned Other, Unidentified, distinct Triage, and named detailed classifications. Its selected option is the active-view presentation; no duplicate field hint or queue-specific read-only empty copy remains. The selected key is preserved across list/detail and existing actions. Core remains the single mapping owner, SQL filters the current classification before count/paging, and no derived destination is stored.

Every exact-message GET, reload, and POST now crosses one reused folder-plus-queue parser. All six POST handlers validate immediately after actor resolution and before exact-message reads, lease work, classification, association, or provider-move operations. Unknown keys and Deleted Items plus retained-classification queue fail closed without side effects.

## Exact branch file inventory

- `docs/capabilities.md` — reconciles MAIL-02/UI-14 local evidence and canonical Unidentified/Triage scope.
- `docs/design/README.md` — replaces one stale broad Needs-sorting sentence with the canonical distinct-state rule.
- `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` — keeps Map and aggregate read-query criteria under one policy owner.
- `src/Pegasus.Core/Intake/RetainedMail.cs` — adds the zero-or-one destination/detail scope, validation, and current row projection.
- `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` — applies current-classification predicates before Count/Skip/Take and reuses the decision mapper.
- `src/Pegasus.Web/Pages/Mail/Index.cshtml` — adds the labelled native selector, selected option, row classification/destination, and preserved links without duplicate hint/queue-empty copy.
- `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` — owns canonical aggregate/detail option parsing and passes the validated list scope; no redundant active-label helper remains.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml` — preserves queue through Back, sections, thread links, search, and existing reasoned actions.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` — validates one reused list context before every exact-message read/action and preserves redirects.
- `tests/Pegasus.Core.Tests/Intake/Classification/MailOperationalDestinationPolicyTests.cs` — proves Map/query agreement and Unidentified/detail boundaries.
- `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` — proves canonical mutually exclusive scope validation.
- `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` — proves every view, current correction precedence, SQL totals, and paging.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — proves compact selector semantics, context preservation, both forged contexts across all six POST handlers, exact no-effect state, and valid success/recovery behavior.

No FRD behavior change, ADR, taxonomy, schema, migration, store, new write operation, framework, MCP surface, Graph/Box/cloud/deployment/permission change, or external write was introduced.

## Verification

Initial UI-14 head:
- Focused Core policy/scope — 38/38 passed.
- Focused SQL/Web — 3/3 passed.
- Canonical non-corpus solution run — Core 862/862, Architecture 98/98, Integration 880/880 passed.

PR-053/054 correction head:
- Exact forged-context theory — 2/2, each exercising all six POST handlers.
- Focused compact/invalid/valid-success/valid-recovery selection — 7/7 passed.
- Full `MailWorkspaceWebTests` — 38/38 passed.
- `dotnet restore ./Pegasus.slnx --locked-mode` — passed.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — passed, 0 warnings/errors.
- Architecture tests — 98/98 passed.
- `git diff --check` — passed (line-ending notices only).
- Four-lens dispositions are recorded in the plan; no unapplied finding remains.

## Traceability

- Distinct operational/detail views → Core agreement, populated SQL, authenticated selector proof.
- Current correction and honest totals/pages → current-decision SQL correction test over seven rows/three pages.
- Compact accessible selector → one labelled native selector, one selected option, absent duplicate hint/queue-empty copy.
- List/detail/action preservation → exact links/hidden fields plus valid move success and uncertain recovery retain `queue=receiving-work`.
- Fail-closed POST boundary → unknown queue and Deleted+queue return 404 across prepare/final Link, prepare/final Unlink, correction, and move; classification/history/provider calls remain unchanged, prepare does not acquire leases, and final refusal does not consume prepared leases.
- Simplicity → one page-boundary parser composition, no stored destination or generic framework.

## Delivery references

- Base: origin/dev `4baae5f0`.
- Initial commit: `4b851ded`.
- PR-053/054 correction: `4a13def9`.
- Pull request: #491 — https://github.com/collisionengineers/pegasus/pull/491
- Target/disposition: dev, open in Review for independent re-review; not self-reviewed or merged.
- Evidence: local Core, disposable SQL, authenticated local Web, and fake provider only; no live mailbox or external write.
