# Format feature plans

These plans enumerate the observable extraction surface for every required input family. They are the detailed scope behind the `EXT-*` catalogue and compatibility matrix.

- [PDF 1.0–2.0](pdf.md)
- [Legacy binary Word `.doc`](doc.md)
- [WordprocessingML `.docx`](docx.md)
- [Outlook `.msg`](msg.md)
- [RFC 5322/MIME `.eml`](eml.md)

Across every format, the only extracted payloads are ordered text and discrete images, as defined by [ADR-0004](../decisions/ADR-0004-text-and-image-output.md). Metadata, relationships, hashes, issues, outcome and measurements are control evidence. Non-image attachments and embedded binary objects are inspected/report-only or explicitly unsupported; their bytes are not emitted. “Complete” is forbidden when an encountered feature may contain required text or images and has no declared treatment.
