# TICK-041 implementation report

## Result and limits

Source-bound OCR extension is implemented and focused local verification passes
in .worktrees/tick-041, branch TICK-041-qualified-ocr, base
522e67f270ab4d6086d9fba04095988db3598888. Root authored Core/store/contracts;
pack_reconcile authored the bounded PdfPig helper and SQL test additions.
An independent reviewer must be neither author. No Azure call, provision,
deployment, genuine OCR output or completed PDF estimate import is claimed.
TICK-085 owns the real retained-estimate caller and PLAT-065 owns activation;
C05 remains open until their allocated acceptance exists.

## Implementation and existing callers

- IntakeOcrOperations retains its current intake producer and adds deterministic
  BeginDocumentAsync for retained Case/occurrence/version/hash/page identity.
  Exactly one complete intake or Case source is valid. Source length is part of
  persisted intent and replay comparison, so altered length cannot reuse a key.
- EfIntakeOcrOperationStore retains the existing SQL operation plus paired
  external work. The request envelope now keeps source length and Case context.
  Replay compares every context identifier, hash, length and page set; no new
  schema, queue, dispatcher, package or compatibility conversion exists.
- ProcessIntakeOcr, already reached through Worker external-work dispatch,
  queries existing IGetCaseDocumentMetadata before reading or consuming Case
  output. Missing/unconfirmed/removed/wrong-context/changed source fails closed.
  The authorized logical reader verifies the actual retained bytes. Case OCR
  completion retains provider output and settles the paired work only; it does
  not analyze an instruction, advance Case workflow or save an estimate.
  Intake analysis/recovery remains its existing path. Known provider operations
  and retained output are reused; uncertain unaccounted submissions are not resent.
- PdfOcrQualification reads public PdfPig resource/text-operation metadata.
  Only actually used anonymous numeric Type3 encoding without ToUnicode qualifies;
  absent ToUnicode alone does not. q/Q, text rendering mode, font size and
  inherited resources are respected. It is not a glyph decoder or an OCR fallback
  for failed business parsing. Direct page text operations cover all five supplied
  exports; no custom Form-XObject expansion is claimed.
- Existing Worker composition already registers the metadata query. No composition
  edit was needed. The new estimator initiation/helper callers are explicitly
  allocated to TICK-085 rather than misrepresented as live in this PR.

## Governing documents

ADR-0040 supersedes ADR-0001; ADR-0003 PdfPig and ADR-0005 ordinary scan bounds
remain accepted. ADR index, FRD-05/07 and INT-16/EXT-12 now name one current OCR
contract. Azure Worker managed identity/custom subdomain and existing GA
prebuilt-layout API2024-11-30 are selected, not activated. Microsoft primary
layout/SDK documentation was checked. Existing SDK dependencies remain unchanged.

## Verification — root single host lane

Executed on Windows/PowerShell7/.NET10 Release. No other heavy verifier ran.

    dotnet restore ./Pegasus.slnx --locked-mode
    dotnet build ./Pegasus.slnx --configuration Release --no-restore
    dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IntakeOcrTests|FullyQualifiedName~AnalyzeRetainedInstructionTests" --logger "trx;LogFileName=tick-041-core.trx"
    dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WorkerCompositionTests" --logger "trx;LogFileName=tick-041-composition.trx"
    dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OcrIntakeRecoveryTests|FullyQualifiedName~AzureDocumentIntelligenceOcrTests|FullyQualifiedName~PdfOcrQualificationTests" --logger "trx;LogFileName=tick-041-focused.trx"

All exit0. Locked restore and first build PASS, zero warnings/errors70.06s.
Core55 PASS520ms; Worker composition11 PASS991ms; Integration38 PASS114s,
zero skips. The last explicit filter intentionally includes Corpus cases:
five SHA-pinned genuine PDFs assert all23 page classifications, and six SQL
Case-source cases use retained genuine YL source bytes through the actual
metadata/content readers. Existing intake recovery tests now use that real
reader instead of the removed private fake. Structural provider responses
prove transport/durability only, not text accuracy or provider acceptance.

No compiler/runtime test failed in this cohort. Read-only discovery encountered
absent guessed file paths/Windows glob arguments; corrected rg discovery found
actual owners. These are not concealed test failures. Normal git diff --check
passed with only ordinary LF/CRLF checkout notices. First report orchestration
failed JavaScript parsing before any tool ran; corrected string encoding, no
file/board write or build/test occurred in that failed invocation.

TRX SHA256 (paths under each project's TestResults):

| Record | SHA256 |
| --- | --- |
| tick-041-core.trx | A6236A75272D02223DC7D25278E3A671EEAB158352F010084B2AF6DDC96CBE34 |
| tick-041-composition.trx | 51A324D913F7D7CBFB8778CF9797674141FF7485A8F65171093FAA2037DB5DC6 |
| tick-041-focused.trx | 55CA0F2F24E8FFDB408C8A7D4D5A60C2C419C8E11F99739F4788D73540CD381C |

## Handoff

Scoped commit and dev PR use the root-authorized skip-ci convention, not a
required-check bypass. Independent exact-head review precedes merge. Exact
integrated proof, TICK-085's caller and live PLAT-065 activation remain open.
No corpus/source PDF was altered or uploaded. PDF and documentation skills
kept genuine source qualification separate from OCR accuracy and canonical
behavior separate from current deployment claims.

## Pushed candidate

PR https://github.com/collisionengineers/pegasus/pull/686 targets dev; head
890f656be13149c140980b86837e2b7897d26117. All13 scoped files committed,
779 insertions/126 deletions; staged whitespace check, commit and push exit0.
The worktree is clean. No source change followed the recorded checks.
Independent review next; no author self-review or live activation claimed.
