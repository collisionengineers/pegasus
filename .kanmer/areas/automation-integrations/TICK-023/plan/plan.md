## Plan — TICK-023 (MCP-01) — retrospective backfill

Reconcile the board with the already-shipped, already-deployed MCP ingress. Steps taken: confirmed all ingress files present at production SHA `2325ed4a`, ran the ingress + connector-authorization integration suites, ran a live read-only probe against the production endpoint (302/400/400, no 404s), and cross-checked `docs/capabilities.md`'s own MCP-01 text for the accepted residuals. No governing document modified; FRD-10 already owns this behaviour.
