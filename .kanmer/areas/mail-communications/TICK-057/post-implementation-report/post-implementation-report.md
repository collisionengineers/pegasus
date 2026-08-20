# Post-implementation report — UI-14

## Outcome

The retained Inbox now offers one accessible selector for Receiving work, Queries, reasoned Other, Unidentified, distinct Triage, and every named detailed classification that is not an aggregate destination. The selected view is preserved across mailbox/folder/search/page/refresh and exact-message detail/actions. Core remains the single mapping owner; Infrastructure translates its narrow query criterion against the existing current classification decision before SQL count and pagination, then derives row destinations on read. Unknown or legacy view keys, contradictory scope, and retained-classification views over Deleted Items fail closed.

## Exact branch file inventory

- docs/capabilities.md — reconciles MAIL-02/UI-14 local evidence and canonical Unidentified/Triage scope.
- docs/design/README.md — replaces the one stale broad Needs-sorting sentence with the canonical distinct-state rule.
- src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs — keeps Map and the compact aggregate read-query criterion under one policy owner.
- src/Pegasus.Core/Intake/RetainedMail.cs — adds the optional zero-or-one destination/detail scope, validation, and current classification/destination row projection.
- src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs — applies current-classification predicates before Count/Skip/Take and reuses the landed decision mapper for page rows.
- src/Pegasus.Web/Pages/Mail/Index.cshtml — adds the native labelled selector, visible active/empty state, row classification/destination, and retained context links.
- src/Pegasus.Web/Pages/Mail/Index.cshtml.cs — owns canonical aggregate/detail option parsing and passes the validated scope to the existing list caller.
- src/Pegasus.Web/Pages/Mail/Message.cshtml — preserves the queue key through Back, sections, thread links, search, and existing reasoned actions.
- src/Pegasus.Web/Pages/Mail/Message.cshtml.cs — validates detail/action queue context, detects a message outside the originating view, and preserves redirects.
- tests/Pegasus.Core.Tests/Intake/Classification/MailOperationalDestinationPolicyTests.cs — proves Map/query agreement across the settled taxonomy and Unidentified/detail boundaries.
- tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs — proves zero-or-one canonical scope and pre-port fail-closed validation.
- tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs — proves every operational/detail view, current correction precedence, SQL totals, and paging.
- tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs — proves the authenticated accessible selector, distinct rows, selected state, list/detail context, and invalid/deleted refusal.

No FRD behavior change, ADR, taxonomy, schema, migration, new store, write operation, action/filter framework, MCP surface, Graph/Box/cloud/deployment/permission change, or external write was introduced.

## Verification

- Focused Core policy/scope selection — 38/38 passed.
- Focused populated SQL and authenticated Web UI-14 proofs — 3/3 passed.
- `dotnet restore ./Pegasus.slnx --locked-mode` — passed.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — passed, 0 warnings/errors.
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` — Core 862/862, Architecture 98/98, Integration 880/880 passed.
- Final selected-option authenticated Web assertion after the suite — 1/1 passed.
- `git diff --check` — passed (line-ending notices only).
- Four-lens findings and all applied dispositions are recorded in the plan.

## Traceability

- Receiving work, Queries, Other, Unidentified, Triage and named detail → Core taxonomy agreement, populated SQL view proof, authenticated Web view proof.
- Current classification/correction only → correction SQL proof and list projection through `MapMailClassificationDecision`.
- Honest totals/pages → seven matching rows over three SQL pages; unrelated row excluded.
- Accessible list/detail preservation → labelled native selector, one selected option, exact message link, Back/hidden/action context, invalid/deleted 404.
- Simplicity → one read-scope extension, one selector, no stored derived destination or generic framework.

## Delivery references

- Base: origin/dev `4baae5f0`.
- Commit: `4b851ded`.
- Branch: `task/tick-057-ui-14-mail-queues`.
- Target/disposition: dev, to remain in Review for independent review; not self-reviewed or merged.
- Evidence: local Core, disposable SQL, and authenticated local Web only; no live mailbox or external write.

- Pull request: #491 — https://github.com/collisionengineers/pegasus/pull/491
