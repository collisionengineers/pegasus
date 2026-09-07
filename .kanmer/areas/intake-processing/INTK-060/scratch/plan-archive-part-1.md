# Archived original plan — part 1 of 4

Original ticket document: `plan/plan.md`
Original SHA-256: `62649b22a7e43d771820d36c4126a65867fc38d99b636c54a20cc5a6468f3a95`
Character range: 0–29999 of 115556
Reconstruction: concatenate the payload sections from parts 1–4 in order.

## Payload

# Stream C implementation plan

## Governing docs

The user-approved D01-D17 decisions authorize the corresponding FRD corrections in this stream. Existing accounts/access and mail FRDs gain explicit administrator recovery, no periodic reviews, authorized staff sends and truthful Sent evidence; domain FRDs follow their named stream owners. Protected operator notes are not overwritten. Four-project architecture and existing policy owners remain binding.

## Starting state

D = 3284f93fc3ea9fd3bbbea9405ec92dc7818378f2, verified live. Owner tickets A PLAT-075, B CASE-047, C INTK-060. Follow the supplied exact file ownership register. User has authorized autonomous execution and the three-owner exception; no new permission request is needed for this implementation.

# Three-machine execution and handoff

This is an approved exception to one-ticket/one-feature-PR work. Future product
implementation uses three owner tickets and three new branches based on the
same current dev commit. Existing tickets are evidence and residual work owners,
not 210 separate implementation PRs. This planning package has no Kanmer ticket.
All three implementation PRs target dev and remain open and unmerged.

## Startup — Astra coordinates before any coding

Read [DECISIONS](DECISIONS.md), [SHARED-CONTRACTS](SHARED-CONTRACTS.md), your stream
plan and [Git dispositions](registers/git-dispositions.md). Read current
AGENTS/NOW/docs index and native Kanmer status/effective gates. Refresh GitHub
heads and the four old PRs; a changed head requires a delta review and updated
preservation table, not restarting or silently discarding this package.

Planning pin D is `3284f93fc3ea9fd3bbbea9405ec92dc7818378f2`; main pin is
`32f8679d3695e0dcab8f310a1c20f8b129d20190`. The shared source checkout is stale
and dirty. Do not reset, stash, clean, checkout, build or implement there.
Create clean worktrees only when product implementation is authorized.

At that time Astra creates exactly three owner records with descriptions below,
reads their effective profiles/gates, supplies this package as research/plan/
checklist and records actual branch/worktree with native Kanmer. Do not force
take the 47 currently claimed records or silently repoint their resume targets.
Use supported native Kanmer commands; never edit board files/branch manually.
If current Kanmer demands a different branch name, store the actual name once
and update all three machine instructions; ownership and three-PR count remain.

| Machine | Owner ticket title | Proposed branch | Worktree relative to own checkout | Final PR title |
| --- | --- | --- | --- | --- |
| Codex / Astra | Pegasus v1 platform, shared foundation and integration | task/pegasus-v1-platform | ../pegasus-worktrees/v1-platform | Complete v1 platform, custody, mail and integration |
| Claude / Fable 5.1 B | Pegasus v1 Case engineering, Glass's and reports | task/pegasus-v1-casework | ../pegasus-worktrees/v1-casework | Complete v1 Case engineering and report workflow |
| Claude / Fable 5.1 C | Pegasus v1 intake, principals and operator shell | task/pegasus-v1-intake | ../pegasus-worktrees/v1-intake | Complete v1 intake, extraction and operator workspace |

Each ticket includes its stream plan, the same frozen D, all shared decisions,
mapped old ticket IDs, allowed files, tests and the PR-open/unmerged stop. Do not
create a fourth foundation owner/PR, planning ticket or generic umbrella batch.
This package remains outside the repository; canonical product documentation
edits follow AGENTS. No secrets or corpus binaries enter tickets or PRs.

## Commit topology — common foundation, then independent work

All three branches are created at the exact same D. Astra authors F01–F03 on A
as common foundation commits F. B and C do read-only source/corpus preparation
until F is reviewed, compiling, and published. Before either has domain commits,
each fetches A and fast-forwards to **the same F commit objects**, not copies:

```text
                 A domain commits ---- A PR -> dev
                /
dev D ---- F ---+--- B domain commits -- B PR -> dev
                \
                 C domain commits ---- C PR -> dev
```

The fast-forward must be `git merge --ff-only <recorded-F-SHA>` on B/C; verify
`git merge-base --is-ancestor <F> HEAD` and record the shared commit identity.
Do not cherry-pick F, rebase it separately, merge dev, merge the foundation into
dev, or target B/C PRs at A. No dev update is authorized. Foundation appears in
all three dev comparison diffs until any later authorized merge; explicitly
label that shared range in each PR. Git ancestry applies it once.

Once streams diverge, do not fast-forward B/C to A's domain head or merge whole
mega branches into one another. A needed shared correction is authored by Astra
on a temporary local branch/checkout rooted at the latest shared F/G boundary,
reviewed, and merged with `git merge --no-ff <G-SHA>` as the **same G commit**
into each stream. Record G and each stream’s distinct merge commit. It changes only
Foundation-owned files/contracts. The temporary helper has no PR; preserve its
SHA in the owner evidence. Resolve conflicts in the owning stream and retest.
Do not make cross-stream contract changes independently.

Composition exception: when a new concrete type exists only on B or C, A
authors a small branch-local DI/host patch against that recorded head. The
stream applies the exact hash-recorded patch in a serialized registration
window; it does not improvise edits to A-owned files. A remains the sole
registration author/reviewer. That patch travels in the domain PR and compiles
there. Common G is reserved for changes whose dependencies exist in every
stream. The combined checkout combines the three registration additions under
A ownership; small composition conflicts are resolved explicitly. This avoids
stubs, reflective registration and importing unrelated domain commits merely
to register a type. Contract/schema changes still require common G.

## Foundation steps — Codex machine only

**F01, Astra + Sol contract review:** freeze the exact shapes and owners in
SHARED-CONTRACTS and the file manifest. Read B/C foundation requests as input;
the accepted shared contract wins when a request uses a different port name.
Port compatible PR670/671 schema hunks and PR639 watermark into the target
schema design, retaining per-hunk dispositions. Reconcile local AGENTS 0.4.2
semantic changes and the explicit task exception without replacing unrelated
dirty work. Publish exact API/enum/field/test fixture signatures in existing
canonical docs and owner-ticket plan, and author the actual shared C# definitions
in every S02 contract path. The A-before-F/B-or-C-after-F manifest exception is
contract-only; domain commands/stores stay in their stream. No consumer branch
may reference an absent contract or create a private copy. No ambiguous
per-stream schema choices.

**F02, Sol implementation; Terra tests:** own all EF entity declarations,
configuration classes, PegasusDbContext, migrations/model snapshot and grants.
Keep the valid existing migration chain and add the single coherent v1 schema
migration needed for the new model; no historical data conversion, dual columns
or old/new implementation switch. A fresh database applies that chain cleanly.
Default-null new facts represent genuinely unrecorded data, not invented domain
values. Configure A/B/C-provided minimal shapes in the existing aggregates.
The A inventory is `handoffs/A-foundation-requirements.json`; include its
credential, mail-attempt/correlation, cache and administrative lease primitive
requirements alongside both B/C inventories. Use
unique constraints for operation keys, T references, one Current estimate,
credential-active session and artifact versions. Add actual runtime-role grants
and bootstrap census in the same diff. F owns global persistence plumbing; B/C
implement their own store methods after the freeze.

F02's lease-clearance verification uses the explicit A-before-F/B-after-F
exceptions on `CaseEditAuthorityTests.cs` and `CaseWorkflowPersistenceTests.cs`.
A adds only that primitive's policy and persistence tests in those files;
B resumes normal domain ownership after the shared F SHA is recorded.

**F03, Terra; Sol independent check:** publish only registrations whose concrete implementations exist at F,
shared test support and stable shared shell markup/class contract. Do not
reference absent B/C types, use no-op registration hooks or add throwing stubs.
New domain handlers and their registration arrive together through the
serialized branch-local registration window below. Foundation alone is an
incomplete development checkpoint. Run locked restore/build, architecture/migration/grant and contract
tests in isolation, record exact F, and invite B/C fast-forward. Missing domain
implementation must be tracked in its exact step rather than falsely passed.

F is one initial synchronization point, not a demand that A finish its entire
platform before B/C start. Later ports use existing local fakes and genuine
source assets for parallel development. Runtime implementations land in their
owners and the combined checkout proves wiring.

## Waves and model delegation

| Wave | Codex A | Claude B | Claude C | Barrier |
| --- | --- | --- | --- | --- |
| 0 | Astra F01; Sol contract audit; Terra F02/F03 | Fable coordinates; Sonnet B01 read-only PR/source inventory | Fable coordinates; Opus C01 read-only evidence/PR inventory | All branches at D; no B/C domain commits |
| 1 | A01 identity and A04 custody: two Sol workers on disjoint paths | Opus B02 transaction; Sonnet records v3 field/manager matrix | Opus C01 correction then C02 provenance; separate Sonnet directory source inventory | Shared F adopted unchanged |
| 2 | Sol A02 Graph; Terra A06 admin query/UI | Opus B03 valuations then B04 estimates; independent Opus importer slice after estimate contract | Opus C03 profiles in bounded batches; second Opus C04/C07 pre-case rules on disjoint files | B totals and C candidate/location interfaces fixed |
| 3 | Sol A03 sending; second Sol A05 connector | Opus B05 reports/Glass's integration; Sonnet B06 Files when schema ready | Sonnet C06 directory and C08 shared shell; Opus C05 third-party extraction | A custody/send and C shared assets available |
| 4 | Terra A07 CI/performance; Luna A08 docs inventory | Sonnet B07 preparation/B08 assembly; Opus resolves complex findings | Sonnet C08 assembly; Opus corpus/failure checks | Each stream's callers wired; no domain placeholders |
| 5 | Fresh Sol A09; Astra unpublished combined verification | Fresh Fable 5.1 B09 full-stream review | Fresh Fable 5.1 C09 full-stream review | Exact heads and all review dispositions recorded |

Fable 5.1 is each Claude orchestrator, never a worker/subagent until the final
fresh whole-stream review. Opus 5 handles complex policy/concurrency/extraction;
Sonnet 5 handles routine UI/adapters/tests/docs. No Haiku. Astra is the Codex
orchestrator; Sol handles complex work/review, Terra routine work, Luna bounded
mechanical inventory. Every delegated task specifies exact files, inputs,
required caller/tests and stop condition. One author per file. A reviewer must
not be the author. A model need not wait idle merely because a separate step
is blocked; continue its already authorized disjoint work.

## Cross-machine evidence and ownership changes

Each owner ticket stores a compact handoff table: item/contract version,
providing commit, consuming stream, files, focused command/result and remaining
operator gate. Record updates at contract freeze, implementation checkpoint,
review and final head. The three root orchestrators communicate through these
shared owner-ticket documents and Git commit identities; one machine's local
path is never another machine's dependency. Subagent notes are merged into the
owner record, not posted as emails/messages to staff. Native MCP writes for
future ticket work are authorized by that implementation task, not by this
planning turn.

File manifest precedence is exact file, then deepest prefix; a tie is a defect.
Unlisted files are closed to edits until Astra assigns one owner. A shared-file
change goes through Astra's common G commit. Domain-file change goes to its
owner; send an exact patch/request, never edit a neighbour's checkout. B/C
domain interfaces frozen in F may be implemented in their files but changes to
the agreed cross-stream signature require G. A change to shared CSS stays C;
B supplies a fixture/expected behavior and uses Case-only assets for Case logic.

Handoff `newPaths` means new relative to D, not permission to recreate a file
already introduced in F. Check the phase fields first: A may have published its
shared records/interfaces at F, after which the domain owner extends that same
file with the real implementation. A domain worker never forks the definition.

## Existing PRs, tickets and worktrees

The 6 September 2026 snapshot accounts for PRs 639/646/670/671, 44 worktrees and
43 local branches. Refresh this census at implementation startup before any
retirement or preservation decision.
Preserve original commits, branches, ticket evidence and any dirty files.
Port required hunks with source SHA and exact target path; compare final
behavior/tests and reject superseded UI/schema churn with a reason. Do not
blind-merge stale branches. After both code preservation and independent review
are proved, authorized closeout may close the old PRs as superseded by named
replacement PRs. This is not a merge and does not prove their tickets Done.
Exactly three replacement PRs remain open when all streams finish. No draft
PRs are created for subagents, helpers, integration or foundation.

Contained branches are preservation/cleanup candidates only. Existing claims
are reconciled individually under current native gates, not forced, silently
released or deleted. Review/verification of already integrated code remains
real work and is included in the three owners' evidence. A genuine post-v1
feature receives the explicit deferred disposition, not a fake implementation.

## Combined verification and final stop

Astra creates an **unpublished** disposable integration checkout from D and
merges the exact A/B/C heads locally. No PR or remote integration branch. Check
conflicts and migration count, run canonical validation and routed UI/corpus
journeys, and record the three inputs plus combined tree/commit. A combined
failure returns to its file owner, then that owner and the combined checkout
retest affected checks. Do not conceal a failed individual PR behind a passing
combination. Refresh integration whenever any input changes.

Final handoff has exactly 3 PR URLs, all to dev and unmerged; three exact heads;
common F/G ancestry; green applicable CI/standalone checks; combined evidence;
old-PR preservation/closure evidence; honest human provider gates; current docs;
and [the operator checklist](OPERATOR-CHECKLIST.md). No dev/main merge, deploy,
reset, live credentials or provider write occurs as a side effect of completion.



# Stream C: intake, directories and shared operator surfaces

## Authority and boundary

This stream implements intake correctness, source-provenance, the top-15
principal instruction profiles, third-party report extraction, current
principal/directory administration, pre-case Image Intake and Triage, Inbox,
Search, Work Centre, the application shell and shared Web assets. It follows
[the final decisions](../DECISIONS.md), the frozen
[shared contracts](../SHARED-CONTRACTS.md), and the execution order and file
ownership in [coordination](../COORDINATION.md).

Stream A alone owns global EF model configuration, migrations, the model
snapshot, composition/DI, shared test support, Graph mail runtime, storage and
MCP. Stream B owns Case pages, Core engineering decisions, report generation
and delivery, and Glass's. Stream C owns its Core policies, adapter/store method
implementations and the C-owned Razor surfaces. C has single ownership of the
outer shell, navigation and shared CSS/JS/icon assets; Stream B owns Case-only
partials and Case-specific assets. The exact requests that C needs from A and
the contracts C exposes to B are in
[C foundation requirements](../handoffs/C-foundation-requirements.json).

The implementation baseline is commit
`3284f93fc3ea9fd3bbbea9405ec92dc7818378f2`. Before editing, re-resolve every
anchor against the final integration base and stop on a material contract or
ownership change. Reuse these production paths before adding anything:

- `ProcessIntake`, `DurableIntake`, `ReconcileUnidentifiedDestinations`,
  `IIntakeSourceReader`, `IInstructionExtractionPolicy`,
  `InstructionFieldExtraction`, `QdosMailRoutePolicy`,
  `QdosMailClassificationPolicy` and `QdosInstructionExtractionPolicy`;
- `MimeKitPdfPigOpenXmlIntakeSourceReader`, `IIntakeReceiptStore`,
  `IIntakeMutationStore`, `IUnidentifiedStore`, `IImageIntakeStore`,
  `IProviderReferenceCatalog`, `IOrganizationAdministrationQueries` and
  `IOrganizationAdministrationStore`;
- `Ext18InspectionAddressPolicy`, `InspectionAddressResolutionPolicy`,
  `InspectionAddressResolutionStore` and `ProviderInspectionModePolicy`;
- the existing `/`, `/Inbox`, `/Search`, `/Triage`, `/Unidentified`,
  `/VehicleImages/{id:guid}`, `/Administration/Organizations` and
  `/Administration/Principals` Razor Pages; and
- `_Layout.cshtml`, `_ShellDialogs.cshtml`, `_EvidenceViewer.cshtml`,
  `_Provenance.cshtml`, `_StatusChip.cshtml`, `site.css`, `site.js` and the
  existing Lucide sprite.

Do not add a generic workflow engine, rules-builder UI, duplicate principal
list, separate case-policy owner, new top-level project, a second OCR vendor or
OCR runtime, or prerelease compatibility path. The one approved OCR boundary is
Azure Document Intelligence `prebuilt-layout` through the existing Worker and
external-work conventions. One-off customers and spreadsheet-driven
recipient/package/chase/garage procedures are deferred. Location choices,
address suggestions, all top-15 profiles and the Pegasus-owned report workflow
remain included; EVA is an optional downstream action.

## Evidence and extraction invariants

- The immutable local corpus is evidence. Never edit, rename, deduplicate in
  place, upload or publish an original. A test manifest records occurrence,
  hash and relationship even when execution deduplicates identical bytes.
- All 81 instruction originals referenced by the 15 existing method files, all
  29 report PDFs in the third-party inventory, and all 14 EVA source workbooks
  are locally available and hash-verifiable. The EVA report's `evacases.xlsx`
  is the matching local `principal-and-repairer-info/every_eva_case.xlsx`.
- The pack-root `providers-worked-on.xlsx` matches the EVA report hash; the
  pinned source snapshot contains a different version. Tests and seeds use the
  explicitly hashed pack-root source and never silently substitute the copy.
- The EVA planning evidence is the
  [reassessment](../../../more_docs/eva_data_export/EVA_Pegasus_Reassessment.md),
  [aggregate findings](../../../more_docs/eva_data_export/Aggregate_findings.json),
  [source inventory](../../../more_docs/eva_data_export/Source_inventory.csv),
  [candidate workflow rules](../../../more_docs/eva_data_export/Candidate_workflow_rules.csv)
  and [derived-date review](../../../more_docs/eva_data_export/Derived_date_review.csv).
  The workflow rows are historical candidates and the date rows are review
  evidence; neither authorizes automation or rewriting the fresh target data.
- The Box-linked E01-E28 originals have no local filename/hash manifest. They
  may explain a proposed rule but cannot supply executable proof, a fixture, an
  activation decision or a claimed pass. A rule supported only by E01-E28 stays
  inactive until the exact original and hash are available.
- Keep raw source text and the smallest useful layout locator with every
  candidate: source/asset hash, occurrence, document role, page, table/cell or
  PDF form-field/region when present, source label, raw value and parser/policy
  version. Normalization never destroys the source value.
- Principal, document issuer, transport sender, effective sender,
  intermediary, claimant, repairer, storage business, third-party engineer,
  report addressee and outgoing recipient remain separate roles.
- Bounded label/cell/region rules may normalize punctuation and whitespace.
  They do not use a confidence score, arbitrary priority or first-match winner.
  Multiple supported interpretations are `Ambiguous`; absent evidence is
  unavailable. Unknown material remains retained for staff review.
- Do not guess make/model from token position, VRM, filename or a short make
  list. Preserve the full vehicle description; split only when labels/layout
  or a separately accepted lookup proves the parts.
- A report deadline, email arrival, enclosing forward or today's date is not
  an instruction or inspection date. Unknown VAT is not `No`. A footer address
  is not an inspection location. A repairer address does not prove a physical
  inspection.
- Extracted candidates never overwrite confirmed staff/Engineer facts.
  Conflicts remain visible and source-linked. Third-party outcomes and amounts
  remain third-party evidence until the existing Engineer-owned command accepts
  them.

## Model allocation

Fable 5.1 orchestrates the stream and owns sequencing, evidence retention and
handoffs; it does not implement a delegated slice. Use Opus 5 for C01-C05 and
C07, where reconciliation, structured extraction, role boundaries and pre-case
identity are correctness-sensitive. Use Sonnet 5 for C06 and C08 after their
contracts are frozen. Do not ask a second model to edit the same slice. Only
after C01-C08 are integrated and their focused checks pass, start a fresh Fable
5.1 context for the whole-stream review in C09.

## Exact repository file map

Paths are repository-root-relative. `Existing` means extend the named file on
the integration base. `Proposed` is the exact new file to create if the
corresponding behavior is still absent after rebasing. C never edits the A/B
handoff files listed here.

### C01 files

**Existing C files:**

- `src/Pegasus.Core/Intake/DurableIntake.cs`
- `src/Pegasus.Core/Intake/ProcessIntake.cs`
- `src/Pegasus.Core/Intake/CaseMatching/EvaluateIntakeCaseMatch.cs`
- `src/Pegasus.Core/Intake/IntakeContracts.cs`
- `src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs`
- `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs`
- `src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs`
- `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs`
- `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs`
- `tests/Pegasus.Core.Tests/Intake/CaseMatching/EvaluateIntakeCaseMatchTests.cs`
- `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs`
- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs`

**A-owned read-only dependencies:**
`src/Pegasus.Infrastructure/Persistence/UnidentifiedEntities.cs` and
`src/Pegasus.Worker/IntakeFunctions.cs`. C supplies the field and caller
inventory; A performs entity/model/migration/grant/DI/host-entrypoint edits.

**Proposed C files:**

- `src/Pegasus.Core/Intake/AnalyzeRetainedInstruction.cs`
- `src/Pegasus.Infrastructure/Persistence/EfRetainedInstructionAnalysisStore.cs`
- `tests/Pegasus.Core.Tests/Intake/AnalyzeRetainedInstructionTests.cs`
- `tests/Pegasus.IntegrationTests/RetainedInstructionAnalysisTests.cs`
- `tests/Pegasus.IntegrationTests/PrincipalSourceManifestTests.cs`, which
  validates the resolved local hash manifest and reports unavailable sources
  without embedding or changing corpus files

**A handoff:** `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`, one
new migration and
`src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
for C-F01; A also updates `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`
and `scripts/Test-MigrationGrants.ps1`.

### C02 files

**Existing C files:**

- `src/Pegasus.Core/Intake/IntakeContracts.cs`
- `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs`
- `src/Pegasus.Core/Intake/ProcessIntake.cs`
- `src/Pegasus.Core/Intake/DurableIntake.cs`
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs`
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.DocMsg.cs`
- `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs`
- `src/Pegasus.Web/Pages/Shared/_Provenance.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ProvenancePanel.cshtml`
- `tests/Pegasus.Core.Tests/Intake/InstructionEvidenceImagesTests.cs`
- `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs`
- `tests/Pegasus.IntegrationTests/MultiFormatGenuineCorpusWebTests.cs`

**A-owned read-only router:**
`src/Pegasus.Core/Custody/ExternalWorkProcessing.cs` and its shared
`tests/Pegasus.IntegrationTests/ExternalWorkProcessingTests.cs`. C supplies the
typed OCR handler/test inventory; A performs the coordinated router/DI/host and
shared-test edit.

**B-owned read-only dependency:**
`src/Pegasus.Core/Vehicle/LookupContracts.cs`. C's finite OCR-VRM candidate
adapter calls the frozen B lookup port; C does not edit or duplicate that
contract or its external lookup implementation.

**Proposed C files:**

- `src/Pegasus.Core/Intake/InstructionExtractionPolicySelector.cs`
- `src/Pegasus.Core/Intake/IntakeOcr.cs`
- `src/Pegasus.Infrastructure/Intake/AzureDocumentIntelligenceOcr.cs`
- `tests/Pegasus.Core.Tests/Intake/InstructionFieldExtractionTests.cs`
- `tests/Pegasus.Core.Tests/Intake/InstructionExtractionPolicySelectorTests.cs`
- `tests/Pegasus.Core.Tests/Intake/IntakeOcrTests.cs`
- `tests/Pegasus.IntegrationTests/StructuredIntakeSourceReaderTests.cs`
- `tests/Pegasus.IntegrationTests/AzureDocumentIntelligenceOcrTests.cs`
- `tests/Pegasus.IntegrationTests/OcrIntakeRecoveryTests.cs`

**Foundation handoff:** C-F02 supplies the OCR operation/result mapping inside
the existing external-work persistence and C-F03 supplies DI/Worker routing in
`src/Pegasus.Infrastructure/DependencyInjection.cs` and
`src/Pegasus.Worker/IntakeFunctions.cs`. Foundation owns optional use of an
existing-estate Document Intelligence resource plus configuration, managed-
identity permission and later deployment/activation. Shared fixture changes,
if required, remain in `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs`.

### C03 files

**Existing C files:**

- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs`
- `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs`
- `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.IntegrationTests/QdosExtractionCoverageTests.cs`
- `tests/Pegasus.IntegrationTests/PrincipalIdentificationCorpusEvidenceTests.cs`
- `reference/workproviders-and-repairers/principal-identification-corpus.v1.json`

**Proposed C profile files:**

- `src/Pegasus.Core/Intake/DirectProviders/Pch/PchInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Ax/AxInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Fw/FwInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Qcl/QclInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Oak/OakInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Sbl/SblInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Black/BlackInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Rjs/RjsInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Dfd/DfdInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Kbs/KbsInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Mp/MpInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Yml/YmlInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Als/AlsInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Bc/BcInstructionExtractionPolicy.cs`

**Proposed C profile and test files:**

- `tests/Pegasus.Core.Tests/Intake/Pch/PchInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Ax/AxInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Fw/FwInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Qcl/QclInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Oak/OakInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Sbl/SblInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Black/BlackInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Rjs/RjsInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Dfd/DfdInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Kbs/KbsInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Mp/MpInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Yml/YmlInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Als/AlsInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/Bc/BcInstructionExtractionPoli
