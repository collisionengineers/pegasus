# Collision Engineers — AI/ML Strategy
## 04 · Data Pipeline & Governance

*How raw case folders and the email archive become safe, versioned, model-ready data — and stay legal under UK GDPR. Defines the **Case Record schema**, the single source of truth shared by training (`03`), evaluation (`05`), and the report renderer.*

---

## 1. Target pipeline

```
                    ┌─────────────────────────────────────────────────────────────┐
   RAW              │  INGEST                                                      │
  case folders  ──► │  • format converters (.msg/.pdf/.docx/xlsx/boxnote → text)   │
  email archive     │  • OCR pass for the ~55 image-only scans                     │
  photos            │  • secret-scan (block Manufacturers.ods + any creds)         │
                    │  • pHash de-duplication of photos                            │
                    └───────────────┬─────────────────────────────────────────────┘
                                    ▼
                    ┌─────────────────────────────────────────────────────────────┐
                    │  CASE RECORD STORE  (structured, provenance + confidence)    │  ← §3 schema
                    │  real PII retained here, access-controlled (production use)  │
                    └───────────────┬─────────────────────────────────────────────┘
                                    ▼
          ┌─────────────────────────┴───────────────────────────┐
          ▼                                                       ▼
┌───────────────────────────┐                     ┌──────────────────────────────┐
│  PSEUDONYMISATION          │                     │  LICENCE-AWARE CORPUS         │
│  (before any train/API)    │                     │  REGISTRY  (Tier A/B/C tags)  │
│  → training/analytics store│                     │  → RAG index (Tier A/B only)  │
└─────────────┬──────────────┘                     └───────────────┬──────────────┘
              ▼                                                     ▼
      DATASET BUILDS (versioned, split registry §7)         RAG retrieval (07 licence rules)
              ▼
      TRAINING (03)  ─►  ADAPTERS  ─►  SERVING  ─►  RENDERER (£ from dated tables) ─► ENGINEER REVIEW ─► FEEDBACK
```

Two stores, deliberately separated: a **production Case Record store** (keeps real PII because reports need it, tightly access-controlled) and a **pseudonymised training/analytics store** (what any model or API is ever allowed to see).

---

## 2. Ingestion specifics

- **Format converters:** `.msg` (Outlook) → text + attachments; `.docx`/`.pdf` → text + embedded images; `.xlsx`/`.ods` → tables; `.boxnote` → JSON text extract. Note `Process.boxnote` is 0 bytes (skip); boxnotes embed author names + IDs (PII — strip on pseudonymisation).
- **OCR:** the ~55 image-only assets (Tesla method PDFs, FOS PNGs, ABP PNGs, Audatex pocket guide) need OCR; use a VLM for scanned pages. Tier-C scans are OCR'd only for per-case ephemeral use, never indexed.
- **Secret scanning:** hard path-blocklist for `Manufacturers.ods` + a trufflehog-class scan over the whole corpus, run at ingest and in CI. Nothing with credentials enters any store.
- **Photo de-duplication:** perceptual hash within a case (drop near-duplicates) and across the archive (feeds the fraud-reuse check, #13 in `02`).
- **Pairing:** assemble records by folder=case-ID; link input↔output by VRN; VIN as secondary key; handle the known irregularities from `01` §3 (embedded-only fee notes, 3-variant case, non-unique `1.jpg`).

---

## 3. Case Record schema v1 (the shared contract)

The spine of the system. Every field carries **provenance** (where it came from) and **confidence** (how sure), so the copilot can show sources and the QA-linter can gate low-confidence fields. **No monetary field is a training target** — £ values are *rendered* from `hours`/`parts`/`operations` + the dated rate table (§5). Illustrative shape:

```jsonc
{
  "case_id": "qdos261560",                 // = folder = Our Ref
  "schema_version": "1.0",
  "instructing_party": { "type": "AMC|solicitor|insurer", "name": "...", "your_ref": "AMA/46338/1" },
  "report_type": "repairable|total_loss",  // desktop-assessment is the default modality
  "eva_entity_tags": ["eva_software"],     // hard-disambiguate internal tool vs rival firm (01 §9)

  "vehicle": {                             // mostly from VRN→API, not vision
    "vrn": "<pseudonymised token in training store>",
    "vin": "<token>", "make": "...", "model": "...", "year": 2018,
    "fuel": "...", "engine_cc": 1997, "odometer": 72104,
    "mot_mileage_timeline": [ ... ], "is_private_hire": true,
    "source": { "make": "DVLA_VES", "model": "MOT_HISTORY", "odometer": "photo_ocr" },
    "confidence": { "odometer": 0.82 }
  },

  "assessment": {                          // ← the model's structured payload (03 §6)
    "impact_area": "left_rear",            // fixed enum
    "impact_magnitude": "moderate",        // Light|Moderate|Heavy
    "roadworthy": false,
    "salvage_category": "N",               // ABI N|S (era-aware rules)
    "status": "total_loss",                // derived by code from cost vs value (05)
    "main_new_parts": [ "Right Rear Door Skin", "A/C Condenser" ],   // plain English, no part #s
    "repairs": [ "Right Front Wing" ],
    "additional_operations": [ "ADAS front radar calibration", "Refit taxi plate" ],
    "labour_lines": [ { "op": "replace_nsr_door", "hours": 3.5, "p10": 3.0, "p90": 4.2 } ],
    "repair_duration_days": 5,
    "hidden_damage_risk": { "score": 0.34, "likely": ["front lock carrier"] },  // from amendment pairs
    "engineers_comments": "…1–4 bespoke sentences…",
    "provenance": { "impact_area": "vlm", "comments": "engineer_edited" },
    "confidence": { "impact_area": 0.91, "salvage_category": 0.88 }
  },

  "valuation": {                           // tool lookups + firm rules, NOT weights (03 §10)
    "guide_values": { "cap": 4425, "glass": 4500, "autotrader": 4600 },  // dated snapshot
    "engineers_value": 3540, "revaluation_count": 1,
    "adjustments": [ { "rule": "mileage_-200_per_10k", "delta": -400 } ],
    "guide_snapshot_id": "val_2026-06-25_...", "source": "eva_screenshot|api"
  },

  "era": {                                 // makes temporal drift explicit (05 era-matched scoring)
    "assessment_date": "2026-06-25",
    "abp_rate_table_version": "2026.1",    // £83.28/hr bodyshop
    "salvage_regime_version": "ABI-2017",
    "fos_guidance_version": "2023-09"
  },

  "manifest": { "base_model": "...", "adapter_hash": "...", "prompt_hash": "...",
                "renderer_version": "...", "reviewed_by": "<engineer>", "signed": true }
}
```

This one object is the training target (`03`), the thing evaluated (`05`), and the renderer's input. Keep it under version control; changes are `schema_version` bumps.

---

## 4. Pseudonymisation spec (before any model/API touchpoint)

- **Consistent surrogates via keyed HMAC**, so the *same* person/VRN/VIN maps to the *same* token across cases (preserves cross-case linkage for analytics and leakage-grouping in `05`) without revealing identity. Key held separately, access-controlled.
- **Names** → realistic surrogate names; **addresses** → outcode only (e.g. `SK8`); **mobiles/emails/refs** → typed placeholders.
- **VINs/VRNs in text targets** → canonical placeholders — the model must *read them per-case* (OCR/API), never **memorise** specific vehicles.
- **Special-category stripping:** remove injury/health sentences and criminal-allegation language entirely (not just names) — see `01` §6, `07`.
- **Photos:** blur number plates and faces for the training/analytics store; **exclude signature images** entirely.
- **Re-identification test:** before release, run a re-ID check on a sample; document residual risk (UK GDPR Art. 11 caveat — don't *assume* anonymity, demonstrate it).
- **Production runtime is exempt** — reports need real PII; that store stays behind access control and is never used directly for training.

---

## 5. Temporal-fact tables (keep £ and rules out of the model)

Versioned, dated lookup tables — the mechanism behind Decision D4 (`00`) and the training principle in `03` §10:

- `abp_labour_rates` — `{effective_from, category, rate}` (bodyshop £83.28/hr for 2026; new row each January; prestige/ADAS/alloy lines too).
- `parts_prices` — dated per-case snapshots (supplier feeds or estimate-derived).
- `valuation_guides` — per-case API lookups at the assessment date; raw responses/screenshots archived by `guide_snapshot_id`.
- `vat_rate`, `total_loss_thresholds`, `salvage_curves`.
- `rules/` — FOS/FCA/ABI guidance documents, each with `effective_from`/`effective_to`, so a 2023 case is judged under 2023 rules.

The renderer reads the version named in `case.era` and computes every £ figure in code. This is what lets you re-render or re-evaluate any historical case correctly years later.

---

## 6. Governance pack (UK GDPR, as of mid-2026)

- **Lawful basis:** training on historical client files is a *new purpose* → **legitimate interests** + a documented **Legitimate Interests Assessment**, under the **Data (Use and Access) Act 2025** (in force). Assess whether the new "recognised legitimate interest" basis (ICO guidance, Mar 2026) applies; if not, standard LI + balancing test with transparency.
- **DPIA — mandatory.** AI processing of this personal data is high-risk per ICO's list; complete a DPIA *before* processing. Covers: data categories (incl. special-category), pseudonymisation, retention, third-country transfers, automated-decision safeguards.
- **Transparency & minimisation:** train on **closed cases only**; pseudonymise first; add a **data-use clause to engagement terms** going forward so future cases are cleanly usable.
- **Automated decision-making:** the engineer is always the decision-maker (no solely-automated Art. 22 decisions); watch the ICO ADM consultation (final guidance summer 2026).
- **Processors:** any hosted API used on personal data must be under a **zero-retention DPA** with **UK/EU endpoints**; log which data class each processor sees.
- **Data-subject rights / erasure:** keep the **per-model training-data manifest** (`03` §12) so an erasure request can be traced to the adapters trained on that case; small owned models make a forced retrain feasible — a quiet advantage of the open-weights path over a giant frozen vendor model.
- **Retention:** align the training store's retention with your case-file retention policy; delete on schedule.

---

## 7. Privilege triage (the email archive)

The inbox likely contains **litigation-privileged** and without-prejudice material. Sending it to a processor under a DPA doesn't waive privilege, but the safe posture is:
- **Closed cases only**, with a cooling-off period, into any training corpus.
- A **privilege/PII triage pass** that filters live/open matters and without-prejudice negotiation threads before ingestion.
- Treat the "value as high as you can" class of email as *evidence for the independence guardrail* (`05`), not as a training target to imitate.

---

## 8. Security runbook

- **Wave 0, immediate:** rotate all OEM-portal credentials in `Manufacturers.ods`; vault secrets; purge the file from storage + backups + git history; permanent ingest blocklist. *(See `01` §5, `07`.)*
- **Access tiers:** the most sensitive playbooks (e.g. the rival-"EVA" rebuttals, `Bad Jobs.xlsx`) get restricted access; the **RAG index and adapters are crown jewels** — treat as production secrets.
- **Audit logging** on the Case Record store and the corpus registry; backups + DR; secret-scanning in CI.

---

## 9. Dataset versioning & split registry

- **Immutable dataset releases** (`v0.1`, `v0.2`, …), each a manifest of case IDs + the pseudonymisation key version + the schema version — reproducible builds.
- **Split registry** enforces the leakage rules defined in `05`: splits are by **incident (case-ID)**, grouped by pseudonymised VIN/VRN token (same vehicle → same split), with amended/audit variants pinned to the same side and a **temporal holdout** (train on past, test on recent). The registry is the authority; training jobs read splits from it, never re-derive them.

---

## Summary

The pipeline's whole job is to get from messy, PII-laden, mixed-licence raw data to a **clean, versioned, pseudonymised Case Record dataset** plus a **licence-aware RAG index** — with security and DPIA handled up front. The Case Record schema is the contract that keeps training, evaluation, and rendering in lock-step, and the dated rate tables are what keep money and rules out of the weights.

*Next: `05-evaluation-framework.md` — how we measure a model against this data before trusting it.*
