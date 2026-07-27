# Collision Engineers — AI/ML Strategy
## 02 · Use-Case Portfolio

*Every use case worth considering — the four you named plus everything else the archive and inbox unlock — scored and sequenced into a build order. The rule throughout: cheap, deterministic, self-funding wins first; owned-weights models later and only where measurement justifies them.*

---

## Scoring rubric

- **Value** (H/M/L): impact on engineer touch-time, revenue, or risk reduction.
- **Effort** (S/M/L): build + data-prep cost, including engineer labelling time.
- **Risk** (L/M/H): court/regulatory, licensing, privacy, or accuracy exposure.
- **Approach**: `code` (deterministic), `API` (frontier + tools/RAG), `SFT` (fine-tuned owned weights), `vision` (small trained head).

Waves group by dependency and by the "prove value before spending" gates in `06`.

---

## The ranked portfolio

| # | Use case | Value | Effort | Risk | Approach | Wave |
|---|---|:---:|:---:|:---:|---|:---:|
| 1 | **Case Record schema + report/fee-note renderer** (deterministic core) | H | M | L | code | 1 |
| 2 | **Intake automation**: `.msg`/PDF/docx/email → Case Record; photo triage + completeness gate | H | M | L–M | API+code | 1 |
| 3 | **Vehicle identity & mileage verifier** (DVLA VES + DVSA MOT History; paid UKVD for full spec/VIN) | H | S | L | code+API | 1 |
| 4 | **QA second-reader ("report-linter")**: arithmetic, status↔salvage-cat coherence, ADAS/taxi-op presence, era-correct rates | H | S–M | L | code+API | 1 |
| 5 | Photo-page auto-layout + photo-type captioning | M | S | L | vision+code | 1–2 |
| 6 | Email triage / job status / SLA tracking | M | S–M | L | API+code | 1–2 |
| 7 | **Knowledge assistant (RAG)** over Tier-A library only, licence-aware | H | M | M | API+RAG | 2 |
| 8 | **Engineer copilot v1**: field pre-fill + valuation tool-calls + comment drafting + **edit capture** | H | L | M | API→SFT | 2 |
| 9 | **Rebuttal / query-response drafter** (your playbooks; highest-moat text task) | H | M | M | API+RAG→SFT | 2 |
| 10 | PAV / diminution dispute drafters (FOS-era-aware; mileage −£200/10k tool) | M–H | M | M | API+RAG | 2–3 |
| 11 | Fee / cashflow ops: reconciliation, 89-day terms, chase drafting, DSO analytics | M | S–M | L | code+API | 2 |
| 12 | Estimate audit / negotiation support (line-diff vs repairer; client-vs-audit pairs as data) | M–H | M | M | API+code | 2–3 |
| 13 | Photo-reuse fraud hash (perceptual hash across archive) | M | S | M | code | 2 |
| 14 | Damage-consistency plausibility screening (flag-only, internal wording) | M | M | M–H | API→SFT | 3 |
| 15 | Supplementary-damage (amendment) risk score from original→amended pairs | M | M | L–M | SFT | 3 |
| 16 | **Owned-weights VLM**: photos+instruction → parts/ops/impact JSON | M | L | M | SFT | 3 (gated) |
| 17 | House-style LLM SFT → DPO/KTO from edit logs | M | M–L | L–M | SFT | 3 |
| 18 | Analytics benchmarks (cost by make/model/damage-area/era; salvage realisation; durations) | M | M | L | code | 3–4 |
| 19 | **Diminution-at-scale** proactive product line (screen own closed book → new revenue) | M–H | M | M | API+code | 4 |
| 20 | R&S (recovery/storage) charge review automation | L–M | S | L | code | 2 (opportunistic) |
| — | Raw training-data licensing | — | — | H | — | **Rejected** |

---

## Build order at a glance

- **Wave 0 (week 1) — Security/governance:** rotate OEM credentials, quarantine `Manufacturers.ods`, access controls, start DPIA. *(Prerequisite, not a "use case".)*
- **Wave 1 (weeks 1–8) — Kill the paperwork:** #1–4 (+5, 6). Deterministic + hosted-API; pays for itself in engineer time.
- **Wave 2 (months 2–5) — Knowledge + drafting:** #7–13. Golden eval set formalised mid-wave (`05`).
- **Wave 3 (months 4–9) — Owned weights, gated:** #14–18. Fine-tune only where Wave-2 frontier baselines show a measured gap.
- **Wave 4 (months 9+) — Products:** #19 + external benchmarks (legal review first).

---

## Use-case one-pagers (build order)

### 1. Case Record schema + report/fee-note renderer  *(Wave 1, `code`)*
- **User / trigger:** every case; the spine of the whole system.
- **In → out:** structured Case Record → rendered `{VRN} REPORT.pdf` + fee note, byte-for-byte template with slotted values.
- **Why first:** reports are ~70% template; a deterministic renderer removes formatting toil immediately, gives every model a clean target, and computes **all £ in code** from dated tables (no model ever does arithmetic). It also creates the field-level **edit-capture** surface that later preference training depends on.
- **Data:** the report structure in `01`; schema defined in `04`.
- **Success:** engineers stop hand-formatting; totals always reconcile. **Kill if:** template variety is too high to parameterise (not observed — structure is consistent).

### 2. Intake automation  *(Wave 1, `API`+`code`)*
- **User / trigger:** a new instruction email lands.
- **In → out:** `.msg` + attachments (6+ instruction formats) + photos → populated Case Record (client, refs, VRN, accident date/circumstances, damage area) + a **photo completeness gate** (is there a plate shot? VIN? damage close-ups? odometer?), auto-requesting what's missing.
- **Approach:** frontier API for robust parsing across messy formats; deterministic validators (VRN regex, date parsing); photo-type classifier (#5).
- **Success:** ≥90% of fields auto-filled correctly per format; missing-photo requests sent without engineer action. **Kill if:** format drift makes parsing unreliable (mitigated by API + human confirm).

### 3. Vehicle identity & mileage verifier  *(Wave 1, `code`+`API`)*
- **In → out:** VRN → **DVLA VES** (make, year, fuel, colour; free), **DVSA MOT History** (model + full mileage timeline; free), paid **UKVD/VDG** for full spec + VIN → cross-checked vehicle record + **odometer-fraud flag** (reading below last MOT).
- **Why:** VRN→API lookup **beats vision** for identity — cheaper, exact, defensible. Vision VIN/odometer reads become a *cross-check*, not the source.
- **Success:** identity auto-resolved for the vast majority of UK plates; mileage anomalies flagged.

### 4. QA second-reader / "report-linter"  *(Wave 1, `code`+`API`)*
- **Trigger:** before an engineer signs.
- **Checks:** arithmetic (hours×rate, VAT, settlement = value − salvage); **coherence** (status vs salvage category vs roadworthiness); presence of expected operations (ADAS calibration when relevant; taxi-plate transfer for private-hire); **era-correct rate** used; parts/repairs internal consistency.
- **Data:** rules + `Bad Jobs.xlsx` as seeded error cases.
- **Success:** catches seeded errors at high recall with low false-positive noise. High value, low risk — it *reduces* error rather than generating content.

### 5. Photo-page auto-layout + captioning  *(Wave 1–2, `vision`+`code`)*
- **In → out:** the case photo dump → ordered, captioned report photo pages (whole-vehicle → damage → identity), blurred/duplicate shots dropped.
- **Approach:** small classifier head on a frozen pretrained backbone (10–12 shot types) + Laplacian blur + perceptual-hash dedupe. Runs on CPU.

### 6. Email triage / job tracking  *(Wave 1–2, `API`+`code`)*
- **In → out:** inbox → per-job state (instructed / awaiting photos / drafted / issued / chasing fee), routed and SLA-tracked. Leverages your email-archive access.

### 7. Knowledge assistant (RAG)  *(Wave 2, `API`+`RAG`)*
- **User:** engineers asking "what's our line on X?" (salvage, blends, PPE, retail-repair, FOS valuation rules).
- **In → out:** question → grounded answer with citations from **Tier-A only** (your playbooks/SOPs) + Tier-B facts; **never** Tier-C content in the index.
- **Guardrails:** EVA disambiguation; licence tags; answer only from retrieved context.
- **Why high-value/low-training:** ships without any fine-tuning and encodes your moat knowledge.

### 8. Engineer copilot v1  *(Wave 2, `API`→`SFT`)*
- **The flagship.** In → out: instruction + photos + vehicle lookup → a **draft Case Record** (impact area/magnitude, parts, operations, labour hours with a range, valuations via tool-calls, draft comments) → engineer reviews/corrects/signs.
- **Approach:** frontier VLM + tools day one; migrate perception/comments to owned SFT where evals justify (#16/#17). **Captures every edit** as future preference data.
- **Success:** measured touch-time reduction; rising share of fields accepted unedited. **Kill if:** engineers edit so heavily there's no time saved (addressed by starting with high-confidence fields only).

### 9. Rebuttal / query-response drafter  *(Wave 2, `API`+`RAG`→`SFT`)*
- **Highest-moat text task.** In → out: an incoming third-party engineer query / insurer challenge → a drafted response grounded in your query-defence library (blends, paint PPE, salvage, retail-repair) and diminution rebuttals.
- **Why:** this is proprietary argumentation nobody else has; big time-saver on disputes; strong later SFT/DPO target from sent-vs-draft pairs.

### 10. PAV / diminution dispute drafters  *(Wave 2–3, `API`+`RAG`)*
- **In → out:** valuation/diminution challenge → drafted response applying FOS methodology **for the correct era**, your mileage-adjustment rule (−£200/10k), and the diminution playbook (including the rebuttal of the rival "EVA" formula).
- **Note:** feeds the Wave-4 diminution-at-scale product (#19).

### 11. Fee / cashflow ops  *(Wave 2, `code`+`API`)*
- **In → out:** issued reports ↔ fee notes ↔ payments reconciled; **89-day terms** tracked; polite chase drafts generated; DSO/aged-debt analytics. Uses the email archive for status.

### 12. Estimate audit / negotiation support  *(Wave 2–3, `API`+`code`)*
- **In → out:** repairer's estimate vs your assessment → line-level diff + talking points. Trained/evaluated on the **client-vs-audit** pairs.

### 13. Photo-reuse fraud hash  *(Wave 2, `code`)*
- **In → out:** perceptual-hash every incoming photo against the archive → flag re-used images across "different" claims. Cheap, high-signal fraud control.

### 14. Damage-consistency plausibility screening  *(Wave 3, `API`→`SFT`)*
- **In → out:** photos + stated accident circumstances → a *flag* where visible damage is inconsistent with the described mechanism. **Flag-only, internal wording** — never an accusation in a report. Your instructions already require this check.

### 15. Supplementary-damage risk score  *(Wave 3, `SFT`)*
- **In → out:** desktop inputs → P(strip-down adds parts) + likely categories, from the **original→amended** pairs. Doubles as a **sellable report line** ("elevated likelihood of additional damage on dismantling") and calibrates reserves.

### 16. Owned-weights VLM  *(Wave 3, `SFT` — gated)*
- **In → out:** multi-image + instruction → parts/operations/impact-area JSON, in your vocabulary.
- **Gate:** build only if Wave-2 frontier-VLM baselines underperform on the golden set, or privacy/cost forces on-prem. Recipe/model choice in `03`.

### 17. House-style LLM (comments) → DPO/KTO  *(Wave 3, `SFT`)*
- **In → out:** structured payload → engineer's-comment prose in your voice. SFT on (payload → comment) pairs, then preference-tuned on captured edits.

### 18. Analytics benchmarks  *(Wave 3–4, `code`)*
- **In → out:** thousands of historical cases → internal dashboards: cost by make/model/damage area/era, salvage realisation, repair durations, total-loss rates. Management insight now; a possible product later.

### 19. Diminution-at-scale (new revenue)  *(Wave 4, `API`+`code`)*
- **In → out:** screen your own **closed book** for cases with recoverable diminution → proactive DV reports. Turns the diminution moat into a repeatable product line. Legal / client-consent review first.

### 20. R&S charge review  *(Wave 2, opportunistic, `code`)*
- **In → out:** recovery/storage figures from R&S WhatsApp notes → sanity-checked against norms; flag outliers. Small but easy.

---

## Rejected / deferred

- **Raw training-data licensing — rejected.** Your data carries pervasive PII, Tier-C third-party copyrighted content, and client confidentiality obligations; licensing it out is legally fraught and **sells your moat**. Only *aggregated, fully-anonymised benchmark statistics* (#18/#19 territory) could ever be external, and only after legal review.
- **Autonomous report issuance — rejected.** CPR 35 requires a human expert to hold and sign the opinion (`07`).
- **Any physical-inspection workflow — out of scope** by your constraint (desktop-only).
- **Out-building commodity damage AI — declined** (see the moat thesis in `00`).

---

## New-revenue candidates (summary for the owner)

Three items above are not just efficiency plays but potential **income**:
1. **Diminution-at-scale (#19)** — proactively surface recoverable DV across your closed cases.
2. **Supplementary-damage risk line (#15)** — a value-add clients will pay for on desktop reports.
3. **Anonymised benchmark products (#18)** — cost/valuation benchmarks by segment, if legal review clears aggregate release.

Each depends on the same foundation (schema + pipeline + owned models) that the efficiency use cases already build — so the marginal cost to reach them is low once Waves 1–3 exist.

*Next: `03-training-strategy.md` — the from-scratch verdict, what to fine-tune, and which open-weights models to own.*
