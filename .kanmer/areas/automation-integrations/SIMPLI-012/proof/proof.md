## Proof — SIMPLI-012

Retrospective decision record, closed 2026-08-20.

- Operator direction quoted verbatim in the ticket body's "Decision (2026-08-20)" section: *"consider the 'qdos alpha' restriction relaxed in terms of scope… All MCP related tickets are within your scope now"*.
- Stale-premise correction evidenced: `docs/operations.md:230` ("live since release 10"), `infra/modules/platform.bicep:425` (`Features__AutomationMcp=true`), live probe 2026-08-20 (`/mcp` 302, `/connect/token` 400, `/authorize` 400 — none 404).
- Resulting scope recorded and cross-checked against live board state: TICK-062 (MCP-05) confirmed `implementing`/taken by another lane at the time this was written; TICK-025 (MCP-03) confirmed closed `done` in this run; TICK-104 (MCP-07) confirmed still `backlog`, gated by TICK-102, unaffected by this decision beyond being in-scope.

This is a decision-record proof — no test suite or deployment applies to this ticket's own contract.
