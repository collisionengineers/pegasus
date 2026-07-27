---
name: collision-engineers-design
description: >-
  This skill should be used when building or styling anything to the Collision
  Engineers brand (independent automotive engineering experts) — websites, HTML
  mocks, document templates, visual artefacts — whether for production or
  throwaway prototypes/mocks. It is the visual design system: design tokens,
  fonts, brand assets, UI kits for the marketing website and the A4
  document/report letterhead system. For writing/tone/voice, use the
  `ce-house-style` skill instead. Document skills (vehicle-valuation,
  diminution-rebuttal, etc.) consult this skill for layout; their specific
  wording lives in each document skill.
---

The visual design system (tokens, fonts, brand assets, UI kits) for Collision Engineers' two official
surfaces: the marketing website and the A4 document/report letterhead. This skill produces no documents
itself — it hands back tokens and layout that the caller applies.

**For voice, tone, or any written copy — use the `ce-house-style` skill, not this one.**

## Router — decide which branch you are in

**1. If invoked by another skill (a document skill consulting this one for layout):**
Do **not** ask what to build. Read **only** the reference for the calling document type, return the
tokens/layout the caller applies, and stop — don't emit a full HTML page unless asked. Use the table
below.

| Calling skill | Read | Document surface |
|---|---|---|
| vehicle-valuation | `references/document-letterhead.md` | *Market Valuation Evidence* (+ *Advert Evidence Pack*) |
| diminution-rebuttal / diminution-report | `references/document-letterhead.md` | *Diminution Rebuttal* |
| any skill emitting a fee note | `references/document-letterhead.md` | *Fee Note* |
| total-loss-assessment | — (deliberate non-CE Audatex format; do **not** apply CE styling) | n/a |
| roadworthy-report | `references/document-letterhead.md` | *Roadworthy Report* |

Canonical handback for the fast path: documents/print red `#C80A32`, warm charcoal `#2C2A27`, the
system-sans (Arial) body stack for document copy, Tw Cen MT / Futura brand faces for logo/display only,
plus the letterhead header/footer spec. These are the single source of truth — callers must not
re-define their own font or colour stack. (Note: `total-loss-assessment` is the non-CE Audatex format,
not the CE-branded *Total Loss Report* letterhead in `ui_kits/documents/`, which does use this system.)

**2. If invoked directly by a user:**
Ask what they want to build, then act as an expert designer who outputs HTML artefacts, document
templates, or production code. Workflow: (a) import `styles/colors_and_type.css` first; (b) read the
reference for the surface you are building (see Quick map); (c) build with the tokens, the master logo,
and Lucide icons; (d) keep within the non-negotiables.

## Quick map — files and references (load on demand)

- `styles/colors_and_type.css` — all design tokens (`@font-face`, colours, type scale, radii, shadows,
  spacing). Import this first in any HTML/CSS output.
- `references/document-letterhead.md` — canonical A4 letterhead spec (layout, tables, footer, section
  headings, local render + brand-font embedding). **Read before building any document/report template.**
- `references/website-system.md` — website layout rules, motion, interaction states, Lucide usage.
  **Read before building website components.**
- `references/palette-and-type.md` — full palette, type scale, spacing, radii, shadows.
- `references/iconography.md` — icon system rules and common glyphs.
- `fonts/` — Tw Cen MT Std (OTF) + Futura Cyrillic (TTF) brand/logo faces; UI/body uses the system sans.
- `assets/` — master logo (`logo_no_margin.png`, red gear-C), white reverse logo, brand imagery,
  engineer signature PNGs (`assets/signatures/`).
- `preview/` — design-system specimen cards for all tokens (colours, type, spacing, components, docs).
- `ui_kits/website/` — hi-fi recreation of `collisionengineers.co.uk` with all reusable components.
- `ui_kits/documents/` — A4 letterhead system (Total Loss Report, Market Valuation Evidence, Diminution
  Rebuttal, Response Letter, Fee Note). Print-ready JSX components + CSS.

## Non-negotiables

- **Two official surfaces only:** the website (`collisionengineers.co.uk`) and the documents/reports
  letterhead system. The internal Collision Command Centre app is excluded from this kit.
- **Master logo:** `assets/logo_no_margin.png` (red gear-C). Never redraw the gear.
- **Colour:** one web red, one doc/print red, one warm charcoal — see `references/palette-and-type.md`.
- **Type:** keep UI/body on the system sans; brand faces (Tw Cen MT / Futura) for logo, display,
  marketing headers, and printed reports only — never long body copy.
- **Icons:** Lucide only (web). No emoji, no hand-drawn icons.
- **Voice:** handled by `ce-house-style`. This skill covers visual design only.
