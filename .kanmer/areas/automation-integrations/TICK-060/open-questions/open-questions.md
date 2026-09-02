# Open questions — TICK-060

- [x] Scope lookup to the authenticated Principal's own API-01 submission.
      — Operator clarification, 2026-08-21.
- [x] Require an actual active Case link for success.
      — Operator clarification and product invariant.
- [x] Treat completed-without-Case as terminal failure.
      — Operator clarification.
- [x] Make random and cross-Principal identifiers indistinguishable absence.
      — Fail-closed isolation.
- [x] Return identifiers only.
      — Operator clarification and documented contract separation.
- [x] Expose no transient processing state.
      — Operator decision, 2026-08-21.
- [x] Use the existing GET route; return empty 202 while unfinished, 200 with
      only `caseReference` on success, generic 422 on terminal failure, and
      404 for unknown/foreign identifiers.
      — Operator decision, 2026-09-02.

## Parked (explicitly deferred)

- [ ] Push/webhook completion is deferred until a real second caller proves it
      necessary.
- [ ] Files, reports, source material, outbound delivery, list/search, and
      general Case detail remain separate capabilities.
