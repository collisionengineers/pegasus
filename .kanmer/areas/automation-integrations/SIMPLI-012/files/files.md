## Files — SIMPLI-012 — decision record

No source files are changed by this ticket — it is a decision record, not an implementation. Referenced evidence:

| Path | Why |
|---|---|
| `docs/operations.md:230` | "Connector flow ... live since release 10" — corrects the stale "dormant" premise. |
| `infra/modules/platform.bicep:425` | `Features__AutomationMcp=true` in production. |
| `docs/capabilities.md` (MCP-01…MCP-07 rows) | Canonical scope text for each MCP capability referenced by the decision. |

Related tickets whose own scope the decision now activates: TICK-062 (MCP-05, already taken/implementing by another lane), TICK-104 (MCP-07, still gated by its own TICK-102 dependency and `Later` horizon).
