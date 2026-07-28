# Observability, Runbook, and Admin Dashboard MCP

Generated: 2026-05-22

**Type:** Monitoring tool + MCP
**Priority:** Medium

## Objective

Make the automation centre observable: show intake volume, extraction failures, review backlog, EVA failures, stuck cases, unmatched images, and top error causes.

## Why it matters for Collision Engineers

The context packs define dashboard sections and runbooks. As soon as Graph/Box/EVA automation is live, staff need to know what is stuck and why.

## Proposed shape

A monitoring service collects events and metrics. A dashboard presents status, and an MCP exposes safe read-only operational queries to internal agents.

## Candidate tools / MCP methods / skill actions

- `get_pipeline_health()`
- `list_stuck_work_items(state, age)`
- `get_top_failure_reasons(period)`
- `get_eva_failure_summary(period)`
- `get_unmatched_image_queue()`
- `generate_daily_ops_report()`

## Inputs

- Event logs
- work item states
- API errors
- retry counts
- review queue data

## Outputs

- Health summary
- alerts
- daily report
- runbook links
- failure queues

## Guardrails

- Read-only for agents by default.
- No PII in broad dashboards unless needed.
- Alerts should route to named owners.
- Keep correlation IDs.

## MVP implementation path

1. Implement JSON event logs with work_item_id.
2. Add dashboard cards.
3. Add daily report.
4. Add runbook links per failure type.

## Test / acceptance criteria

- Stuck work item alert fires.
- EVA auth failure visible.
- Daily report matches event counts.
- Runbook link appears.

## Risks and open questions

- Instrumentation gaps.
- Alert fatigue.
- Need support ownership.

## Project source basis

- context_pack_2/16_MONITORING_OBSERVABILITY_AND_RUNBOOKS.md

## External reference basis

- Model Context Protocol documentation: https://modelcontextprotocol.io/docs/getting-started/intro
- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
