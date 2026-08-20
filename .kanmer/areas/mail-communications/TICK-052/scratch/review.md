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
