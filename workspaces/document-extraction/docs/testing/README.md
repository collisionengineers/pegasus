# Test programme

The test programme proves declared extraction behaviour incrementally. Successful happy-path text extraction or a green unit suite is not evidence that an entire format is supported.

## Test lanes

| Lane | Purpose | Starts when |
|---|---|---|
| T0 harness | Deterministic builds, test discovery, manifest/schema safety and evidence capture | Now |
| T1 contracts | Detection, result/outcome taxonomy, deterministic identities/order, limits and cancellation | Shared foundations |
| T2 storage/decoding | CFB, ZIP/OPC, PDF filters, MIME transfer decoders, RTF and code pages | Owning foundation/parser |
| T3 format conformance | Specification-derived PDF, DOC, DOCX, MSG and EML feature cases | Each format unit |
| T4 end-to-end/nesting | Handler-to-common-result projection, text, images, nested supported files and issue propagation | Vertical slices |
| T5 differential | Semantic comparison against exact-version independent reference tools | Oracle and comparator approved |
| T6 security/fuzz | Malformed input, expansion/depth limits, parser fuzzing and active/external-content denial | Every parser milestone |
| T7 genuine corpus | Approved, manifested operator cohort and implementation-author-hidden holdout | CollisionSpike-facing subset |
| T8 performance | Allocation, throughput, decoded expansion, cancellation and concurrency budgets | Stable behaviours |
| T9 CLI/package | Library/CLI equivalence, process I/O, exit codes, deterministic bundles and publish variants | Public extraction API stable |
| T10 integration | Caller-owned CollisionSpike adapter and Web/Worker operational proof | Accepted public extraction package |

Differential comparisons use independently chosen, exact-version tools or specification expectations for each format. Every comparator defines normalisation, tolerated differences and diagnostic artefacts before results are interpreted; no comparator is a production dependency.

## Required format coverage

The evidence matrices in the [PDF](../formats/pdf.md), [DOC](../formats/doc.md), [DOCX](../formats/docx.md), [MSG](../formats/msg.md) and [EML](../formats/eml.md) plans are mandatory fixture catalogues. Each positive feature has absent, boundary, malformed and resource-limit companions where meaningful. Cross-product cases cover nesting, mislabelling, polyglots, duplicate evidence, cancellation and active/external-content denial. The [compatibility matrix](../compatibility/feature-matrix.md) is updated with the implementation and tests; fixture count alone is never coverage.

The headless process lane additionally proves the [CLI contract](../architecture/headless-cli-contract.md): one input only, file/stdin equivalence, library-result equivalence, stable JSON/images, rejection of non-image asset materialisation, exit-code mapping, output collision/partial-write safety, Ctrl+C, and absence of extracted content or sensitive filenames from stdout/stderr diagnostics.

## Current runner

The active tests use MSTest on Microsoft.Testing.Platform and target .NET 10. Run the managed suite from the repository root:

```powershell
dotnet test --solution CollisionDocNet.slnx
```

Run the broader deterministic repository checks with:

```powershell
.\scripts\Invoke-RepoCheck.ps1
```

The repository check requires only the pinned .NET SDK and repository-owned files. It does not require Microsoft Office, an external office suite, a desktop session, a browser or a service host.

Coverage, fuzzing, external oracles, genuine corpora, benchmarks and publish-variant tests remain explicit opt-in lanes. Framework-dependent CLI smoke tests become part of the ordinary gate when the CLI exists. Self-contained, single-file and Native AOT packages are tested separately per target runtime and cannot inherit evidence from the framework-dependent build. Add dependencies only with a licence/security record, and never make the ordinary unit lane networked or nondeterministic.

## Evidence rule

Every compatibility claim names the `EXT-*` unit, exact specification and oracle revisions, fixture IDs, command/exit result, input class, boundary exercised and known gaps. Use [the evidence template](evidence-record.md); do not replace evidence with a percentage-complete estimate.

Current local evidence:

- [EV-2026-07-23 — foundation tooling audit](evidence/EV-2026-07-23-foundation-tooling-audit.md)
- [EV-2026-07-23 — local operational sample cohort preparation](evidence/EV-2026-07-23-local-five-format-sample-cohort.md)
- [EV-2026-07-23 — five-format scope and plan validation](evidence/EV-2026-07-23-scope-plan-validation.md)
- [EV-2026-07-23 — Wave 2 CFB v3/v4 reader](evidence/EV-2026-07-23-wave2-cfb-reader.md)
- [EV-2026-07-23 — Wave 2 shared storage and detection subset](evidence/EV-2026-07-23-wave2-shared-storage.md)
- [EV-2026-07-23 — Wave 6 EML extraction subset](evidence/EV-2026-07-23-wave6-eml-subset.md)
- [EV-2026-07-23 — Wave 3 direct DOC text subset](evidence/EV-2026-07-23-wave3-doc-text.md)
- [EV-2026-07-23 — Wave 7 DOCX subset](evidence/EV-2026-07-23-wave7-docx-subset.md)
- [EV-2026-07-23 — Wave 8 MSG subset](evidence/EV-2026-07-23-wave8-msg-subset.md)
- [EV-2026-07-23 — Wave 9 PDF core subset](evidence/EV-2026-07-23-wave9-pdf-core.md)
- [EV-2026-07-23 — Wave 4 structured DOC subset](evidence/EV-2026-07-23-wave4-doc-structured.md)
- [EV-2026-07-23 — Wave 10 passive PDF surface](evidence/EV-2026-07-23-wave10-pdf-surface.md)
- [EV-2026-07-23 — Wave 5 public Extraction and CLI subset](evidence/EV-2026-07-23-wave5-extraction-cli.md)
- [EV-2026-07-23 — Waves 11–13 cross-format local evidence](evidence/EV-2026-07-23-waves11-13-cross-format.md)
- [EV-2026-07-24 — opaque sample E2E and caller boundary](evidence/EV-2026-07-24-sample-e2e-and-caller.md)
- [EV-2026-07-24 — local DOC CFB/FIB compatibility correction](evidence/EV-2026-07-24-local-doc-cfb-fib-correction.md)
