## C02 — assumptions and deviations (implementer, attempt 1)

- [ ] ASSUMPTION 1 (C02 implementer, attempt 1): a field with two readings the
  document itself supports is recorded `SourceCandidateDisposition.Ambiguous`,
  not `Conflicting` — because the C02 invariant says "multiple supported
  interpretations are Ambiguous" and reserves conflict for a candidate that
  contradicts a confirmed staff/Engineer fact; C01 had it as Conflicting and its
  one assertion in `AnalyzeRetainedInstructionTests` was corrected with a
  comment. Alternatives: leave C01's mapping and map Ambiguous only in the B
  projection (two owners disagreeing), or add a fifth disposition (widens a
  frozen contract).
- [ ] ASSUMPTION 2 (C02 implementer, attempt 1): the e-mail reader keeps the
  retained body whole as its first fragment (unchanged text, unchanged label,
  now carrying a `CurrentBody` locator whose region bounds the current message)
  and emits the quoted history as an ADDITIONAL fragment after it — because a
  true split would move the provider's original from the first fragment to the
  second and change which candidate wins document order in the existing QDOS
  corpus output, which the plan requires to stay unchanged. Alternatives: split
  the body in place (changes existing extraction ranks), or record only a
  boundary offset and emit no second fragment (fails the "separate fragments"
  expectation).
- [ ] ASSUMPTION 3 (C02 implementer, attempt 1): the durable OCR operation id IS
  the external-work item id, and the attempt count lives inside the request
  envelope stored in `QualifiedPagesJson` — because F's `IntakeOcrOperations`
  provides no work-item foreign key and no attempt column, and the vehicle
  lookup precedent already keys its request row by the work item id.
  Alternatives: a new column (would need C-F02 reopened), or a separate attempt
  table (a second aggregate the plan forbids).
- [ ] ASSUMPTION 4 (C02 implementer, attempt 1): an OCR operation scoped to a
  logical DOCUMENT VERSION fails closed with `ocr_source_unavailable` rather
  than guessing a content length — because A04's `ReadLogicalDocumentVersion`
  requires an expected content length and F's OCR storage records only the
  source SHA-256, while the pre-case INTAKE ASSET path reads its length from the
  receipt's own asset record and is fully supported. Alternatives: pass zero (a
  claim A04 would have to ignore), or query document custody from intake (a
  second owner of document identity).

**C-F02 status: no stop needed.** OCR operation/result persistence maps onto the
storage the foundation already froze — `IntakeOcrOperationEntity` /
`IntakeOcrOperations` in `V1FoundationEntities.cs` and migration
`20260906054658_V1PlatformFoundation`, with web/worker grants — plus the
existing external-work outbox for the queue row. No entity and no table was
invented. Structured candidate provenance likewise maps onto F's
`IntakeSourceCandidateEntity.LocatorJson`, widened here to a version 2 envelope.

**Deviation 1:** `src/Pegasus.Infrastructure/Persistence/EfIntakeOcrOperationStore.cs`
is a new file the C02 file map does not list, although C-F02 assigns "C store
methods" to C. The alternatives were folding EF code into the Azure adapter or
into the unrelated receipt store, both of which break ownership.

**Two follow-ups for A (C-F03), written out in full in the report:** register
`ExternalWorkKinds.IntakeOcr = "intake_ocr"`, route it to `IProcessIntakeOcr` in
`ProcessQueuedExternalWork`, and compose `IIntakeOcrProvider` /
`IIntakeOcrOperationStore` / `IProcessIntakeOcr` in DI and the Worker. C edited
none of those files.
