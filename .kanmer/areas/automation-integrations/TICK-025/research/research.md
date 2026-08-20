## Research — TICK-025 (MCP-03) — retrospective backfill

**Question:** Does `dev` need implementation for Automation Actor intake-queue actions through the same Core use cases as the QDOS-alpha staff app?

**Findings (verified 2026-08-20):**
- `src/Pegasus.Web/Mcp/IntakeMcpTools.cs` exposes `pegasus_intake_queue_list`, `pegasus_intake_submit`.
- `src/Pegasus.Web/Mcp/UnidentifiedMcpTools.cs` exposes `pegasus_unidentified_list/get/resolve`.
- Present at production SHA `2325ed4a`.
- **`docs/capabilities.md` MCP-03 row is the governing scope statement and it is narrow by design:** "Implemented (queue list, durable intake submission on the automation channel) behind the shared composition gate, enabled in production since release 9; non-blocking for `0.1.0-alpha.1` acceptance." The row does not commit to attach/create-case/reclassify actions — those are not part of MCP-03's accepted scope. (`capability-survey.md`'s "PARTIAL — no attach/create-case/reclassify" framing describes a broader intake-action surface than MCP-03's own capabilities-table commitment; the capabilities table, not the survey, is canonical per `docs/index.md`.)
- Tests: `AutomationMcpIngressTests` (6/6, 2026-08-20) exercises the tool inventory including `pegasus_intake_queue_list`/`pegasus_intake_submit`.

**Implications:** Judged against MCP-03's own committed scope (queue list + durable intake submission), the capability is fully implemented and deployed with no gap. Attach/create-case/reclassify are future intake-action scope, not part of this ticket's contract, and are not blocking.

**Open questions:** none.
