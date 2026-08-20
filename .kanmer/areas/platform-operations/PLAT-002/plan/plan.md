# Plan — PLAT-002: give Web pages one staff-actor root

Estimated diff: 24 files, about +90 / -250. Web-only mechanical consolidation;
no user-visible, Core, database, Infrastructure, or deployment change.

## Approach

Add one metadata-free StaffPageModel above every staff Razor Page that currently
parses claims. It reuses StaffActorFactory unchanged and owns the one public
operation-key generator. AdministrationPageModel and CaseMutationPageModel
derive from it; the remaining actor callers derive directly. Anonymous
Uploads/Request remains on PageModel and calls the static operation-key method.
Upload keeps its local ExternalReceiptToken generation because receipt identity
is a separate concept despite sharing the GUID-N representation.

This is smaller and clearer than a neutral key utility: there is no second
operation-key policy or external boundary. It also satisfies the user-selected
complete consolidation without pulling unrelated receipt semantics into a
staff-page abstraction.

## Governing docs

PLAT-002 has no linked PRD, FRD, or ADR, and its resolved chore profile does not
require one. Behaviour and durable architecture are unchanged; this applies the
existing AGENTS.md and docs/engineering.md one-owner rules within Web.
No governing document is created or modified. docs/current-architecture.md is
updated only as the downstream as-built snapshot.

## Steps

1. Add `src/Pegasus.Web/Pages/StaffPageModel.cs`, reusing the exact claim
   inputs and StaffActorFactory call from AdministrationPageModel. Use the
   existing `[NotNullWhen(true)] out ActionActor?` signature and the exact
   public static GUID-N operation-key generator. Add no authorization metadata,
   service, validation, receipt-token logic, or unrelated helper.
2. Derive AdministrationPageModel and CaseMutationPageModel from the root and
   remove only duplicate actor/operation-key code and obsolete usings. Run a
   Release build now to prove both inheritance trees and existing Case Details
   Razor static calls before wider migration; if Razor requires it, name
   StaffPageModel directly at those view calls.
3. Migrate all 18 direct actor callers in files.md to StaffPageModel. Replace
   only factory clauses/helpers, retaining operand order, short-circuiting,
   failure results, and authorization. Keep local SubjectId-to-Guid parsing in
   PasswordChange and Triage/Details. Replace the remaining operation-key
   copies. In Upload, change actor resolution only and preserve its distinct
   ExternalReceiptToken generation and validation.
4. Keep Uploads/Request explicitly AllowAnonymous and deriving from PageModel;
   replace its private operation-key method with
   StaffPageModel.NewOperationKey. In DependencyDirectionTests, reuse
   FindRepositoryRoot/source inspection to assert: the sole Web
   StaffActorFactory call is in StaffPageModel; Pages have exactly the two
   intentional GUID-N application sites (StaffPageModel operation keys and
   Upload receipt tokens); RequestModel remains anonymous and outside the staff
   inheritance tree.
5. Refresh docs/current-architecture.md. Run all four simplification lenses
   (reuse, simplification, efficiency, altitude) over the branch diff and
   immediate surroundings. Apply behaviour-preserving findings and append a
   dated Simplification pass section here with every finding disposition.
6. After simplification, run the exact verification below. Record changed files,
   preservation claims, simplification dispositions, command results, and any
   deviation in the post-implementation report before opening the PR.

## Verification

On the final task branch:

- `dotnet restore ./Pegasus.slnx --locked-mode`
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ShellAndStatusPageWebTests|FullyQualifiedName~AdministrationSearchAccountWebTests|FullyQualifiedName~StaffSignInSecurityTests|FullyQualifiedName~CaseCreateWebTests|FullyQualifiedName~CasesIndexWebTests|FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~AssessmentReportDraftWebTests|FullyQualifiedName~QdosCustodialWebTests|FullyQualifiedName~QdosIntakeWebTests|FullyQualifiedName~IntakeWebNegativeTests|FullyQualifiedName~ImageIntakeWebTests|FullyQualifiedName~MailWorkspaceWebTests|FullyQualifiedName~OperationsWebTests|FullyQualifiedName~QdosTriageIntegrationTests|FullyQualifiedName~TriageQueuesWebTests"`
- `rg -n "StaffActorFactory\.TryCreate" src/Pegasus.Web` returns only
  StaffPageModel.cs.
- The GUID-N inventory returns exactly StaffPageModel.cs (operation key) and
  Upload.cshtml.cs (receipt token); no duplicate operation-key owner remains.
- Uploads/Request remains AllowAnonymous, derives from PageModel rather than
  StaffPageModel, and calls StaffPageModel.NewOperationKey.

Per repository workflow, proof.md repeats the required evidence on merged
`main`, not on the pre-merge branch or merged `dev`.

## Risks / open questions

- Static access through CaseMutationPageModel should compile by C# inheritance;
  Step 2 is the early stop check and the direct StaffPageModel name is the
  behaviour-neutral fallback.
- Inline factory calls participate in compound conditions; replace only that
  clause and preserve operand order.
- StaffPageModel must remain metadata-free. Existing explicit/fallback
  authorization stays authoritative; the anonymous exception is guarded.
- A raw GUID-N search crosses two concepts. Do not “simplify” Upload's receipt
  token into the operation-key owner; assert both intentional sites.
- Re-run both inventories in the task worktree before editing. Include newly
  found instances only when they share these verified semantics; stop on a
  materially different policy.
- No open user question remains.
