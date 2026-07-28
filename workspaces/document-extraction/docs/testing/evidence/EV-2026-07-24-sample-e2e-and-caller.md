# EV-2026-07-24 — opaque sample E2E and CollisionSpike caller boundary

Scope: privacy-preserving evaluation of the twelve authorised local PDF/EML/MSG copies and an opt-in adjacent CollisionSpike Web adapter. This record explicitly captures failures; it is not caller acceptance, deployment, live verification or permission to publish operational data.

## Opaque local samples

The Release CLI processed four PDF, four EML and four MSG inputs twice under the default 10 MiB policy and a 30-second internal deadline. All inputs were below 10 MiB and no reparse point was present. Across 24 corrected invocations:

- 12/12 canonical results and asset sets were deterministic across retry;
- 24/24 bundles passed JSON parsing, asset hash/length, declared-file and path-containment checks;
- zero exception or staging leak occurred; and
- observed wall time was 296–4,514 ms; peak working set was not collected.

Outcomes before cohort-specific corrections:

- EML: one `Complete`, three `Partial`; all four had non-empty evidence; aggregate issues `EML_HTML_ACTIVE` ×4 and `NESTED_INCOMPLETE` ×3.
- PDF: four `Partial`; all four had non-empty text/metadata/assets; aggregate issues `PDF_INLINE_IMAGE_PASSIVE` ×32 and `PDF_XOBJECT_NOT_INTERPRETED` ×372.
- MSG: four `Corrupt`; no text/assets/participants; aggregate issues `DETECTED_CONTAINER_CORRUPT` and `FILENAME_HINT_MISMATCH` ×4 each.

The first attempt was rejected at CLI usage validation because it supplied an invalid policy identifier; it produced no bundle or staging leak and is not counted as extraction evidence.

MSG diagnosis showed a standards-compatibility defect: all four CFB files used a red root directory entry permitted by MS-CFB. The corrected reader passes 139 Storage tests and all four now parse/detect as Outlook items. At this intermediate evidence point a downstream Outlook value-decoding exception and PDF/EML cohort issues remained, so the required “all samples fully extract” gate was **failed**. That failure is retained here as chronology and was not the final result.

## Corrected final cohort result

After the CFB root-colour, MAPI `PtypGuid`, nested DOCX, MIME boundary, PDF inline-image/XObject and cumulative nesting corrections, the same opaque cohort was processed twice again:

- PDF: 8/8 invocations `Complete` and deterministic; 254 passive assets, 74,361 ordered segments and 97,534 extracted characters in aggregate; the 32 retained inline-image notices are informational;
- EML: 8/8 invocations `Complete` and deterministic; HTML references remained passive and no relationship was retrieved;
- MSG: 8/8 invocations `Complete` and deterministic, including both embedded DOCX documents; retained dependency/drawing notices are informational; and
- total: all 12 authorised samples were `Complete` on both runs, with no timeout, exception, missing result or silent engine fallback.

This closes the authorised twelve-file sample gate only. It is not full format conformance, differential verification, hidden-holdout evidence, CollisionSpike acceptance or proof that every feature-matrix row is complete.

The corrected evaluation used the already-built Release CLI, one exact copied input and one new ignored output directory per operation, equivalent to `dotnet run --project .\src\CollisionDocNet.Cli\CollisionDocNet.Cli.csproj --configuration Release --no-build -- extract --input <exact-sample-path> --output <new-ignored-bundle-directory>`. The default policy enforced the 10 MiB input ceiling and 30-second internal deadline. A separate final repository audit deliberately did not reopen the sensitive samples; it verified the committed code and ordinary offline suite instead.

## CollisionSpike opt-in Web path

The adjacent repository contains an additive `CollisionDocNetQdosSourceReader` and `Features:CollisionDocNetExtractor` gate, disabled by default. Evidence reported by the scoped implementation agent:

- custom opt-in Web integration tests: 5/5 passed;
- existing default multi-format Web tests: 31/31 passed;
- architecture tests: 29/29 passed;
- Infrastructure and Worker Release builds: zero warnings/errors.

The custom tests cover EML/DOCX translation, ordered fragments, a real synthetic `POST /Intake/Qdos` Core assessment, unsupported mapping, cancellation, DI resolution and no content leak. The Worker is not `Called`: it has no authorised Qdos trigger/caller. The sibling project references require an adjacent converter checkout and are not a portable release dependency. Global custom activation is not accepted because legacy PdfPig/MimeKit-specific expectations and broader outcome/assets/nesting gates remain.
