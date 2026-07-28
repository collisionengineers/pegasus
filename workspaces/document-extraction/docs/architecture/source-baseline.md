# Source and evidence baseline

Recorded: 2026-07-23, Europe/London.

## Product source hierarchy

The extractor is specification-led. Primary format specifications define the required structures and semantics; owned conformance fixtures and tests turn those requirements into executable evidence. Independent tools and retained source trees are secondary behavioural oracles only.

Every port unit records the exact specification edition/revision or a downloaded artefact hash. “Latest” is not a reproducible version.

## Primary specifications

The complete feature decomposition and supporting references live in the linked format plans. Revisions below are the research baseline recorded on 2026-07-23; evaluation manifests pin downloaded artefacts and hashes rather than relying on a moving web page.

| Format or capability | Primary baseline | Recorded scope |
|---|---|---|
| [PDF family](../formats/pdf.md) | [ISO 32000-2:2020](https://www.iso.org/standard/75839.html), [Errata Collection 3](https://pdfa.org/sponsored-standards/), and the [archived PDF 1.0–1.7 specifications](https://pdfa.org/resource/pdf-specification-archive/) | Accept and classify the PDF 1.0–2.0 family by observed features, extensions and profile claims. PDF/A, PDF/X, PDF/UA, PDF/E and related profiles are not separate parsers. |
| CFB | [[MS-CFB] revision 12.0](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-cfb/53989ce4-7b05-4f8d-829b-d08d6148375b), 2024-04-23 | Read-only CFB major versions 3 and 4, shared by `.doc`, `.msg` and encrypted OOXML wrappers. |
| [Legacy Word `.doc`](../formats/doc.md) | [[MS-DOC] revision 12.5](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/ccd7b486-7881-484c-a137-51170af7cc22), 2026-02-17; pinned hashes in the [DOC provenance ledger](../licensing/doc-source-provenance.json) | Direct binary CFB/FIB/CLX/property extraction. Supporting baselines are `[MS-CFB]` 12.0, `[MS-ODRAW]` 12.4, `[MS-OLEDS]` 13.0, `[MS-OLEPS]` 9.0, `[MS-OSHARED]` 11.1, `[MS-OFFCRYPTO]` 14.0, `[MS-OVBA]` 15.0 and `[MS-OFORMS]` 9.1. Pre-97 Word and mislabeled `.doc` families have an explicit classification/decision unit. |
| [WordprocessingML `.docx`](../formats/docx.md) | [ECMA-376 fifth editions](https://ecma-international.org/publications-and-standards/standards/ecma-376/), `[MS-DOCX]` 22.1 (2025-11-13) and `[MS-OI29500]` 24.0 (2026-05-19) | Required independent ZIP/OPC/XML input handler covering Strict/Transitional, Markup Compatibility, extensions, encrypted CFB wrappers and passive active/external content. It is never an intermediate for `.doc`. |
| [Outlook `.msg`](../formats/msg.md) | [[MS-OXMSG] revision 18.0](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxmsg/b046868c-9fbf-41ae-9ffb-8de2bd4eec82), 2025-05-20 | Generic MAPI property evidence plus typed projections for mail, reports, meetings/appointments, contacts/lists, tasks and other Outlook item classes. `[MS-OXPROPS]`, `[MS-OXCMSG]` and `[MS-OXRTFCP]` are owned supporting inputs. |
| [RFC 5322/MIME `.eml`](../formats/eml.md) | [RFC 5322](https://www.rfc-editor.org/info/rfc5322/), [RFC 2045](https://www.rfc-editor.org/info/rfc2045/) through RFC 2049, and [RFC 6532](https://www.rfc-editor.org/info/rfc6532/) | Modern and required obsolete message syntax, internationalised headers, full MIME entity trees, transfer/charset decoding, nested/report/TNEF bodies and passive protected content. Each extension RFC is pinned by its owning unit. |
| Headless process boundary | [Microsoft unattended Office automation guidance](https://learn.microsoft.com/en-us/office/client-developer/integration/considerations-unattended-automation-office-microsoft-365-for-unattended-rpa) | Managed library plus CLI only. Office automation, external office-suite, desktop, browser, mailbox, web and hosted-service runtimes are not product dependencies. |

Unknown extensions, profile claims, optional structures and application-specific properties are retained as bounded evidence. Encountering one prevents `Complete` whenever it affects the declared supported subset; it is never silently ignored.

## Local development and research inputs

- Installed and pinned SDK: .NET `10.0.302`; .NET `10.0.300` is also installed.
- `sample-doc-files/` is not an approved fixture root. Immediate metadata indicates copied profile-style trees and potentially private material. Do not recurse into, read, move, rename, delete or publish it without a separate provenance and recoverability audit.
- Tests use manifest-scoped fixtures and explicit approved external corpus paths. Corpus tooling must reject profile, cache, application-data and reparse-point roots before enumeration.
- The DOC specification bundle is retained only under ignored `artifacts/research/doc/2026-07-24/specifications/`; [the acquisition script](../../scripts/Acquire-DocSpecifications.ps1) verifies its pinned hashes and is never part of the offline repository check.

## Pegasus intake and workspace boundary

Current source evidence establishes a development-only local caller: the `Pegasus.Web` Razor Page `POST /Intake/Upload` calls `Pegasus.Core.Intake.ProcessIntake.ExecuteAsync`. It is enabled only with the `DevelopmentOffline` runtime profile and `Features:LocalIntake`; otherwise `/Intake` returns `404`. This is not production or deployed-caller evidence.

`Pegasus.Infrastructure` owns the current intake implementation registrations and `Pegasus.Core` owns the business policy and ports. This document-extraction workspace is source-only and independently buildable: it is not in `Pegasus.slnx`, and no Pegasus application project references `CollisionDocNet`; it has no current application adapter, caller, or production consumer.

The workspace's 10 MB source limit, deterministic reviewable text/images plus control provenance, and visible incomplete outcomes are extraction-boundary constraints. A future integration may add a `Pegasus.Infrastructure` adapter only under a separately accepted integration contract and caller-backed proof; `Pegasus.Core` must retain the policy deciding whether unsupported, encrypted, corrupt, or resource-breaching content can lead to case or reference creation, and workspace code must remain free of Pegasus Core types.
