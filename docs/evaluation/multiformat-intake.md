# Multi-format intake evaluation

Evaluation date: 2026-07-23, Europe/London

Status: **Local caller evidence; not production or operator acceptance**

## Scope

This evaluation records the Development-only `POST /Intake/Qdos` caller used on
2026-07-23. ADR-0006 subsequently replaced that route with `/Intake/Upload` and
contained the QDOS rules behind the provider-neutral `ProcessIntake` use case;
the results remain historical QDOS-policy evidence. Corpus material stayed local, ignored, and
immutable. Tests upload only sanitised, hash-derived names; test output and the
inventory contain no source names or content.

No Azure service, mailbox, Box folder, or external model was called. No billed
OCR was run.

## Redacted corpus inventory

The repository-wide ignored `corpus/` inventory contains 1,192 files and
1,098,669,618 bytes. Its redacted manifest hash is
`312795590A2FED329125E1374B9C554EF13034FBD67E28439ED9E4728731197E`.
The inventory artifact is ignored at
`artifacts/evaluation/multiformat-corpus-inventory.json`.

| Format | Files | Bytes |
| --- | ---: | ---: |
| EML | 286 | 485,546,394 |
| PDF | 387 | 444,059,046 |
| DOC | 43 | 3,199,051 |
| DOCX | 19 | 22,329,915 |
| MSG | 23 | 85,953,536 |
| JPG | 9 | 1,651,773 |
| PNG | 45 | 2,888,894 |

These counts establish available format shapes, not correctness expectations.

## Before/after caller evidence

Before implementation, nine synthetic multi-format cases failed at the real Web
upload boundary. DOCX, DOC, MSG, JPEG, and PNG returned the old `.eml/.pdf`
validation page; nested/attachment image provenance and the MIME nesting guard
were absent.

After implementation and independent review fixes, 22 synthetic Web integration
cases pass:

- DOCX text reaches deterministic QDOS field extraction;
- corrupt DOCX is a visible terminal `Unsupported` result;
- DOC and MSG are retained in `Needs sorting` without a reference;
- direct JPEG/PNG remain review evidence without OCR;
- bounded nested EML retains supported attachments, inline images, and nested
  messages with provenance;
- exact duplicate image occurrences retain distinct IDs and the same content
  hash;
- a PDF with two image objects produces two downloadable image assets;
- a PDF with one raster object produces one asset; no collage segmentation is
  attempted;
- a low-text, full-page-raster PDF produces exactly one scanned-page OCR
  candidate;
- a low-text PDF without a dominant raster stays in `Needs sorting` with no OCR
  candidate; and
- MIME trees beyond 128 entities and attached-message nesting beyond eight
  levels stop with visible `intake_limit_exceeded` evidence and no reference,
  even when earlier content would otherwise confirm QDOS;
- repeated decoded nested payloads stop at 25 MB and cannot allocate a reference;
- DOCX entry-count and uncompressed-size expansion fixtures fail visibly with
  `docx_limit_exceeded`; and
- a tampered local content-addressed artifact returns a generic integrity
  conflict and is not served.

## Genuine-input smoke evidence

Five repository-wide corpus samples at or below the 10 MB Web limit are pinned by
SHA-256 and pass through the same Web caller:

| Format | Result | Scope of claim |
| --- | --- | --- |
| DOC | `Needs sorting`; no reference/OCR | Deferred container is retained visibly |
| MSG | `Needs sorting`; no reference/OCR | Deferred container is retained visibly |
| JPEG | `Needs sorting`; no reference/OCR | Ordinary image is retained, not OCR input |
| PNG | `Needs sorting`; no reference/OCR | Ordinary image is retained, not OCR input |
| DOCX | `Needs sorting`; `openxml-engine` evidence | Genuine package is readable; no field-accuracy claim |

The current genuine corpus category has 11 passing Web tests in total. One historically
low-text PDF is now explicitly `Needs sorting`, not OCR, because it has no
dominant page raster. This is the intended scan-only OCR policy.

The recorded full validation gate with required corpus evidence passed
after this increment: 11/11 Core tests, 57/57 non-corpus integration tests,
29/29 architecture tests, and 11/11 corpus tests, with no failures or skips. It
also completed the Release build, repository guards, Bicep compilation, and
project-skill validation. This is repository evidence, not deployment evidence.

## Not proved

- human-approved field-level accuracy or an untouched holdout for DOCX/EML/PDF;
- genuine PDF embedded-image separation across all encodings;
- genuine nested-EML depth/attachment combinations and malformed/encrypted
  format cohorts;
- Document Intelligence OCR accuracy, cost, retry, or confidence handling;
- Worker/Graph delivery, private Blob staging, Box custody, or production
  retention;
- automated DOC/MSG extraction or vehicle-registration OCR/VLM.

Those are separate caller-backed evaluations. The local artifact store under
ignored `artifacts/` is development evidence only and must not be described as
production custody.
