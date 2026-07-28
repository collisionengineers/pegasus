# Ingestion, Extraction, Deduplication and Provenance

## Executive conclusion

Before model training, the raw folders and inbox need a reproducible ingestion pipeline that preserves origin, chronology, source role, duplicates and report versions. Without it, apparently strong training metrics can be caused by the same evidence appearing in both training and test data or by later information leaking into an earlier assessment.

The correct output is an immutable artifact layer plus a governed canonical case graph. Task-specific training datasets should be generated from that layer under explicit inclusion rules rather than assembled manually from loose files.

## Objective

Create a repeatable pipeline that converts raw case folders and inbox material into immutable artifacts, structured case records and task-specific datasets without losing evidential provenance.

## Recommended pipeline

```text
Authorised source
    → immutable raw landing zone
    → malware and file-integrity checks
    → hashing and duplicate detection
    → format-specific extraction
    → entity and case linking
    → redaction/pseudonymisation
    → quality validation
    → governed case store
    → task-specific dataset build
```

## Raw landing zone

Raw files should be copied into write-once or versioned storage. Preserve:

- original filename;
- original folder or mailbox path;
- received and modified timestamps;
- sender and attachment relationship;
- byte hash;
- source system;
- access-control class;
- collection-authorisation record.

The raw landing zone should not be the training dataset. It is the evidence and recovery layer from which controlled derivatives are produced.

## Format-specific extraction

### PDF

Use a tiered process:

1. extract with a robust PDF renderer/text engine;
2. detect malformed or implausible text;
3. classify pages as narrative, table, estimate, embedded image, fee note or boilerplate;
4. apply OCR to image-only or failed pages;
5. preserve page coordinates for evidence citations;
6. validate extracted totals and key identifiers.

The sample demonstrates that basic PDF parsers can return malformed Unicode glyph names while PDFium-style extraction returns readable text. Extraction quality must therefore be measured rather than assumed.

### Images

For each image:

- decode and verify integrity;
- record dimensions, format and colour mode;
- correct orientation if reliable metadata exists;
- preserve the untouched original;
- create standardised derivatives for models;
- calculate cryptographic and perceptual hashes;
- detect embedded text, plates, faces and other privacy-sensitive regions;
- link the image to its email or evidence event.

Do not infer capture time from a WhatsApp filename without marking it as filename-derived and unverified. The sample images do not retain EXIF.

### MSG and EML

Extract:

- sender and recipient addresses;
- sender organisation and role;
- sent/received timestamps;
- subject and conversation identifiers;
- plain and HTML bodies;
- attachment names and hashes;
- quoted-message boundaries;
- signatures and disclaimers;
- mailbox folder and case-linking evidence.

Long signatures, confidentiality notices and repeated quoted threads should be retained in the raw artifact but removed or segmented from most training views.

### DOCX

Extract paragraphs, headings, tables, lists, hyperlinks, embedded images and comments. Preserve the order of document blocks. Several instruction and template documents use tables or highlighted fields, so plain paragraph extraction is insufficient.

### XLSX and ODS

Extract:

- workbook and sheet names;
- cell values and formulas;
- named ranges;
- hidden rows/sheets;
- effective date or version text;
- table regions and units.

Formula results and formulas should both be retained. A model should not silently learn an old spreadsheet result when the governing formula is available.

### Box Note

Box Notes in the sample are JSON structures. Extract text nodes, headings, lists and author/version metadata where present. Empty or placeholder notes should remain explicitly empty rather than being treated as extraction failures.

## Deduplication

Three duplicate classes should be managed separately.

### Byte-identical duplicates

Use SHA-256 to identify exact copies. Retain each source relationship while storing one content object.

### Visually equivalent images

Email systems, messaging applications and PDF creation can resize or recompress the same photograph. Use perceptual hashes and embedding similarity, followed by conservative verification.

Never delete a source reference simply because content is equivalent. The fact that a photograph was supplied in a particular message may be evidentially important.

### Semantic document duplicates

Reports may differ only by a reference, date, fee note or amendment. Use structural comparison to identify:

- exact duplicate;
- template-equivalent;
- version of the same report;
- audit derivative;
- amended or addendum version.

These relationships are labels, not reasons to discard the files.

## Case linking

Link artifacts using multiple signals:

- Collision Engineers reference;
- instructing-party reference;
- VRM and VIN;
- claimant/client token;
- accident date;
- email thread;
- attachment origin;
- folder name;
- report metadata.

No single identifier is sufficient. Registrations may contain spaces or plate changes, references may be reformatted and filenames may be incorrect.

Link confidence should be recorded, with low-confidence cases queued for human confirmation.

## Redaction and pseudonymisation

Create different controlled derivatives:

1. **Operational copy:** necessary identifiers retained for live work.
2. **Analytics copy:** direct personal identifiers replaced with stable tokens.
3. **General model-training copy:** only task-required features retained.
4. **Demonstration copy:** fully anonymised and manually checked.

For vision tasks, plate and face masking may be appropriate unless recognition of that region is the explicit authorised task. Keep the masking transform and source relationship reproducible.

## Quality gates

An artifact should not enter a training build until:

- extraction succeeded or failure is explicitly classified;
- case linking is confirmed above a defined threshold;
- duplicate relationships are resolved;
- rights/access class permits the intended use;
- personal data is minimised for the task;
- timestamps and source role are present;
- version relationships are known where applicable;
- required labels pass schema validation.

## Lineage manifest

Every dataset release should produce:

```yaml
dataset_release:
  name:
  version:
  built_at:
  purpose:
  source_snapshot:
  query_or_selection_rules:
  included_case_ids_hash:
  schema_version:
  redaction_version:
  annotation_version:
  split_definition:
  file_hashes:
  known_limitations:
  approvers:
```

This allows a result to be reproduced, audited or deleted without manually reconstructing a folder selection.

## Conclusion

The ingestion pipeline is not administrative overhead. It determines whether the resulting model learns genuine engineering relationships or merely memorises duplicated documents, later evidence and source-specific templates. Provenance-preserving extraction should be the first engineering workstream.
