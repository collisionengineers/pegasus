# Collision Engineers — AI/ML Strategy
## 06 · Roadmap & Costs

*A staged plan a small firm can actually afford, where early cheap wins fund later bets. All £ figures are planning ranges (mid-2026) to be re-quoted at build time; GPU and API prices move. The recurring theme: **compute is cheap, engineer time is the real budget.***

---

## 1. Shape of the plan

Five waves, each with a **go/no-go gate**. Waves 1–2 pay for themselves in engineer time before any serious model spend in Wave 3. Nothing expensive happens on faith.

| Wave | Theme | Duration | Spend (rough) | Gate to proceed |
|---|---|---|---|---|
| **0** | Security & governance | Week 1 | ~£0–2k (time) | Unconditional — do first |
| **1** | Deterministic core + intake | Weeks 1–8 | ~£8–25k eng + £tens/mo API | Measured touch-time drop on real cases |
| **2** | Knowledge + drafting (RAG, copilot) | Months 2–5 | ~£10–25k eng + API | Engineers accept a majority of drafted fields/text |
| **3** | Owned weights (gated) | Months 4–9 | ~£5–20k GPU+eng | Frontier baselines underperform on the golden set (`05`) |
| **4** | Products (DV-at-scale, benchmarks) | Months 9+ | Commercial, case-by-case | Legal review + demonstrated internal accuracy |

**Indicative Year-1 all-in: ~£30–80k**, front-loaded into Waves 1–2.

---

## 2. Wave detail & milestones

### Wave 0 — Security & governance (week 1)
- Rotate OEM-portal credentials; vault secrets; purge `Manufacturers.ods` from storage/backups/git; add ingest blocklist + secret scan (`01`, `04`, `07`).
- Start the **DPIA** and the Legitimate Interests Assessment; add a data-use clause to engagement terms (`04` §6).
- **Milestone:** no credentials in any store; DPIA underway. **Cost:** internal time (+ maybe a few hundred £ of legal review).

### Wave 1 — Kill the paperwork (weeks 1–8)
- **Case Record schema + deterministic report/fee-note renderer** (`04` §3) — £ computed from dated tables.
- **Intake automation** (.msg/PDF/docx/email → Case Record) + **photo triage/completeness gate**.
- **Vehicle identity & mileage verifier** via free **DVLA VES + DVSA MOT History** (+ paid UKVD for full spec/VIN).
- **QA "report-linter"** (arithmetic, coherence, era-correct rates, ADAS/taxi-op presence).
- **Milestone / gate:** a real case flows instruction→draft-record→rendered report with a measurable engineer touch-time reduction. **Cost:** contractor build £8–25k; API £tens/month.

### Wave 2 — Knowledge + drafting (months 2–5)
- **RAG knowledge assistant** over Tier-A only, licence-aware (`01` §7).
- **Engineer copilot v1** — field pre-fill + valuation tool-calls + comment drafting + **edit capture** (the flywheel).
- **Rebuttal / PAV / diminution drafters** from your playbooks.
- **Fee/cashflow ops** and **photo-reuse fraud hash** (opportunistic).
- **Formalise the golden eval set** (`05`) mid-wave.
- **Milestone / gate:** engineers accept a majority of drafted fields and rebuttal text; golden set scored against a frontier baseline. **Cost:** build £10–25k; API grows but stays modest.

### Wave 3 — Owned weights, gated (months 4–9)
- Only the capabilities where Wave-2 baselines underperform: **owned VLM** (photos→parts/ops/impact), **house-style LLM** (comments) → DPO/KTO from edit logs, **supplementary-damage risk head**, **damage-consistency screening**, **analytics benchmarks**.
- **Milestone / gate:** a fine-tuned adapter beats the frontier baseline on the golden set and passes regression gates (`05` §9); served on scale-to-zero GPU. **Cost:** see §3 — training £250–2k/cycle, small serving.

### Wave 4 — Products (months 9+)
- **Diminution-at-scale** screening of the closed book; **anonymised benchmark** products.
- **Gate:** legal/consent review; internal accuracy demonstrated. **Cost:** commercial, evaluated per product.

---

## 3. Cost model (realistic ranges)

### Running inference (day-to-day)
| Option | Cost | Verdict |
|---|---|---|
| Frontier API per case (incl. retries) | **£0.10–0.50/case → £20–100/month** at 50–200 cases/mo | Day-1 workhorse; pennies |
| Serverless per-token open-model host (8B) | < £10/month | Cheapest if they host your LoRA |
| **Scale-to-zero serverless GPU, own weights** | **£15–40/month** | Recommended owned-weights serving |
| Rented GPU 24/7 (L40S/A100/H100) | £450 / £900 / £1,500+ /month | < 2% utilisation at your volume — **indefensible** |
| On-prem box (RTX 5090 ~£2k; RTX PRO 6000 96GB ~£8–9k) | £60–250/mo amortised + power | Only for data-residency/court posture or 10–50× volume |

### Training (one-off per cycle, ~5,000 cases)
GPU rental (re-verify): A100-80GB ~£1.2–1.6/hr, H100 ~£2.0–2.9/hr, L40S ~£0.7–1.0/hr.
| Run | Hardware | Wall time | Per run | Full cycle (3–5 ablations + evals) |
|---|---|---|---|---|
| 8B VLM LoRA | 1×H100 / 2×A100 | 15–25 hrs | £40–70 | **£250–600** |
| 30B-A3B / 32B LoRA-QLoRA | 2×H100 / 4×A100 | 150–300 GPU-hrs | £400–900 | **£1,000–2,000** |

### One-off data bootstrap
- Frontier pass over ~5,000 cases (extraction + verification, two passes): **< £1,500** total (roughly half with batch API).

### People (the real budget)
- **Contract ML/data engineer:** £400–700/day. Wave 1 ≈ **£8–25k**; Wave 2 similar.
- **Engineer labelling / QA:** **150–250 expert hours** across the programme (2–4 hrs/week) — the **critical path**. Protect it or everything slips.

**The number that dominates all of the above:** at ~150–200 reports/month, a 30–60 min/report touch-time saving at £40–80/hr loaded cost is **~£4,000–12,000/month of freed capacity** (`00`). Model spend is noise against that.

---

## 4. Buy vs build

- **Damage-AI vendors (Tractable, Solera/Audatex Qapter, CCC):** not buyable at your scale and **not court-grade** — they produce insurer/repairer estimates, not independent CPR-35 expert opinions. Consume commodity damage detection via a **frontier API**, don't license a platform.
- **Estimating/valuation systems** you already use (your internal "EVA", Audatex, Glass's/CAP): integrate with them; don't rebuild them.
- **Free government data:** DVLA VES + DVSA MOT History — build the thin integration, it's near-zero cost and high value.
- **Build (your moat):** the schema/renderer, the RAG over your playbooks, and the fine-tuned comment/rebuttal/damage adapters. Nobody sells these because they're *yours*.

---

## 5. Staffing & sequencing

- **Now:** owner/lead engineer sponsor + a **fractional/contract ML engineer** for Waves 1–2. No full-time ML hire needed yet.
- **Wave 3:** the same contractor runs the (cheap, occasional) training cycles; consider a part-time ML hire only if Wave-3 models prove out and volume grows.
- **Ongoing:** whoever owns the golden set (an engineer) is as important as whoever owns the code — the dataset is the durable asset (`03`).

---

## 6. Funding logic

```
Wave 1 touch-time savings  ──►  fund Wave 2 build
Wave 2 capacity gains      ──►  fund Wave 3 GPU + eng
Wave 3 owned models + data ──►  enable Wave 4 revenue products
```

Each wave is cash-flow positive before the next begins. If a gate isn't met, you stop with a working, cheaper system already in hand — there is no "big bang" that must succeed.

---

## 7. What would change these numbers

- **Case count & amendment rate** (confirm in Wave 0/1) — drives dataset sufficiency and whether the supplementary-damage/ DPO work is viable.
- **Email-archive volume/quality** after privilege triage and dedupe.
- **API contract terms** for valuation guides (Glass's/CAP/UKVD) — affects the valuation automation's cost and licensing.
- **Open-model landscape** — a stronger/cheaper base than Qwen3-VL-8B may appear; adapters re-train on it cheaply (`03`).

*Next: `07-risk-register-and-compliance.md` — the risks, the CPR-35 AI-use policy, and what to do when something goes wrong.*
