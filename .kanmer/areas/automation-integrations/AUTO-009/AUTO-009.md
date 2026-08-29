---
id: AUTO-009
type: ticket
title: >-
  FRD-10/FRD-11 and ADR-0035: AI job ledger, automation.jobs scope and
  per-estimate VAT
status: done
area: automation-integrations
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-28T08:08:19.245Z'
  review: '2026-08-28T08:15:18.708Z'
  verifying: '2026-08-28T08:19:28.265Z'
  done: '2026-08-29T10:14:01.579Z'
taken_at: '2026-08-28T08:13:06.610Z'
branch: task/auto-009-ai-job-docs
worktree: ../pegasus-worktrees/auto-009-ai-job-docs
labels:
  - docs
  - ai
  - mcp
  - adr
groups:
  - EPIC-011
links:
  - TICK-074
  - AUTO-006
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
commits:
  - 879fbba8
prs:
  - '585'
archived: false
created: '2026-08-28T08:05:30.128Z'
updated: '2026-08-29T10:14:01.579Z'
---

## What

- New `docs/adr/0035-ai-job-ledger.md` (one decision): a durable `AiJobs` ledger, pull-based, leased by a named connector client, distinct from the worker-dispatched `ExternalWorkItems` outbox and from the AI-09 `AiWorkRequests` pointer hand-off; supersedes the boundaries.md exclusion of a shared AI ledger. Add to `docs/adr/README.md`.
- FRD-11: the AI Job List is the AI-10 catalogue — kinds Estimate, Unidentified resolution, Query response, Unidentified-queue pass (scheduled passes are created by external crons through the Automation Actor, never a Pegasus timer — D5); states Queued / Taken / Draft ready / Completed / Failed / Cancelled / Expired; started-by; result shapes; staff review actions. Report contract: the Current estimate's VAT % overrides the built-in rule (D9).
- FRD-10: tool inventory gains `pegasus_ai_job_list/create/take/progress/complete/fail/release` under new scope `automation.jobs` with a consent description; kill-switch behaviour; note the missing `automation.mail` consent line.

## Owns

`docs/frd/frd-10-mcp-automation-and-actor-boundary.md`, `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`, `docs/adr/0035-ai-job-ledger.md`, `docs/adr/README.md`.

## Verification

- [ ] ADR has frontmatter per AGENTS.md and one decision.
- [ ] `scripts/Test-DocumentationLinks.ps1` passes.
