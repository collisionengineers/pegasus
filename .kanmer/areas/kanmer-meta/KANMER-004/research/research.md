# Research

## Current board

- Snapshot: 2026-08-17 immediately before structural mutation.
- Configured areas: 38.
- Pre-existing tickets: 245 (147 active, 98 archived).
- Execution ticket: KANMER-004, making the working migration set 246.
- Existing groups: EPIC-001 and HZN-001; both must be preserved.
- No malformed/off-board items or board warnings were reported.

## Finding

The areas conflate durable ownership with UI presentation slices, temporary simplification work, bugs, PR review and integration initiatives. Groups now provide the missing cross-domain dimension. The approved model therefore uses areas only for durable ownership and groups for initiatives.

## Governing context

AGENTS.md owns repository process. No product PRD, FRD or ADR is appropriate.

## Concurrency rule

The board is live. Every per-ticket patch must use a fresh get_item read and expected_updated. Existing groups and all non-area state must be preserved.
