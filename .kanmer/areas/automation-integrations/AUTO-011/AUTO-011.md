---
id: AUTO-011
type: ticket
title: AI job ledger and automation.jobs connector tools
status: review
area: automation-integrations
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-28T08:36:05.746Z'
  review: '2026-08-28T08:55:41.485Z'
taken_at: '2026-08-28T08:40:07.967Z'
branch: task/auto-011-ai-job-ledger
worktree: ../pegasus-worktrees/auto-011-ai-job-ledger
labels:
  - backend
  - wave-3
  - ai
  - mcp
groups:
  - EPIC-011
  - EPIC-005
links:
  - TICK-074
  - AUTO-009
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
prs:
  - '590'
archived: false
created: '2026-08-28T08:35:47.092Z'
updated: '2026-08-28T08:55:41.485Z'
---

## What

Wave 3 of [[EPIC-011]]; implements ADR-0035 (`docs/adr/0035-ai-job-ledger.md`, merged to dev by [[AUTO-009]]) / FRD-11 § AI Job List / FRD-10 tool inventory. `Core/AiWork/AiJobs.cs`: `AiJobKind {Estimate, UnidentifiedResolution, QueryResponse, UnidentifiedQueuePass}`, `AiJobState {Queued, Taken, DraftReady, Completed, Failed, Cancelled, Expired}`, `AiJobRecord`, commands create (staff `PerformCasework`; automation for the queue pass), take/release/progress/complete/fail (automation; 30-minute lease, expired lease reads as Queued), cancel (staff); `IAiJobStore`, `IAiJobQueries` (open, by subject, recent, counts). Reuse the AI-09 patterns (operation key, version, `ISendToAiControl` kill switch, `AutomationActorResolver`). Table `AiJobs` + migration + web grant; ActionHistory `ai_job_*` events. MCP `Web/Mcp/AiJobMcpTools.cs`: `pegasus_ai_job_list/create/take/progress/complete/fail/release` under new scope `automation.jobs`; consent descriptions for `automation.jobs` and the missing `automation.mail`; fix the stale "generate EVA bundles" text on `automation.assessment`. Result pointers per kind as FRD-11 states.

## Owns

`src/Pegasus.Core/AiWork/**`, `src/Pegasus.Infrastructure/Persistence/EfAiJob*.cs` + migration, `src/Pegasus.Web/Mcp/AiJobMcpTools.cs`, `src/Pegasus.Web/Mcp/AutomationMcp.cs`, `AutomationMcpExtensions.cs` (registration), `src/Pegasus.Web/Pages/Connect/Authorize.cshtml.cs`, tests `Core.Tests/AiWork/**`, `IntegrationTests/AutomationMcp*Tests.cs`.

## Verification

- [ ] Grant census passes; kill switch refuses takes; lease expiry returns jobs to Queued.
- [ ] Tools exercised through the MCP ingress test.
