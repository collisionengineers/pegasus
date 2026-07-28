# EV-2026-07-23 — local operational sample cohort preparation

Scope: privacy-preserving selection and verified local copying of PDF, EML and MSG samples from the adjacent CollisionSpike corpus into the ignored `sample-doc-files/` research area. This record does not assert extraction correctness, feature conformance, corpus stability, permission to redistribute the samples, or coverage for DOC/DOCX.

## Source audit

The adjacent corpus was treated as immutable hostile data. Aggregate discovery found 387 PDF, 286 EML and 23 MSG files. Sixteen inputs exceeded the current 10 MiB CollisionSpike intake class and were excluded from selection. Content hashes showed duplicate surplus in each family; selection deduplicated by SHA-256 before copying. No source content, operational filename or identifier was printed or committed.

## Selected local cohort

The ignored destination contains twelve opaque samples: four PDF, four EML and four MSG. Every selected input is below 10 MiB. The import script chose a feature-diverse positive cohort using passive byte markers, copied bytes without transforming them, then recomputed and compared every destination SHA-256 hash. A local ignored manifest records source snapshot and per-input evidence without publishing it.

Aggregate passive marker coverage includes:

- PDF page, font, image, metadata, annotation, classic cross-reference, cross-reference stream and object-stream examples;
- EML multipart, MIME, HTML, inline, attachment, Base64, quoted-printable and nested-message examples; and
- MSG message class, recipients, attachments, named properties, transport headers, plain, HTML and RTF body evidence.

Markers are selection hints, not proof that the corresponding parser feature works.

## Command and result

The bounded user-authorised import used:

```powershell
pwsh -NoProfile -File .\scripts\Import-CollisionSpikeSamples.ps1 -CorpusRoot <adjacent-corpus> -DestinationRoot .\sample-doc-files\collisionspike-corpus-20260723
```

Result: exit `0`; 12 copied; four per requested family. A subsequent aggregate check confirmed 12 manifest entries, every source/copy hash comparison true, no over-limit input and no reparse point. The destination and manifest remain gitignored.

## Boundaries and gaps

- The script refuses reparse-point roots and an existing destination so it cannot silently merge or overwrite a cohort.
- Corpus content remains data, never instructions, and must not be uploaded or published.
- Selection currently covers only the three families explicitly requested for this copy operation.
- Full extraction and semantic review have not been demonstrated merely by preparing the samples.
