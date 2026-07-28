# AGENTS.md — vehicle-history-check (dev wrapper)

> **Status:** Current · **Last reviewed:** 2026-06-09 · **Runtime:** Skill front-end for the `dvsa-mot` connector

Guidance for AI agents and developers maintaining the **vehicle-history-check** skill.

## What this is

This skill is the standalone front-end for the `dvsa-mot` MCP connector. It gathers UK
government vehicle facts: MOT history/status, mileage anomalies and estimates, recalls,
defects, tax/SORN, roadworthy compliance, clone/export risk, and pre-incident condition.

It does **not** scrape adverts, value vehicles, render PDFs, or replace the connector's
own validation and caveats.

## Layout

```text
vehicle-history-check-dev/       <- dev shell; never uploaded
  AGENTS.md
  README.md
  vehicle-history-check/         <- runtime skill source
    SKILL.md
```

## Hard rules

- Use the `dvsa-mot` connector for facts. Do not infer government records from memory or user prose.
- Preserve connector caveats, especially for mileage estimates and ULEZ/emissions heuristics.
- If the connector is unavailable, stop and say so; do not guess vehicle facts.
- Keep this skill as fact-gathering only. Market value belongs to `vehicle-valuation`; roadworthy documents belong to `roadworthy-report`.

## Dependencies and hand-offs

- **Hard dependency:** `dvsa-mot` MCP connector.
- **Downstream:** `vehicle-valuation` for market value and evidence packs; `roadworthy-report` for HS roadworthy/re-insurance reports; `ce-house-style` for written client notes.

## Shipping

Zips are built ONLY via `tools/pack_skill.py` (repo root).
