---
id: AUTO-016
type: ticket
title: Import a raw estimate artifact through MCP into the canonical Case importer
status: backlog
area: automation-integrations
order: 40
assignee: ''
profile: feature
labels:
  - mcp
  - automation
  - estimates
  - work-pack
  - wave-B
groups:
  - EPIC-011
links:
  - ENG-002
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
deployment: not-deployed
archived: false
created: '2026-09-01T21:54:35.679Z'
updated: '2026-09-03T15:15:26.944Z'
---

## What

Add an `automation.assessment` MCP tool that accepts `case_id`, `expected_version`, `edit_lease_token`, `operation_key`, `file_name`, `media_type` and base64 bytes, imports the artifact through the shared Core estimate-import command, and returns Draft identity/name/status, replay state, source hash, parser/provider and structured blockers/errors.

## Why

Operator decision D16 (2026-09-01). Today only `pegasus_estimate_save` and `pegasus_estimate_list` exist (`src/Pegasus.Web/Mcp/AssessmentMcpTools.cs`); no MCP tool parses a document.

## Approach

- Reuse the Core command created by the whole-page import ticket (this ticket is blocked by it); no second parser or import path.
- Attribute the import to the Automation Actor; same Case plus same hash replays; a different artifact creates the next Draft.
- The governing-docs chore adds the tool to the FRD-10 tool table under `automation.assessment`.

## Verification

- [ ] A real external Claude Code MCP client round trip against the local host (`Invoke-LocalDevelopment.ps1 -Action Start`) imports an Audatex sample and returns the documented shape; the log is retained as `reference/` evidence.
- [ ] Replay, blockers and lease/version refusals are covered by Core and integration tests.
- [ ] Architecture tests prove one import implementation.

## Outcome
