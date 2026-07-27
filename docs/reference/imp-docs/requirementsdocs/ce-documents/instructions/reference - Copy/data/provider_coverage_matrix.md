# Provider Coverage Matrix

- Generated: 2026-05-25
- Parser source: `docs/reference/raw/collisionrelateddocs/Settings Backup/providers.json`
- Job-sheet sources: `Backup of CE Job Sheet 260429.xlsm`, `Backup of CE Job Sheet 260309.xlsm`
- Mapped-principals source: `Mapped Principals.xlsx`
- Full matrix: `docs/reference/data/provider_coverage_matrix.csv`
- Parser provider fixture: `docs/reference/data/parser_provider_presets_v1.json`
- Parser corpus fixture ledger: `docs/reference/data/parser_corpus_fixture_ledger.csv` and `docs/reference/data/parser_corpus_fixture_ledger.json`
- Latest parser corpus regression report: `docs/reference/data/parser_corpus_regression_report.json`

## Summary

- Parser provider presets: 26
- Unique parser coverage codes/names: 30
- Job-sheet principal table codes: 50
- Actual job principal codes observed: 20
- Actual job principal codes not parser-covered: ACSP, OAK/AX, PRINCIPAL, WOODLANDS
- Job-sheet principal table codes not parser-covered: 30
- Mapped-only uncovered codes: 26

## Parser MVP Regression Status

- Latest corpus run: 134 files under `docs/reference/raw/collisionrelateddocs/Instructions/`.
- Reader-level blockers: 0.
- Provider presets exercised by at least one matched instruction: 26 current presets. The latest corpus report records 3 remaining `UNKNOWN` evidence/image-pack review cases.
- Short image-only instruction PDFs can use optional local Tesseract OCR when a local or bundled binary is discoverable; the six MP image-only instruction PDFs now read through `pymupdf_ocr_fallback`.
- Image-only evidence packs remain review-required and are not OCRed as instruction documents by default.
- Deterministic fallback extraction now fills blank-rule engineer-report fields where visible in text; `tests/parsertests/output1.json` records the CNX regression fixture.
- Evidence-only image packs are recorded as `not-eva-export-candidate` in the fixture ledger and cannot produce EVA JSON.
- The generated fixture ledger records expected provider status, extracted fields, review blockers, and export snapshot status for every corpus file.

## Parser Presets

| Provider preset | Parser code | Engineer report | Detect phrases |
| --- | --- | --- | --- |
| `ALISON` | `ALISON` | False | ALISON LAW SOLICITORS |
| `ALS` | `ALS` | False | Auto Logistic Solutions |
| `AMS` | `AMS` | False | AMS Solicitors |
| `AX` | `AX` | False | AX Reference |
| `BC` | `BC` | False | Baker & Coleman |
| `BLACK` | `BLACK` | False | Blackstone Legal |
| `CNX (Engineers)` | `CNX` | True | Connexus Vehicle Assessors |
| `DFD` | `DFD` | False | Davison Flynn Duke Solicitors |
| `EVA (Engineers)` | `EVA` | True | Exclusive Vehicle Assessors |
| `FW (Garage)` | `FW` | False | fairwaylegal; Inspection Location: |
| `FW (Solicitor)` | `FW` | False | fairwaylegal |
| `HDUK` | `YML` | False | HD UK NETWORK |
| `KBS` | `KBS` | False | KNIGHTSBRIDGE |
| `KERR` | `KERR` | False | kerrbrown |
| `KMR` | `KMR` | False | KMR Law |
| `MP (Branded)` | `MP` | False | Rose Hill Works; Please arrange to inspect the above vehicle at your earliest convenience. |
| `MP (Simple)` | `MP` | False | Please arrange to inspect the above vehicle at your earliest convenience. |
| `OAK` | `OAK` | False | Oakwood |
| `PCH (Lawshield)` | `PCH` | False | Lawshield |
| `PCH (Performance)` | `PCH` | False | Performance Car Hire |
| `QCL` | `QCL` | False | qc-law |
| `QDOS` | `QDOS` | False | QDOS |
| `RJS` | `RJS` | False | Robert James Solicitors |
| `SBL` | `SBL` | False | Smart Business Link |
| `SWAN` | `SWAN` | False | Swan Solicitors Ltd |
| `TEN` | `TEN` | False | tenlegal |

## Actual Job Principals Not Parser-Covered

| Code | Count | Lost cause | Reason |
| --- | ---: | --- | --- |
| `ACSP` | 1 | yes | OCR quality is too low |
| `OAK/AX` | 1 | no |  |
| `PRINCIPAL` | 2 | no |  |
| `WOODLANDS` | 2 | no |  |

## Job-Sheet Principal Table Codes Not Parser-Covered

| Code | Principal name | Inbox | Instructions | Drag into EVA |
| --- | --- | --- | --- | --- |
| `ABRAHAMS` | Abrahams Solicitors | Engineers | Attached to email | No |
| `ACSP` | Accident Specialists (Direct jobs) | Engineers | Direct they will confirm if in Storage or not - check board | No |
| `ALL` | Alliance &Cooper | Engineers |  | No |
| `AS` | Aman Solicitors Advocates | Engineers | Instructions in email | No |
| `ASLS` | Affinity Seven Law Solicitors | Engineers | Word doc in Email | No |
| `AVI` | Avisons Solicitors | Engineers | PDF/Word doc in email | No |
| `BAKER` | Baker Hardman | Engineers | Most jobs come through Omar | No |
| `CASTLE` | Castle | Whatsapp | Always received in Whatsapp | No |
| `CHECK INSTRUCTIONS` | FRAZ | On Track Whatsapp | Different Principals e.g Focus, Pebble, Swade Client Code/Insured = Owner in full on spreadsheet/Whatsapp message | No |
| `CREATE FOR EACH` | Arianna Autos | Andy's Whatsapp | Instructions in Whatsapp | No |
| `CW` | Countrywide | Andrew/Engineers | Details usually in email body | No |
| `GG` | Graham Coffey (GGP) | Engineers | Graham Coffey | No |
| `GRAHAM COFFEY (GGP)` | Graham Coffey (GGP) | Engineers | Raja /Fraz | No |
| `HTU` | HTU Assessors Ltd | Ben | Body of email | No |
| `LEX` | LEX Solicitors | Engineers | All jobs come from Hackney Solutions | No |
| `LPS` | LPS Solicitors | Engineers | Don't do many jobs for them - jobs come via Omar | No |
| `MATT` | Matt Rowland Solicitors | Engineers | Letter of instruction by email | No |
| `MBH` | MBH Solicitors | Engineers | Most jobs come from Nabeel | No |
| `N/A` | Questgates or Brownsword | Info | Value via Glass's (or Percayso if not on Glass's) Print .PDF and attach to email Check TL etc via EVA copy and paste | N/A |
| `R1AM/MOTORX` | R1AM/MOTORX | Engineers | Do not message customer for images. Whatsapp R1AM/MOTORX | No |
| `RELAY` | Relay Motor Group | Whatsapp | No instructions - Customers name and accident date | No |
| `RL` | Regent Law  Ltd | Engineers |  | No |
| `ROZZII` | ROZZII/Green Destinations | Engineers | Images with instruction | No |
| `SS` | Savas & Savage | Andrew | Instructions come via Andy | No |
| `STALLION` | Stallion | Not Applicable | In Whatsapp - Stallion Bodyworks | No |
| `TA` | Turnams | Andrew | Go direct to Andy usually image based but Andy will tell you | No |
| `TP` | Taylor Price | Engineers | PDF in email | No |
| `WIL` | Williams & Co | Andy to Engineers | Word doc in Email | No |
| `WLS` | Woodlands | Engineers | Complete Injury Claims | No |
| `ZEN` | Zenith Lawyers | Engineers |  | No |

## Mapped-Only Uncovered Codes

`ARSHED`, `BROADWAY`, `CAN`, `CLIFTON`, `EMPIRE`, `FOCUS`, `HALO`, `HVL`, `MCADE`, `MOORHEY`, `MOTORX`, `OSMAN`, `PEBBLE`, `PREMIER`, `PRESTIGE`, `PROACTIVE`, `R1AM`, `RC`, `SILVER 100`, `SKY`, `SLUG`, `SUSSEX`, `SWADE`, `VOGUE`, `WATERMANS`, `WHITELINE`
