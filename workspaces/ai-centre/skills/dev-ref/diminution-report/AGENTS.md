# AGENTS.md — diminution-report (dev wrapper)

Guidance for AI agents and developers maintaining the **diminution-report** skill.

## What this is

The **claimant-side** diminution skill: it builds Collision Engineers' own diminution opinion (full report, advice note, or stigma review). Do not conflate it with `diminution-rebuttal` (which rebuts an opponent's formula report) — they are separate skills with opposite postures.

## Layout (wrapper vs upload)

```
diminution-report-dev/                 <- this dev shell — NEVER uploaded
  README.md   AGENTS.md
  diminution-report/                   <- the CLEAN skill = ships to cowork/Desktop
    SKILL.md
    references/   (diminution methodology + drafting guidance)
```

## Hard rules

- Keep voice/wording in `ce-house-style` and layout/letterhead in `collision-engineers-design` — do not carry local copies.
- Any value-dependent conclusion routes through `vehicle-valuation`; do not invent valuations here.

## Status / shipping

**Dev-holding** per `dev-docs/repo/DISTRIBUTION-READINESS.md` — not distributed yet. Once promoted, zips are built ONLY via `tools/pack_skill.py` (repo root).
