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
- `tests/Pegasus.Core.Tests/Intake/Bc/BcInstructionExtractionPolicyTests.cs`
- `tests/Pegasus.IntegrationTests/Top15InstructionCorpusTests.cs`
- `src/Pegasus.Core/Intake/MachineReadRegistrationResolution.cs`
- `src/Pegasus.Infrastructure/Intake/VehicleRegistrationCandidateLookup.cs`
- `tests/Pegasus.Core.Tests/Intake/MachineReadRegistrationResolutionTests.cs`
- `tests/Pegasus.IntegrationTests/MachineReadRegistrationLookupTests.cs`

These are the explicit fourteen new profile suites. The existing QDOS suite
stays in place, and the integration suite consumes the one profile registry.

**A handoff:** register the selector and fifteen policies only in
`src/Pegasus.Infrastructure/DependencyInjection.cs`. A does not copy profile
codes or criteria into composition.

### C04 files

**Existing C files:**

- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs`
- `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs`
- `src/Pegasus.Core/Intake/ProcessIntake.cs`
- `src/Pegasus.Core/Triage/TriageContracts.cs`
- `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs`
- `src/Pegasus.Infrastructure/Persistence/InspectionAddressResolutionStore.cs`
- `src/Pegasus.Web/Pages/Intake/Details.cshtml`
- `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs`
- `tests/Pegasus.Core.Tests/Intake/Qdos/QdosMailClassificationPolicyTests.cs`
- `tests/Pegasus.Core.Tests/Intake/DefinitiveIntakeCaseTypeTests.cs`
- `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/QdosTriageReplayIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/TriageFromIntakeIntegrationTests.cs`

**Proposed C test:**
`tests/Pegasus.IntegrationTests/QdosAttachmentTriageIntegrationTests.cs` owns
the empty-current-body, duplicate PDF/DOC, quoted-message and mixed-category
cases. C-F04 supplies the A-owned T-reference schema/migration/snapshot.

### C05 files

**Existing C files:**

- `src/Pegasus.Core/Documents/DocumentContracts.cs`
- `src/Pegasus.Core/Intake/IntakeContracts.cs`
- `src/Pegasus.Core/Intake/ProcessIntake.cs`
- `src/Pegasus.Infrastructure/Intake/MimeKitPdfPigOpenXmlIntakeSourceReader.cs`
- `src/Pegasus.Web/Pages/Shared/_EvidenceViewer.cshtml`
- `src/Pegasus.Web/Pages/Shared/_Provenance.cshtml`
- `tests/Pegasus.IntegrationTests/MultiFormatGenuineCorpusWebTests.cs`

**Proposed C files:**

- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportContracts.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportExtraction.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportProfiles.cs`
- `src/Pegasus.Core/Intake/ThirdPartyReports/ThirdPartyReportValidation.cs`
- `tests/Pegasus.Core.Tests/Intake/ThirdPartyReports/ThirdPartyReportExtractionTests.cs`
- `tests/Pegasus.IntegrationTests/ThirdPartyReportCorpusTests.cs`
- `tests/Pegasus.IntegrationTests/ThirdPartyReportProvenanceWebTests.cs`

**A handoff:** C-F02 persistence/DI only. **B handoff:** C-B02 is consumed in
B-owned `src/Pegasus.Core/Assessment`, `src/Pegasus.Core/Reports` and Case
partials; C does not edit those trees.

### C06 files

**Existing C files:**

- `src/Pegasus.Core/Cases/OrganizationAdministration.cs`
- `src/Pegasus.Core/Address/InspectionAddressResolution.cs`
- `src/Pegasus.Core/Cases/ProviderInspectionModePolicy.cs`
- `src/Pegasus.Infrastructure/Persistence/EfOrganizationAdministration.cs`
- `src/Pegasus.Infrastructure/Persistence/EfProviderReferenceCatalog.cs`
- `src/Pegasus.Infrastructure/Persistence/InspectionAddressChoicesQueries.cs`
- `src/Pegasus.Infrastructure/Persistence/InspectionAddressResolutionStore.cs`
- `src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json`
- `src/Pegasus.Web/Pages/Administration/Organizations/Index.cshtml`
- `src/Pegasus.Web/Pages/Administration/Organizations/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml`
- `src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/Principals/Index.cshtml`
- `src/Pegasus.Web/Pages/Administration/Principals/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml`
- `src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml`
- `src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml.cs`
- `tests/Pegasus.Core.Tests/Cases/OrganizationAdministrationTests.cs`
- `tests/Pegasus.Core.Tests/Address/InspectionAddressResolutionPolicyTests.cs`
- `tests/Pegasus.IntegrationTests/OrganizationAdministrationPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs`
- `tests/Pegasus.IntegrationTests/InspectionAddressChoicesPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/InspectionAddressChoiceBrowserTests.cs`

**Proposed C files:**

- `src/Pegasus.Core/Cases/OrganizationDirectory.cs`
- `src/Pegasus.Core/Cases/ClaimSourceAdministration.cs`
- `src/Pegasus.Infrastructure/Persistence/EfOrganizationDirectory.cs`
- `src/Pegasus.Infrastructure/Persistence/EfClaimSourceAdministration.cs`
- `src/Pegasus.Web/Pages/Administration/ClaimSources/Index.cshtml`
- `src/Pegasus.Web/Pages/Administration/ClaimSources/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Administration/ClaimSources/Edit.cshtml`
- `src/Pegasus.Web/Pages/Administration/ClaimSources/Edit.cshtml.cs`
- `tests/Pegasus.Core.Tests/Cases/OrganizationDirectoryTests.cs`
- `tests/Pegasus.Core.Tests/Cases/ClaimSourceAdministrationTests.cs`
- `tests/Pegasus.IntegrationTests/OrganizationDirectoryPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/OrganizationDirectoryWebTests.cs`
- `tests/Pegasus.IntegrationTests/ClaimSourceAdministrationTests.cs`
- `tests/Pegasus.IntegrationTests/InspectionAddressSuggestionTests.cs`

**A handoff:** C-F05 owns entity declarations/configuration, migration,
snapshot, grants, DI and host entrypoint edits. A removes the automatic-EVA
column and composition/Worker registrations only. **B handoff:** B consumes
C-B03 from its Case location picker and C-B06 for manual-only EVA, removing
automatic policy/work-item use from
`src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs`,
`src/Pegasus.Core/Eva/EvaSubmissionWorkItem.cs`,
`src/Pegasus.Core/Eva/EvaApiContracts.cs` and
`src/Pegasus.Infrastructure/Persistence/EfAutomaticEvaSubmissionStore.cs`,
`src/Pegasus.Infrastructure/Persistence/EfEvaSubmissionModeStore.cs` and
`src/Pegasus.Infrastructure/Persistence/EfEvaSubmissionWorkStore.cs`. C removes
only its Principal-page controls and its administration command/query fields.

### C07 files

**Existing C files:**

- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`
- `src/Pegasus.Core/ImageIntake/ImageIntakeLifecycle.cs`
- `src/Pegasus.Core/Triage/TriageContracts.cs`
- `src/Pegasus.Core/Documents/RequestUploadPolicy.cs`
- `src/Pegasus.Core/Intake/IntakeContracts.cs`
- `src/Pegasus.Core/ProviderApi/ProviderSubmission.cs`
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs`
- `src/Pegasus.Web/ProviderApi/ProviderApiEndpoints.cs`
- `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml`
- `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml.cs`
- `src/Pegasus.Web/Pages/Triage/Index.cshtml`
- `src/Pegasus.Web/Pages/Triage/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Triage/Details.cshtml`
- `src/Pegasus.Web/Pages/Triage/Details.cshtml.cs`
- `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeLifecycleTests.cs`
- `tests/Pegasus.Core.Tests/Triage/TriageReplayTests.cs`
- `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs`
- `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`
- `tests/Pegasus.Core.Tests/Intake/IntakeEnvelopeLimitsTests.cs`
- `tests/Pegasus.Core.Tests/Qdos/QdosBoundaryContractTests.cs`
- `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs`
- `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs`

**A-owned read-only contract:**
`src/Pegasus.Core/Custody/CustodyContracts.cs` (`ICaseArtifactCustody`).

**Proposed C files:**

- `src/Pegasus.Core/Intake/RetainIncomingArtifact.cs`
- `tests/Pegasus.Core.Tests/Intake/RetainIncomingArtifactTests.cs`
- `tests/Pegasus.IntegrationTests/TriageReferenceAllocationTests.cs`
- `tests/Pegasus.IntegrationTests/PublicUploadSessionTests.cs`
- `tests/Pegasus.IntegrationTests/IncomingArtifactCustodyTests.cs`

A owns the C-F04/C-F06 entity declarations/mappings, migrations, snapshot,
grants, DI/host entrypoint edits and A04 content/custody adapter. F owns the
exact host request limits/configuration/deployment. B alone edits
`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` to consume C-B04.

### C08 files

**Existing C files:**

- `src/Pegasus.Web/Pages/Index.cshtml`
- `src/Pegasus.Web/Pages/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Mail/Index.cshtml`
- `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Mail/Message.cshtml`
- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`
- `src/Pegasus.Web/Pages/Uploads/Request.cshtml`
- `src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs`
- `src/Pegasus.Web/Pages/Search/Index.cshtml`
- `src/Pegasus.Web/Pages/Search/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Search/_CasePreview.cshtml`
- `src/Pegasus.Web/Pages/Upload.cshtml`
- `src/Pegasus.Web/Pages/Upload.cshtml.cs`
- `src/Pegasus.Web/Pages/UploadStatus.cshtml`
- `src/Pegasus.Web/Pages/UploadStatus.cshtml.cs`
- `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml`
- `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml.cs`
- `src/Pegasus.Web/Pages/Intake/Source.cshtml`
- `src/Pegasus.Web/Pages/Intake/Source.cshtml.cs`
- `src/Pegasus.Web/Pages/Intake/Asset.cshtml`
- `src/Pegasus.Web/Pages/Intake/Asset.cshtml.cs`
- `src/Pegasus.Web/Pages/Intake/Image.cshtml`
- `src/Pegasus.Web/Pages/Intake/Image.cshtml.cs`
- `src/Pegasus.Web/Pages/Unidentified/Index.cshtml`
- `src/Pegasus.Web/Pages/Unidentified/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Unidentified/Details.cshtml`
- `src/Pegasus.Web/Pages/Unidentified/Details.cshtml.cs`
- `src/Pegasus.Core/Operations/OperationsSnapshot.cs`
- `src/Pegasus.Core/Operations/DashboardCounts.cs`
- `src/Pegasus.Web/Pages/Shared/_Layout.cshtml`
- `src/Pegasus.Web/Pages/Shared/_LayoutAuth.cshtml`
- `src/Pegasus.Web/Pages/Shared/_LayoutExternal.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ShellDialogs.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ErrorSummary.cshtml`
- `src/Pegasus.Web/Pages/Shared/_EvidenceViewer.cshtml`
- `src/Pegasus.Web/Pages/Shared/_FreshnessBanner.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ImageGallery.cshtml`
- `src/Pegasus.Web/Pages/Shared/_InstructionDraftFields.cshtml`
- `src/Pegasus.Web/Pages/Shared/_LucideSprite.cshtml`
- `src/Pegasus.Web/Pages/Shared/_MetricCard.cshtml`
- `src/Pegasus.Web/Pages/Shared/_PageHeader.cshtml`
- `src/Pegasus.Web/Pages/Shared/_Provenance.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ProvenancePanel.cshtml`
- `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml`
- `src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml`
- `src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml`
- `src/Pegasus.Web/Pages/Administration/Shared/_AdminNav.cshtml`
- `src/Pegasus.Web/Presentation/OperatorLabels.cs`
- `src/Pegasus.Web/wwwroot/css/site.css`
- `src/Pegasus.Web/wwwroot/js/site.js`
- `src/Pegasus.Web/wwwroot/favicon.ico`
- `src/Pegasus.Web/wwwroot/fonts/inter/InterVariable.woff2`
- `src/Pegasus.Web/wwwroot/fonts/inter/InterVariable-Italic.woff2`
- `src/Pegasus.Web/wwwroot/fonts/inter/LICENSE.txt`
- `src/Pegasus.Web/wwwroot/images/logo_no_margin.png`
- `src/Pegasus.Web/wwwroot/images/lucide-sprite.svg`
- `src/Pegasus.Web/wwwroot/images/marks/access.png`
- `src/Pegasus.Web/wwwroot/images/marks/accounts.png`
- `src/Pegasus.Web/wwwroot/images/marks/automation.png`
- `src/Pegasus.Web/wwwroot/images/marks/checkmark.png`
- `src/Pegasus.Web/wwwroot/images/marks/configuration.png`
- `src/Pegasus.Web/wwwroot/images/marks/mailboxes.png`
- `src/Pegasus.Web/wwwroot/images/marks/organisations.png`
- `src/Pegasus.Web/wwwroot/images/marks/pegasus-lockup.png`
- `src/Pegasus.Web/wwwroot/images/marks/principals.png`
- `src/Pegasus.Web/wwwroot/images/marks/roles.png`
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs`
- `tests/Pegasus.IntegrationTests/DashboardCountersWebTests.cs`
- `tests/Pegasus.IntegrationTests/WorkCentreLabelTests.cs`
- `tests/Pegasus.IntegrationTests/ShellAndStatusPageWebTests.cs`
- `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/UploadDropzoneBrowserTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/UploadRowsBrowserTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/UploadStatusRefreshBrowserTests.cs`
- `tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs`

**Proposed C files:**

- `src/Pegasus.Core/Search/RetainedMaterialSearch.cs`
- `src/Pegasus.Infrastructure/Persistence/EfRetainedMaterialSearch.cs`
- `src/Pegasus.Web/Pages/Search/_RetainedMaterialPreview.cshtml`
- `src/Pegasus.Web/Pages/Mail/Compose.cshtml`
- `src/Pegasus.Web/Pages/Mail/Compose.cshtml.cs`
- `tests/Pegasus.Core.Tests/Search/RetainedMaterialSearchTests.cs`
- `tests/Pegasus.IntegrationTests/RetainedMaterialSearchWebTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/OuterShellBrowserTests.cs`
- `tests/Pegasus.IntegrationTests/StaffCorrespondenceWebTests.cs`

**A handoff:** C-F07 query mapping/index if measured, C-F08 Graph-retained-mail
and general staff-send contracts, C-F09 shared support and DI. C reads the
A-owned `src/Pegasus.Core/Operations/StaffMailSend.cs` contract but does
not edit it. B consumes shared shell/assets but owns
every file under `src/Pegasus.Web/Pages/Cases/Shared` plus the Case-only
`src/Pegasus.Web/Pages/Shared/_EditFinishConfirm.cshtml` and
`src/Pegasus.Web/Pages/Shared/_EditHeartbeat.cshtml`.

### C09 files

C09 creates no production file. It reviews the integrated C-owned diff and
updates only existing tests or implementation files named above when an
in-scope finding requires it. Evidence output uses the repository's existing
ignored `artifacts/evaluation` convention. Governing documentation changes are
returned to the root/Foundation owner; C does not create new Markdown in the
repository.

## C01 - freeze evidence and absorb the PR 639 and PR 646 corrections

**Owner/model:** Fable 5.1 orchestrates; Opus 5 implements and self-checks.

**Inputs and dependencies:** final integration base; the locally resolvable
principal/report/EVA sources; PR 639 branch
`task/intk-048-unidentified-manual-link` at recorded tip `51e7306c`; PR 646 at
recorded tip `32a5a62ce4f13baba45a0bad06df5498f38dcd19`; and the independently
reviewed PR-069 correction evidence. No schema work starts until Stream A
accepts the recheck-watermark and retained-analysis requests in the handoff JSON.

**Production callers:** `DurableIntake` invokes destination synchronization
during receipt processing and `Pegasus.Worker/IntakeFunctions.cs` invokes the
existing periodic reconciliation sweep. The `/Received/{id:guid}` supported link,
unlink and Triage actions must surface operation conflicts rather than return a
false success. `/Received/{id:guid}` invokes `AnalyzeRetainedInstruction` for
initial analysis and re-evaluation; that command resolves the immutable logical
source/version through A04, calls the existing reader and C profile selector,
and persists candidates without allocating a principal or Case.

**Change:**

1. Generate a read-only execution manifest for the 81 instruction originals,
   29 report PDFs and 14 EVA workbooks from their recorded hashes. Keep the
   manifest under the repository's existing ignored evaluation-artifact area;
   do not add corpus binaries to Git.
2. Diff PR 639 against the integration base by behavior, not by blindly
   merging its stale branch. Port only the final reviewed rule: an Unidentified
   resolution follows the receipt's effective destination across link, unlink,
   Triage creation and relink; completed rechecks leave the candidate page;
   each reopen/re-resolve operation key is unique per transition and stable on
   replay.
3. Preserve `ReconcileUnidentifiedDestinations` as the one Core owner. Extend
   the existing `IUnidentifiedStore` and EF adapter methods; do not create a
   second reconciler or infer state in Web/Worker.
4. Ask Stream A for the single nullable recheck watermark, mapping, migration,
   runtime grant and snapshot. C implements the Core and store behavior around
   that frozen schema.
5. Keep PR 639's unrelated stale test churn and generated migration files out.
   Record a line-by-line preservation table for every retained behavior before
   the old PR is later closed as superseded.
6. Diff PR 646 by behavior and absorb its create-only Provider API residual:
   reuse `EvaluateIntakeCaseMatch.ExecuteDeclaredAsync` and the provider's
   existing normalization/eliminator; a unique or ambiguous existing-Case match
   terminates with `provider_existing_case_match`, mutates no existing Case and
   allocates no duplicate. Keep its documentation/shared-fixture edits with the
   owning streams and record a hunk disposition before root closeout.
7. Implement `AnalyzeRetainedInstruction` as the one production path for
   unresolved retained material. It reads A04's logical document/version,
   identifies a document profile independently of route identity, extracts and
   persists C-B01 candidates, and returns `Analyzed`, `NoProfile`, `Ambiguous`,
   `SourceUnavailable` or `Conflict`. A document match proposes a principal;
   only a staff confirmation or separately accepted versioned route may
   authorize normal Case/PO allocation. Re-evaluation calls the same command
   with expected receipt version and an idempotent operation key.

**Tests and expected outputs:**

- Core: link Case A -> resolve A -> unlink -> reopen -> link Case B -> resolve B
  yields exactly one open/current queue at every stage; replay makes no new
  history; operation conflicts propagate.
- Real SQL: a completed no-change recheck is absent from
  `ListResolutionsToRecheckAsync`; with page size one, the next stale row becomes
  visible; a second sweep returns `Candidates=0`, `Corrected=0`, `Failures=0`.
- Architecture: Worker and `DurableIntake` still call the one reconciler; no
  direct Web state derivation appears.
- Provider API: the first declared instruction creates one Case; an identical
  second submission ends with `provider_existing_case_match`, one Case and one
  link remain, and ambiguous matches also allocate nothing.
- Each of all 15 genuine profile samples reaches extraction through
  `AnalyzeRetainedInstruction`; no-route samples persist candidates but create
  zero Cases, multiple profiles return `Ambiguous`, replay writes no duplicate,
  and staff confirmation is required before allocation.
- Evidence-manifest check reports `81/81`, `29/29`, and `14/14` hash matches,
  identifies the two differently hashed `providers-worked-on.xlsx` copies, and
  reports E01-E28 as `unavailable`, never `passed`.

**Focused commands:**

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReconcileUnidentifiedDestinationsTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~UnidentifiedReconciliationTests|FullyQualifiedName~ProviderApiSubmissionTests|FullyQualifiedName~RetainedInstructionAnalysisTests"
pwsh -File ./scripts/Test-MigrationGrants.ps1
```

Stop if the corrected lifecycle requires another state owner, more than the one
watermark, an invented source fixture, or a swallowed conflict.

## C02 - preserve structured source provenance through the intake pipeline

**Owner/model:** Fable 5.1 orchestrates; Opus 5 implements.

**Production callers:** `AnalyzeRetainedInstruction` and queued intake consume
the reader result; `QdosInstructionExtractionPolicy` and every new profile emit
candidates; `EfIntakeReceiptStore`/`EfIntakeMutationStore` persist them; the
existing instruction draft and provenance partials display them. When the
reader identifies eligible pages, the existing external-work Worker route calls
`IProcessIntakeOcr`, which uses A04 to reopen the exact logical source/version,
calls the C Azure adapter, persists the result, and re-enters
`AnalyzeRetainedInstruction` once.

**Change:**

1. Extend the existing `IntakeContentFragment`/candidate path with one minimal
   structured locator capable of page, table/cell, PDF form field and bounded
   text region. Preserve raw text, role, source label, source/asset hash,
   occurrence and reader/policy version. Do not introduce a parallel document
   model.
2. Extend `MimeKitPdfPigOpenXmlIntakeSourceReader` and its DOC/MSG partial to
   emit structure already exposed by PDFPig/Open XML. Keep EML/MSG outer
   transport, proved original sender, current message body, quoted history and
   attachments distinguishable. Its positive scan-like or unusable-text-map
   result supplies exact page numbers; corrupt, encrypted, non-renderable or
   merely ambiguous documents are refused without OCR.
3. Extend `InstructionFieldExtraction` to bound values by label, sibling cell,
   form field or region. Return every viable source candidate and explicit
   `Missing`/`Ambiguous` outcomes. Do not collapse role conflicts.
4. Hand Stream B the frozen candidate/provenance projection. B remains the
   owner of accepting values into Case/assessment/report aggregates.
5. Add one provider-neutral page OCR contract in `IntakeOcr.cs`: exact logical
   source/version and SHA-256, qualified page list, operation ID, result state,
   provider/model/API version, response hash, page text, words/lines/tables with
   coordinates/confidence, and typed failure/retry metadata. Preserve
   `Pending`, `Processing`, `Completed`, `RetryScheduled`, `Failed` and
   `Unknown`; an uncertain provider side effect stays `Unknown` until the
   recorded operation is reconciled and is never blindly resubmitted.
6. Implement `AzureDocumentIntelligenceOcr` with the existing `HttpClient` and
   `Azure.Identity`, no package, against Azure Document Intelligence
   `prebuilt-layout`, REST `api-version=2024-11-30`. Submit only the qualified
   pages through the documented `pages` parameter, poll the returned operation
   location within the existing bounded external-work attempt, validate the
   operation identity and response, and map coordinates without accepting a
   field from confidence alone. The REST contract is the official
   [Analyze Document API](https://learn.microsoft.com/en-us/rest/api/aiservices/document-models/analyze-document?view=rest-aiservices-v4.0+%282024-11-30%29).
7. Reuse the existing external-work outbox, dispatcher, retry/recovery and
   attribution path. Store source/page/operation identity before the HTTP call;
   completion stores response hash/version and page output atomically before
   re-analysis. Timeout/throttle/outage schedules only a safe retry; malformed,
   low-confidence, missing-structure or inconsistent output fails closed to
   staff review. Do not add another queue, Worker, OCR vendor or runtime.

**Tests and expected outputs:**

- Reader fixtures retain outer sender and current/quoted message bodies as
  separate fragments, preserve attachment identity, page/cell/form locators
  and byte hash, and replay identically.
- OAK header labels bind to their aligned cells; ALS paired columns retain the
  correct party; DFD form fields retain field identity; flattened neighboring
  text cannot swap their values.
- Two different candidates for one field return `Ambiguous` with both raw
  values and locators. Missing model/VAT/date returns unavailable. No test
  expects a guessed value.
- Existing QDOS corpus output stays unchanged except for the explicit C03/C04
  corrections.
- Embedded-text pages result in OCR calls `0`; a scan-like genuine local sample
  and genuine stored YL69YFO provider output, if present, retain page coordinates,
  confidence, response hash and `2024-11-30` provenance. If genuine stored
  output is absent, automated adapter tests use a structural non-domain fake and
  provider correctness remains `INCONCLUSIVE` pending operator activation; no
  OCR text/result is fabricated as evidence.
- Timeout, throttling, restart after submit, ambiguous operation lookup and
  replay recover one durable operation without a second side effect. Corrupt,
  encrypted, non-renderable, low-confidence and ambiguous pages create no
  accepted candidate or Case.

**Focused commands:**

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~InstructionFieldExtraction"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MultiFormatGenuineCorpusWebTests|FullyQualifiedName~AzureDocumentIntelligenceOcrTests|FullyQualifiedName~OcrIntakeRecoveryTests"
```

Stop if a profile must depend on a global positional line number, Infrastructure
would own a business mapping, the reader cannot retain the required layout, or
the adapter needs another OCR package/vendor/runtime.

## C03 - implement all fifteen instruction profiles

**Owner/model:** Fable 5.1 orchestrates batches; Opus 5 implements. Build in
volume order: QDOS/PCH, AX/FW/QCL/OAK, then SBL/BLACK/RJS/DFD/KBS/MP/YML/ALS/BC.
Do not activate a sender route merely because its document profile passes.

Every profile uses [the common method and ranking](../../reports/principals/top-15-principals.md),
its linked immutable samples, and the later local delta map in
[PRINCIPAL_DOCUMENT_MAPS](../../../more_docs/PRINCIPAL_DOCUMENT_MAPS.md).
The delta map's E01-E28-only statements remain guarded and inactive.

| Profile and source | Individual bounded rule | Required corpus-test output |
| --- | --- | --- |
| [QDOS](../../reports/principals/QDOS/method.md) | Extend the existing QDOS policy. Scope `Our Client`, `Our Ref`, `Registration`, `Our Client's Vehicle`, accident/date, explicit Mileage/Speedo, location and circumstances. Keep damage, pre-existing damage, driveability, third-party facts and requested work separate. The principal default is Image Based Assessment even when a repairer address is extracted; a staff repairer-location override records reason and keeps both sources. | Each of five originals emits the exact labelled claimant/reference/VRM and all usable optional fields with locators. Full vehicle text survives; make/model split only with labelled evidence. Damage never appears inside accident circumstances. Missing lower-page evidence is withheld. Existing confirmed data wins a conflict. |
| [PCH](../../reports/principals/PCH/method.md) | Distinguish audit from credit-repair forms; policyholder from driver; principal claim from insurer claim; repairer/storage and hire/rates appendix. Connexus roles remain explicit. Performance/Parkhouse, Lawshield and Everywhen signatures are separate until evidenced. | Five originals produce policyholder, correct claim role, VRM, separately labelled make/model, incident date, explicit location/repairer and available rates. Driver/Connexus/footer values do not replace policyholder/principal. Unproved variants remain unmatched. |
| [AX](../../reports/principals/AX/method.md) | Scope Name/VRM inside Client Details even when Bodyshop Details comes first. Keep bodyshop distinct from inspection location. `Report Due on` is a deadline, never inspection date; tolerate observed section-order variants. | Five originals emit client, AX reference, VRM, labelled vehicle, accident date/circumstances and role-specific bodyshop/location. Deadline is retained with its role and inspection date is unavailable unless explicitly appointed/completed. |
| [FW](../../reports/principals/FW/method.md) | Support a current inline email instruction and attachments. Exclude quoted earlier instructions. Scope insured, third party and inspection-location blocks separately. | Five MSG originals produce insured/reference/VRM/make-model/date/location/circumstances from the current instruction. Quoted values and third-party vehicle/name do not become claimant facts; conflicting current values are ambiguous. |
| [QCL](../../reports/principals/QCL/method.md) | QC Law is issuer; Complex Reports may be sender/intermediary. Support tab/concatenated labels without losing boundaries. Keep Box reference and report-due date roles distinct. | Five DOCX originals emit claimant, `Our Ref`, VRM, explicit make/model, accident date and location. `Report Due on` never fills inspection date; missing model/reference stays unavailable rather than borrowed from metadata. |
| [OAK](../../reports/principals/OAK/method.md) | Bind header-table labels to aligned values; preserve Source/Introducer separately; a generic total-loss request is requested work, not an accepted outcome. | Five DOC originals return the aligned `Our Ref` and instruction date, client/VRM/model/address/circumstances with cell locators. Sequential flattened values fail closed; total-loss wording does not emit a report verdict. |
| [SBL](../../reports/principals/SBL/method.md) | Parse paired sections and route roles, international registration/address shapes, blank placeholders, repairer, hire and rates. No sender domain is currently accepted. | Five PDFs emit policyholder/claim/registration/make-model/date/circumstances plus explicit role-scoped optional fields. Blanks remain unavailable; international values preserve raw form; document match alone returns no automatic route activation. |
| [BLACK](../../reports/principals/BLACK/method.md) | Parse combined Vehicle text with a terminal validated registration; keep claimant address role. Never split on an arbitrary hyphen or fixed line. | Five PDFs preserve combined raw vehicle text and emit VRM only when terminal form validates. Make/model is emitted only where its boundary is proved; address remains claimant/location evidence, not company footer. |
| [RJS](../../reports/principals/RJS/method.md) | Match the exact `Client vehicle registration` family; allow make=`none` and a separately present model; preserve claimant contact/address roles. | Five DOC originals emit client, solicitor reference, VRM and available contact/location. Literal `none` remains an explicit source value/absence state; model may survive independently; no fixed header offsets. |
| [DFD](../../reports/principals/DFD/method.md) | Read the PDF form-field relationships (`Text3`-`Text17`) and keep `Date instructed`, accident date and `Your Reference` roles explicit. | Five PDFs emit only fields whose form geometry proves their label/value association, each with form-field/page locator. Date roles never swap. If original geometry is unavailable, the affected field is ambiguous/unavailable. |
| [KBS](../../reports/principals/KBS/method.md) | Normalize curly/straight apostrophes and whitespace; use explicit client/vehicle/location/contact blocks. A make-only vehicle is genuine absence of model. | Five DOCX/PDF originals emit client/reference/registration/make and scoped location/contact; missing model and VAT stay unavailable; today's date is never emitted. |
| [MP](../../reports/principals/MP/method.md) | Support PDF, Word and affected-page OCR variants. Scope `Our Vehicle` person-only forms. Distinguish `Instruction` from `Inspection`; reject malformed report years. | Eleven originals emit role-correct client/reference/VRM/date/location where proved, preserve scan locators, and withhold malformed dates. Requested inspection never becomes completed inspection. |
| [YML](../../reports/principals/YML/method.md) | Route the confirmed HDUK-branded instruction family to principal YML while preserving issuer HDUK. No generic alias layer or unproved sender-domain activation. | Five HDUK PDFs emit YML as principal candidate and HDUK as document issuer, plus client/reference/VRM/vehicle/date/location/circumstances. The two identities remain separately queryable and source-linked. |
| [ALS](../../reports/principals/ALS/method.md) | Preserve paired client/third-party columns, owner versus client, garage/repairer evidence and explicit `VAT Registered`. Source vehicle classes are not salvage categories. | Five DOC originals emit the correct party-column values, explicit VAT state, reference/VRM/model/date/location/circumstances and repairer. Column swaps, footer addresses and vehicle-class-as-salvage are negative assertions. |
| [BC](../../reports/principals/BC/method.md) | Recognize the unlabelled RTA header token in context rather than requiring `Our Ref`; support desktop/physical request variants; allow a blank inspection address. | Five DOC originals emit client, contextual reference, terminal VRM, incident/instruction dates and available address/narrative. Blank address stays unavailable, physical request remains requested method, and Baker & Coleman spelling alone does not activate a route. |

For every sample, serialize expected candidates as field, normalized value,
raw value, role, document role, source hash, occurrence, page/cell/form/region,
policy key/version and disposition. Include negative assertions for neighboring
party/address/date labels and unchanged confirmed values. Keep the original
five/eleven samples as development evidence; create holdouts only from new
genuine, operator-labelled originals. Do not relabel a design sample as an
untouched holdout.

**Production wiring:** replace the single injected extraction policy with the
smallest Core-owned document-profile selection that reuses
`IInstructionExtractionPolicy` and the existing evidence-state registry.
`AnalyzeRetainedInstruction` is the caller for all fifteen profiles and selects
by versioned document signature and role independently of allocation authority;
accepted route identity is corroborating evidence, never a prerequisite or a
fact inferred from the document. `ProcessIntake` keeps QDOS automatic allocation
and may allocate another profile only after independently accepted route or
staff-confirmed principal evidence. Multiple document profiles remain
ambiguous. Stream A performs the post-C03 DI patch only after the concrete
classes exist and records the C head and patch hash; no reflection, discovery,
stub or no-op registration hook is introduced.

Implement INTK-049 in the same Core profile path. A machine-read VRM from only
document OCR or vehicle-image recognition expands through one finite map,
`O` <-> `0` and `I` <-> `1`, into at most eight distinct stable-order candidates
that match GB current/prefix/suffix/dateless or Northern Ireland syntax. The
valid original comes first. `VehicleRegistrationCandidateLookup` delegates each
candidate to B's existing `IVehicleLookupAdapter`; it does not call DVLA/DVSA or
create another lookup client. Accept exactly one `Current`, `Stale` or `Partial`
candidate only after every other attempt is conclusively `NotFound`. Zero or
multiple viable results and any throttled/unavailable/failed unresolved attempt
remain ambiguous. Preserve raw reading plus every request/result/provenance;
never reinterpret staff-confirmed, ordinary embedded-text or Case-search input.

**Focused commands and expected result:**

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~InstructionExtraction|FullyQualifiedName~MailRoute|FullyQualifiedName~MailClassification"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Corpus|FullyQualifiedName~PrincipalIdentificationCorpusEvidenceTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~RetainedInstructionAnalysisTests|FullyQualifiedName~MachineReadRegistrationLookupTests"
```

Both commands exit 0. The result report contains a per-profile/per-field matrix,
ambiguity and missing counts, no unresolved evidence reference, and no claimed
accuracy threshold without operator-labelled holdouts. Stop on any activation
whose only support is E01-E28, a domain-only principal choice, or a second list
of principal codes.

## C04 - QDOS attachment Triage and fail-closed allocation

**Owner/model:** Fable 5.1 orchestrates; Opus 5 implements.

Use the exact local deltas and source limitations in
[QDOS_IDENTIFICATION_AND_FIELDS](../../../more_docs/QDOS_IDENTIFICATION_AND_FIELDS.md)
alongside the accepted QDOS method and current policy code. E01-E28-only
examples remain unavailable evidence under C01.

**Production callers:** `ProcessIntake` calls `QdosMailRoutePolicy`,
`QdosMailClassificationPolicy` and the selected extraction profile; the
existing Triage registration path allocates Triage; normal case allocation
continues through the existing Case allocator.

**Change:**

1. Add the locally evidenced attachment `Triage Only Request` predicate without
   broad body-keyword matching. The current message body and subject predicates
   remain supported.
2. Collapse byte-identical or proven format-equivalent PDF/DOC renderings into
   one category candidate while retaining both occurrences. Distinct plain and
   combined notification categories remain ambiguous; never invent a winner.
3. Route a definitive triage request to the global Triage sequence
   `T-00001`, `T-00002`, ... with no yearly/principal reset or reuse. It never
   allocates a normal Case/PO. A later formal instruction uses the current Case
   allocator and links the Triage.
4. Remove QDOS damage concatenation into accident circumstances and unsupported
   first-token make/model inference. Use C03's distinct candidates and preserve
   the full vehicle description.
5. Keep the accepted QDOS Image Based Assessment principal default even when
   extraction finds a repairer address. Staff may choose repairer location only
   through the existing address-resolution decision with expected version and
   a required reason; method and location remain independent.

**Tests and expected outputs:** empty-current-body EREF subject plus duplicate
PDF/DOC triage letters yields one Triage and no Case/PO; replay returns the same
T reference. Quoted-only triage text does not match. Plain plus combined
distinct notification evidence is ambiguous. Formal instruction after Triage
allocates one normal Case and links it without consuming/reusing the T number.
Repairer-address extraction leaves effective location `Image Based Assessment`
until a reasoned staff override; overriding does not assert Physical inspection.

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~QdosMailClassificationPolicyTests|FullyQualifiedName~DefinitiveIntakeCaseTypeTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~QdosTriageIntegrationTests|FullyQualifiedName~TriageFromIntakeIntegrationTests"
```

Stop if any pre-case path allocates a Case/PO, if T references can reset/reuse,
or if an address changes inspection method implicitly.

## C05 - extract third-party reports as source evidence

**Owner/model:** Fable 5.1 orchestrates; Opus 5 implements.

Use the [report extraction review](../../reports/third-party-report-extraction.md),
[source inventory](../../reports/third-party-source-inventory.json) and
[candidate checks](../../reports/third-party-candidate-extractions.json). The
29 PDFs are design and regression evidence, not operator-accepted Case facts.

**Production callers:** intake retention and source reader identify document
role; the new Core-owned issuer/profile selector emits source candidates; the
existing Case provenance/chip projection displays them. Stream B owns commands
that accept estimate, valuation, assessment or report facts and owns the final
Pegasus report snapshot.

**Change and family cases:**

- Connexus: preserve amendment/base relationship, header date versus later
  comment date, initial labour £2,394.25 and agreed labour £3,351.95; reconcile
  £5,119.92 net/£1,023.98 VAT/£6,143.90 gross without conflating retail, trade,
  mid and reserve.
- Exclusive EREHR: preserve issuer and reference roles; reconcile page-one
  excluding-VAT with page-two breakdown/gross rather than flagging duplicates.
- EVA bodyshop: classify by issuer/report evidence, not generic EVA layout;
  preserve `Supp1`, repeated text and appended image-page relationships.
- Laird: the Supplementary heading controls; link the base report and do not
  fill omitted fields from an unrelated document.
- Montgomery: preserve the printed 26.2 x £90 contradiction with printed
  labour £1,582.20 while retaining the net/VAT/gross values that reconcile;
  keep model and odometer conflicts distinct.
- sPrint: keep ordinary zero totals and £8,250 contract repair as different
  amount roles; neither is selected by position.
- John R Bell: OCR only affected pages and preserve agreed/revised amounts and
  scan locators; human verification is required for critical low-quality OCR.
- GG/Audatex, MotorCheck, TonBridge invoice and EVA image-only PDFs are negative
  report cases. Route them respectively to the existing estimate, vehicle
  history, invoice evidence and image evidence paths; emit no invented report
  verdict.

The typed C-B02 projection is a strict superset of C-B01. It enumerates issuer;
engineer name/qualifications; report and claim references with roles; report
date; revision/amendment/supplement and related base report; VRM,
make/model/variant, VIN, mileage/unit; claimant; accident and inspection dates;
repairer/location; current outcome, repairability, roadworthiness/reason,
damage zones, severity/narrative, prior damage, tyres, restraints and airbags;
labour hours/rate/amount, paint/materials, parts, special/additional charges,
discounts, net, VAT rate/amount and gross, each with initial/claimed,
assessed/agreed, revised/supplement and contract-repair roles; valuation
guide/date, trade/retail/mid/PAV, mileage/condition adjustments and final value;
salvage category/value/bid, excess, reserve, cash-in-lieu and deductions; repair
duration/range; requested and observed inspection method; comments, supplement
reason and declaration/signatory; and photo/diagram roles.

Every item carries logical source document/version, immutable SHA-256,
occurrence, page/region/table-cell/form-field locator, raw text/value/unit/
currency, normalized candidate, field/party/version role, reader/profile
version and usable/missing/ambiguous/conflicting validation. Arithmetic or
cross-field reconciliation is a separate finding and never changes source
text/value. C does not convert a source candidate into a CE conclusion; B's
existing command makes any accepted Engineer decision.

**Tests and expected outputs:** all 29 originals classify to the recorded family
or explicit negative role; unknown/multiple issuers are ambiguous; supplements
link only to a proved base; replay is deterministic; confirmed CE facts remain
unchanged. End-to-end upload/mail retention -> profile -> candidates -> source
chips works. B's acceptance command receives a frozen source candidate, while
the final report remains blocked/stale according to B's own rules.

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ThirdPartyReport"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ThirdPartyReport|FullyQualifiedName~MultiFormatGenuineCorpusWebTests"
```

Stop if issuer is inferred from principal/folder, a negative file becomes a
report, source arithmetic is silently repaired, or C writes an Engineer value.

## C06 - current principal, organization and address directory

**Owner/model:** Fable 5.1 orchestrates; Sonnet 5 implements after A freezes the
schema and DI hooks.

**Production callers:** existing Organization/Principal administration Core
queries/stores and `/Administration/Organizations` and
`/Administration/Principals`; intake route selection reads the existing
provider reference catalog; `/Administration/ClaimSources` maintains the local
directory; the Case location picker consumes C's
`IInspectionAddressSuggestionQueries` through Stream B; accepted choices use
the existing address policy and resolution store. All mutations require
Administrator authorization, expected version, actor/time/reason and
idempotency.

**Change:**

1. Seed the current top-15 principal identities and only locally accepted route
   evidence. C-F05 freezes these exact codes and principal IDs before the clean
   target migration: QDOS/C001, PCH/C002, AX/C003, FW/C004, QCL/C005, OAK/C006,
   SBL/C007, BLACK/C008, RJS/C009, DFD/C00A, KBS/C00B, MP/C00C, YML/C00D,
   ALS/C00E and BC/C00F, where `Cnnn` abbreviates the full GUIDs stated in the
   handoff JSON. The migration/bootstrap inserts each once and a fresh-database
   test checks exact ID/code uniqueness. Preserve HDUK as document issuer for
   YML. Document-profile evidence is separately versioned and does not activate
   a sender route. Do not import historical
   cases, the 528 EVA contact rows wholesale, 412 `FAO The Court` addressees as
   organizations, inferred G-J columns, or an unaccepted sender domain.
2. Extend the existing organization/principal administration owner with the
   minimum current directory records used by intake and address suggestions:
   stable organization/contact/location identity, role, active state, version
   and source. Keep incoming domain, intermediary, report addressee, repairer,
   outgoing recipient and principal role separate. Define Claim Source as its
   own linked record with stable ID, name, contact, telephone, email, notes,
   active flag, version and audit/provenance; its locations, routes and
   principal identity remain separate. B copies the selected Claim Source ID,
   values and version into the Case, and later directory edits do not rewrite
   that snapshot.
3. Add `InspectionAddressSuggestionQuery`,
   `InspectionAddressSuggestionResult` and
   `IInspectionAddressSuggestionQueries` beside the existing address-choice
   contracts in `InspectionAddressResolution.cs`. Its one method is
   `SearchAsync(Guid caseId, string prefix, CancellationToken)`; require at
   least two normalized characters, and return no suggestions for a shorter
   prefix. The 20-row cap is internal, not caller-configurable. Extend the
   existing `InspectionAddressChoicesQueries.cs` adapter to search the union of
   Case's current claimant, repairer and storage addresses, the principal's
   prior accepted inspection locations, and active locations maintained by
   `OrganizationDirectory.cs`/`EfOrganizationDirectory.cs`.
4. Match a trimmed, collapsed-whitespace, case-insensitive name prefix or an
   uppercase, whitespace-free postcode prefix. Use no fuzzy/geographic
   inference. Deduplicate by stable location ID, order exact normalized matches
   before prefix matches and then by normalized name, postcode and stable ID,
   and return at most 20.
5. Each address result returns stable location ID, display/business name, full
   address, postcode, role, source kind, source record ID and source version.
   No network call, new package or nationwide address-provider integration is
   part of v1. Stream B copies the chosen address and its source/version into
   the Case; later directory edits do not rewrite that snapshot.
6. Manage one principal default inspection-location choice in place: `Image
   Based Assessment` or one sourced/manual physical address. QDOS seeds `Image
   Based Assessment`. No Principal page seeds or offers a physical-attendance
   CE method; all CE assessments remain desktop. A third party's requested or
   observed method is retained as raw evidence only. Later extraction of a
   repairer address creates an address choice and provenance; it does not
   replace the default. A staff address override requires a reason and keeps
   both facts, and selecting an address never changes B's separate CE assessment
   method.
7. Retain only the optional manual EVA setting and explicit staff action. Remove
   `EvaAutomaticSubmission` from C's administration contracts, forms, summaries
   and store methods. A removes its column, automatic store/DI/runtime wiring;
   B keeps EVA as an explicit optional downstream route. Principal-method docs,
   source evidence and seed data never silently enable manual EVA.
8. Present v3 Principal, organization and Claim Sources CRUD using the named
   admin pages, authorization and concurrency errors. Claim Source list/edit/
   create/disable operations retain name, contact, telephone, email and notes;
   writes require Administrator actor, expected version, reason and idempotent
   operation key. No password or
   mailbox controls enter these pages. Directory notes do not create workflow,
   recipients, packages or chasers.

**Tests and expected outputs:** all 15 principals appear once with stable codes;
YML/HDUK roles remain distinct; only accepted domains route; duplicate active
codes/domains and stale writes fail; non-admin writes forbid; disable preserves
referenced history; QDOS defaults to Image Based Assessment; reasoned repairer
override changes location but not CE assessment method and remains source-linked.
Claim Source CRUD round-trips all six data fields, keeps route/location/
principal separate, and a changed directory record does not rewrite a Case.
Address
search returns no more than 20 deterministic prefix matches from the four local
sources, with source/version; non-prefix and inactive entries are absent, and
selecting then editing the directory leaves the Case snapshot unchanged. No
test installs or calls an external address service. Automatic EVA has no schema,
setting, DI registration, runtime work item or UI control; manual EVA remains an
explicit optional action. The fresh database contains no imported historical
Case/email rows.

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Organization|FullyQualifiedName~ProviderInspectionMode"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OrganizationAdministration|FullyQualifiedName~ProviderDomainReference|FullyQualifiedName~InspectionAddress"
```

Stop if seed data assumes a spreadsheet workflow, merges roles, requires a
second principal catalog, calls an external address provider, or reintroduces
automatic EVA.

## C07 - Image Intake, Triage and PR 671 preservation

**Owner/model:** Fable 5.1 orchestrates; Opus 5 implements. A supplies Image
Intake/Triage schema and migrations; C owns Core behavior, store methods and
C-owned pages. Coordinate any Case-list projection with Stream B.

**Production callers:** mailbox/manual image registration uses
`IImageIntakeStore`; `/VehicleImages/{id:guid}`, `/Triage`,
`/Triage/{id:guid}`, Search and Work Centre query the records; the
formal-instruction association path creates the normal Case and link.

**Change:**

1. Re-apply the useful PR 671 behavior against the integration base at recorded
   tip `743311a0f4ac68794672510e596abd7d89ae47bb`: nullable
   known principal on Image Intake, staff set/replace/clear with expected
   version, inactive-principal rejection, `Not known` display and unchanged
   Awaiting-instruction query count. Do not import its migration/snapshot or
   stale Test UI snapshot; A and C reproduce behavior in owned files. Record a
   hunk/behavior disposition table with retained, superseded or rejected reason;
   preserve the branch/evidence and leave closure to the root workflow.
2. Give every pre-case Triage one immutable global T reference. Image evidence
   may create/link a Triage when instruction identity is incomplete; it does not
   allocate a normal Case/PO. Preserve grouped images, registration evidence,
   principal candidate, receipt and all source provenance.
3. Formal instruction creates the normal Case through the existing allocator,
   links the Triage/Image Intake and retains both pre-case references. Wrong or
   ambiguous principal remains staff-resolvable before allocation.
4. Render v3 pre-case queue/detail behavior without merging Triage, Unidentified
   and Image Intake identities or adding a global Images page.
5. Reuse `IntakeEnvelopeLimits` as the single channel-limit owner. Set the
   manual/public per-file cap to exactly `100 * 1024 * 1024` bytes, pin the
   app-wide multipart body budget to exactly
   `(200 * 1024 * 1024) + 64 * 1024` bytes instead of deriving 20 x 100 MB, and
   retain 20 as the staff batch file-count cap. Provider API remains bounded by
   its existing 30 MB decoded envelope, with its per-file effective bound no
   higher than that envelope. F applies matching host/ingress and accepted
   `DocumentRequests` settings only after measuring the 2 GiB Web container;
   configuration may tighten but never raise Core limits.
6. Extend the existing `RequestUploadPolicy` and `EfDocumentRequestStore` with a
   fixed public submission session. Link generation still records the current
   limits version. The first file whose content and custody are accepted starts
   one non-sliding 15-minute window; failed pre-success attempts do not start it.
   Additions and explicit replacements addressed by server-issued occurrence ID
   are allowed until explicit replay-safe finalization or expiry. Finalized or
   expired sessions refuse later bytes without Case disclosure. A limits-version
   mismatch returns typed `LimitsVersionMismatch` plus `MayReissue=true`; B's
   Case-side handler may create a new link only on explicit staff action. It
   never silently migrates or auto-reissues an outstanding link.
7. Implement `RetainIncomingArtifact` for every received, Unidentified, Triage,
   Image Intake and public-upload occurrence. It invokes A's one
   `ICaseArtifactCustody` contract with Case or holding destination, immutable
   occurrence, operation key, media identity, proposed safe original name,
   size/hash and bounded stream. Persist and render `Pending`, `Confirmed`,
   `Failed` or `Unknown`. Pending/failed/unknown never renders upload success,
   never consumes finalization and never ages out from staging. A confirmed
   replay returns the same logical document/version; equal filenames never
   overwrite occurrences. Re-evaluation reads that exact logical version
   through A04 after staging cleanup.
8. Append Triage staff notes through the existing attributed, versioned,
   replay-safe history and use the existing eligible-engineer query for
   assignment. Do not add `Assign to me` or mutable note replacement.

**Tests and expected outputs:** first two independent triages are `T-00001` and
`T-00002`; concurrent allocation is gap-tolerant but unique and never reuses a
committed number. Replay returns the same reference. Image principal can be
set/replaced/cleared only by staff with the expected version; inactive and stale
writes fail. Adding a principal does not change Awaiting counts/query growth.
Formal instruction yields one Case plus retained T/Image links; ambiguous input
yields no Case/PO. Limit tests assert 100 MiB per file, 200 MiB + 64 KiB global
multipart, 20 files and 30 MiB Provider API without a derived 2 GiB budget.
Public-session tests assert the first confirmed file starts exactly 15 minutes,
failed pre-start attempts do not, later successes do not extend expiry,
add/replace/finalize replay safely, expiry/finalization refuse bytes, and version
mismatch yields refusal/reissue. Custody tests cover duplicate occurrences,
same filename/different bytes, partial and uncertain failure, restart/retry,
authorization and exact source-version reads with zero live Box calls.

```powershell
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntake|FullyQualifiedName~Triage|FullyQualifiedName~IntakeEnvelopeLimits|FullyQualifiedName~RequestUpload|FullyQualifiedName~RetainIncomingArtifact"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ImageIntakePersistenceTests|FullyQualifiedName~ImageIntakeWebTests|FullyQualifiedName~TriageQueuesWebTests|FullyQualifiedName~TriageFromIntakeIntegrationTests|FullyQualifiedName~PublicUploadSessionTests|FullyQualifiedName~IncomingArtifactCustodyTests|FullyQualifiedName~ProviderApiSubmissionTests"
```

Stop if Triage and normal Case share an allocator/reference, a principal is
required to register images, a query-per-row regression is introduced, an
accepted upload lacks confirmed custody, or a host limit exceeds Core policy.

## C08 - v3 Inbox, Search, Work Centre, shell and shared Web assets

**Owner/model:** Fable 5.1 orchestrates; Sonnet 5 implements after C02/C06/C07
contracts and A's Graph query contract are stable.

**Production callers and files:** extend `/Inbox` and `/Inbox/{id}` over
`ListRetainedMail`, `GetRetainedMail`, freshness and deleted-mail search;
`/Search/Index` over `ISearchCases`, Image Intake and the new retained-material
query; `/Index` over `OperationsSnapshot`; shared layout/dialog/evidence/status
partials plus `site.css`, `site.js` and the existing icon sprite.

**Complete v3 route and ownership matrix:**

| v3 surface | Production route and PageModel | Owner and implementation result |
| --- | --- | --- |
| Sign-in/account | `/Account/SignIn`, `/Account/PasswordChange`, `/Account/SignOut`, `/Account/AccessDenied` | A-owned identity pages; consume C's external/auth layout styles only. |
| Outer shell | Every authenticated page through `Pages/Shared/_Layout.cshtml`; Ctrl+K handled by `_ShellDialogs.cshtml`/`site.js` | C single owner. One Ctrl+K command palette provides route commands and submits text to server route `/Search?query=...`; there is no second browser-side search engine or alternative global-search control. |
| Work Centre | `/` -> `Pages/Index.cshtml(.cs)` -> `OperationsSnapshot` | C-owned. Preserve five queried metrics, typed attention rows and real target links. |
| Inbox | `/Inbox` -> `Pages/Mail/Index.cshtml(.cs)`; `/Inbox/{id:guid}` -> `Pages/Mail/Message.cshtml(.cs)` | C-owned retained-mail presentation/query. A owns Graph polling/storage. |
| Staff upload | `/Upload`, `/Upload/Status/{id:guid}`, `/Upload/Group/{id:guid}` -> the existing root PageModels | C owns the v3 staff intake presentation; A owns retained-byte storage and queue runtime. Preserve the six typed per-file outcomes. |
| Intake evidence | `/Received/{id:guid}`, `/Received/{id:guid}/Source`, `/Received/{id:guid}/Asset/{assetId:guid}`, `/Received/{id:guid}/Image` | C-owned source review/provenance routes; no new mutation appears on Source/Asset/Image. |
| Cases/new Case | `/Cases`, `/Cases/Create`, `/Cases/{id:guid}`, `/Cases/{id:guid}/Closure`, `/Cases/{id:guid}/Custody`, `/Cases/{id:guid}/Tasks`, `/Cases/{id:guid}/Vehicle`, `/Cases/{id:guid}/Workflow`, `/Cases/{id:guid}/Assessment`, `/Cases/{caseId:guid}/Documents/{occurrenceId:guid}/Download`, `/Cases/{caseId:guid}/Documents/Export`, `/Cases/{caseId:guid}/Eva/Send` | B-owned. C supplies shared shell/assets, address query, pre-case links and provenance contracts only. |
| Search | `/Search` -> `Pages/Search/Index.cshtml(.cs)` with `_CasePreview` and proposed `_RetainedMaterialPreview` | C-owned typed cross-record search; server queries remain authoritative. |
| Triage | `/Triage`, `/Triage/{id:guid}` | C-owned. T references remain distinct from Cases. |
| Unidentified | `/Unidentified`, `/Unidentified/{id:guid}` | C-owned retained unresolved-material workflow; supported actions reuse current Core commands. |
| Image-initiated record | `/VehicleImages/{id:guid}`, reached from Work Centre/Search/Cases | C owns record/detail behavior; B owns its Case-list projection. There is no global Images page. |
| Operations | `/Operations` | A-owned operational runtime page. C links external-work attention rows to it and does not copy its handlers. |
| Administration landing/navigation | `/Administration` and `Pages/Administration/Shared/_AdminNav.cshtml` | A owns the landing page/PageModel; C owns the shared admin navigation partial and shell link matrix; each domain page remains with its named owner. |
| Principal/organization directory | `/Administration/Organizations`, `/Administration/Organizations/Edit/{id:guid}`, `/Administration/Principals`, `/Administration/Principals/Create`, `/Administration/Principals/Replace/{organizationId:guid}/{principalId:guid}`, `/Administration/Principals/EvaSubmission/{organizationId:guid}/{principalId:guid}`, proposed `/Administration/ClaimSources` and `/Administration/ClaimSources/Edit/{id:guid}` | C-owned. EVA page exposes manual optional enablement only. |
| Platform administration | `/Administration/Access`, `/Administration/Accounts`, `/Administration/Accounts/Confirm/{operation}/{staffId:guid}`, `/Administration/Accounts/Edit/{id:guid}`, `/Administration/Roles`, `/Administration/Mailboxes`, `/Administration/MailCategories`, `/Administration/Automation`, `/Administration/Automation/Activity`, `/Administration/Configuration` | A-owned; consumes C navigation/assets. Service health stays Administrator-only. |
| Service health and action logs | proposed `/Administration/ServiceHealth` and `/Administration/ActionLogs` | A-owned platform/audit queries and pages; C supplies navigation/assets only. |
| Report administration/case reports | proposed `/Administration/Reports` and the existing `/Cases/**` report actions | A owns Administration Reports; B owns individual Case report generation/actions. C supplies navigation/assets only. |
| Public upload | `/Uploads/{token}` -> `Pages/Uploads/Request.cshtml(.cs)` | C owns token/session policy and presentation/handlers using the external layout; A owns byte storage/custody adapter and B owns Case-side link create/revoke. C07 supplies the fixed 15-minute session and channel limits. |
| Connector authorization | `/authorize` | A-owned identity/MCP boundary; excluded from C behavior changes. |
| Error/status | `/Error` and `/status/{code:int}` | Existing shared error/status pages remain reachable through the C-owned common layouts/assets; C adds no behavior or explanatory workflow. |

**Change:**

1. Port the v3 shell as Razor/shared assets: rail navigation, the single Ctrl+K
   command palette defined above, record tabs, Add menu, refresh/status and
   notifications. Keep native authorization, routes, anti-forgery, freshness
   and server data; do not paste the prototype's in-memory router, fixtures or
   generated IDs.
2. Inbox retains mailbox/folder/search/queue/unread/sort/page in the URL, shows
   message/attachments/classification/association/thread context, and opens a
   full message without losing the workspace state. Mailbox/folder/search/
   queue/unread/sort/page are URL and retained-query state only. Opening,
   previewing, filtering or changing the unread scope never marks read/unread,
   moves, deletes or categorizes a message in Outlook. A's adapter remains the
   only Graph caller.
3. In `Pages/Mail/Message.cshtml.cs`, add POST handlers `OnPostReplyAsync`,
   `OnPostReplyAllAsync` and `OnPostForwardAsync`, mapping explicitly to the
   corresponding S09 compose modes. `Pages/Mail/Compose.cshtml.cs` uses
   `OnPostSendAsync` for `New`. `Pages/Triage/Details.cshtml.cs` adds
   `OnPostSendChaserAsync`: reply to the selected retained instruction, or use
   `New` only when staff explicitly select a new message. Each invokes A's
   `IStaffMailSend` with server actor, approved mailbox, linked Case/Triage,
   purpose, mode, immutable original-message/thread identity when applicable,
   To/CC, subject/body, authorized attachment versions/hashes, expected context
   version, payload hash and operation key. Require antiforgery. Render A's
   canonical S12 state projection; do not define a C send-state vocabulary.
   Provider acceptance remains Submitted until matching Sent evidence exists.
   Reconciliation is an action on an uncertain operation, not a new send state.
   Same-key/same-payload POST replays; a changed payload or stale/unauthorized
   context makes no send. Unknown never triggers a blind retry. B owns its
   Case/report send handlers against the same A transport.
4. Search returns Cases, retained mail, Triage/Unidentified and Image Intake
   with typed identities and previews. A T reference or Image reference is not
   treated as a Case reference. Invalid/stale selections fail predictably and
   query errors return the existing unavailable response.
5. Work Centre continues to derive its counts and attention list from
   `OperationsSnapshot`; preserve distinct Case, held, mail, Triage and external
   work kinds and route each action to its real production page.
6. Populate the shell notification menu from at most 10 current actionable
   `OperationsSnapshot` attention rows, in the snapshot's stable urgency/order,
   with its typed label and valid production link. Zero rows omits the control's
   list content; do not show a fake `No notifications` item and do not add a
   notification table, client-side list or store.
7. Give Stream B stable shared partial/CSS contracts and a merge window before
   B ports Case pages. C does not edit B-owned Case partials. B must consume the
   shared tokens/assets instead of cloning them.

**Tests and expected outputs:** keyboard actions operate only in safe contexts;
record tabs preserve mail/case context; unauthorized links/actions stay hidden
and server-forbidden; Inbox query state survives preview/full-message/back;
opening is read-only; Search returns each record kind with its own reference and
route; Work Centre counts match source queries and has no N+1 growth. Browser
tests cover narrow/wide layouts, focus, Escape, validation, stale concurrency,
empty and failure states. Snapshot generation includes every routed C page and
the catalogue has no broken local asset/link. Test doubles assert zero Graph
write calls for open/preview/filter/unread/sort/folder actions. All execution
and browser verification uses local substitutes and makes no Outlook, Box,
address-vendor, Glass's, EVA or other external call.
Correspondence tests drive the real PageModel handlers with A's recording local
transport and assert one call on success, zero calls on GET/invalid actor/
mailbox/recipient/attachment/stale context, replay on duplicate POST and visible
`Unknown` without resend after an ambiguous outcome. Public-upload browser tests
prove add, replace, finalize, fixed expiry and version-refusal/reissue against
C07 policy. Notification tests assert 0/1/10/over-10 attention rows, valid links
and no independent query/store.

```powershell
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MailWorkspace|FullyQualifiedName~StaffCorrespondenceWebTests|FullyQualifiedName~PublicUploadSessionTests|FullyQualifiedName~Search|FullyQualifiedName~DashboardCountersWebTests|FullyQualifiedName~WorkCentre"
pwsh -File ./scripts/Update-TestUiSnapshots.ps1
pwsh -File ./scripts/Update-TestUiSnapshots.ps1 -Verify
pwsh -File ./scripts/Test-UiCatalogue.ps1
```

All commands exit 0; snapshot verification uses a fresh capture. Stop if a
shared asset change breaks B's frozen interface, if Web duplicates Core policy,
or if the prototype becomes a production data source.

## C09 - integrate, verify and conduct the fresh whole-stream review

**Owner/model:** a fresh Fable 5.1 context only after C01-C08 are integrated.
It reviews; it does not reopen settled design or add adjacent features.

1. Verify every C-owned production caller is reachable and every A/B handoff is
   implemented at the exact heads recorded in coordination. Run a source-field
   trace from immutable original to reader fragment, policy candidate,
   persisted provenance, C UI and B acceptance/report projection.
2. Produce per-profile and per-report-family matrices with true pass, missing,
   ambiguous and conflict counts. E01-E28 remain explicitly unavailable. A
   passing design corpus is not called an accuracy measurement.
3. Compare PR 639, PR 646 and pinned PR 671
   (`743311a0f4ac68794672510e596abd7d89ae47bb`) preservation tables against the
   final diff. Every
   relevant behavior is present or has a reasoned superseding implementation;
   old PRs remain for the root closeout workflow.
4. Review four lenses: reuse/duplication, simplicity, query/runtime efficiency,
   and abstraction altitude. Fix only in-scope findings and record a disposition
   for every finding.
5. Run the canonical exact-head gates once after focused checks:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category=Corpus"
pwsh -File ./scripts/Test-MigrationGrants.ps1
pwsh -File ./scripts/Update-TestUiSnapshots.ps1 -Verify
pwsh -File ./scripts/Test-UiCatalogue.ps1
```

Every command records cwd, exact SHA, exit code and output. A later pass does
not erase an earlier failure. Completion requires exit 0, no unresolved
cross-stream contract, no source/role loss, no guessed field, no unreviewed
route activation, no N+1 regression, and all C-owned v3 journeys passing. Stop
there; deployment, merge, cloud reset, Outlook/Box/Glass's/EVA writes and old-PR
closure remain outside this stream's implementation authority.
6. After every gate passes and the fresh review findings are disposed, create or
   update exactly one Stream C PR targeting `dev`. Record the PR URL, C head SHA,
   tested SHA and dependencies on the single A and B PRs. Leave all three PRs
   open and unmerged. Do not create a fourth PR, retarget the branch or merge.

## Independent intake-review dispositions

These dispositions close the thirteen findings in `review/intake-review.md`;
the named step and handoff remain the executable authority.

| Finding | Disposition |
| --- | --- |
| C01 | Fixed in C01/C03 and C-F01/C-F03: `AnalyzeRetainedInstruction` is the reachable production command for all fifteen profiles, persists unresolved candidates, and withholds allocation until staff confirmation or an accepted route. Exact clean-target principal IDs/codes are frozen separately from route evidence. |
| C02 | Fixed in C02 and C-F02: page-qualified Azure Document Intelligence `prebuilt-layout` REST, API `2024-11-30`, is wired through the existing external-work path with durable operation recovery and A04 logical-source reads. Missing genuine provider output is `INCONCLUSIVE`, never fabricated. |
| C03 | Fixed in C08 and C-F08: compose/reply/forward/chaser POST handlers call A's one general staff-send port; local recording transport proves handlers without Outlook mutation. |
| C04 | Fixed in C09: exactly one Stream C PR is created or updated against `dev`, recorded and left open/unmerged with the A/B dependencies. |
| C05 | Fixed in C05 and C-B02: the third-party projection enumerates the full report field/version set and is a typed provenance-preserving superset of C-B01. |
| C06 | Fixed in C07/C08 and C-F06: C owns `/Uploads/**` policy/presentation, the fixed non-sliding session and limit-version refusal/reissue; A owns bytes/custody; B owns Case-side link create/revoke. |
| C07 | Fixed in C07 and C-F06: `RetainIncomingArtifact` calls A's custody contract for every received/Unidentified/Triage/Image/public-upload artifact and preserves pending/failed/unknown and logical-version recovery. |
| C08 | Fixed in C06, C-F05 and C-B03: principal default is one inspection-location choice, Image Based Assessment or a sourced/manual address. Third-party requested/observed method stays raw evidence and cannot create a CE physical-attendance method. |
| C09 | Fixed in the exact file map, route matrix, ownership qualification and automatic-EVA split: Foundation/A/B files are read-only dependencies and each owner makes its own changes. |
| C10 | Fixed in C03/C-F03 and the coordination gate: C creates/tests concrete profiles first; A then authors the exact hash-recorded DI patch. No reflection, stubs or no-op registration. |
| C11 | Fixed in C06/C-F05/C-B03: Claim Source has explicit stable identity, contact fields, notes, active/version/audit state and is copied to a Case snapshot independently of locations/routes/principal identity. |
| C12 | Fixed in C08: the menu projects at most ten typed, linked `OperationsSnapshot` attention rows and has no placeholder or independent store. |
| C13 | Fixed in C07/C09: PR 671 is pinned at `743311a0f4ac68794672510e596abd7d89ae47bb`, receives a hunk/behavior disposition, and is checked at the fresh final review. |

## Ticket-by-ticket residual acceptance

This table is additional required scope in the named step, not a separate PR
or licence for adjacent cleanup. Read each linked ticket’s current body/gates.
The current reason overrides stale inherited ticket wording; verify already
integrated clauses and implement only the remaining gap.

| Ticket | Step | Exact residual / acceptance |
| --- | --- | --- |
| CASE-011 | C07 | Triage retains images but lacks the shared viewer. Reuse the current evidence viewer and authorization; do not duplicate files or image controls. |
| CASE-031 | C06 | Claimant address needs extraction, persisted ownership, display and EVA ClmAdd mapping. It is distinct from inspection and repairer addresses. |
| CASE-032 | C07 | Projection change is merged; check source/receipt identity and current pre-case labels, not obsolete image-case allocation language. |
| CASE-037 | C08 | Replace CSP-discarded Search inline actions with the shared shell binding and static href fallback. |
| CASE-041 | C06 | Fast address selection is present. Repairer choice remains inert until INTK-058 supplies real data; source values must be visible before assignment. |
| CASE-042 | C07 | Awaiting instruction is a pre-case queue with no normal Case/PO allocation. Rewrite inherited image-initiated case wording and prove promotion through a real instruction. |
| CASE-045 | C07 | Preserve PR671 optional Image Intake principal behavior with reviewed hunk dispositions; F owns its schema. |
| DELIV-034 | C06 | Verify the merged principal-credential tamper-test correction against its own merge evidence; no need to rebuild the fixed test. |
| DELIV-036 | C03 | Verify the merged QDOS regex-cache/timeout correction while extending source-backed profiles; do not recreate a second regex registry. |
| ENG-011 | C05 | Retain an odometer observation from genuine photo/report evidence with units and source; never infer confirmed mileage from uncertain OCR. B02 owns acceptance/display. |
| ENG-017 | C02 | Use one vehicle-photograph membership policy for intake/completeness/export; logos/document screenshots do not silently qualify. B06 consumes it. |
| INTK-002 | C02 | Name realistic adapter failures and prove composition without creating a generic exception framework. Reuse existing result/refusal conventions. |
| INTK-004 | C02 | One Core decision vocabulary should drive labels and Operations destinations. Separately fix accepted results with no Case identity instead of masking them with labels. |
| INTK-019 | C07 | Use explicit eligible Engineer selection for Triage instead of Assign to me; reuse account query and Core authority. |
| INTK-031 | C05 | Identify issuer and document role, then extract usable report fields with provenance. Its claim that original-report verdict gates normal Case allocation conflicts with current Audit invariants. |
| INTK-032 | C05 | Unknown issuer/layout must not guess an Audit outcome. Use page-level text/OCR fallback and accept only unambiguous labelled fields; expose conflicting and unavailable values individually. |
| INTK-033 | C04 | Triage-request classification is merged into main. The body says it is stranded but later implementation exists; complete exact proof and leave remaining presentation work elsewhere. |
| INTK-034 | C04 | Triage source images are retained in the merged implementation. Shared viewer work remains CASE-011 and should not reopen custody implementation. |
| INTK-035 | C04 | Known-registration promotion from Unidentified is merged. Verify ambiguity/no-registration refusal and close the historical gap after its own gates. |
| INTK-036 | C03 | Instruction date comes only from scoped instruction evidence, never deadline/accident/forward date; preserve source locator. |
| INTK-037 | C07 | Display immutable global T references while retaining internal typed IDs; never allocate normal Case/PO for pre-case material. |
| INTK-038 | C07 | Use shared operator labels for Image Intake analysis and source availability; no raw internal JSON or duplicate label list. |
| INTK-039 | C07 | Grouped matching/custody is merged. Later D50 means image-only material remains pre-case; retire contradictory normal-Case allocation language and verify association. |
| INTK-040 | C07 | Mailbox image routing is merged. Reconcile its destination with Awaiting instruction and preserve grouping and original source identity. |
| INTK-045 | C02 | Share the existing concurrency predicate and inspect every inner layer at the named stores. Surface exhausted conflicts; do not turn this into broad exception normalization. |
| INTK-047 | C08 | Upload pages are ported; per-file details sit beneath one submission decision. Verify the current grouped and public flows after limit/session/custody fixes. |
| INTK-048 | C01 | The recorded implementation and draft PR 639 remain active. Resolve linked Unidentified state through its existing worktree, coordinating with PR-069; do not take it again. |
| INTK-049 | C03 | Resolve only the documented finite machine-read VRM alternatives through existing DVLA/DVSA lookup; exactly one proved result is required, not fuzzy guessing. |
| INTK-051 | C07 | Preserve current upload links/limit generation semantics; after policy change return typed refusal/reissue, never a broken finalize path. |
| INTK-052 | C07 | Enforce accepted separate100MB per-file, approximately200MBmultipart and30MBProviderAPI limits; no derived2GB request budget. F owns host limits. |
| INTK-053 | C02 | A bookkeeping concurrency failure is swallowed without a trace. Record the failure and preserve retry/reconciliation ownership instead of pretending success. |
| INTK-054 | C07 | Append staff Triage notes using existing attributed history and version/replay conventions; no mutable replacement. |
| INTK-055 | C07 | Implement fixed non-sliding15minute public submission sessions with replay-safe finalization, expiry and limit-version refusal; A04 owns durable custody. |
| INTK-056 | C05 | Read standalone Audit outcome from the identified report status, not any repairable/total-loss phrase or previous salvage history elsewhere in the document. |
| INTK-057 | C04 | Two observed historical worker failures expose null CaseType with case_created. Enforce a consistent decision/result before allocation and make unresolved work visible. |
| INTK-058 | C06 | Extract repairer name and address into the per-Case repairer record. Do not confuse it with claimant, principal or inspection location; feed the existing Inspect-at selector. |
| INTK-059 | C07 | Allow an optional known Principal on Triage without making uncertain identity mandatory or creating a normal Case. |
| MAIL-029 | C08 | Preview/search gaps are useful to fix. Restoring a raw Custody column conflicts with the accepted operator evidence view; show actionable availability using existing components. |
| MAIL-034 | C08 | Scope selected-row rules to Inbox so they do not alter Cases rows. Verify both pages at the same viewport. |
| PLAT-028 | C06 | One Principal administration area owns workflow and API settings. Combine overlapping controls with PLAT-050 and top-15 activation evidence; keep credentials secret and scoped. |
| PLAT-029 | C08 | Integrated shell is implemented. Later Case sections and admin health relocation require combined navigation checks; do not preserve dead CSS/routes solely because this ticket introduced them. |
| PLAT-032 | C02 | Unify vehicle inline-image classification across EML/MSG while retaining format differences and excluding logos/signatures. |
| PLAT-043 | C04 | MCP ingress checks its scope, but most Triage commands receive only actor ID text. Pass the typed actor through Core authorization for equivalent Web/MCP policy. |
| PLAT-050 | C06 | EVA toggles and Provider API credentials belong in the existing Principal settings dialog, not a parallel admin concept. |
| PLAT-059 | C08 | Route one Create/Add entry to the accepted instruction-backed allocation dialog, not absent /Cases/Create; B displays the resulting Case. |
| PLAT-061 | C08 | Suppress the empty gated tooltip when no condition exists. Preserve accessible names and real disabled-state reasons. |
| PLAT-065 | C02 | Implement required page-restricted Azure Document Intelligence OCR through existing source reader; F prepares infrastructure, later operator activation proves provider output. |
| PR-069 | C01 | Preserve PR639 reversal/relink lifecycle alongside INTK048, retaining its existing claim/evidence and exact recheck-watermark tests. |
| TICK-001 | C03 | QDOS production acceptance is not implied by passing tests, old deployment or 45.6% workload share. Record operator-reviewed extraction/holdout and complete the usable journey. |
| TICK-034 | C06 | The pack supplies principal/repairer spreadsheets. Normalize candidate addresses with provenance and duplicates for approval; do not bulk-load unreviewed data as business truth. |
| TICK-035 | C03 | The user's top-15 request supersedes post-alpha scheduling. Activate the 14 additional recent-workload principals using the shared route/extraction owners, separately from mailbox onboarding. |
| TICK-041 | C02 | Use Azure OCR only for scan-like/unusable text-map pages; digital extraction first, source/layout retained, no confidence-only acceptance. |
| TICK-058 | C01 | Principal-scoped API is already merged and enabled. PR 646 covers residual behavior; remove the stale API-absent brief and avoid rebuilding the contract. |
| TICK-060 | C04 | Provider status/result behavior exists in the accepted API contract. Verify own-principal receipt/result and failure shapes, then identify only unmet API-03 behavior. |
| TICK-073 | C05 | Use deterministic mappings for supported reports/instructions, OCR for unreadable pages, and AI proposals only for genuinely unsupported material with exact provenance and human acceptance. |
| TICK-074 | C06 | D08 includes sourced directory-backed address suggestions and principal defaults; no separate AI engine or nationwide postcode vendor is needed. B02 saves selected provenance. |
| UIIMP-003 | C08 | Generic prototype integration overlaps the named approved Case/admin tickets. Preserve useful prototype evidence and keep Test UI distinct from deployable Razor changes. |
| UIIMP-009 | C08 | Remove genuinely superseded routes/CSS only after actual caller checks, including dynamic selectors. No speculative whole-site style rewrite or compatibility redirect collection. |
| UIIMP-012 | C08 | Already implementing; preserve its claim. Rename the Triage panel to Notes without changing append-only history semantics and reconcile disabled-action rules with actual preconditions. |



## Stop condition

All assigned implementation, independent review, standalone and combined checks are complete; exactly three replacement PRs target dev, open and unmerged. No merge, deployment, reset or live provider write. External provider/workload evidence remains honestly named operator gates, never fabricated PASS.
