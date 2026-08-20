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

## Simplification pass — 2026-08-20

- **Reuse:** Reused `MailOperationalDestinationPolicy`, `MailClassificationSelection`, `MailWorkspaceScope`, `EfIntakeReceiptStore.MapMailClassificationDecision`, retained-mail SQL paging, and the existing explicit query-string context convention. No second taxonomy, mapping table, projection store, or UI action convention was added.
- **Simplification:** Removed a fabricated `MailClassificationResult` used only to render a detailed-view label by adding the natural `DecisionLabel(MailCategory)` overload to the existing label helper. Removed a second enum scan from queue parsing by carrying the destination on the existing view option. Validated mutually exclusive scope choices before destination lookup. Applied all findings.
- **Efficiency:** The classification predicate is composed before `CountAsync`, `Skip`, and `Take`; current classifications are loaded for only the page in the existing batched receipt query. No materialize-then-filter path or per-row query was introduced. Populated SQL evidence proves exact totals and three pages, including a corrected current decision.
- **Altitude:** Kept one narrow read-scope extension and one selector. No schema, migration, store, write operation, generic filter/action framework, quick preview, MCP surface, Graph call, or deployment change. The small Core criterion record is justified by the existing Core→Infrastructure boundary and keeps the business mapping single-owned.

No unapplied simplification finding remains.
