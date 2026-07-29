# Licensing, source rights, and distribution gates

This document is the sole prose owner for dependency licensing, specification-source rights, clean-room constraints, and distribution gates for this workspace. It is an engineering record, not legal advice, legal clearance, or distribution approval. Machine-readable provenance remains unchanged.

## Product and distribution boundary

No product `LICENSE` or authorised `PackageLicenseExpression` exists. Local `.nupkg`, `.snupkg`, and CLI ZIP outputs are evaluation artefacts only. They must not be published, pushed to a feed, or represented as an accepted release until an authorised owner:

1. selects the product licence;
2. confirms ownership and provenance;
3. reviews required notices;
4. accepts the declared feature set; and
5. resolves the patent and source-rights gates below.

`PackageRequireLicenseAcceptance=false` records that there is presently no authorised licence text for a consumer to accept. It grants no rights.

Local sample and corpus material are excluded from build, package, and dependency-manifest inputs. No specification excerpt, package, or product artefact may be published until the applicable gates in this document are formally accepted.

## Dependency review

Scope: `EXT-PKG-001`, local framework-dependent release-candidate packaging reviewed on 2026-07-24.

### Production dependencies

The production projects contain no third-party `PackageReference`. They use the .NET 10 shared framework and base class library for format detection, containers, parsing, JSON, and the headless CLI.

The baseline candidate does not package Office, Outlook, an external office suite, a hosted service, a native extraction engine, or a third-party format parser.

The .NET SDK is pinned to `10.0.302` by `global.json`. Framework-dependent packages require a compatible .NET 10 runtime on the target host. The framework is not embedded in the baseline candidate. Microsoft runtime and SDK redistribution terms must be reviewed for the selected deployment route.

### Test and tooling dependencies

| Dependency | Pinned version | Scope | Recorded package licence | Treatment |
|---|---:|---|---|---|
| `MSTest.Sdk` | 4.0.2 | Test build and execution only | MIT | Pinned by `global.json`; absent from production package dependency groups. |
| `BenchmarkDotNet` | 0.15.8 | Opt-in performance executable only | MIT | Referenced directly only by `tests/performance`; not a production dependency. Its transitive graph is recorded in the generated dependency manifest. |

The recorded licence expressions were read from restored NuGet package metadata. Transitive test and tooling packages remain governed by their own package metadata. The generated dependency manifest is an inventory, not a licence conclusion or legal clearance.

## DOC specification sources and clean-room boundary

Scope: `DOC-R00`, owned by `EXT-DOC-001`, recorded on 2026-07-24.

### Acquired baseline and machine provenance

Nine current published specification revisions are pinned in the machine-readable provenance ledger at `docs/licensing/doc-source-provenance.json`. Its retained-byte identities remain unchanged and authoritative; its licensing and distribution review destination is this document.

The date-stamped DOCX publications and current PDFs were downloaded to the ignored local path:

```text
artifacts/research/doc/2026-07-24/specifications/
```

The acquisition script verifies all 18 files by SHA-256. Full specification publications must not be committed to or redistributed from this repository.

Microsoft warns that the associated content may change frequently. Consequently:

- the unversioned PDF URLs are secondary retained copies;
- the date-stamped DOCX publications are the normative acquisition objects; and
- hashes remain mandatory because a filename does not prove immutability.

The `Published Version` and `Previous Versions` tables on each official landing page were reviewed on 2026-07-24. Those pages are moving change-history references, not pinned normative inputs. The provenance-ledger hashes remain authoritative for the acquired bytes.

| Specification | Official landing and version-history reference |
|---|---|
| MS-DOC | `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/ccd7b486-7881-484c-a137-51170af7cc22` |
| MS-CFB | `https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-cfb/53989ce4-7b05-4f8d-829b-d08d6148375b` |
| MS-ODRAW | `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-odraw/8560795e-7759-4745-838f-f7f2ef2f1872` |
| MS-OLEDS | `https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-oleds/85583d21-c1cf-4afe-a35f-d6701c5fbb6f` |
| MS-OLEPS | `https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-oleps/bf7aeae8-c47a-4939-9f45-700158dac3bc` |
| MS-OSHARED | `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oshared/d93502fa-5b8f-4f47-a3fe-5574046f4b8d` |
| MS-OFFCRYPTO | `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-offcrypto/3c34d72a-1a61-4b52-a893-196f9157f083` |
| MS-OVBA | `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-ovba/575462ba-bf67-4190-9fac-c275523c75fc` |
| MS-OFORMS | `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/9c79701a-8c3e-4429-a139-b60ac3a1d50a` |

### Authorised implementation inputs

The DOC work is specification-led. Only the exact publications identified by the provenance ledger may serve as normative inputs unless this source-rights record is revised.

The following clean-room rules apply:

- No upstream parser implementation is an authorised source.
- A behavioural oracle may be used only in an isolated differential test after its exact identity, version, licence, and invocation have been recorded.
- Mechanical translation, copied parser structure, and copied prose are prohibited.
- A generated table requires a recorded specification section, generator revision, and output review.
- The unnamed read-only implementation mentioned in `EV-2026-07-23-wave3-doc-text.md` cannot presently be reconstructed. It is not an approved source and must not be relied on unless its identity, revision, and licence are recovered and reviewed.

The repository owner directed the programme to proceed through `DOC-I13` on 2026-07-24. That direction accepts the specification-led clean-room boundary for internal research, owned implementation work, generated tables retained as repository source, and owned testing. It does not prove that any particular capability is implemented or called, does not constitute legal advice, and does not authorise publication of the specifications or product distribution.

The accepted internal-implementation decision is recorded as `ADR-0005` in the [decision index](../../../docs/decisions/README.md).

## Microsoft notice and patent boundary

The Microsoft Open Specifications notice permits copies for developing implementations. It also permits necessary portions, included schemas, IDLs, and code samples to be distributed in qualifying implementation documentation. It does not grant a general right to republish complete specifications, grant a trademark licence, or itself grant patent rights.

At the 2026-07-24 review:

- Microsoft’s Open Specification Promise listed MS-DOC, MS-CFB, MS-ODRAW, MS-OSHARED, MS-OFFCRYPTO, MS-OVBA, and MS-OFORMS.
- The promise was limited to conforming implementations, Microsoft Necessary Claims, and covered versions.
- The promise did not address third-party rights.
- MS-OLEDS and MS-OLEPS were not found in the current Open Specification Promise or Community Promise lists.

MS-OLEDS and MS-OLEPS therefore require explicit patent and licensing review before product distribution or publication of derived tables. No patent clearance is inferred for any specification solely from its presence in a Microsoft publication or promise list.

## Required review before release or publication

Formal release acceptance remains open for:

- exact-revision patent coverage, especially for MS-OLEDS and MS-OLEPS;
- the permitted scope of committed generated tables and specification excerpts;
- Microsoft runtime and SDK terms for the selected deployment route;
- product ownership, provenance, required notices, and product licence selection;
- review and acceptance of the declared feature set; and
- an authorised reviewer’s name and acceptance timestamp.

Generated tables may be committed as owned implementation source only when all of the following are recorded:

1. the generator and its revision;
2. the exact normative input hashes;
3. the applicable specification-section mapping; and
4. independent review.

Internal direction, an engineering inventory, machine provenance, local packaging, or acceptance of a clean-room method does not by itself establish legal clearance, deployment, distribution approval, implementation by a real caller, or release acceptance.