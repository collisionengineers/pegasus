# Open questions — SIMPLI-014

The following product decisions cannot be inferred from code or technical evidence and block full implementation/acceptance.

- [ ] **Assessment outcomes ([[TICK-204]]):** Should Pegasus support all four rendererref1 outcomes—Total loss, Repairable, Cash in lieu, and Contract repair—as the accepted RPT-02 set? Recommendation: **yes, accept all four**; the table and JSON schema both define them, and treat the “three outcomes”/three-value-dropdown text in `DESIGN_SPEC.md` as stale wording.
- [ ] **Unaccepted wording and people data ([[TICK-216]]):** Please provide/approve the Category N, A, B and N/A salvage wording, Recovery & Storage paragraph, final Statement of Truth, and qualifications for E Mawdsley and N O'Reilly—or explicitly choose which affected variants/assets remain unavailable. Recommendation: **provide and accept the final wording/qualifications before integration**; do not ship placeholders or claim a closed-gated variant as delivered.
- [ ] **Audit specification model ([[TICK-205]]):** For Audit reports, should Pegasus retain and render two immutable repair specifications (conservative and maximised) with uplift, as RPT-03 currently says, or one canonical repair specification? Recommendation: **retain two versioned Audit specifications plus computed uplift**, because that matches the allocated RPT-03 behaviour and avoids overwriting evidence.
- [ ] **Audit template ([[TICK-207]]):** What accepted layout/wording should the missing Audit report template use? Recommendation: **supply or approve a representative Audit report/template before RPT-03 is implemented**; do not derive legal/report wording from the assessment samples.
- [ ] **Template disposition ([[TICK-206]]):** May the integration initially activate only the accepted assessment/fee-note families evidenced by rendererref1 and defer or retire unsupported workspace catalogue entries (blank letterhead, roadworthy/criminal, Part 35, generic expert report, etc.)? Recommendation: **yes**; migrate only caller-backed accepted families and keep unsupported entries out until their owning capability and evidence exist.

## Parked (explicitly deferred)

Technical choices such as the exact existing Web/Worker execution host, Chromium lifecycle, density policy implementation, and MCP/CLI disposition are not operator product questions here. They remain with [[TICK-203]], [[TICK-211]], [[TICK-213]]–[[TICK-215]], [[SIMPLI-012]], and [[PLAT-007]], to be resolved from accepted architecture and runtime proof.
