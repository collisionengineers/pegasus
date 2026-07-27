# DOC specification-source and rights record

Scope: `DOC-R00`, owned by `EXT-DOC-001`, recorded 2026-07-24. This is an engineering provenance record, not legal advice or distribution approval.

## Acquired baseline

The nine current published revisions are pinned in [the machine-readable provenance ledger](doc-source-provenance.json). Their date-stamped DOCX publications and current PDFs were downloaded to the ignored local path `artifacts/research/doc/2026-07-24/specifications/`. The acquisition script verifies all 18 files by SHA-256. Full specification publications must not be committed or redistributed from this repository.

Microsoft warns that the associated content may change frequently. The unversioned PDF URLs are therefore secondary retained copies. The date-stamped DOCX publications are the normative acquisition objects, but their hashes remain mandatory because a filename is not proof of immutability.

## Published-version and change-history references

The `Published Version` and `Previous Versions` tables on each official landing page were reviewed on 2026-07-24. These pages are moving change-history references rather than pinned normative inputs; the ledger hashes remain authoritative for the retained bytes.

| Specification | Published-version and previous-version history |
|---|---|
| MS-DOC | [Microsoft landing and history](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/ccd7b486-7881-484c-a137-51170af7cc22) |
| MS-CFB | [Microsoft landing and history](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-cfb/53989ce4-7b05-4f8d-829b-d08d6148375b) |
| MS-ODRAW | [Microsoft landing and history](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-odraw/8560795e-7759-4745-838f-f7f2ef2f1872) |
| MS-OLEDS | [Microsoft landing and history](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-oleds/85583d21-c1cf-4afe-a35f-d6701c5fbb6f) |
| MS-OLEPS | [Microsoft landing and history](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-oleps/bf7aeae8-c47a-4939-9f45-700158dac3bc) |
| MS-OSHARED | [Microsoft landing and history](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oshared/d93502fa-5b8f-4f47-a3fe-5574046f4b8d) |
| MS-OFFCRYPTO | [Microsoft landing and history](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-offcrypto/3c34d72a-1a61-4b52-a893-196f9157f083) |
| MS-OVBA | [Microsoft landing and history](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-ovba/575462ba-bf67-4190-9fac-c275523c75fc) |
| MS-OFORMS | [Microsoft landing and history](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/9c79701a-8c3e-4429-a139-b60ac3a1d50a) |

## Implementation decision

- The DOC implementation is specification-led. Only the exact publications in the ledger may act as normative inputs until this record is revised.
- No upstream parser implementation is an authorised source. Behavioural oracles may be used only in isolated differential tests after their exact identity, version, licence and invocation are recorded.
- No mechanical translation, copied parser structure or copied prose is permitted. Generated tables require a recorded specification section, generator revision and output review.
- The unnamed read-only implementation mentioned in `EV-2026-07-23-wave3-doc-text.md` cannot presently be reconstructed. It is not an approved source and must not be relied on for further work unless its identity, revision and licence are recovered and reviewed.

## Microsoft notice and patent boundary

The Open Specifications notice permits copies for developing implementations and permits necessary portions, included schemas, IDLs and code samples to be distributed in qualifying implementation documentation. It does not grant a general right to republish complete specifications. It also grants no trademark licence and does not itself grant patent rights.

Microsoft's Open Specification Promise currently lists MS-DOC, MS-CFB, MS-ODRAW, MS-OSHARED, MS-OFFCRYPTO, MS-OVBA and MS-OFORMS. The promise is limited to conforming implementations, Microsoft Necessary Claims and covered versions, and does not address third-party rights. MS-OLEDS and MS-OLEPS were not found in the current Promise or Community Promise lists during the 2026-07-24 review. They require explicit patent/licensing review before product distribution or derived-table publication.

## Decision status

The repository owner directed the programme to proceed through `DOC-I13` on 2026-07-24. That direction accepts this specification-led clean-room boundary for internal research, owned implementation, generated tables retained as repository source, and owned testing. It does not constitute legal advice or permission to publish the specifications themselves.

Formal release acceptance remains **open** for:

- exact-revision patent coverage, especially MS-OLEDS and MS-OLEPS;
- the permitted scope of committed generated tables or specification excerpts;
- product ownership, notices and licence selection; and
- an authorised reviewer name and acceptance timestamp.

Until those items are accepted, no specification excerpt, package or product artefact may be published. Generated tables may be committed as owned implementation source only when their generator, exact input hashes, section mapping and independent review are recorded.

See accepted internal-implementation [ADR-0005](../decisions/ADR-0005-doc-source-and-clean-room-boundary.md) and the separate [product dependency/licence review](dependency-review.md).
