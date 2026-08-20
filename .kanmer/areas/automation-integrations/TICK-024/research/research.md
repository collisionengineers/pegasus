## Research — TICK-024 (MCP-02) — retrospective backfill

**Question:** Does `dev` need implementation for Automation Actor Case actions (search/get/edit-lease) through the same Core use cases as the staff app?

**Findings (verified 2026-08-20):**
- `src/Pegasus.Web/Mcp/CaseMcpTools.cs` exposes `pegasus_case_search`, `pegasus_case_get`, `pegasus_case_edit_begin`, `pegasus_case_edit_renew`, `pegasus_case_edit_end` — same tool inventory listed in `AutomationMcpIngressTests.ExpectedTools`.
- Present at production SHA `2325ed4a` (`git cat-file -e`).
- Tests: `AutomationMcpIngressTests` (6/6 pass, 2026-08-20) exercises the tool inventory and gate-off behaviour; case actions share the composed Core use cases used by the staff UI (no duplicate business logic — same `Pegasus.Core` ports).
- `docs/capabilities.md` MCP-02 row: "Implemented (search, get, edit-lease begin/renew/end) behind the shared composition gate, enabled in production since release 9; non-blocking for `0.1.0-alpha.1` acceptance." No named residual beyond the shared composition-gate boundary already covered by MCP-01.

**Implications:** MCP-02 is fully implemented and deployed per its own capability text, with no distinct residual of its own.

**Open questions:** none.
