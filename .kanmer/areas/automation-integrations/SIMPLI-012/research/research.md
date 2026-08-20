## Research — SIMPLI-012 — decision record, 2026-08-20

**Question:** What is the post-alpha disposition of AI/MCP work — resume with an activation plan, or remove?

### Operator direction (2026-08-20)
The operator gave explicit direction settling this decision: *"consider the 'qdos alpha' restriction relaxed in terms of scope… All MCP related tickets are within your scope now"* (operator, 2026-08-20).

### Premise correction
This ticket's own body (written 2026-08-17) frames AI/MCP as "dormant... surfaces" — that premise is stale. Verified 2026-08-20:
- `docs/operations.md:230` — "Connector flow (ADR-0027, live since release 10)".
- `infra/modules/platform.bicep:425` — `Features__AutomationMcp=true` in the production Web container app.
- Live read-only probe (2026-08-20): `/mcp` → 302, `/connect/token` → 400, `/authorize` → 400 against the production endpoint — none 404, i.e. live and responding.
- MCP-01/02/03/04/06 are all implemented and now recorded `done` on the board (this run's PROOFS lane closed TICK-023/024/025/015/022/027 evidence audits; TICK-026/MCP-04 was already done).

MCP has not been dormant since release 10 (2026-08-18); the restriction this ticket was written to react to had already lapsed in production before the operator's 2026-08-20 direction, which now makes the resume decision explicit rather than inferred.

### Decision
**RESUME / ACTIVATE — not remove.** All MCP-related tickets are in active scope per the operator's direction. This ticket does not itself implement or activate scope beyond what already exists; it records the decision and the scope it makes active.

### Resulting scope (as of 2026-08-20)
- **MCP-05** (TICK-062, "broader classified-email workspace actions") — status: `implementing`, taken by another agent (`claude-code`, branch `task/tick-062-mcp-05-mail-workspace`) concurrently with this research being written. Confirms the decision is already being acted on.
- **MCP-03 completion** — this run's audit found MCP-03 (TICK-025) already fully implements its own committed `docs/capabilities.md` scope (queue list + durable intake submission); closed `done` in this run.
- **MCP-07** (TICK-104, Administration-configurable Send to AI channel connector) — still `Later / 1.3.0` per `docs/capabilities.md`, blocked by TICK-102; in scope per the operator's direction but not activated by this decision record alone (its own blocking dependency and allocation horizon still govern its own move).

### Open questions
None — the operator's direction is explicit and dated.
