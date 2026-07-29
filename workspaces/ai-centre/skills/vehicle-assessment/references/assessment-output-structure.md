# Assessment Output Structure

> **Source-workspace boundary:** This file is an experimental output schema, not a mandatory Pegasus deliverable, caller, policy, current instruction, or acceptance authority. `Pegasus.Core` and an authorised human own accepted reports and outcomes.


The canonical shape of the engineer information pack, the closing key-information summary
table, and the branded-PDF payload mapping.

## Default pack — section order

Produce the pack in chat as structured markdown. Use these sections in this order; drop a section
only when it is genuinely not applicable, and say so where the omission is informative:

1. **Repair estimate** — the centrepiece. A rate line above the table (rate class + figure,
   uplift decisions, and the location evidence behind the regional-uplift call). Then the
   estimate table exactly as constructed per `references/estimate-construction.md`, which owns
   the canonical column set, status flags, grouping, totals block, and sensitivity line — do
   not restate the spec here.
2. **Economics & outcome** — mandatory whenever any candidate value exists (adopted PAV *or*
   guide screens as case evidence): repair total vs **each** candidate value, the ratio, and
   the instructed-ceiling arithmetic (e.g. 80% of each candidate PAV). A total-loss position
   always cites the completed estimate — total loss never waives the table. If no value exists
   at all: the repair total stands alone and PAV is named as missing evidence — never the
   inverse (no ratio/threshold claim without a costed total).
3. **Basis: vehicle, evidence & damage catalogue** — identity basis, evidence reviewed
   (dated/classified), visible damage panel-by-panel. Labels inline.
4. **Concealed-damage risk & outstanding evidence** — the costing posture in
   `references/estimate-construction.md` puts justifiable concealed-damage scope in the table
   as P lines, so each item here normally ties to its P-line number; an item that cannot yet
   carry an honest line states the £ sensitivity of the line it would add.
5. **Supporting positions** — roadworthiness & storage, ADAS/EV-HV, manufacturer-method
   pointers, provisional salvage, further options. Include only the sub-parts that apply;
   empty sub-parts are dropped without comment.
6. **Confidence & human checks** — one desktop caveat, one provisional-pending list (see
   `references/source-governance.md`).
7. **Key information summary** — the closing table (below). Always the final section. Hard
   rule on the Repair total row: **Repair total is never "not costed" — it is always the
   computed sum of the table's lines; the only permitted qualifier is "(provisional — n lines
   assumption-based)".** Honest-unresolved wording remains valid only for genuinely external
   rows (PAV, salvage category, SRS state).

Evidence labels are the honesty core: label sections once where a heading declares the label,
and label per-row in mixed tables (the estimate table always carries per-row justification and
status).

## Key information summary — closes every deliverable

Every deliverable ends with one concise two-column table that puts the headline positions in one
place, so the engineer can read the bottom line without re-reading the document. It appears as
the final section of the pack, as the final `datatable` section of the branded PDF, and at the
end of the chat summary. Keep each value to a single line; include only the rows that apply, and
mark unresolved rows honestly (e.g. "Undetermined — geometry evidence outstanding").

| Row | Value style |
|---|---|
| Outcome | `Total loss` / `Repairable` / `Undetermined` |
| Basis | One line: the deciding evidence or ratio (e.g. "repair £8,400 vs PAV £9,000 — 93%") |
| Roadworthy | `Roadworthy` / `Unroadworthy` + the dominant evidence in a few words |
| Labour rate | Class and figure (e.g. "Standard £83.28/hr" / "Prestige £103.06/hr + VM £5.00") |
| Repair total | £x,xxx.xx inc VAT (ex-VAT in brackets where useful) |
| PAV / ratio | PAV figure and repair-to-PAV percentage, or "PAV not yet evidenced" |
| Salvage position | Provisional category view + "subject to AQP review", or `n/a` |
| Storage | Justified / not justified, with daily rate where it applies |
| ADAS / EV flags | Calibration or HV items in scope, or `none identified` |
| Outstanding evidence | The two or three items that most limit confidence |

The table summarises conclusions already made and labelled in the body — it never introduces a
new position, and it inherits the body's caveats rather than restating them.

## Deliverables

Deliverables policy is stated once, in SKILL.md — this file defines only the pack and payload
shapes.

## CE-branded PDF — collisionrenderer payload mapping

Template: `expert-report` while the assessment is a broad information pack. Once the outcome is
settled and instructed, prefer `total-loss-report` (write-off, settlement = value less salvage)
or `repairable-contract-repair-report` (repairable outcome).

Payloads are **camelCase**. Map the pack into the template like this:

| Pack section | Payload |
|---|---|
| Case refs and date | `meta.ourRef`, `meta.yourRef`, `meta.date` (DD/MM/YYYY) |
| Title | `title` (e.g. "Vehicle Damage Assessment"), `subtitle` with vehicle + registration |
| Opening | `salutation`, `reLine`, `intro[]` (instruction basis + independence line) |
| Repair estimate | section with one datatable block — one row per estimate line (#, operation, item, WU/qty, £, justification, status) plus totals rows; valuebox for Total inc VAT |
| Vehicle identified | section with a `datatable` block (Registration, Make/Model, First Registered, Mileage, Colour, VIN) |
| Photos | section with `mediarow` blocks (caption + note per image slot) |
| Evidence reviewed / visible damage | sections with `paragraph` and `bullets` blocks |
| Source-backed points vs inference | separate sections — keep the labels in the prose |
| Repair scope / ABP position | `paragraph` + `bullets`; figures as £x,xxx.xx |
| Economics | `valuebox` block for the headline figure where an outcome is stated |
| Key information summary | final section before sign-off, heading "Summary of Key Information", one `datatable` block mirroring the closing table |
| Sign-off | `signature` — omit fields to keep firm defaults; explicit `""` suppresses a line |

Line justifications are allowed in the branded PDF (they are banned only from Audatex row
descriptions) but must pass the house-style lint.

Process, in order:

1. Build the payload.
2. Keep all PDF-rendered prose within `ce-house-style` — lint with
   `ce-house-style/scripts/lint_house_style.py --payload payload.json` where Python is
   available; otherwise apply its banned-terms list in-context. No internal workflow terms
   ("EVA" as a system, "uplift", "AI", modes) in rendered fields.
3. `collisionrenderer` `validate` the payload; fix every error.
4. `render`, present the PDF.

This is the only render path for the branded document. If the connector is unavailable, present
the validated payload JSON and stop — never route the branded pack through another renderer,
HTML, or DOCX path.

## Chat summary conventions

Close in chat with: the vehicle, the damage in one or two sentences, the headline position
(repair scope / economics / outstanding evidence), anything estimated or assumed, and then the
key information summary table as the final element of the message. Plain English, no padding.
