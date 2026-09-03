# Open questions — PLAT-069 (2026-09-02)

- [ ] Does "any query is not current" (D37 notice condition) mean only
  `ServiceHealthState.Partial` or `Failed` as the mockup implements, or every
  state other than `Current` (which would include `Running`, `Configured`
  and `ReviewRequired`)?
  Planned default (plan 2026-09-03): `Partial` or `Failed` only; the
  Core predicate ignores `ExternalWorkLimitReached`. A different answer
  changes one `ServiceHealthPolicy` method and its tests.
- [ ] When the existing Operations result-limit condition
  (`Operations.LimitReached`) and the D37 health condition both hold, should
  the page show two distinct label-only notices, or should the existing
  limit warning (whose hint sentence breaks the no-explanatory-copy rule) be
  removed or folded into the one notice?
  Planned default (plan 2026-09-03): two separate notices; the limit
  notice keeps its own predicate and is reduced to the `Partial data` label
  only. Alternative: leave its sentence byte-for-byte as an accepted
  existing violation.
- [ ] Sequencing against PLAT-051: `Pages/Administration/ServiceHealth`
  does not exist on origin/dev. May PLAT-069 merge first (table removed,
  notice shown without a link until the page lands), or must it wait for
  PLAT-051 so the link is live in the same PR ("no dead link")?
  Planned default (plan 2026-09-03): wait for PLAT-051; an `asp-page` to an
  absent page renders `href=""` in this app (UIIMP-008 precedent), which is
  the dead link the ticket forbids.
