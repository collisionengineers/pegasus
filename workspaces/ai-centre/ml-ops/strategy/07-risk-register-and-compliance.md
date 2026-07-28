# Collision Engineers — AI/ML Strategy
## 07 · Risk Register & Compliance

*The risks worth losing sleep over, and the court/regulatory playbook for a firm whose product is an independent expert opinion. The single most important idea in this document: **AI drafts, the engineer decides and signs.** Everything below protects that line.*

---

## 1. Top-10 risk register

Owner and review cadence to be assigned at kick-off; review at each wave gate.

| # | Risk | Likelihood × Impact | Mitigation |
|---|---|---|---|
| 1 | **Exposed OEM credentials** (`Manufacturers.ods`, plaintext) | High × High | Rotate all now; vault; purge from storage/backups/git; permanent ingest blocklist; secret-scan in CI (`01`, `04`) |
| 2 | **PII / special-category (injury) data reaches an API, index, or training set** | Med × High | Pseudonymise before any model touchpoint; strip health/criminal sentences; UK/EU zero-retention DPAs; DPIA (`04`) |
| 3 | **Report struck out / expert discredited over undisclosed AI use** | Med × High | AI-use policy §3; engineer verifies every figure & photo; in-report AI disclosure; provenance logging; notify PI insurer |
| 4 | **Licence breach**: Tier-C (Thatcham/OEM/guide) content in persistent RAG or training | Med × High | Three-tier corpus registry enforced at index build; Tier-C = per-case ephemeral only; output-similarity checks (`01` §7) |
| 5 | **Temporal drift**: wrong-era rates/rules/valuations in outputs | Med × Med | Versioned dated tables; £ computed in code; era-matched eval + canary tests (`04` §5, `05` §4) |
| 6 | **Train/eval leakage** via amended/audit pairs & recurring vehicles | Med × High (silent) | Incident-level splits, VIN/VRN-token grouping, variant co-location, temporal holdout (`05` §3) |
| 7 | **Desktop-label under-estimation bias** baked into models | Med × Med | Document the bias; train hidden-damage-risk head from amendment pairs; report a supplementary-damage caveat (`03` §8) |
| 8 | **Privilege/confidentiality breach** from email-archive mining | Med × High | Closed cases only + cooling-off; privilege/PII triage before ingest (`04` §7) |
| 9 | **Small-firm overspend** (GPU/enterprise before value proven) | Med × Med | Deterministic + API first; SFT gated on measured gap; monthly caps; wave go/no-go gates (`06`) |
| 10 | **Hallucinated parts/figures or leaked rival-playbook text** in client-facing output | Med × High | Constrained decoding to schema + code validators; retrieval-grounded text; EVA entity tags; hallucination gate; QA-linter before issue (`05` §6) |

---

## 2. The regulatory landscape (England & Wales, mid-2026)

Two regimes bear on this work; both have moved recently.

**Court / expert evidence (CPR Part 35):**
- An expert's report must be the **independent product of their own expertise**, with an overriding **duty to the court** (CPR 35.3), certified by a **statement of truth**; a false statement risks contempt (CPR 32.14).
- ***Ayinde v London Borough of Haringey* / *Al-Haroun v Qatar National Bank* [2025] EWHC 1383 (Admin)** (5 June 2025) sharply criticised AI misuse in litigation (fabricated citations) and confirmed the courts will treat unverified AI output as a serious professional failing.
- **Proposed rule change:** the **Civil Justice Council's February 2026 consultation** proposes amending **Practice Direction 35 to require an expert to declare, within the report, the AI tools used and the purpose** for which they were used (transcription excepted). Treat AI-use disclosure as **effectively mandatory** and design for it now.

**Data protection (UK GDPR):**
- **Data (Use and Access) Act 2025** in force (Royal Assent 19 June 2025); ICO **recognised-legitimate-interest** guidance (March 2026); ICO **automated decision-making** consultation (final guidance expected summer 2026).
- **DPIA mandatory** for this processing; lawful basis = legitimate interests + LIA; special-category safeguards; closed-cases-only training (`04` §6).

---

## 3. AI-use policy for CPR-35 reports (adopt before Wave 2)

A short written policy, applied to every report, that keeps the firm on the right side of the above:

1. **AI is a drafting aid, never the author.** The named engineer holds the opinion. No report is issued that the engineer has not read in full and independently satisfied themselves is correct.
2. **Mandatory verification checklist before signing:** every **figure** (hours, rates, values, salvage, settlement) reconciles; every **photo** is of the correct vehicle and supports the stated damage; the **vehicle identity** matches the DVLA/MOT record; the **parts/operations** are appropriate; the **comments** contain no invented facts; the correct **era rates/rules** were applied.
3. **In-report AI disclosure** (per the CJC proposal): a standard sentence stating that AI tools were used to assist drafting/collation and that all opinions and conclusions are the engineer's own, independently verified. Keep the wording current with the final PD35 amendment.
4. **Provenance logging:** for each report, an append-only (WORM) record of model inputs/outputs, the version manifest (`04` §3), guide-value snapshots, the engineer's edits, and the sign-off identity — retained ≥6 years. This is both the professional-standards posture *and* the answer to "did AI write this?" in cross-examination.
5. **Independence guardrail:** the system must **flag and refuse** pressure to bias a figure (the *"value as high as you can"* pattern). Tested in the golden set (`05` §2).
6. **No solely-automated decisions** (UK GDPR Art. 22) — the engineer is always the decision-maker.

**Cross-examination readiness — likely questions and the honest answer:**
- *"Did AI write this report?"* → "AI assisted with drafting and collation; the opinions are mine, and I verified every figure and image against source data, as our logged record shows."
- *"Can you reproduce it?"* → "Yes — pinned model version, fixed settings, and the dated rate tables produce the same output; here is the manifest."
- *"How do you know the valuation is right?"* → "It's computed from named guide sources on the assessment date, with our documented adjustments; the model does not invent figures."

---

## 4. Professional & insurance notifications

- **Inform your professional indemnity insurer** before deploying AI-assisted drafting; confirm cover.
- **Notify/keep aligned** with your professional bodies (IAEA/institute affiliations shown on reports) and monitor their AI guidance.
- **Watch** the Academy of Experts / EWI positions and judicial AI guidance as they evolve.

---

## 5. Regulatory & rates watch (assign an owner + cadence)

| Item | Why it matters | Cadence |
|---|---|---|
| PD35 AI-disclosure amendment (CJC) | Determines the exact in-report wording | Track to final rule |
| ICO ADM / legitimate-interest guidance | Lawful basis + automated-decision safeguards | Quarterly |
| **ABP rate guide** (annual) | New labour rate each January → new `abp_rate_table` row | Annual (Jan) |
| ABI salvage code / FCA valuation review / FOS positions | Rules the reports must apply, by era | On reissue |
| Guide-provider (Glass's/CAP/UKVD) terms | Valuation-automation licensing | On contract change |

The dated-tables design (`04` §5) means these updates are **data edits, not model retrains** — add a new versioned row, and historical cases stay correct under their own era.

---

## 6. Incident response

Short runbooks, owned and rehearsed:
- **PII leak** (data sent to a non-compliant service, or exposed store): contain, assess against UK GDPR 72-hour breach-notification duty, notify ICO/data subjects if required, remediate, log.
- **Licence breach** (Tier-C content found in an index/output): purge from the store, regenerate the index, review the ingest tags, notify the licensor if required.
- **Model error in an *issued* report:** correction/re-issue procedure; assess whether any reliant proceedings must be informed; root-cause via the provenance log; add the case to the `Bad Jobs` regression suite (`05`).
- **Credential exposure recurrence:** rotate, scan, and tighten access.

---

## 7. Model governance

- **Versioning & rollback:** base model frozen and versioned; the **adapter is the release unit**; every release carries its eval scorecard and manifest; roll back on regression (`05` §9).
- **Human sign-off gates** at every decision point; low-confidence/out-of-distribution cases auto-escalate to a human (`03` §11).
- **Training-data manifests** per adapter (which case IDs trained it) → supports GDPR erasure and court disclosure (`03` §12, `04` §6).
- **Annual review** of the whole stack: models, licences, DPIA, rate tables, and this register.

---

## Summary

The firm's product is *independent expert judgement*, and the law is tightening precisely around AI's role in producing it. The strategy is safe **because** it keeps the engineer as the author of record, computes figures deterministically from sourced data, logs everything for reproducibility, and discloses AI use as the courts are about to require. Do Wave 0 security this week, run the DPIA before training, adopt the AI-use policy before the copilot goes live, and keep the rates/rules watch current — and the AI programme strengthens the firm's court standing rather than threatening it.

---

*End of the strategy suite. Start at `00-executive-summary.md`; act on Wave 0 (`06`) immediately.*
