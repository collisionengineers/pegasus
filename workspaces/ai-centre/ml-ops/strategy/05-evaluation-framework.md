# Collision Engineers — AI/ML Strategy
## 05 · Evaluation Framework

*Measurement before models. The golden set and its metrics are the gate every capability passes through — the thing that tells you whether a frontier API is already good enough (so you don't fine-tune) or not (so you do). Build this **before** any training, per the decision ladder in `03`.*

---

## 1. Why this comes first

The decision ladder (`03` §3) only works if you can *measure* the gap between a frontier API and your needs. Without a scored test set you will either over-build (train models you didn't need) or under-check (ship an assistant that quietly makes CPR-35 reports wrong). The golden set is cheap insurance against both, and it doubles as the acceptance test for every wave in `06`.

---

## 2. Golden set construction

- **Size:** 150–300 closed cases to start (grow over time). Enough for stable field-level metrics without an unaffordable labelling burden.
- **Stratify by:** report type (repairable vs total loss), instructing-party type, vehicle age/segment, damage area, and **era** (assessment year) — so metrics aren't dominated by the common case.
- **Labels:** the engineer-signed report *is* the reference for most fields (your work product = ground truth). An engineer spot-checks and, for a subset, records the "ideal" payload where the historical report had slips.
- **Include hard cases deliberately:**
  - **`Bad Jobs.xlsx`** cases → hard negatives / known-failure regression suite.
  - **Independence-guardrail cases** seeded from the real *"value as high as you can"* email → the model must **flag**, never comply.
  - **Amended cases** → held aside to test the hidden-damage-risk head (`03` §8) and to measure desktop under-estimation.
  - **In-progress cases** (`QDOS261786`-style) → a live backtest: run the pipeline now, score when the real report lands.
- **Engineer time:** budget ~150–250 expert hours across the programme to build and maintain this — it is the **critical path** (`06`), not compute.

---

## 3. Leakage rules (get this wrong and every number lies)

The split unit is the **incident (case-ID)**, not the file or the photo. Enforced by the split registry (`04` §9):

1. **Group by vehicle:** all cases sharing a pseudonymised **VIN/VRN token** go to the **same** split (a vehicle re-assessed months later must not appear in both train and test).
2. **Pin variants together:** original + amended + audit reports of one incident stay on the **same** side.
3. **Temporal holdout (primary):** train on cases up to month *M*; validate on *M+1..M+2*; test on the **most recent ~6 months**. This is the only honest test of the temporal-drift machinery and mirrors how production will actually experience new vehicles/EVs/ADAS.
4. **Secondary random split** for capacity diagnosis (is the model data-limited or capability-limited?).
5. **Boilerplate dedupe:** strip the ~60–75% template text before scoring text tasks, so the model gets no credit for reciting the statement of truth.
6. **Photo dedupe:** pHash-dedupe before splitting so the same image can't leak across sides.

At ~5,000 cases a temporal split lands roughly 4,200 / 300 / 500 (train/val/test).

---

## 4. Era-matched scoring (the temporal-drift test)

Every test case is scored **under the rules and rates of its own assessment date** (`04` §5):
- The model predicts **hours + parts + operations**; the harness computes **£** from the ABP table effective on `case.era.assessment_date`.
- A 2023 case uses 2023 rates, 2023 salvage regime, 2023 FOS guidance. A £-error therefore reflects *model error*, never inflation.
- **Canary pairs:** identical inputs differing only by assessment date must produce the correct *different* £ outputs — a direct regression test that the dated-table plumbing works.

---

## 5. Field-level metrics (structured payload)

Keyed to the Case Record schema (`04` §3). Tolerances shown are starting targets to calibrate against the frontier baseline.

| Field | Metric | Starting target |
|---|---|---|
| `impact_area` (enum) | Exact-match accuracy | ≥ 0.90 |
| `impact_magnitude` (Light/Mod/Heavy) | Within-one-class accuracy | ≥ 0.95 |
| `roadworthy` | Accuracy / F1 | ≥ 0.95 |
| `salvage_category` (N/S) | Accuracy | ≥ 0.90 |
| `status` (repair vs total loss) | F1 **given correct components** | ≥ 0.97 (it's arithmetic — residual errors must trace to a wrong component, not the decision) |
| `main_new_parts` / `repairs` | Set precision / recall (F1) | recall ≥ 0.85, precision ≥ 0.80 |
| `additional_operations` | Set F1; **plus targeted recall** on ADAS-calibration & taxi-plate ops | ADAS/taxi recall ≥ 0.90 |
| `labour_lines[].hours` | MAE, and **P10–P90 coverage** | MAE ≤ 15%; ≥ 80% of actuals inside predicted band |
| `repair_duration_days` | MAE | ≤ 1 day |
| `engineers_value` | % error vs guide-anchored truth | within ±5–8% |
| `hidden_damage_risk` | AUC / calibration (Brier) on amended cases | AUC ≥ 0.75 (*measure first*) |

**Whole-case acceptance:** the headline number is **% of fields the engineer accepts unedited** — it maps directly to touch-time saved and is the real product metric.

---

## 6. Text metrics (comments, rebuttals)

Free-text can't be graded by exact match. Use:
- **House-style rubric** (accuracy, appropriate hedging, tone, no invented facts), scored by engineers on a sample each release.
- **LLM-as-judge** with **anchored examples** (good/bad exemplars in the rubric) for cheap continuous monitoring — calibrated against the engineer scores, not trusted blind.
- **Blind pairwise preference:** engineer comment vs model comment vs frontier-draft — target the fine-tuned model **preferred ≥ 60%** over the frontier draft before it's worth deploying (`03`).
- **Hallucination checks (hard gate):** no parts, figures, part numbers, or vehicle facts in the text that aren't in the structured payload; **no Tier-C content** (Thatcham/OEM text) reproduced; **EVA entity** used correctly (internal tool vs rival firm — `01` §9).

---

## 7. System-level metrics

- **Extraction accuracy per instruction format** (solicitor letter / intake form / AX request / plain email / `.docx`) — track separately; the 6+ formats fail differently.
- **Photo triage:** completeness-gate precision/recall (does it correctly demand a missing VIN/odometer/plate shot?); shot-type classifier accuracy; blur/dedupe false-positive rate.
- **QA-linter:** precision/recall on **seeded errors** (inject arithmetic slips, status↔category mismatches, wrong-era rates, missing ADAS op) — high recall matters more than precision here, but keep false positives low enough that engineers don't tune it out.
- **Vehicle/mileage verifier:** identity resolution rate; odometer-anomaly detection vs known MOT rollbacks.

---

## 8. Operational KPIs (the business-level scoreboard)

Tracked continuously in production — these are what the owner actually cares about:

- **Engineer touch-time per report** (the headline; target a 30–50% reduction).
- **% fields accepted unedited** (rising = model improving; feeds preference data).
- **Turnaround time** (instruction → issued report).
- **Amendment rate** and **complaint rate** (must not rise — ideally fall as the QA-linter catches slips).
- **Escalation rate** (how often the pipeline hands off to a human / bigger model).
- **Cost per case** (API + serving) vs the touch-time saved.

---

## 9. Regression gates & release criteria

- **A model ships only if** it beats the incumbent (frontier baseline or prior adapter) on the field-level targets **and** doesn't regress the `Bad Jobs` suite, the independence-guardrail cases, or the general-capability check (IFEval/MMLU-subset, `03` §12).
- **Every release is versioned** with its adapter hash, dataset version, and eval scorecard (`04` §9) — so a regression can be rolled back and a court challenge answered with "here is exactly what produced this report."
- **Drift monitors** (input: make/model mix, EV share, ADAS-fitment, photo resolution; output: per-field edit rates, hours residual, parts precision/recall) alert when live performance diverges from the golden set — triggering a golden-set refresh and a possible retrain (`06` cadence).

---

## Summary

Build the 150–300-case golden set first, with strict incident-level + temporal splits and **era-matched scoring**. Measure the frontier baseline against it. Fine-tune only the capabilities where the gap is real, and re-run the same gates on every release. The single number that matters commercially is **% of fields accepted unedited** — everything else exists to move that safely.

*Next: `06-roadmap-and-costs.md` — the waves, milestones, and realistic £ ranges that put all of this on a small-firm budget.*
