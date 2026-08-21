# Plan — TICK-060: Provider API terminal Case/PO lookup

## Approach

After API-01 establishes receipt identity, add one Core-owned Principal-scoped terminal-result query backed by the existing staged work/evaluation/Case-link data. The endpoint exposes only generic nonterminal, terminal Case/PO, or bounded terminal failure; it does not copy the staff queue-state projection or create a result store.

## Governing docs

- **Meets and modifies `docs/frd/frd-09-provider-and-intermediary-routes.md`**: only the authenticated Principal's receipt/result is visible, cross-Principal lookup fails closed, and Case/PO is returned only from actual allocation authority. The operator-authorized contract removes transient Processing as a public state.
- Consume ADR-0030 and the API-01 receipt contract; no new ADR or state taxonomy is added.

## Steps

1. Integrate completed TICK-061 authentication and TICK-058 receipt identity/wire conventions.
2. Add a Core query/result type accepting authenticated Principal and staged receipt ID with exactly three outcomes: nonterminal, terminal Case/PO, or terminal bounded failure.
3. Implement one no-tracking EF projection joining the Principal-owned staged receipt, durable work/evaluation, processed receipt, and actual active Case link; unknown and foreign identifiers return the same absence.
4. Add `GET /api/provider/submissions/{receiptId}`: nonterminal → 202 plus `Retry-After: 2`; actual Case/PO → 200; terminal failure → stable problem response; absent/foreign → 404.
5. Ensure responses omit internal state names, attempt counts, exceptions, receipt contents, source downloads, and general Case fields.
6. Add Core/integration/contract/architecture tests covering pending, retry-scheduled, completed-with-link, completed-without-link, terminal failure, unknown, foreign Principal, revocation, immutable references, and disabled composition.
7. Refresh current architecture, run simplification lenses, locked restore, Release build, focused/full tests, and record the post-implementation report.

## Verification

SQL/Web tests seed each durable state and assert the external response without calling processing. Cross-Principal and unknown responses must be indistinguishable. Success is impossible until the actual Case link exists, even if a processing decision says `case_created`.

## Risks / open questions

- A completed receipt without a Case link remains nonterminal for this contract unless it has a terminal bounded failure; it must never invent a reference.
- Webhooks, listing/search, retention SLA, and live throttling are deferred until a real caller proves the need.
