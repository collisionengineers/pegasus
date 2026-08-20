## Independent review — 2026-08-20 — exact head `d4c951f5d7b687f62923099ff6cd322b63906aeb`

### Changes

- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` adds authenticated retained-message Case search/target loading and separate link/unlink POST orchestration over the existing `IUploadCaseDecision.SearchAsync`, `IGetCase`, `IGetIntake`, `IAcquireCaseEditLease`, `ILinkIntake`, and `IReverseIntakeLink` ports.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml` renders current association, bounded suggestions, canonical target detail, and separate shared reason dialogs. It exposes no active-to-active swap.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` adds the real authenticated link → unlink → fresh replacement-link journey plus roleless/stale-receipt no-write coverage.
- `docs/capabilities.md` and `docs/current-architecture.md` record the local caller and its evidence tier.
- No Core, Infrastructure, EF schema/migration, permission, provider, generic framework, or external-write file changed.

### Comments and disposition

1. **Blocking — exact Mail submissions are not replay-safe.** Both handlers validate the freshly loaded receipt/Case state and acquire a lease before entering the Core history replay check. Repeating the exact successful link or unlink POST after a lost response is rejected as a generic stale/current-state conflict, so the caller defeats the replay protection its plan/capability note claims. Disposition: filed [[PR-048]], blocking [[TICK-052]].
2. **Blocking — a failed association after lease acquisition can strand the Case lease.** A race or eligibility/store failure after `IAcquireCaseEditLease` is caught and rendered recoverable, but the five-minute lease is neither released nor otherwise recovered. The rerender generates a new operation key, so an immediate retry can conflict until expiry. Disposition: filed [[PR-049]], blocking [[TICK-052]].
3. **Blocking — the new Case-search result does not meet the accepted accessible-result shape.** Only `Review <reference>` is inside the focusable link; visible registration, claimant, and stage do not contribute to the link's accessible name, contrary to `docs/design/README.md`'s search-result contract. Disposition: filed [[PR-050]], blocking [[TICK-052]].
4. **Non-blocking/pass — scope and simplicity.** The diff is a thin Web caller, reuses the canonical bounded suggestions and existing Core/lease/link/reverse owners, stages correction as unlink then a separate search/review/reason/link, and adds no active-to-active swap, second policy/store/schema, or generic action abstraction. Disposition: accepted.
5. **Non-blocking/pass — report and governing-doc inventory.** The PIR names all five changed files honestly, the plan's FRD-08/design constraints match the intended diff, and the capability/current-architecture wording stays at local SQL/Web evidence without external-write or deployment claims. Disposition: accepted.

### Verdict

**Needs changes; do not merge.** Exact head and file inventory were checked, open questions are resolved, and the four-lens simplification record is proportionate. PR #490 remains blocked by [[PR-048]], [[PR-049]], and [[PR-050]]. CI was still running when the blocking verdict was recorded; green CI would not resolve these behavioral/accessibility findings. Re-review the replacement head after the blockers land.

### CI state at handoff

Repository run `32416440980`: changes, documentation, local-development-scripts, reference-data, and unit passed; infrastructure was correctly skipped; browser and SQL integration shards 1–3 were still running. Review handoff was finalized without waiting because the blocking verdict is independent of CI and the blocker implementation has begun.

## Independent re-review — 2026-08-20 — exact head `6b7c62a4c87096e52a183f4d6e73aca2a4495c0f`

### Changes since the first review

- Split link and unlink into explicit prepare/final POSTs so the Case lease token and one association operation key survive into the final shared reason dialog.
- Final association POSTs now delegate directly to the existing Core commands, allowing their operation-fingerprint replay check to run before current-state rejection.
- Added definitive-failure release compensation through the existing `IReleaseCaseEditLease`; non-definitive outcomes retain the same confirmation authority.
- Extended the existing shared reason dialog with hidden fields for the concrete link and unlink callers.
- Moved every visible Case-result identity fact inside the one selection link.
- Added SQL/Web coverage for exact link/unlink replay, same-key changed-reason conflict, successful lease consumption, stale-state release/reacquisition, and accessible result identity.

### Blocker dispositions

1. [[PR-048]] **fixed-in-PR.** Exact final link and unlink POST replays reach the canonical Core fingerprint/history owner and succeed without extra history; changed reason under the same operation key conflicts.
2. [[PR-049]] **partially fixed, superseded by a narrower remaining blocker.** Successful definitive-failure compensation releases the lease and permits immediate reacquisition. However a recoverable failure of the release call is swallowed and the only retained lease token is unconditionally cleared, recreating the five-minute stranded lease with no compensation retry. Filed [[PR-052]].
3. [[PR-050]] **fixed-in-PR.** Each result now has exactly one focusable link and its accessible name includes reference, registration, claimant and stage.
4. **New blocking — prepared authority is not exact-message/action bound.** `AssociationLeaseState` omits message/receipt identity and Link-versus-Unlink intent. The retained protected state can be carried to another message targeting the same Case, or repurposed across actions, and the final handler resolves/mutates that other receipt using the valid Case lease and new operation fingerprint. Filed [[PR-051]].
5. **Pass — scope/simplicity/PIR.** The correction changes only Web orchestration, the existing shared partial and focused tests. No Core, Infrastructure, EF/schema/migration, permission, external write, second policy/store, generic action framework or active-to-active swap was added. The correction plan and PIR accurately name the six-file total PR inventory and the repeated four-lens pass is proportionate.

### Verdict

**Needs changes; do not merge.** [[PR-048]] and [[PR-050]] are resolved at this head. [[PR-049]]'s normal compensation path is resolved but its release-failure edge is now [[PR-052]]. [[PR-051]] must bind preparation authority to the exact message/receipt and exact action. Replacement CI run `32419589093` was still running when this behaviorally blocking verdict was recorded; green CI cannot resolve these findings.

## Final independent re-review — 2026-08-20 — PASS

Reviewed PR #490 at exact head `563bb2ecfbbcb5070080d6d9c5b08791abe08b2b` against the complete TICK-052 and group documents, plan, checklist, PIR, full six-file diff, and simplicity rails.

### Changes and dispositions

- PR-048 — fixed in PR: prepared confirmations are replay-safe and fingerprint-conflicting changes are refused; exact successful replays do not duplicate history.
- PR-049 — fixed in PR: definitive association failures compensate the existing lease without changing uncertain-outcome retry behaviour.
- PR-050 — fixed in PR: an exactly-one search result is one accessible link whose name includes the case identity.
- PR-051 — fixed in PR: protected authority is bound to the exact message, server-derived receipt, Link/Unlink action, case, versions, lease token, and operation key. Cross-message and cross-action substitutions fail before Core mutation and compensate the prepared lease.
- PR-052 — fixed in PR: transient release failure preserves the protected exact confirmation for retry; roleless requests are forbidden before consuming it; confirmed or definitive compensation clears it.
- Existing stale-version, role/auth, replay, and history behaviour remains covered.
- The Mail page remains a thin caller over canonical bounded UploadCaseDecision suggestions and existing IGetCase/IGetIntake, lease, LinkIntake, and ReverseIntakeLink ports. The workflow is exact unassociated link, reasoned unlink, then separate search/summarize/confirm/reason replacement link; no active-to-active swap was introduced.
- Scope is exact: Razor/page-model/tests plus capability and current-architecture updates. No Core, EF, schema, migration, permission-provider, or generic framework growth.
- PIR matches the six-file diff. The dated four-lens simplification pass is proportionate and records no unapplied findings.

CI run `32421520046` passed in full at this SHA: changes, documentation, local-development-scripts, reference-data, unit, browser, all three SQL integration shards, and SQL integration coverage. Infrastructure was correctly skipped by path classification.

Verdict: PASS; no blocking findings remain.
