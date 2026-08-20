# Proof — PLAT-003

Type: visual + command-log. Released in **release 14** (`d91fd7d7…`, PR #458), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Live: the rail "Queues" badge shows **10**, agreeing exactly with the Not-ready(10)/Review(0)/Held(0) queue counts; Inbox/Cases/Operations rails render no invented count (absent, per the design authority).
- Verification lane at the cut: global `RailCountsPageFilter` (`IAsyncPageFilter`) sourcing `IDashboardQueries.GetCaseStageCountsAsync` on authenticated requests; `_Layout` renders a badge only for a present key; `RailCountsWebTests` present.
- Full transcript: DELIV-013 scratch.
