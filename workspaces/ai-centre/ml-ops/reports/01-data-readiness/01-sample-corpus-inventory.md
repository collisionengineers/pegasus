# Sample Corpus Inventory and Readiness Assessment

## Executive conclusion

The supplied sample is small but unusually rich because it links remote vehicle images, instructions, professional reports, estimates, valuations, correspondence and domain references. Its main AI value is the case-level relationship between those artifacts and the version history of the engineer's conclusion.

The sample is sufficient to design a canonical schema and pilot programme. It is not large enough to estimate production model performance or prove that the wider archive has usable rights, representative coverage and reliable labels. Those questions require a governed inventory of the additional cases.

## Purpose

This report records what is present in the supplied sample and assesses its immediate value for AI development. It is an inventory of the extracted files, not an assumption that the wider archive has the same distributions.

## Quantitative inventory

The source review covered 300 files in total, including two ZIP archives that duplicated extracted
material. The approved local `corpus/ai-centre/` snapshot retains the 298 working files, which
divide into:

| Area | Files | Material observed |
|---|---:|---|
| Example case bundles | 115 | Images, instructions, emails, reports, estimates, fee notes and versioned assessments |
| Domain/reference library | 183 | Repair methods, valuation, salvage, SOPs, query responses, diminution, training and case-law material |

The eight case folders contain:

- 84 standalone JPG, JPEG or PNG images;
- 20 PDFs;
- Outlook MSG files;
- one case instruction in DOCX;
- one case-specific Box Note;
- seven completed assessment outcomes;
- one case with instructions and limited evidence but no final report in the sample.

The wider reference library contains a mixture of PDF, DOCX, XLSX, PNG, JPG, Box Note, MSG, EML, ODS and text files.

## Case-bundle structure

Although file naming differs by instructing source, the recurring workflow is recognisable:

1. an instruction arrives by email or attachment;
2. vehicle and incident details are stated;
3. photographs are supplied directly or obtained from another party;
4. external facts such as mileage, history and valuation are researched;
5. the engineer constructs a damage and repair specification;
6. a repairable or total-loss report is produced;
7. a fee note is issued;
8. later evidence or a challenge may result in an audit, amendment or reply.

The completed reports expose a comparatively rich target schema:

- vehicle make, model, registration, VIN, odometer, engine and fuel;
- assessment date and source;
- impact area and magnitude;
- roadworthy status;
- repair or total-loss outcome;
- salvage category and value;
- retail, trade and engineer value;
- new parts, repairs and additional operations;
- labour hours and hourly rate;
- labour, parts, paint, specialist, subtotal, VAT and total;
- engineer comments, condition, history and settlement reasoning.

## High-value observations

### Version history is supervision

One case includes an original report, an audit report and an amended client report. The amended version records additional damage reported after strip-down and changes parts, labour, repair duration, reserve and total cost. This is more valuable than a single final report because it supports:

- supplement-risk modelling;
- uncertainty calibration;
- analysis of what remote evidence did not reveal;
- identification of recurring amendment causes;
- model evaluation against the information available at each point in time.

Versions must not be collapsed into one final record. Each needs an effective timestamp, evidence set and parent version.

### Remote assessment must remain explicit

Collision Engineers does not perform physical inspections. Location wording in instructions or reports must not be converted into a label implying that a Collision Engineers representative inspected the vehicle physically. A canonical field should distinguish:

- image-based assessment;
- evidence supplied by a repairer, client or instructing party;
- later findings reported after dismantling or strip-down;
- external database or documentary facts;
- engineer inference from the supplied material.

### Sender role affects truth status

The emails include instructions, missing-evidence notes, repairer evidence, third-party challenges and internal/outbound engineering reasoning. One repairer-supplied message asks for a high valuation. This is an independence-control signal, not a valid target.

All email-derived content therefore needs:

- sender organisation and role;
- inbound or outbound direction;
- whether it is evidence, instruction, opinion, request or outcome;
- whether Collision Engineers adopted, rejected or qualified it.

### The reference library mixes durable and transient knowledge

The library includes manufacturer methods and salvage codes, but also dated price guides, older pocket guides, changing valuation guidance, templates and time-specific surcharge notes. These sources should not all be treated as equally current or authoritative.

Each reference needs:

- source and owner;
- publication/effective date;
- superseded date where known;
- jurisdiction and vehicle applicability;
- licensing status;
- authority tier;
- review owner.

## Technical data-quality findings

### Images

- Dimensions are adequate for classification and many detection tasks.
- The sample includes portrait and landscape images.
- No standalone case image retains EXIF.
- Some images are close-ups without sufficient vehicle context.
- Several photographs contain reflective surfaces, glare, shadows or environmental clutter.
- Some images show odometers or identifiers rather than damage.
- The same evidence may exist as a standalone file, email attachment and embedded report image.

### PDFs

- The reports are visually readable and contain text layers.
- Basic extraction can produce malformed `/uni...` sequences because of embedded font maps.
- More robust PDFium-style extraction succeeds on representative reports.
- Some pages are primarily embedded images with repeated headers.
- OCR fallback and page-type classification are required.

### Office and mail formats

- DOCX material contains useful paragraphs and tables.
- XLSX files include matrices, guide tables and a small number of formulas.
- Box Notes are JSON-backed and can be extracted directly.
- MSG files expose sender, recipient, subject, body chronology and attachments.
- EML files can contain forwarded policy or guidance material.

## Readiness by data family

| Data family | Current readiness | Main work required |
|---|---|---|
| Report fields and totals | High | Robust parsing, schema normalisation, version linking |
| Report narrative/style | High | Remove boilerplate and identifiers; classify author and approval |
| Email routing and case assembly | High | Thread reconstruction, role classification, attachment linking |
| Domain retrieval | Medium-high | Rights review, authority/date metadata, chunking and citation |
| View/quality classification | Medium | Create explicit image labels |
| Damage detection | Medium-low | Bounding boxes or masks and component taxonomy |
| Image-to-estimate learning | Low as-is | Case-level alignment, evidence states, current price separation |
| Valuation prediction | Medium | Timestamped guide evidence and leakage controls |
| Amendment/supplement risk | Promising | More versioned cases and chronology |

## Wider archive audit required

Before selecting models, the thousands-report archive should be profiled for:

- number of distinct claims and vehicles;
- reports per year and instructing source;
- images per case and image resolution;
- repairable/total-loss/category distribution;
- vehicle makes, models, ages and fuel types;
- remote-evidence source;
- proportion with Audatex or other line-item estimates;
- proportion with amendments, audits, queries and final outcomes;
- missing or corrupted files;
- duplicate rate;
- availability of current and historical guide evidence;
- data-rights and retention status.

## Conclusion

The sample is AI-useful now for extraction, retrieval, workflow automation and report QA. It becomes suitable for serious vision and multimodal fine-tuning after case-level linking and targeted annotation. The wider archive should first be turned into a governed case registry; file count alone is not a reliable measure of training value.
