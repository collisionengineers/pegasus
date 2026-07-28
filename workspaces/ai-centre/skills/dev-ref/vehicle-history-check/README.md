# vehicle-history-check (dev wrapper)

> **Status:** Current · **Last reviewed:** 2026-06-09 · **Runtime:** Skill front-end for the `dvsa-mot` connector

Dev workspace for the `vehicle-history-check` skill. The runtime skill source is the inner
`vehicle-history-check/` directory; distribution zips are built ONLY via `tools/pack_skill.py`
(repo root).

This skill is the standalone front-end for the **`dvsa-mot`** connector (DVSA MOT History +
DVLA Vehicle Enquiry): MOT history/status, mileage validation + `current_mileage_estimate`,
recalls, defects, tax/SORN, roadworthy go/no-go, clone/export risk, pre-incident condition.
It gathers government facts only — no scraping, no valuation, no PDF rendering — and hands
off to `vehicle-valuation` (market value) or `roadworthy-report` as needed.

## What it provides

- **`vehicle-history-check/SKILL.md`** — runtime workflow, connector tool selection, caveats, and hand-offs.
- **`AGENTS.md`** — maintainer rules for preserving the fact-gathering boundary.
- **This README** — wrapper overview and packaging notes.

## Foundations and dependencies

The skill depends on the `dvsa-mot` connector. It hands off to `vehicle-valuation` for
market value, `roadworthy-report` for roadworthy/re-insurance documents, and `ce-house-style`
for any written client note.

## Layout

```text
vehicle-history-check-dev/
  AGENTS.md
  README.md
  vehicle-history-check/
    SKILL.md
```
