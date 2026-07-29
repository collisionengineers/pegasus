---
name: ce-cost-defence
description: Use this source-workspace experiment to assemble source-labelled repair-cost evidence and a draft response for authorised human review; it does not provide legal advice, decide an argument, or issue a report. Triggers include "draft a response to the defendant's queries", "justify our repair costs to the court", "rebut their engineer's report", "respond to the Part 35 questions on cost", "they're disputing our labour hours / paint / blend / rate", "build the cost defence report", or any explicit instruction to generate the Collision Engineers branded cost-justification document for a court. Produces a CPR-compliant Word document in the fixed Collision Engineers house style (logo header, FAO The Court block, red-ruled headings, summary table, point-by-point rebuttal, statement of truth, signature, footer) via a deterministic generator so every report looks identical. Do NOT use for pre-accident VALUATION disputes, EVA damage assessments, or anything outside defending repair costs to a court.
---

## Authority boundary

This package may produce evidence, candidates, or draft output only. `Pegasus.Core` and an authorised human own every accepted case fact, cost, category, outcome, legal position, and approval.
# Collision Engineers — Repair Cost Defence Report

## What this skill does

Assembles a source-labelled repair-cost evidence draft for authorised human review. It does not determine the firm's position, address or persuade a court, guarantee procedural compliance, sign, serve, or issue a report.

The branded Word document is produced by `scripts/build_report.js` — a tested deterministic generator that fixes the entire house style (fonts, brand red, logo header, ref block, red-ruled headings, table styling, footer, statement of truth, signature block). **Never edit the generator to change styling.** The experiment maps user-approved content into a `data.json` fixture and may run the deterministic generator. Formatting consistency is not legal, factual, engineering, or approval evidence.

## Workflow — follow in order

### 1. Read the reference files

Before writing anything, load:

- `references/document_structure.md` — the fixed report skeleton, the `data.json` schema, and worked guidance on each section.
- `references/source_material.md` — **which areas of the supplied case files to examine** so you can build a strong, evidence-based rebuttal. (The case files differ on every job; this tells you what to look for, not what to copy.)
- `references/brand.md` — the fixed branding facts (only needed if you have to touch the generator or explain the styling).

### 2. Examine the case material

The user will normally supply (names vary by job):

- **Our original report** — Collision Engineers' own assessment of the vehicle and damage.
- **Our repair specification / breakdown** — the itemised operations, work units, labour rate, paint, parts, and totals that the costs are built from.
- **The defendant's letter / engineer's comments** — the specific challenge(s) being made.
- **The defendant's repair specification** (often an Audatex / insurer print-out) — their competing figures, if provided.

Read `references/source_material.md` for the specific areas to extract from each. Pull out the actual figures, operations and challenge points for **this** job — never carry over numbers from the example files.

### 3. Identify every challenge and prepare a rebuttal

List each distinct point the defendant raises (e.g. labour hours not supported, no blend images, rate too high, paint times excessive, no breakdown given). For each, prepare a specific, evidence-grounded response that points back to your original report and itemised specification. The defendant's own omissions (no inspection, no alternative methodology, no contradicting times, no market/manufacturer evidence) are usually your strongest material — highlight them.

If a material fact is genuinely missing and you cannot defend a point without it, ask the user with `ask_user_input_v0` (group up to 3 questions; don't ask one at a time). Otherwise proceed and flag any assumption in your chat summary.

### 4. Build the `data.json` content object

Follow the schema in `references/document_structure.md`. Save it to `/home/claude/work/<reg>_data.json`. Keep the tone the way the example reports read: measured, professional, addressed to the Court, never insulting the other engineer — let the evidence do the work.

### 5. Run the generator

```bash
mkdir -p /home/claude/work
cd <this-skill-dir>
npm install docx --no-save 2>/dev/null   # only if docx isn't already available
node scripts/build_report.js /home/claude/work/<reg>_data.json /home/claude/work/<reg>_cost_defence.docx
```

The generator resolves the bundled logo at `<this-skill-dir>/assets/logo.jpeg` automatically. If you don't know where the skill is mounted, run `find / -name build_report.js -path '*ce-cost-defence*' 2>/dev/null` first.

### 6. Validate, then present

Copy the finished file to the outputs directory and present it:

```bash
cp /home/claude/work/<reg>_cost_defence.docx /mnt/user-data/outputs/
```

Then call `present_files` with the output path. Optionally convert to PDF for a visual check using the docx skill's `soffice.py` if the user wants to eyeball it.

### 7. Summarise the build

In your chat reply, briefly give: the vehicle/matter, our cost vs theirs, the challenge points you rebutted (one line each), and any assumption you made. Be honest about anything you guessed — the engineer reviews and signs the report.

## Fixed details — never vary between reports

These are baked into the generator and must not change:

- **Footer:** `Collision Engineers, 77-79 Hoylake Road, Moreton, Wirral, CH46 9PY | engineers@collisionengineers.co.uk` and `www.CollisionEngineers.co.uk`.
- **Brand red** `#C8102E`, Arial throughout, A4, red-ruled section headings, red-header tables.
- **Signatory:** never default or invent one. Include only an explicitly supplied, authorised signatory in an already accepted payload.
- Part 35-related text is reference/template evidence only; include it only when an authorised human supplies and approves the exact text.
- Do not infer a court addressee. Use only an explicitly supplied, approved addressee.

Even if a defendant document shows a different address or postcode, the Collision Engineers chrome stays as above.

## Tone

Plain, professional, court-appropriate English. Confident but not combative. The argument is won on evidence and transparency (itemised hours, manufacturer/Audatex times, the defendant's lack of inspection or contrary evidence), not on attacking the other engineer. No hedging padding, no "I hope this helps".

## Common mistakes — check before finalising

1. Carrying over figures, registrations or names from the example case files instead of this job's. Every number must come from the current source material.
2. Missing one of the defendant's challenge points — list them all, rebut each.
3. Changing the generator's styling to "improve" it. Don't; the format must be identical every time.
4. Treating a template statement or CPR-related line as approved legal content without explicit human authorization.
5. Inventing a manufacturer time or work unit. If a figure isn't in our specification, say it's an estimate or ask.
6. Making the rebuttal personal. Keep it about the evidence.

## File index

- `scripts/build_report.js` — deterministic branded report generator. Import / run; never restyle.
- `assets/logo.jpeg` — Collision Engineers logo for the header.
- `references/document_structure.md` — report skeleton + `data.json` schema + section guidance.
- `references/source_material.md` — which areas of the case files to examine (repeatable; no job-specific data).
- `references/brand.md` — fixed branding facts.
