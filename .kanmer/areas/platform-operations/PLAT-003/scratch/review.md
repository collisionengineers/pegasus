## Independent review — PR #458 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green. A global `IAsyncPageFilter` supplies `ViewData["RailCounts"]` per authenticated request from the same `GetCaseStageCountsAsync` the dashboard uses — no second count owner, cheap single query. The scope decision is honest and design-authority-grounded: only Queues gets a badge because the rail rule forbids the shell inventing figures a page never queried; Inbox/Cases correctly stay badge-less. Rail count benefits from INTK-013's corrected NotReady figure automatically.
