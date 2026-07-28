# AGENTS.md — ce-house-style (dev wrapper)

Guidance for AI agents and developers maintaining the **ce-house-style** skill.

## What this is

The **foundation writing skill** — the single authority for *how* Collision Engineers writes
every external output (emails, chasers, covering notes, expert reports, valuation commentary,
diminution rebuttals, Part 35 responses, fee notes). Complements `collision-engineers-design`
(which owns visual design); this skill owns voice, tone, register, and vocabulary.

## Layout (wrapper vs upload)

```
ce-house-style-dev/                          <- this dev shell — NEVER uploaded
  AGENTS.md                                  <- this file
  README.md                                  <- human overview
  ce_communication_style_tone_profile.docx   <- archival source for the tone profile
  ce-house-style/                            <- the CLEAN skill = ships to cowork/Desktop
    SKILL.md
    references/   (canonical-responses, banned-terms, email-patterns, document-tone-notes)
    scripts/lint_house_style.py              <- banned-terms linter
```

## What's editable vs frozen

- **Editable:** references, the banned-terms list, the linter.
- **Hard rule:** the **independence line** and **British English / no-emoji / no-exclamation**
  conventions are non-negotiable. External output must pass the linter with **zero hits**.

## Dependencies

- **Standalone** — no dependencies on other skills.
- **Depended on by** every document skill. `diminution-rebuttal` invokes
  `scripts/lint_house_style.py`; on cowork/Desktop that is a *soft* dependency (skills upload
  individually), so the linter is run when this skill is present, otherwise the banned-terms
  list is applied manually.

## Path conventions

Run the linter from the skill root: `python scripts/lint_house_style.py <file_or_text>`.
No repo-rooted or absolute paths.

## Shipping

Zips are built ONLY via `tools/pack_skill.py` (repo root).
Status: production-ready.
