# AGENTS.md — manufacturer-methods-evidence (dev wrapper)

Guidance for AI agents and developers maintaining the **manufacturer-methods-evidence** skill.

## What this is

A decision-support skill that points to manufacturer repair-method evidence and produces safe, paraphrased reasoning. It does **not** replace live OEM repair data, Thatcham, or repairer method access.

## Layout (wrapper vs upload)

```
manufacturer-methods-evidence-dev/      <- this dev shell — NEVER uploaded
  README.md   AGENTS.md
  manufacturer-methods-evidence/        <- the CLEAN skill = ships to cowork/Desktop
    SKILL.md
    references/   (per-method-area guidance + method index)
    scripts/      (method-index validation)
```

## Hard rules

- **Never reproduce copyrighted OEM content** — no verbatim procedures, diagrams, dimensions, or step-by-step method text. Paraphrase to safe decision pointers only.
- Keep the live source of truth (current OEM/Thatcham data) authoritative; this skill organises evidence, it does not invent it.

## Dependencies

Invokes `total-loss-assessment`, `ce-house-style`, and `vehicle-valuation` at point of need (see `SKILL.md`).

## Shipping

Zips are built ONLY via `tools/pack_skill.py` (repo root).
