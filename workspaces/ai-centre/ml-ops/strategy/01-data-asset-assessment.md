# Collision Engineers — AI/ML Strategy
## 01 · Data Asset Assessment

*What you hold, what it's worth for AI, and what is dangerous in it. Grounded in a direct inspection of the 8 example cases in the authorised `corpus/ai-centre/raw/Reports-selected/` snapshot and the 183-file reference library in `corpus/ai-centre/raw/Documents/`. You have thousands more cases and the full email archive on the same patterns.*

---

## 1. The shape of the asset

Two distinct bodies of data, with very different AI uses:

1. **Case files** (`Reports-selected/`) — the operational record: each case is an **input bundle → output report**. This is your supervised training set and the engine of the copilot.
2. **Reference library** (`Documents/`) — domain knowledge: rate guides, rules, OEM methods, and your own playbooks. This is RAG (retrieval) material and, for the proprietary parts, style/argumentation training data.

Plus a third you control but we haven't sampled: the **email inbox/archive**, which is where instructions, photos, negotiations, and fee-chasing all live.

---

## 2. Anatomy of a case (the training pair)

A case folder is named by a **client-prefixed job reference** (e.g., `QCL26261`, `DFD26137`, `QDOS261560`) which equals Collision Engineers' own "Our Ref". Inside, a consistent input→output structure:

**INPUTS (what the engineer receives)**
- **One instruction**, in one of *at least six* formats: a solicitor's letter (PDF or `.docx`), a filled intake form (your own "Engineer instruction request form", or a client's e.g. AX "InspectionRequest"), or a plain email — almost always delivered as an **Outlook `.msg`**.
- **10–20 photos**: WhatsApp-recompressed JPEGs and/or bodyshop-numbered JPGs. Subjects seen: whole-vehicle establishing shots with the number plate legible, damage close-ups, VIN captured through the windscreen or via the door etch, occasionally an odometer, and **"R&S" (Recovery & Storage) WhatsApp screenshots** carrying quoted recovery/storage charges.
- Occasionally a **repairer's estimate export** (e.g., `DFD26137/REPORT.PDF` from the bodyshop's own estimating package).

**OUTPUTS (what the firm produces)**
- **`{VRN} REPORT.pdf`** — the final Vehicle Damage Assessment (16–18 pages; roughly half are embedded photo pages).
- **`{VRN} FEE NOTE.pdf`** — the fee note (sometimes standalone, sometimes only embedded in the report, sometimes both).

### The report's structure (what the model is learning to fill)

Consistent across cases. The **fixed scaffolding** (headings, statement of truth, fee-note terms) is boilerplate. The **variable payload** — the actual intellectual output — is:

- **Vehicle summary**: Make, Registration, Model, Status (Repair / Total Loss), ABI Category (N/S), Salvage Value, Repair Cost, Legal Status (roadworthy / unroadworthy), Engineer's Value, Impact Magnitude (Light/Moderate/Heavy), Impact Area.
- **Narrative**: Nature of Incident (templated), **Engineer's Comments** (the genuinely bespoke 1–4 sentences), Vehicle History Check (Experian/MIAFTR), Unrelated/Pre-existing Damage, Pre-Incident Condition.
- **Decision branch**: repairable → *Reserve* + *Repair Duration*; total loss → *Settlement* (= value − salvage) + *Salvage* + Category.
- **Vehicle data grid**: Retail/Trade/Engineer's Value, Glass's code, VIN, odometer, engine cc, fuel, condition.
- **Repair specification**: *Main new parts required* (plain-English names, **no part numbers, no per-line prices**), *Repairs required* (panel list), *Additional operations* (ADAS calibration, corrosion protection, diagnostics, paint prep, and notably **"remove/refit taxi plate" / "door taxi decal"** — many vehicles are private-hire).
- **Repair Cost Calculations**: Hours × Hourly Rate (**£83.28/hr**, the 2026 ABP bodyshop rate, constant across all sampled cases) → Total Labour + Parts + Paint/Materials + Specialist/Other + VAT → Total.

**Key ratio for strategy:** a report is ~50% photo pages; of the *text*, ~60–75% is boilerplate or slot-filled template. The model-worthy, differentiating content is a small fraction: **the numeric tables, the parts/operations lists, and the short engineer's comment.** This is why the architecture is "predict a compact structured payload, then render," not "generate 16 pages of prose."

---

## 3. Pairing conventions (how we assemble records automatically)

The filenames make automated input↔output pairing tractable:

- **Case ID = folder name = "Our Ref"** (lowercased in the report, e.g. `qdos261560`).
- **VRN is the strongest cross-artifact key** — it appears in output filenames (`{VRN} REPORT.pdf`, `{VRN} FEE NOTE.pdf`, `{VRN} client/amended/audit report.pdf`) and in email subjects. VINs are a secondary key present inside every report.
- **"Your Ref"** links to the instructing party's own system (QDOS `AMA/#####/1`, Knightsbridge `AA.######`, Ten Legal `NV.####.PI`, DFD `RJP/#####`, AX numeric).
- **Audit variant** is stored as `A.{CASEID}/` with an `a.` ref prefix.

**Watch-outs for the ingestion code:** fee notes are sometimes only embedded in the report PDF (dedupe); one case has *three* report variants; generic filenames like `1.jpg` and `R & S Note.png` recur across folders and are **not globally unique** — must be folder-scoped.

---

## 4. Gold signals (worth more than their count)

These rare artifacts are disproportionately valuable for training and evaluation:

| Signal | Example seen | Why it's gold |
|---|---|---|
| **Original vs amended report** | `QDOS261560`: desktop estimate £2,369.59 → amended £2,843.54 after strip-down; delta isolates to one added part ("Frt Lock Carrier", +£473.95) + comment *"additional damage found once stripped."* | Trains a **supplementary-damage risk score**; calibrates desktop under-estimation. |
| **Client vs audit report** | Same case: independent £2,369.59 vs audited-repairer £2,683.00 vs amended £2,843.54 — three totals, one incident. | Estimate-audit training + eval; shows the negotiation gap. |
| **Valuation-tool screenshot** | `TEN26074`: internal "EVA" valuation tab showing Glass's/CAP/Cazana/Parkers/AutoTrader values → chosen Engineer's Value £3,540 (revaluation count 1). | Labelled intermediate: how multi-source guides become one Engineer's Value. |
| **In-progress / incomplete case** | `QDOS261786`: instruction + 1 client photo + an *empty* Audatex boxnote, no report yet. | Negative/partial example; a live backtest (score when the real report lands). |
| **Integrity signal** | `DFD26137`: bodyshop email *"value as high as you can."* | Seeds an **independence guardrail** eval — the model must flag, never comply. |
| **QA tracker** | `Bad Jobs.xlsx` (complaints/bad-job notes by reg + engineer). | Hard-negative mining + a permanent regression suite. |

---

## 5. ⚠ Security findings — act this week

- **Plaintext credentials.** `Documents/Manufacturer tech info/Manufacturer's Tech Websites/Manufacturers.ods` is a store of **OEM technical-portal usernames and passwords in clear text** (multiple brands; several sharing a reused password pattern). **Actions:** rotate every credential now; move secrets to a password manager/vault; delete the file from shared/synced storage **and from git history**; exclude it permanently from every corpus build via a hard path blocklist. Run a secret-scanner over the whole archive to catch others.
- **Watermarked third-party PDFs** carry staff names + vehicle regs (e.g. "Collision Engineers – Patrick Rooney" on Thatcham packs) — both a PII and a licensing exposure (§7).

---

## 6. PII census (pervasive and unredacted)

The data is **not** redacted. Categories confirmed present across cases:

- **Claimant identity**: full names, **home addresses**, mobile/landline numbers.
- **Vehicle identifiers**: VRNs and VINs (client *and* third-party), private-hire licence numbers visible on-plate.
- **Third parties**: TP names, TP VRNs, insurers (QBE/Gallagher Bassett, Covea, Haven), repairer/bodyshop details.
- **Staff & professionals**: engineer names + **signature images**, solicitor/handler names and emails, case references.
- **Special-category (UK GDPR Art. 9/10) risk**: PI-matter instructions can mention **injuries/health**; fraud-flag language touches **criminal-offence** data. These sentences must be stripped, not just name-masked.
- **Photo-embedded PII**: number plates, faces/reflections of people, and documents photographed in-frame.

Implication: **pseudonymise before any model or API touches the data** for training/analytics; keep real PII only in the live production system (reports need it), behind access control. Full spec in `04`; lawful basis/DPIA in `04`+`07`.

---

## 7. Licence tiering (this constrains RAG *and* training)

Not all "knowledge" can be freely indexed or trained on. Tag every document at ingest:

| Tier | What | May we train on it? | May we put it in a persistent RAG index? |
|---|---|---|---|
| **A — Proprietary (yours)** | Reports, engineer's comments, diminution rebuttals, PAV responses, query-defence snippets, SOPs, UN-roadworthy phrase lists, mileage-adjustment calculator | **Yes** | **Yes** |
| **B — Purchased / public reference** | ABP rate guides, ABI salvage code, FCA valuation review, FOS guidance, (public) case-law summaries | As lookup tables/facts, **no redistribution** | Yes (internal), keep versioned |
| **C — Per-job licensed / restricted** | **Thatcham escribe packs** (explicit no-copy licence + criminal-liability notice + per-job watermark), **OEM manuals** (JLR TOPIx, Audi erWin, Tesla, BMW, VW, Volvo), **guide-value lookups** (Glass's/CAP — protected by UK database right), Kwik-Fit pricing (marked confidential), Practical Classics scans | **No** | **No** — per-case ephemeral context only |

The distinction matters commercially and legally: systematically caching Tier-C guide values or copying Thatcham/OEM text into a persistent store can breach licences and the UK sui-generis database right even though individual facts aren't copyrightable. **Enforcement point:** the corpus registry refuses to index anything tagged Tier-C.

**Reference-library inventory (183 files):** Manufacturer tech info (63, mostly Tier-C), PAVs/valuation (62, mixed B/A), Query responses (17, Tier-A), Diminution (13, Tier-A — highest moat), SOP Guides (8, Tier-A), Training (6, third-party), ABP Guides (5, Tier-B), plus smaller folders. Formats: 66 pdf, 34 png, 32 docx, 21 jpg, 16 boxnote, 6 xlsx.

---

## 8. Temporal / versioned content (your explicit concern, at the data level)

Several sources are **dated and superseded** — they must be stored with effective dates, never treated as timeless:

- **ABP rate guides**: yearly editions (2023, 2024, 2025, 2026); £83.28/hr is the *2026* bodyshop rate. New edition each January.
- **Kwik-Fit tyre price guide**: dated 2023-10.
- **ABI salvage code** (28/05/2025), **FCA valuation review** (2024, updated 2025), **FOS screenshots** (many stamped Sept 2023) — reissued/changed over time; and the *rules* themselves change (the ABI category regime changed in 2017).
- **OEM/Thatcham pulls**: dated to specific jobs.

This is exactly why the strategy separates **weights (era-stable)** from **versioned tables/APIs (dated facts)** — see `03`/`04`. A 2023 case must be scored and re-rendered under 2023 rates and rules, not today's.

---

## 9. Two data-hygiene traps specific to this corpus

- **"EVA" is ambiguous.** In `Diminution/` it is a **rival firm** (*Exclusive Vehicle Assessors*) whose valuation formula your playbooks *rebut*; in `SOP Guides/EVA Setup Guide.docx` and the report footers it is **your own internal assessment software**. Without an explicit entity tag in the schema and RAG metadata, a future assistant will one day paste the rival's formula critique into a client-facing valuation, or mis-attribute a number. Treat disambiguation as a hard rule.
- **OCR debt.** ~55 files carry knowledge only as images/scans (Tesla method PDFs with no text layer, FOS guidance PNGs, ABP PNGs, Practical Classics scans). These need OCR before they're searchable; the Audatex pocket guide and Tesla methods are image-only. Budget an OCR pass in the pipeline (`04`).

---

## 10. Data to start capturing *now* (so future models are better)

The archive is rich but missing a few high-value signals that only exist if you capture them going forward:

1. **Engineer edits** — the diff between an AI draft and the final signed report. This is the single most valuable future training signal (preference data) and the basis for measuring acceptance. It only exists if authoring moves into the schema/tool (not free Word editing).
2. **Strip-down outcomes** — when a desktop estimate is later amended, log the delta explicitly (which parts, why). Powers the supplementary-damage product.
3. **Salvage realisations** — actual salvage sums achieved vs predicted, to calibrate salvage-value curves.
4. **Valuation provenance** — the multi-source guide figures behind each Engineer's Value (you already screenshot these; capture them structured).

---

## Bottom line

You are sitting on a **high-quality, self-labelling supervised dataset plus a proprietary knowledge base** — the rare combination that makes bespoke AI worthwhile. The three things standing between you and using it are all addressable: **security** (rotate those credentials), **privacy** (pseudonymise + DPIA), and **licensing** (the A/B/C tiers). Handle those in Wave 0/1 and the rest of the strategy is unblocked.

*Next: `02-use-case-portfolio.md` turns this asset into a ranked list of things to build.*
