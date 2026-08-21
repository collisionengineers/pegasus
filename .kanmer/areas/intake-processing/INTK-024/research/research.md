# QDOS extraction mapping and methodology

Presented for approval; live copy (kept current) at
<https://claude.ai/code/artifact/abb2c56d-a857-474a-add5-0b6c7e1875b0>.

Amendments since first drafted: the operator added the EREF10 letter
(33742_1_LtrtoEngineerIn.pdf) to the mapping folder and confirmed EREF10 is
**Inspection + Audit** — letter title "ENGINEER NOTIFICATION (REPORT + AUDIT
REPORT)", no third-party report attached; "AUDIT REPORT NOTIFICATION" letters
are audits of the attached bodyshop report. Rules 1–4, 6, 9 below (plus a
wrapped-line prefix-subsumption fix found running the corpus end-to-end) are
merged via PR #494 / [[INTK-023]]; rules 5 and 7 await this document's
approval.

Corpus evidence for the intake-extraction remediation. Sources analyzed (all
local, `corpus/`, never committed): `corpus/qdosmapping/` (21 files, supplied
2026-08-21), the pre-existing corpus root (258 files incl. 16 further EREF
instruction emails), `corpus/cereference/` (2,269 files harvested from
collisionsuite `potential-reference-files/cereference`), and
`corpus/documentexamples/` (18 files from collisionsuite renderer reference).
Git-history search across all refs found **no** previously committed corpus —
there is no larger recoverable corpus in history.

## 1. Per-file mapping — `corpus/qdosmapping/`

### Instruction emails

| File | From | Body shape | Attachments | Extraction notes |
| --- | --- | --- | --- | --- |
| (EREF10) Mr Paul Larcombe · AMA_47857_1 · PG18 BTY | amaitland-smith@ | signature-only | LtrtoEngineerIn (33742_1, in folder) | **Inspection + Audit** (operator-confirmed); no TP report; VRM also in subject |
| (EREF12) Mr Naheem Ayeni · AMA_47701_1 | amaitland-smith@ | signature-only | LtrtoAuditEngin + Bodyshopreport + GGEestimate | Audit shape; no VRM in subject; report + estimate secondary sources |
| (EREF12) Mr Liam Kinnear · KAD__46384_1 · YD14VGJ | lbirchenough@ | **inline-letter body** | none | Letter text IS the body; ref carries double slash (KAD//46384/1) |
| (EREF13) Mr Yasaab Hussain · AKH__47764_1 | mhitchen@ | signature-only | LtrtoAuditEngin + 7 damage JPGs | Direct image attachments → case evidence (images ticket) |
| (EREF19) Lookers · JF_ND_47684_1 | nduncombe@ | signature-only | LtrtoAuditEngin + Bodyshopreport | **Org claimant**; letter is the only name source. = live QDOS26005 |
| (EREF33) Mr Lewis Robertson · TG_46848_1 | Accounts@ | message + inline letter | EngineersReport + Bodyshopsuppreport + RepairInvoice | **Not an instruction** — re-audit request on an existing case |
| (EREF5) A B C Central · JF__47847_1 · M555 MJF | mhitchen@ | signature-only | LtrtoEngineerIn + 10 damage JPGs | Org claimant + private plate + direct images |
| (EREF8) Mr Derek King · AKH_SBU_47856_1 · FC55DEL | sburton@ | signature-only | LtrtoEngineerIn | Canonical |
| (EREF9) Mr Tomasz Mydlowski · AKH_ND_47630_1 | nduncombe@ | signature-only | LtrtoAuditEngin + Bodyshopreport + 1_Images-V1.pdf | = live QDOS26007; photos embedded in a PDF (17 jpgs) |
| (EREF9) Miss Dionne Harvey · AMA_47808_1 · SB71 LSK | amaitland-smith@ | signature-only | LtrtoEngineerIn | Spaced VRM in subject and letter |

### Engineer-Triage emails

Subject grammar `Engineer Triage - Our Claim Reference {NNNNN_1}[ ,] Vehicle
Registration {VRM}`; real message bodies; 1–6 damage JPGs (three examples;
claim ref 46384 pairs with the Kinnear instruction). No current classification
predicate matches them (no "Triage Only Request" phrase) — flagged; a
predicate change is its own ticket.

### Documents

| File | Readable by | Labels present |
| --- | --- | --- |
| 29895_1_LtrtoEngineerIn.pdf (= QDOS26006 letter) | pypdf + PdfPig | Our Ref / Our Client / Our Client's Vehicle (curly ') / Registration "V2 MTM" / Date of Accident (long) + repeat block (Vehicle Registration, Accident Date numeric) + p2: circumstances paragraph, Damage Area, TP Vehicle/TP Registration, Vehicle Status. Title "ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)" |
| 33742_1_LtrtoEngineerIn.pdf (= EREF10 letter) | any | Same template; "ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)" → Inspection + Audit |
| LtrtoAuditEngin.pdf (= QDOS26007 letter) | any | Same template; "AUDIT REPORT NOTIFICATION" |
| qdos26005.pdf (= QDOS26005 letter) | any | Same template; org claimant "Lookers" |
| LtrtoEngineerIn.pdf (Harvey) | any | Same template |
| Bodyshopreport555017-V1.pdf | **PdfPig only** | Client / Vehicle / Reg No / Colour / Speedo / Claim No / repair-cost prose / guide mileage "…at 82500 Miles" (**not** the odometer) |
| qdos26005-original-report.pdf | any | Our Ref 17279; **Mileage: 28000 Miles** (true odometer row); Colour |
| Images-V1.pdf | PdfPig | Page text lists original jpg filenames; 17 embedded photos 60–320 KB |

## 2. Wider corpus shapes

- Letter template stable 2022→2026 (15 further letters, identical label set,
  curly apostrophes, spaced/private VRMs, double-slash refs).
- Subject variants: RTA form; client-first form; one bare-ref form (single
  occurrence — no rule).
- In-progress traffic shares the EREF subject — extraction stays behind
  classification.
- InspectionRequest_*.pdf (AMS shape): mapped, no rule (no AMS route).
- Solicitor letters use Claimant:/Incident Date: — already covered.
- Non-QDOS grammars (Our ref, Client/Insured, Accident date) recorded for
  future provider policies only.
- Ordinal dates ("27th April 2026") in instruction material.
- documentexamples/ + most cereference categories are CE output documents —
  renderer reference, mapped at category level.

## 3. Methodology

1. Real corpus only; tests skip-if-absent; no synthetic corpus shapes.
2. The app's MimeKit/PdfPig reader is the referee for expected values.
3. Instruction letter outranks report; report fills gaps (fragment rank).
4. Guide figures are never facts (pre-incident-value mileage ≠ odometer).
5. Third-party rows are poison for claimant fields.
6. Ambiguity still withholds — fixes remove false conflicts only.

## 4. Rule changes

Rules 1–4, 6, 9 landed (PR #494): apostrophe normalization; Claimant's
Vehicle label; typed-value canonical dedupe; TP guard; ordinal dates; policy
v3 — plus wrapped-line prefix subsumption within the winning fragment.
Awaiting approval: **5** (report labels Reg No/Speedo/Vehicle-line, scoped to
report-titled documents) and **7** (accident-circumstances paragraph). **8**
(Damage Area — evidence only, no rule) is a documented decision. Explicitly
not proposed: bare-ref subject grammar, AMS policy, non-QDOS grammars,
Engineer-Triage predicate.

## 5. Corpus housekeeping

reference/qdosmapping → corpus/qdosmapping; collisionsuite trees copied to
corpus/cereference (2,269 files, 1.6 GB) and corpus/documentexamples (18);
local only, sources untouched, nothing committed, existing corpus unmodified.
