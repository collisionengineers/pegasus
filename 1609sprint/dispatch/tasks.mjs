// Sprint 1609 task table. Consumed by sprint-1609.mjs; nothing here runs on its own.
//
// Field guide
//   kind        deterministic | gh | codex
//   wave        A (before the docs-gate PR merges) | B (after)
//   dir         short worktree directory name under args.worktreeRoot (MAX_PATH headroom)
//   baseRef     start point for a new branch; existing-branch tasks reuse the branch as is
//   verifyFirst run the verifier before the first Codex turn and hand it the failure log
//   build       whether the verifier runs the Release solution build (prose-only tasks skip it)
//   tests       [{ project, filter, env? }] run with --no-build after the build
//   docs        run Test-DocumentationLinks and Test-MarkdownPlacement (base..HEAD)
//   pr          { number } for an existing PR (push + comment), or { title, base } for a new one
//   mergeOrder  prose carried into the PR body
//   expectedDelta (U2 only) the uncommitted paths the prepare stage may commit

const CORE = "tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj";
const INTEGRATION = "tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj";
const ARCHITECTURE = "tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj";

export const CASE_FEATURE_CLASSES = [
  "CaseAssetPreparationWebTests", "CaseClosureWebTests", "CaseCustodyWebTests", "CaseDamageAndViewerWebTests",
  "CaseDetailsWebTests", "CaseEditModeWebTests", "CaseEstimateHeaderWebTests", "CaseRecordFrameV26WebTests",
  "CaseRecordGapsV26WebTests", "CaseReportApprovalWebTests", "CaseTasksWebTests", "CaseValuationV26WebTests",
  "CaseValuationWebTests", "CaseVehicleWebTests", "CaseWorkflowWebTests"
];

export const TASKS = [
  {
    id: "U1", kind: "deterministic", wave: "A",
    branch: "task/sprint-docs-gate", baseRef: "origin/dev", dir: "docs-gate",
    commitTitle: "docs: repair v27 mockup links and admit 1609sprint to the placement gate",
    build: false, tests: [], docs: true,
    pr: { title: "Repair v27 mockup links and admit 1609sprint/ to the Markdown placement gate", base: "dev" },
    mergeOrder: "Merge first: every open PR's documentation lane fails on the two v27-notes links until this is on dev."
  },
  {
    id: "C731", kind: "gh", wave: "A",
    close: { number: 731, comment: "Closing as superseded: #765 replaces this PR on current dev (its body says so) and the audit-reference change tracked under 1609sprint/audit-reference-change removes the remaining reason for it. Branch left in place." }
  },
  {
    id: "U2", kind: "codex", wave: "B",
    branch: "perf/first-use-and-image-cache", existingDir: "performance-next", dir: "performance-next",
    mergeDevFirst: true,
    expectedDelta: [
      ".github/workflows/ci.yml", "docs/runbook.md", "scripts/Invoke-TestShard.ps1", "scripts/Test-TestShard.ps1",
      "tests/Pegasus.IntegrationTests/CaseAssetPreparationWebTests.cs", "tests/Pegasus.IntegrationTests/CaseCapabilityPagesTestSupport.cs",
      "tests/Pegasus.IntegrationTests/CaseClosureWebTests.cs", "tests/Pegasus.IntegrationTests/CaseCustodyWebTests.cs",
      "tests/Pegasus.IntegrationTests/CaseDamageAndViewerWebTests.cs", "tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs",
      "tests/Pegasus.IntegrationTests/CaseEditModeWebTests.cs", "tests/Pegasus.IntegrationTests/CaseEstimateHeaderWebTests.cs",
      "tests/Pegasus.IntegrationTests/CaseRecordFrameV26WebTests.cs", "tests/Pegasus.IntegrationTests/CaseRecordGapsV26WebTests.cs",
      "tests/Pegasus.IntegrationTests/CaseReportApprovalWebTests.cs", "tests/Pegasus.IntegrationTests/CaseTasksWebTests.cs",
      "tests/Pegasus.IntegrationTests/CaseValuationV26WebTests.cs", "tests/Pegasus.IntegrationTests/CaseValuationWebTests.cs",
      "tests/Pegasus.IntegrationTests/CaseVehicleWebTests.cs", "tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs",
      "tests/Pegasus.IntegrationTests/CaseWebTestSupport.Recording.cs"
    ],
    deltaCommitTitle: "test: split CaseDetails into feature classes and run six SQL shards",
    brief: "tasks/U2.md",
    reviewFirst: true,
    commitTitle: "test: review corrections for the six-shard Case test split",
    build: true,
    tests: [
      { project: INTEGRATION, filter: CASE_FEATURE_CLASSES.map((c) => `FullyQualifiedName~Pegasus.IntegrationTests.${c}.`).join("|"), label: "fifteen-case-classes" }
    ],
    preTestScripts: [{ name: "shard-regression", args: ["scripts/Test-TestShard.ps1"] }],
    discoveryCheck: true,
    docs: true,
    pr: { number: 764, refreshBody: true },
    mergeOrder: "Merge before task/estimate-import-route and task/audit-reference-a (both touch files this split rewrites)."
  },
  {
    id: "U3", kind: "codex", wave: "B",
    branch: "perf/first-use-and-image-cache", existingDir: "performance-next", dir: "performance-next",
    brief: "tasks/U3.md",
    commitTitle: "test: build the Production host once for the telemetry phase assertions",
    build: true,
    tests: [{ project: INTEGRATION, filter: "FullyQualifiedName~ProductionCompositionTests|FullyQualifiedName~WorkspaceRequestTimingFilterTests" }],
    docs: true,
    pr: { number: 764 },
    mergeOrder: "Same PR as the six-shard slice; removes the ~35-minute serial class that timed out shard 6."
  },
  {
    id: "P766", kind: "codex", wave: "B",
    branch: "codex/u50-pdf-image-intake", baseRef: "origin/codex/u50-pdf-image-intake", dir: "u50-intake",
    brief: "tasks/P766.md", verifyFirst: true,
    commitTitle: "fix: restore the U50 image-intake and vehicle-lookup test contracts",
    build: true,
    tests: [
      { project: CORE, filter: "FullyQualifiedName~AutomaticImageIntakeTests|FullyQualifiedName~ProcessIntakeTests" },
      { project: INTEGRATION, filter: "FullyQualifiedName~AutomaticVehicleLookupTests" }
    ],
    docs: true,
    pr: { number: 766 },
    mergeOrder: "Base of #767; merge before it."
  },
  {
    id: "P765", kind: "codex", wave: "B",
    branch: "task/audit-original-report-current", existingDir: "audit-original-report", dir: "audit-original-report",
    brief: "tasks/P765.md", verifyFirst: true,
    commitTitle: "fix: custody outbox recovery for supplied mailbox reports",
    build: true,
    tests: [{ project: INTEGRATION, filter: "FullyQualifiedName~CustodyOutboxIntegrationTests" }],
    docs: true,
    pr: { number: 765 },
    mergeOrder: "Base of task/audit-reference-a; merge before it."
  },
  {
    id: "P767", kind: "codex", wave: "B", dependsOn: ["P766"],
    branch: "codex/case-linking-pdf", existingDir: "case-linking", dir: "case-linking",
    mergeBranchFirst: "origin/codex/u50-pdf-image-intake", placementBase: "origin/codex/u50-pdf-image-intake",
    brief: "tasks/P767.md", verifyFirst: true,
    commitTitle: "fix: case-linking PDF custody and intake test contracts",
    build: true,
    tests: [{ project: INTEGRATION, filter: "FullyQualifiedName~MultiFormatIntakeWebTests|FullyQualifiedName~UnidentifiedRecordWebTests|FullyQualifiedName~ImageCaseCustodyIntegrationTests|FullyQualifiedName~ImageViewingWebTests|FullyQualifiedName~QdosAllocationRecoveryTests" }],
    docs: true,
    pr: { number: 767 },
    mergeOrder: "Stacked on #766."
  },
  {
    id: "I1", kind: "codex", wave: "B",
    branch: "task/estimate-import-route", baseRef: "origin/dev", dir: "est-import",
    brief: "tasks/I1.md",
    commitTitle: "fix: make estimate import refusals visible and accept Glass's section rounding",
    build: true,
    tests: [{ project: INTEGRATION, filter: "FullyQualifiedName~AssessmentEstimateImportWebTests|FullyQualifiedName~GlassEstimatePdfParserTests", env: { PEGASUS_REFERENCE_PACK_ROOT: "__REFERENCE_PACK__" } }],
    docs: true,
    pr: { title: "Estimate import: open dialogs in place, surface refusals, accept Glass's section rounding", base: "dev" },
    mergeOrder: "site.js and case-workspace.js also change in #764; merge #764 first, then rebase this branch."
  },
  {
    id: "I2", kind: "codex", wave: "B",
    branch: "task/inbox-unidentified-resolved", baseRef: "origin/dev", dir: "inbox-unid",
    brief: "tasks/I2.md",
    commitTitle: "fix: leave the Inbox Unidentified scope once the item resolves",
    build: true,
    tests: [
      { project: INTEGRATION, filter: "FullyQualifiedName~RetainedMailPersistenceTests|FullyQualifiedName~MailWorkspaceWebTests" },
      { project: CORE, filter: "FullyQualifiedName~MailOperationalDestinationPolicyTests" }
    ],
    docs: true,
    pr: { title: "Inbox: a resolved Unidentified item leaves the Unidentified scope", base: "dev" },
    mergeOrder: "#767 edits Message.cshtml, MailWorkspaceWebTests.cs and FRD-08; whichever merges second rebases."
  },
  {
    id: "I3", kind: "codex", wave: "B",
    branch: "task/vehicle-type-autofill", baseRef: "origin/dev", dir: "veh-type",
    brief: "tasks/I3.md",
    commitTitle: "feat: derive Vehicle type from the DVLA lookup",
    build: true,
    tests: [
      { project: CORE, filter: "FullyQualifiedName~Vehicle|FullyQualifiedName~Assessment" },
      { project: INTEGRATION, filter: "FullyQualifiedName~VehicleLookupGapFill|FullyQualifiedName~ProductionVehicleLookup|FullyQualifiedName~CaseRecordGapsV26|FullyQualifiedName~Migration" },
      { project: ARCHITECTURE, filter: null }
    ],
    docs: true,
    pr: { title: "Auto-fill Vehicle type from the DVLA lookup", base: "dev" },
    mergeOrder: "Independent."
  },
  {
    id: "I4", kind: "codex", wave: "B",
    branch: "task/audit-quick-fixes", baseRef: "origin/dev", dir: "audit-fixes",
    brief: "tasks/I4.md",
    commitTitle: "fix: audit quick fixes (upload link, heartbeat codes, POST-only pages, Worker schedule, batch names)",
    build: true,
    tests: [
      { project: INTEGRATION, filter: "FullyQualifiedName~UploadConfirmationWebTests|FullyQualifiedName~UploadOutcomeQueriesTests|FullyQualifiedName~MailWorkspaceWebTests|FullyQualifiedName~WorkerActivationReleaseContractTests|FullyQualifiedName~CaseWorkflowWebTests" },
      { project: CORE, filter: "FullyQualifiedName~ActorDisplayNames" },
      { project: ARCHITECTURE, filter: null }
    ],
    docs: true,
    pr: { title: "Codebase audit quick fixes (16 Sep)", base: "dev" },
    mergeOrder: "Touches the intake upload surface shared with #766/#767; rebase whichever merges second."
  },
  {
    id: "I5", kind: "codex", wave: "B",
    branch: "task/report-generation-freshness", baseRef: "origin/dev", dir: "rpt-fresh",
    brief: "tasks/I5.md", deliverOnReviewExhaustion: true,
    commitTitle: "fix: report generation identity, freshness and rendering corrections",
    build: true,
    tests: [
      { project: INTEGRATION, filter: "FullyQualifiedName~Reports|FullyQualifiedName~CaseReportApproval|FullyQualifiedName~AssessmentReportDraft|FullyQualifiedName~CaseWorkspace" },
      { project: CORE, filter: "FullyQualifiedName~Reports" }
    ],
    docs: true,
    pr: { title: "Report and fee note: command identity, freshness policy, footer and culture corrections", base: "dev" },
    mergeOrder: "Merge before task/estimate-document (both edit AssessmentReportLayout.cs)."
  },
  {
    id: "I6", kind: "codex", wave: "B", dependsOn: ["I5"],
    branch: "task/estimate-document", baseRef: "task/report-generation-freshness", dir: "est-doc",
    placementBase: "task/report-generation-freshness",
    brief: "tasks/I6.md",
    commitTitle: "feat: estimate document PDF and specialist-hours calculation fixes",
    build: true,
    tests: [
      { project: CORE, filter: "FullyQualifiedName~Estimate|FullyQualifiedName~Reports|FullyQualifiedName~RepairSpecification" },
      { project: INTEGRATION, filter: "FullyQualifiedName~Reports|FullyQualifiedName~CaseEstimateHeader|FullyQualifiedName~AssessmentEstimate|FullyQualifiedName~RepairSpecification" },
      { project: ARCHITECTURE, filter: null }
    ],
    docs: true,
    pr: { title: "Estimate document (PDF) with specialist-hours calculation fixes", base: "task/report-generation-freshness" },
    mergeOrder: "Stacked on task/report-generation-freshness (both edit AssessmentReportLayout.cs); GitHub retargets to dev when that merges."
  },
  {
    id: "I7", kind: "codex", wave: "B",
    branch: "task/ui-guardrails", baseRef: "origin/dev", dir: "ui-guard",
    brief: "tasks/I7.md",
    commitTitle: "docs: add the Pegasus UI guardrails skill and root routing rule",
    build: false, tests: [], docs: true,
    pr: { title: "Pegasus UI guardrails skill and root routing rule", base: "dev" },
    mergeOrder: "Independent."
  },
  {
    id: "I8", kind: "codex", wave: "B", dependsOn: ["P765"],
    branch: "task/audit-reference-a", baseRef: "origin/task/audit-original-report-current", dir: "audit-a",
    placementBase: "origin/task/audit-original-report-current",
    brief: "tasks/I8.md",
    commitTitle: "feat: one Audit reference prefix (a.) for both assessment outcomes",
    build: true,
    tests: [
      { project: CORE, filter: "FullyQualifiedName~Audit|FullyQualifiedName~CreateAuditCase" },
      { project: INTEGRATION, filter: "FullyQualifiedName~CaseRecordFrameV26|FullyQualifiedName~ProviderApiSubmission|FullyQualifiedName~CaseCreate|FullyQualifiedName~LinkedCaseReplacement|FullyQualifiedName~AuditCase|FullyQualifiedName~CaseAcceptance" }
    ],
    docs: true,
    pr: { title: "Audit reference: a. prefix for both outcomes (stacked on #765)", base: "task/audit-original-report-current" },
    mergeOrder: "Stacked on #765. CaseRecordFrameV26WebTests.cs is rewritten by the six-shard split in #764: rebase after #764 merges."
  }
];
