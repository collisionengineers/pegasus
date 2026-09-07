# B09 fresh review — exact head 0951e6525 (2026-09-07)

Reviewer: an independent agent that implemented none of the stream
(read-only; ticket docs via Kanmer, PR 672 body + all comments, diff
against the merge base 3284f93fc). Verdict: **REVIEW: PASS** — no
blocking finding (no false evidence claim, no fail-open behaviour, no
undisclosed business-rule duplication).

## Should fix before merge — dispositions

1. Report generate / fee-note / prepare-delivery / send handlers have no
   routed HTTP proof (Core/persistence only). **Fix**: routed journey test
   `CaseReportDeliveryWebTests` on the combined base (helper `b-work/b05t`).
2. Session expiry enforced only on the provider's return; resume and the
   estimator-URL read ignored `ExpiresAtUtc`. **Fix**: `ResumeAsync` settles
   an expired open session `Expired` instead of re-opening it (the claimed
   result lookup is read-only and not subject to it); `GetEstimatorUrlAsync`
   returns null past expiry; the page offers Resume only for an unexpired
   Active session or a held result; gateway test
   `ResumingAnExpiredSessionSettlesItInsteadOfReopeningTheProvider`.
3. `21b3e34f1` moved `CaseDataFieldNames` out of the A-owned
   `CaseDataEntities.cs` (register-driven, pure move) without disclosure on
   the thread. **Fix**: disclosed on PR 672 (5565531697), A's acknowledgement
   requested.
4. Simplification pass recorded only for B02, B03, B04, B06, B08 and the
   cursor slice; none for B01 (web port), B05, B07. **Fix**: read-only pass
   over those slices running; findings and dispositions to be recorded here.
5. PR body stale (head, open items) and checklist 0/11. **Fix**: checklist
   B01–B08 ticked; body rewritten at the final head.
6. `EfGlassRepairEstimateCaseAuthority` untested directly (km→miles, three
   refusals). **Fix**: persistence-level cases (helper `b-work/b05t`).
7. Pack B04 "compare" between estimate versions has no UI and no recorded
   deferral. **Deferred** to a follow-up ticket: v3 shows every version on
   its own tab with duplicate / discard / Use as Current; a side-by-side
   compare needs a design decision in `docs/design/README.md` first.

## Observations — dispositions

8. Case page Glass's launch/resume catching `InvalidOperationException` was
   never explicitly accepted by A. Asked explicitly (5565531697).
9. The 267/287 combined figures carry local C shims (disclosed); A requires
   a shim-free final proof with C suites — that is A's complete verification
   checkout, where 0951e6525 is merged (A 5565522606). The PR body's
   "225/228" is the helper-base figure; corrected in the rewrite.
10. Glass's composition is A's on the shared ref (known handoff).
11. Web computes the VAT override flag that Core re-validates — a candidate
    Core factory; not a second decision. Left.
12. `GlassSession.AwaitingImport` message is a how-it-works sentence. **Fix**:
    reworded to a state statement.
13. Conduct checks clean (guard delegation, typed conflicts, no logger in the
    Glass folder, fixtures on the estate, scripted provider only, no
    TODO/stub, migrations all A-authored).

## Review questions

- Q1 plan vs ticket: three pack items were delivered nowhere and recorded
  nowhere (#1 evidence, #2 expiry, #7 compare) — now fixed or deferred with
  a ticket; other deferrals were already reasoned.
- Q2 implementation vs plan: every record claim backed by code and a named
  test; gaps were evidence tiers (#1, #6), no stubs, nothing
  registered-but-unreachable within B's ownership.
- Q3 simplification pass: honest for its range; incomplete in range (#4).
