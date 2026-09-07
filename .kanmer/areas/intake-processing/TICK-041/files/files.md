# Files

- `src/Pegasus.Core/Intake/IntakeOcr.cs`: extend the existing source-bound request/operation and Worker command; expose deterministic retained-document initiation without another queue or provider.
- `src/Pegasus.Infrastructure/Persistence/EfIntakeOcrOperationStore.cs`: persist and compare the authorized source context and byte length in the existing request envelope; preserve current atomic external-work/result settlement.
- Existing PdfPig adapter boundary: add narrow font-resource qualification for actually used anonymous Type3 encodings. Existing scan qualification stays unchanged. TICK-085 consumes this alongside its PDF reader.
- Existing `IntakeOcrTests`, `OcrIntakeRecoveryTests` and provider/parser tests: update known contract consumers and add Case-source authorization/hash/replay/completion tests plus genuine font fixtures. No large new test harness.
- `docs/adr/0040-qualified-document-intelligence-ocr.md`, ADR-0001 metadata and ADR index, FRD-05/07, capabilities: one current OCR contract, no deployment claim.

No schema, runtime, package or new provider. TICK-085 owns parser/Web/MCP import changes; PLAT-065 owns infrastructure and live activation.
