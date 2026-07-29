---
name: ce-house-style
description: >-
  Use this skill whenever you are drafting, editing, or reviewing any written
  output for Collision Engineers — emails, chasers, covering notes, expert
  reports, valuation commentary, diminution rebuttals, addenda, Part 35
  responses, fee note covering letters, dispute responses, or any other text
  that will be sent to a client, solicitor, insurer, or court. Also use it when
  asked to check a draft for tone, style, or banned terms. Triggers on: "draft
  an email", "write to the TPI", "chase the solicitor", "check the tone",
  "write the commentary", "dispute response", "rebuttal", "Part 35",
  "covering note", "fee note letter", "does this sound right", or any request
  to write in CE's voice — even if the skill is not named.
---

# Collision Engineers — House Style

The single authority for **how Collision Engineers writes**. All written output — from a two-line delivery note to a sixteen-page expert report — follows this voice.

**One sentence:** *Communicate as an independent vehicle engineering expert: concise, professional, evidence-based, calm under challenge, and confident without being confrontational.*

## Voice

Calm, professional, technically authoritative. Every message positions CE as an **independent expert**: fact-driven, confident but measured, cooperative even when disagreeing. Authority comes from clarity and justification — never from forceful or emotive language.

## Tone

- **Primary:** professional, neutral, courteous, assured.
- **Contextual:** *Supportive* (assisting handlers/clients) · *Firm* (defending reports or positions) · *Concise* (routine delivery) · *Detailed* (disputes and technical challenges).
- Frustration, urgency, and disagreement are expressed **factually, never emotionally**.

## Mechanics

- **British English** throughout: *-ise* spellings, *colour*, *roadworthy*, *tyre*. Dates as `DD/MM/YYYY`. Currency as `£1,200.00`.
- **Casing:** Title Case for document titles and service names; Sentence case for body; **UPPERCASE** for document section headings and eyebrows.
- **Person:** "We" for the firm, "you" for the reader/client. Reports speak in measured third person.
- **No emoji. No slang. No exclamation marks.**

## The independence line

Use this to close disputes — it reframes the matter as independent professional judgement, not negotiation:

> *"We confirm that we have no financial interest in the settlement of this claim and see no reason to deviate from our independent report."*

## Evidence citations

Statements are assertive and evidence-based. Cite the source where it helps: ABP rates, Audatex, Glass's Guide, Cazana, CAP HPI, Thatcham, FOS guidance, ABI guidance, market data, safety standards, CPR Part 35.

## If invoked by another skill

Don't reload all four references. Read only what the calling task needs, then return the corrected
text plus a pass/fail on banned terms:

| Calling context | Read | Subset that applies |
|---|---|---|
| External report / valuation commentary | `references/banned-terms.md` + `references/document-tone-notes.md` | Avoided/preferred terms + the tone block for that document type (e.g. *Market Valuation Evidence*) |
| Email / chaser / covering letter | `references/email-patterns.md` + `references/banned-terms.md` | Message shapes + banned terms |
| TPI dispute / rebuttal reply | `references/canonical-responses.md` + `references/banned-terms.md` | Dispute scripts + banned terms |

For a **valuation report** specifically: enforce the banned list (which already bans `EVA system`,
`guide value`, `uplift`, `cherry-picked`, `highest adverts found`, `selected to increase value`,
`Engineer Value`) and the *Market Valuation Evidence* tone (neutral, evidence-led, never argue
against the assessed value). **Scope: the check applies to PDF-RENDERED prose only** — report
text, commentary, and the advert display fields. Internal payload fields that never reach the PDF
(`valuation_mode`, `guide_value`, `guide_value_unavailable_reason`, `evidence_role`,
`comparability_note`, `differences_note`, `evidence_assessment.basis`) are exempt: a mode label or
the phrase "guide value" INSIDE those fields is correct and needs no comment — do not flag it, do
not explain it. With a Python runtime, finish with `python scripts/lint_house_style.py --payload
<payload.json>` (field-aware; zero hits required) or plain `lint_house_style.py` for prose files;
without Python (staff Desktop), apply the banned list in-context to the same scoped field set.

## References — load when needed

- `references/canonical-responses.md` — five templated dispute/query scripts (total loss, repair spec, salvage × 2, general). Load when handling a TPI challenge or dispute reply.
- `references/banned-terms.md` — enforced banlist: AI tell-tales and internal workflow terms. Load when finalising any external output.
- `references/email-patterns.md` — the three standard message shapes and preferred stock phrases. Load when drafting emails, chasers, or covering notes.
- `references/document-tone-notes.md` — per-document-type tone, register, and sign-off rules. Load when writing or reviewing a specific report type.

## Quick do / avoid

**Do:** state what was reviewed → what was found → the engineering or valuation reason. Close politely without flourish.

**Avoid:** see `references/banned-terms.md` for the full enforced list. The short version: no AI tell-tales ("delve", "comprehensive", "seamless", "it is important to note"), no internal workflow terms ("EVA" as a system, "guide uplift", "prompt", "mode", "AI", "tool output", "draft strategy"), no sales language, no unsupported absolutes, no emotional language.

## Validation

Before finalising any external output, run:

```bash
# run from this skill's root directory
python scripts/lint_house_style.py <file_or_text>      # prose files / inline text
python scripts/lint_house_style.py --payload draft.json # valuation payloads (PDF-rendered fields only)
```

Zero hits required before presenting output to the user. On machines without Python, apply the
banned list in-context — for valuation payloads, only to the PDF-rendered fields (see above).
