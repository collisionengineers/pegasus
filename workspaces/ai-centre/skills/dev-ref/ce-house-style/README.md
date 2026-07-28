# ce-house-style

The writing voice of Collision Engineers — a foundation skill that governs tone, register, and
vocabulary across every external output, from a two-line delivery note to a sixteen-page expert
report.

**The voice, in one line:** communicate as an independent vehicle engineering expert — concise,
professional, evidence-based, calm under challenge, and confident without being confrontational.

## What it provides

- **`SKILL.md`** — the voice, mechanics (British English, `DD/MM/YYYY`, `£1,200.00`), tone
  spectrum, and the critical independence line.
- **`references/`** — `canonical-responses.md` (dispute/query scripts), `banned-terms.md`
  (enforced banlist: AI tell-tales + internal workflow terms), `email-patterns.md`,
  `document-tone-notes.md` (per-document register).
- **`scripts/lint_house_style.py`** — checks output against the banned-terms list. **Zero hits
  required** before any external send.

## Status

Production-ready. Used by all document skills.

## Layout

`README.md` and `AGENTS.md` live in this `ce-house-style-dev/` wrapper; the uploadable skill is
the nested `ce-house-style/` folder. See `AGENTS.md` for maintenance notes.
