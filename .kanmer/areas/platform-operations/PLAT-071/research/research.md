# Research — PLAT-071: DOC/MSG extraction deployment status

## Question

Is automatic legacy DOC and Outlook MSG extraction implemented through a real
Pegasus caller and present in the current production deployment, making the
contrary current-state documentation stale? What limitations remain true?

## Findings

- The intended format ownership is unambiguous. FRD-05 says DOC and MSG use the
  CollisionDocNet-derived compound-file readers, while PDF remains on PdfPig,
  DOCX on OpenXml, and EML on MimeKit
  (`docs/frd/frd-05-documents-extraction-and-custody.md:8`). ADR-0025 selected
  in-application integration behind the existing Core port rather than a
  standalone package
  (`docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md:39-42,71-78`).
- The current production caller path is complete, not merely registered.
  `IIntakeSourceReader.ReadAsync` is the Core port
  (`src/Pegasus.Core/Intake/IntakeContracts.cs:585-587`);
  `ProcessIntake` invokes it
  (`src/Pegasus.Core/Intake/ProcessIntake.cs:139-143`); Infrastructure
  registers `MimeKitPdfPigOpenXmlIntakeSourceReader` behind
  `ProviderApiIntakeSourceReader` as that port and registers
  `ProcessIntake`
  (`src/Pegasus.Infrastructure/DependencyInjection.cs:445-450`). The Worker
  reaches `ProcessIntake` through the durable queued-intake path, as recorded
  by the caller comment in
  `src/Pegasus.Infrastructure/Persistence/Migrations/20260819115323_UnidentifiedWork.cs:235-241`.
- The reader routes `.doc` and `.msg` to distinct live formats and dispatches
  them to `ReadDoc` and `ReadMsgAsync`
  (`src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs:108-117,1001-1010`).
  The implementations are in
  `MimeKitPdfPigOpenXmlIntakeSourceReader.DocMsg.cs:18-170`, backed by the
  bounded parsers under
  `src/Pegasus.Infrastructure/Intake/DocumentExtraction/{Cfb,Word,Msg}`.
  Persisted provenance includes
  `collisiondocnet-doc-msg-0.1` in the reader version
  (`MimeKitPdfPigOpenXmlIntakeSourceReader.cs:19-20`).
- Caller-backed tests cover readable and fail-closed behavior.
  `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` uploads genuine
  fixture-shaped DOC and MSG bytes through the Web caller, asserts extracted
  text, and separately asserts unreadable containers remain reviewable.
  Parser contract tests live under
  `tests/Pegasus.IntegrationTests/DocumentExtraction/`. SIMPLI-013 recorded
  136/136 parser tests, 202/202 focused intake regressions, and direct Web-path
  DOC/MSG outcomes in its post-implementation report.
- SIMPLI-013 was independently reviewed, merged as PR 449, and proved after
  merge. Its proof records release 14, source inclusion, production smoke, and
  promotion to `main`; its board item is Done with
  `deployment: production`. Commit
  `c7457628cbf883843aaad1539f94fee49fef5cc7` introduced the adapter and is
  reachable from both current development/release history and the deployed
  release-38 source.
- Current production deployment was verified read-only on 2026-09-03. Azure
  Container Apps reports
  `pegasus-prod-web-252ow37gij--0f0e90ae44ff` as the latest and latest-ready
  revision, Healthy, Provisioned, RunningAtMaxScale, active, and receiving 100%
  of traffic. Its immutable image is
  `sha256:b791d9587224d30d68fd6abcbd1e1d5f389f2baefc3702d9ec2d2f37398eef15`,
  matching the release-38 record at
  `docs/operations.md:355-378`.
- The release-38 source commit
  `0f0e90ae44ffda7339ca2a460310deeb98121afa` has the DOC/MSG implementation
  commit as an ancestor. Direct reads at that exact source show the DOC/MSG
  dispatch, both adapter methods, the parser-version marker, and the composed
  `IIntakeSourceReader`. Therefore the active production image was built from
  a source tree containing and composing the feature. This establishes
  deployment; it does not claim that a genuine production DOC or MSG has been
  received or that extraction accuracy has operator acceptance.
- `docs/current-architecture.md` already describes the active reader,
  per-format limits, DOC/MSG behavior, and release-14 deployment at lines
  237-269. However, line 162 in the same file still lists automated legacy DOC
  and MSG extraction as absent. The two claims cannot both be true.
- `docs/operations.md:265` says automatic DOC/MSG extraction remains deferred,
  and its deferred-capability table at line 1548 says the automatic production
  parser is deliberately absent. Both lines predate the integration: `git
  blame` attributes them to the 2026-08-03 baseline commit
  `e180d61e9`. They survived the release-14 documentation update
  `25e170ffe`, which added the correct architecture statement but did not
  remove these older entries. This is stale documentation, not an intentionally
  narrower description of deployment.
- The old qualification contains one still-useful limitation: local parser
  fixtures and deployment do not prove accuracy over a human-reviewed genuine
  cohort/untouched holdout or prove production use. The external-service clause
  is also still valid if a future external processor is proposed, but it is not
  an activation condition for the already deployed in-process parser.
- No external research sources are declared for this ticket's area/labels
  (`get_sources` returned zero declarations), so none were fetched or treated
  as authority.

## Implications

The correction is documentation-only and should describe one coherent target
state:

- Remove DOC/MSG extraction from both lists of absent/deferred capabilities.
- Replace the dated-evidence statement in `docs/operations.md` with a concise
  deployed-state statement tied to release 14 and the current caller, while
  preserving the honest qualification that genuine-cohort accuracy and actual
  production-document use have not been proved.
- Remove the contradictory absent bullet from
  `docs/current-architecture.md`; retain its existing detailed active-reader
  section and fail-closed limitations.
- Do not change FRD-05 or ADR-0025 unless a documentation link needs mechanical
  adjustment: they already state the correct intended mechanism.
- Do not change code, dependencies, parser behavior, tests, Azure resources, or
  the protected operator notes. No external processor is being selected.

## Open questions

None. The live deployment, caller path, origin of the stale statements, and
remaining evidence limitation are all established without requiring an
operator decision.
