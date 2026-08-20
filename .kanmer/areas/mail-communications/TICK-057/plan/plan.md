# Plan — UI-14

## Chosen approach

Extend the existing retained-mail read scope with one optional view: an aggregate operational destination or one exact known category whose canonical policy result is DetailedClassification. MailOperationalDestinationPolicy owns compact query criteria and uses the same criteria for its Map result; the EF adapter translates those criteria against existing classification columns before Count/Skip/Take. After paging, the landed decision projection supplies each row's current classification and policy-derived destination. Web reuses MailClassificationSelection for named options and the existing query-string context convention.

This is smaller and safer than persisting a destination, filtering a materialized page, copying the mapping into EF/Web, or adding a generic mail-filter framework.

## Governing docs

- docs/frd/frd-08-email-mailbox-and-background-processing.md: preserves classification/destination separation, canonical detailed views, reasoned Other, distinct Unidentified/Triage, SQL-honest counts and active filter context.
- docs/design/README.md: semantic accessible navigation, visible active state, honest empty state, accessible pagination and exact list/detail return.
- No ADR: existing Core read contract, EF adapter and Razor caller carry the change.

## Steps

1. Extend MailOperationalDestinationPolicy with the smallest immutable SQL-query criterion and prove the criterion agrees with Map for every destination.
2. Extend MailWorkspaceScope/ListRetainedMail with zero-or-one destination/detail filter and fail-closed validation; reuse MailCategory validation.
3. Apply the policy criterion or exact category against existing classification decision columns before SQL count/paging, then reuse the current classification mapper for row projection.
4. Add one accessible queue/detail filter surface to /Inbox and carry its key through mailbox/folder/search/page/manual refresh and /Inbox/{id} return/action context.
5. Add focused Core, disposable SQL and authenticated Web tests for Receiving work, Queries, reasoned Other, Unidentified, Triage, a named detailed view, current corrections, counts/paging and context preservation.
6. Reconcile only canonical UI-14/Unidentified wording in ticket/capabilities/design, run locked restore/Release build and proportional suites, then perform and record the four simplification lenses.
7. Write the exact PIR, push one branch, open a PR to dev and leave TICK-057 in Review for independent review.

## Risks and mitigation

- Mapping drift: Map and SQL criteria share one Core-owned descriptor; tests enumerate all destinations.
- False totals/pages: filter is composed before CountAsync, Skip and Take.
- Context loss: extend the existing explicit query-string convention and prove list→detail→return.
- Scope growth: no writes, new persistence, action framework, quick preview, MCP or external behavior.

## Proof

Local Core tests, populated disposable SQL tests and authenticated Web tests; locked Release build and diff check. No deployment or live mailbox claim.
