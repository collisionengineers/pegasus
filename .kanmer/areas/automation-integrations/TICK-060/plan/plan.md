# Plan — TICK-060: Return the resulting Case/PO or fail

## Approach

After API-01 establishes the Principal-owned submission receipt, add one Core-owned result query over the existing staged work, evaluation, receipt, and actual Case link. The query is not a general lookup: it accepts only the authenticated Principal and its own submission receipt. It returns generic nonterminal while work is unfinished, succeeds only with an actual linked Case/PO, and fails terminally when completed processing did not create or link a Case. It creates no result store and returns no files or Case detail.

## Governing docs

- **Modifies `docs/frd/frd-09-provider-and-intermediary-routes.md`**: replace receipt/status/result wording with the settled rule that the provider may retrieve only its own submission's linked Case/PO; completed-without-Case is failure; files/report delivery remain separate and excluded.
- Meet the accepted Principal security boundary in ADR-0004. Do not reserve a new ADR number or supersede an unrelated ADR decision for this behavioral change.

## Steps

1. Integrate the implemented API-01 receipt identity and API-04 Principal authentication contracts after their real callers exist.
2. Add one Core query/result type accepting authenticated Principal plus its submission receipt with three outcomes: unfinished, linked Case/PO success, or terminal failure.
3. Implement one no-tracking EF projection joining Principal ownership, staged work/evaluation, processed receipt, and actual active Case link; completed work without a Case link maps to terminal failure.
4. Add the provider result endpoint using the separately settled wire contract: generic nonterminal while unfinished; success containing only immutable Case/PO when linked; bounded terminal failure when completed without a Case; indistinguishable absence for unknown/random/foreign identifiers.
5. Ensure responses omit files, reports, source material, general Case fields, internal state names, attempt counts, and exception details.
6. Add Core/integration/contract/architecture tests for unfinished work, completed-with-link success, completed-without-link failure, technical failure, unknown/random/foreign identifiers, revocation, immutable references, and disabled composition.
7. Refresh current architecture, run the simplification lenses, locked restore, Release build, focused/full tests, and record the post-implementation report.

## Verification

SQL/Web tests seed each durable outcome and assert that only an actual active Case link can produce success. Completed processing without a Case link is a terminal failure. Random and cross-Principal receipt identifiers are indistinguishable from unknown. Response assertions prove that no file, report, source, Case-detail, search, or outbound-delivery surface exists.

## Risks / open questions

- Exact transport mappings remain unresolved in the canonical provider wire-contract decision and must be settled before implementation.
- A processing decision that says `case_created` is insufficient without the actual Case link.
- Webhooks, listing/search, retention SLA, and live throttling remain deferred.
