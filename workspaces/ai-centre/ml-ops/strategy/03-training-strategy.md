# Collision Engineers — AI/ML Strategy
## 03 · Training Strategy

*The modelling doctrine: what to train, what not to, which open-weights models to own, and how money stays out of the weights. Model names/licences are stated as of mid-2026 (verified July 2026) and marked **[re-verify at build]** where they must be re-confirmed against the live model hub before committing.*

---

## 1. The core principle: fine-tune open weights, own the result

You require **downloadable, ownable weights**. That rules out fine-tuning closed models and rules *in* a clean path:

> **Fine-tune a permissively-licensed open model (Apache-2.0 / MIT) with your data → you own the resulting checkpoint outright, forever.**

The base model authors gift commercial rights under those licences; your fine-tune (a LoRA adapter or merged checkpoint) is yours. No vendor can deprecate it, price you out, or revoke access. Frontier APIs remain useful — for bootstrapping data, extraction, QA, and inference fallback — but they are **tools**, not the asset. The asset is your dataset + your adapters.

---

## 2. From-scratch training: the verdict

**No — for every model class that matters. Not feasible, and not necessary.**

| Model class | What "from scratch" needs | What you have | Verdict |
|---|---|---|---|
| LLM | ~10–36 **trillion** tokens; even a weak 1B model ≈ thousands of GPU-hours (~£20k+) to land *worse* than a free 0.6B open model | ~10–50M report tokens (+ noisy email tokens) | **Indefensible — 4–6 orders short** |
| VLM | Hundreds of millions of image-text pairs **plus** a pretrained LLM inside | ~10⁴–10⁵ images | **Indefensible — 3–4 orders short** |
| Small vision head | Could train a small classifier on 10⁴–10⁵ images | same | **Feasible but still wrong from zero:** a *pretrained* backbone (SigLIP/DINOv2) + a small trained head wins with far fewer labels |

**Conclusion:** nothing is trained from random initialisation. "Owning weights" is achieved by *fine-tuning*, not by pre-training. The only "trained-by-you" component is a lightweight photo-QC classifier **head** on frozen pretrained features.

---

## 3. The decision ladder (how each capability is built)

For every capability, climb only as far as the evidence justifies:

```
1. Deterministic code / lookup tables   ← arithmetic, rates, VRN→API, templating. ALWAYS first.
2. Frontier API + tools + RAG           ← day-one workhorse for parsing, drafting, reasoning.
3. Fine-tuned open weights (LoRA SFT)   ← ONLY where (2) underperforms on the eval set (05).
4. Preference tuning (DPO/KTO)          ← once engineer-edit data accumulates.
```

Promotion from one rung to the next requires a **measured gap on the golden set** (`05`). This prevents spending GPU money on problems a frontier API already solves.

---

## 4. Task decomposition → model portfolio

The mistake to avoid is "one big model does the case." The right design routes each capability to the cheapest thing that works. Legend: **(code)** deterministic · **(API)** frontier+tools · **(VLM)** fine-tuned open vision-language · **(LLM)** fine-tuned open text · **(head)** small trained vision head.

| Capability | Day 1 | Target | Notes |
|---|---|---|---|
| Format conversion (.msg/.docx/.pdf) | code | code | Parsers, never ML. OCR the ~55 scans via a VLM. |
| Instruction/email → intake JSON | API | LLM (8B LoRA) | 6+ formats; API bootstraps silver labels, own model takes over after ~1k validated docs. |
| Photo QC & completeness | head+code | head+code | 10–12 shot-type classes on frozen backbone; blur = Laplacian; dedupe = pHash. CPU, <50ms/img. |
| Plate (VRN) OCR | API/VLM | VLM | VLM reads it; validate with UK plate regex. |
| **Vehicle identity** | **code** | **code** | **VRN→DVLA VES + MOT History + UKVD beats vision.** Vision VIN read = cross-check only. |
| VIN / odometer reading | VLM | VLM+code | Validate VIN vs UKVD; odometer vs MOT mileage curve (fraud flag). |
| Damage area + magnitude | API | VLM (8B→30B) | Multi-image; enum-constrained output. |
| Parts / repairs / operations lists | API | VLM + code rules | Model drafts; rules inject ADAS-calibration/corrosion/taxi-plate ops from vehicle spec + flags. |
| Labour hours | API draft | code (retrieval+GBM) | See §7 — structured prediction with uncertainty, not free-text. |
| Repair vs total loss, Cat N/S, salvage | code | code | Arithmetic once components known; fit the firm's threshold from history. |
| Roadworthy Y/N | API | code+LLM | Rule-shaped checklist; your UN-roadworthy phrase library as output vocabulary. |
| Valuation (Engineer's Value) | code | code + tiny model | Guide lookups + your adjustment rules (−£200/10k, condition). **Never in LLM weights.** |
| Engineer's Comments (1–4 sentences) | API | **LLM LoRA** (flagship style asset) | SFT on (payload → comment); thousands of pairs available. |
| Rebuttals / PAV / query defences | API+RAG | LLM+RAG | Argumentation from playbooks; DPO from sent-vs-draft. |
| Report rendering, VAT, totals | code | code | Template engine + Decimal arithmetic, unit-tested. Models never do the maths in the document. |

**Read this table as the anti-over-engineering guardrail:** most of the case is `code` + `API`. Fine-tuning is concentrated where your *moat* is — reading your photos, writing in your voice, arguing your way.

---

## 5. Open-weights model candidates (mid-2026)

### Vision-language (VLM)

| Model | Sizes | Licence | Fit |
|---|---|---|---|
| **Qwen3-VL** *(primary)* | 2B/4B/8B/32B dense, 30B-A3B & 235B-A22B MoE | **Apache-2.0** | Best multi-image handling, native dynamic resolution, 256K context, strong OCR (VIN/odometer/screenshots). Clean ownership. 8B released Oct 2025. |
| GLM-4.5V | 106B-A12B MoE | **MIT** | Document-understanding SOTA-class; too big to self-host cheaply — candidate **open teacher** for distillation. |
| InternVL3 | up to 78B | Mostly permissive; **check per-checkpoint** | Strong benchmarks; MIT-ish alternative — verify each checkpoint's licence. |
| Qwen2.5-VL | 7B/32B/72B | 7B & 32B **Apache-2.0**; 72B Qwen licence | Proven fallback; use 7B/32B for clean ownership. |
| Mistral Small 3.x | 24B (vision-capable) | **Apache-2.0** | One model for VLM+LLM duty; clean licence. Fallback. |
| Gemma 3 | 4B/12B/27B | Gemma Terms (commercial OK, custom) | Cheap fixed image-token cost (good for 20-photo cases) but weaker "ownership" than Apache. Fallback. |
| Llama Vision | 11B/90B | Llama Community; multimodal EU-domicile caveat | Effectively single-image; poor fit for 10–20-photo cases. **Skip.** |

### Text (LLM)

| Model | Sizes | Licence | Fit |
|---|---|---|---|
| **Qwen3** *(primary)* | 0.6–32B dense, 30B-A3B, 235B-A22B | **Apache-2.0** | Strong JSON/instruction following; same family as the VLM (one serving stack). |
| gpt-oss | 20B (3.6B active), 120B | **Apache-2.0** | Strong reasoning per active param; good rebuttal-drafting fallback. |
| Mistral Small 3.x / Magistral Small | 24B | **Apache-2.0** | Clean fallback. |
| DeepSeek-V3.x / R1 | 671B MoE | **MIT** | Not for self-hosting; recommended **open teacher** for distillation (no frontier ToS exposure). |

> **Recommendation — standardise on one base.** Use **Qwen3-VL-8B** with per-task **LoRA adapters** (parsing, damage/parts, comments). A VLM is also a competent text model, so one base = one serving deployment (vLLM multi-LoRA). **Escalation tier:** Qwen3-VL-30B-A3B (MoE: 32B-class quality at ~3B-active inference cost) or 32B dense, used only where the 8B underperforms. **[re-verify the Qwen3-VL checkpoint lineup and licences at build time]** — the open-model frontier moves monthly; the *dataset* is the durable asset, adapters are disposable and re-trainable on a newer base in an afternoon.

### Multi-image token budget (a decisive detail)

Your cases have **10–20 photos**. Qwen-family dynamic resolution costs ≈ `(H/28)×(W/28)` tokens/image **[re-verify exact patch math]**: a 1280×960 photo ≈ ~1,550 tokens, so 16 raw photos ≈ 25k tokens — trainable but wasteful, since WhatsApp recompression means little real signal above ~1MP. **Policy:** the shot-type classifier routes downsampling — damage close-ups + VIN/odometer at 1024px long side (~1,300 tokens), whole-vehicle/context at 640px (~500 tokens), documents/screenshots at 1280px. Budget ≈ **11–14k image tokens/case** for training, ≤20k at inference. If VRAM binds, Gemma 3's fixed 256 tokens/image (20 photos = ~5k tokens) is the cheap alternative, at the cost of fine detail.

---

## 6. Training recipes

- **Method: LoRA** (not full fine-tune, not QLoRA unless VRAM-bound). For 8B: LoRA r=32, α=64, dropout 0.05, all linear layers, lr 1e-4, cosine, bf16, **vision tower frozen** — fits 1×H100-80GB (or 2×A100). Unfreeze the last 2–4 ViT blocks only if damage recognition underfits (the one plausible reason: WhatsApp-artifact domain shift). QLoRA (NF4) only when forced onto ≤48GB cards. **Full FT: no** — 8× cost, forgetting risk, no expected gain at these data sizes. For 30B-A3B/32B: QLoRA on 1–2×H100 or LoRA on 4×A100.
- **Data format:** one conversation per case. *System* = task spec + fixed vocabularies (impact-area enum, magnitude enum, operations catalogue) + JSON schema. *User* = interleaved tagged images + parsed instruction + DVLA/UKVD spec JSON + MOT history. *Assistant* = the structured payload (impact area, magnitude, roadworthy, parts[], repairs[], operations[], labour-hours-by-line, repair-days, hidden-damage-risk, comments). **No £ anywhere in the targets.** Enforce the schema at inference with **constrained decoding** (vLLM guided decoding / xgrammar) so malformed JSON is impossible.
- **One multi-task adapter first**, split into per-task adapters only if eval shows interference.

### Dataset minimums (realistic)

| Capability | Minimum viable | Comfortable |
|---|---|---|
| Instruction parsing | 500–1,000 docs | 2,000+ |
| Shot-type classifier | 2,000 images | 5,000 |
| Damage area/magnitude | 1,500 cases | all (3–10k) |
| Parts/ops generation | 3,000 cases (long tail of parts is the constraint) | 5,000+ + retrieval for rare parts |
| Labour hours (retrieval/GBM) | 2,000 cases | all |
| Comments style | 1,000–2,000 pairs | 5,000 |
| Rebuttals | 200 pairs + RAG | 1,000 |

### Curriculum

1. **Stage 0 — silver labels at gold quality.** Parse your own standardised reports (code + frontier assist) into payload JSON; validate **deterministically** (lines sum to totals, VAT checks, enum membership). Because the *output* is your own work product, these are effectively gold labels obtained at silver cost.
2. **Stage 1 — SFT** on validated pairs.
3. **Stage 2 — engineer-in-the-loop.** Production corrections accumulate as fresh gold; refresh adapters quarterly.

---

## 7. Labour hours — is it learnable?

**Yes, with decomposition, and kept explainable for court:**
- **Replacement/standard operations** → **nearest-neighbour retrieval** over your own history (same panel + operation + vehicle segment → median + IQR). Accurate *and* defensible ("median of 14 comparable cases").
- **Repair/blend/judgement hours** → **gradient-boosted trees (LightGBM/CatBoost)** over (segment, panel, magnitude, ops, vehicle age, **assessment year**) with **quantile outputs (P10/P50/P90)** so the copilot shows a range, not false precision.
- At ~5,000 cases × ~8 lines ≈ 40,000 line items — adequate for GBM; thin for rare panels (fall back to retrieval + engineer).
- **Never train on Thatcham times** (Tier-C, no-copy). The `assessment_year` feature lets the model capture genuine complexity drift (ADAS/EV uplift) separately from rate inflation.

---

## 8. Preference tuning — with one critical caution

- **Do NOT** naively DPO original→amended pairs with `chosen = amended`. The amendment encodes strip-down findings the model **cannot see** in the photos; training toward it teaches **hallucination of hidden damage**. Correct uses:
  - **Hidden-damage risk head** — P(strip-down adds parts | photos, zone, vehicle) + likely categories, learned from the amendment deltas (if ~10–30% of cases amend, that's 500–1,500 positives; AUC ≥0.75 plausible — *uncertain until measured*).
  - **Reserve-uplift calibration.**
- **The clean preference signal is engineer edits** of model drafts: **KTO** on per-field accept/edit events (works unpaired) after ~500–1,000 events; **DPO** (β=0.1, lr 5e-6, 1 epoch, + an SFT anchor loss) for comment style on draft-vs-final pairs.
- **`Bad Jobs.xlsx`** → hard-example mining + a permanent regression suite.

---

## 9. Distillation & continued-pretraining policy

- **Distillation ToS.** Frontier providers' terms generally prohibit using outputs to train *competing* models **[re-verify current wording]**. A narrow internal damage-assessment model is arguably non-competing, but the robust posture is: use frontier models to **extract/verify what is already in your own documents** (your IP; low exposure), and where teacher *rationales* are wanted, use an **MIT open teacher** (DeepSeek-V3.x/R1, GLM-4.5V) — zero ToS exposure. Log provider + date for every synthetic token so contested data can be regenerated.
- **Continued pretraining on the domain corpus: skip.** 10–50M tokens won't meaningfully move an 8B model; forgetting risk is real; and the tempting additions (Thatcham/OEM) are **licence-prohibited** from training (Tier-C). SFT + RAG covers it.

---

## 10. Keeping money and drift out of the weights (your explicit concern)

**In the weights (era-stable):** perception (damage/area/magnitude), parts & operations vocabulary, labour-hours judgement (with an `assessment_year` feature), roadworthiness reasoning, comment/rebuttal style.

**Never in the weights — in versioned tables/APIs keyed by assessment date:** ABP labour rates, parts prices, valuation-guide values, VAT rate, total-loss thresholds, salvage curves, and FOS/FCA/ABI rule versions.

**Render-time contract:** the model outputs **hours + parts + operations**; deterministic code computes **£** from the table version effective on the assessment date; every report persists a manifest `{base_model, adapter_hash, rate_table_version, guide_snapshot_ids, prompt_hash, schema_version}`. Historical evaluation is **era-matched** (a 2023 case scored with 2023 tables), so any £-delta measures *model error*, not inflation. Full mechanism and schema in `04`; era-matched scoring in `05`.

---

## 11. Serving

- **Day one:** frontier API (pennies/case; see `06`).
- **Owned weights:** serve on **scale-to-zero serverless GPU** (~£15–40/month at your volume) with **vLLM multi-LoRA** (one base, swap adapters per task) and **temperature 0 + constrained decoding** for reproducibility. 24/7 dedicated GPU is not justified at 50–200 cases/month (`06`). On-prem only if data-residency/court posture or 10–50× volume growth demands it.
- **Fallback chain at every model stage:** constrained-decode retry → escalate 8B→30B → frontier API → human. Low-confidence / out-of-distribution cases (new model, EV high-voltage damage, borderline total-loss ±10%) auto-escalate and are flagged for the engineer.

---

## 12. Catastrophic-forgetting guards & versioning

- LoRA itself bounds drift; mix **5–10% general instruct data** into SFT; gate releases on a small IFEval + MMLU-subset regression (**<2–3 point** drop allowed).
- The base model stays **frozen and versioned**; the **adapter is the release unit**. Keep a **per-model training-data manifest** (which case IDs trained which adapter) — this is what makes GDPR erasure and court disclosure tractable (`04`, `07`).

---

## Summary

Train nothing from scratch. Fine-tune **Qwen3-VL-8B** (Apache-2.0) with LoRA adapters for the three things that are actually yours — reading your photos, writing your comments, arguing your rebuttals — and only after a frontier baseline proves it's needed. Keep every pound and every rule in dated tables, computed in code. You end up owning a small, portable, court-reproducible model plus the dataset that regenerates it on any future base.

*Next: `04-data-pipeline-and-governance.md` — the pipeline that produces this training data safely, and the Case Record schema that ties `03`/`04`/`05` together.*
