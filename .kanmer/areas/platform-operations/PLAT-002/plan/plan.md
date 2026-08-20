# Plan — PLAT-002: give Web pages one staff-actor root

Estimated diff: 24 files, about +90 / -250. Web-only mechanical consolidation;
no user-visible, Core, database, Infrastructure, or deployment change.

## Approach

Add one metadata-free StaffPageModel above every staff Razor Page that currently
parses claims. It reuses StaffActorFactory unchanged and owns the one public
N-format operation-key generator. AdministrationPageModel and
CaseMutationPageModel derive from it; the remaining actor callers derive from it
directly. The AllowAnonymous upload-request page remains on PageModel and calls
the static key generator by type. This beats a neutral utility because there is
no second policy or external boundary to justify another abstraction, and it
beats the rejected narrow option because the user selected complete
consolidation and the one-owner checks can then be true.

## Governing docs

PLAT-002 has no linked PRD, FRD, or ADR and its resolved chore profile does not
require one. The change preserves product behaviour and makes no durable
architectural choice: it applies the existing AGENTS.md and
docs/engineering.md one-owner/simplicity rules inside the existing Web project.
No governing document is modified or created. docs/current-architecture.md is
updated only as the downstream as-built snapshot.

## Steps

1. Add src/Pegasus.Web/Pages/StaffPageModel.cs by reusing the exact claim inputs
   and StaffActorFactory call from AdministrationPageModel. Give TryGetActor the
   existing NotNullWhen nullable signature and move the exact
   Guid.NewGuid().ToString("N") generator here as public static. Add no
   authorization metadata, service, validation, or unrelated helper.
2. Make AdministrationPageModel and CaseMutationPageModel derive from the new
   root and delete only their duplicate actor/key implementations and obsolete
   usings. Build before migrating direct pages so the two existing inheritance
   trees and Case Details Razor static calls are proved unchanged.
3. Migrate all 18 direct actor-calling page models in the files survey to
   StaffPageModel. Replace inline factory blocks/private helpers with
   TryGetActor; preserve each existing condition and failure result. In
   PasswordChange and Triage/Details, keep local parsing of actor.SubjectId for
   the existing staff Guid. Delete the remaining application key copies.
4. Keep Uploads/Request explicitly AllowAnonymous and deriving from PageModel;
   replace only its private generator with StaffPageModel.NewOperationKey.
   Add architecture coverage in DependencyDirectionTests using the existing
   FindRepositoryRoot/source-scan convention: exactly one Web factory call,
   exactly one application Pages N-format generator, and reflection proof that
   RequestModel is anonymous and not a StaffPageModel.
5. Refresh docs/current-architecture.md with the new as-built Web owner. Run the
   focused verification below, then the required simplification pass over the
   branch diff (reuse, simplification, efficiency, altitude), applying
   behaviour-preserving findings and recording every disposition under a dated
   Simplification pass section appended to this plan before the PR opens.

## Verification

Proof is produced from the task branch and later repeated on merged dev:

- dotnet restore --locked-mode
- dotnet build --configuration Release
- dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build
- dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build --filter "FullyQualifiedName~ShellAndStatusPageWebTests|FullyQualifiedName~AdministrationSearchAccountWebTests|FullyQualifiedName~StaffSignInSecurityTests|FullyQualifiedName~CaseCreateWebTests|FullyQualifiedName~CasesIndexWebTests|FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~AssessmentReportDraftWebTests|FullyQualifiedName~QdosCustodialWebTests|FullyQualifiedName~QdosIntakeWebTests|FullyQualifiedName~IntakeWebNegativeTests|FullyQualifiedName~ImageIntakeWebTests|FullyQualifiedName~MailWorkspaceWebTests|FullyQualifiedName~OperationsWebTests|FullyQualifiedName~QdosTriageIntegrationTests|FullyQualifiedName~TriageQueuesWebTests"
- rg "StaffActorFactory.TryCreate" src/Pegasus.Web returns only StaffPageModel.cs.
- rg "Guid.NewGuid\(\)\.ToString\(\"N\"\)" src/Pegasus.Web/Pages returns
  only StaffPageModel.cs; test-only generators are intentionally excluded.
- rg verifies Uploads/Request still contains AllowAnonymous, derives from
  PageModel rather than StaffPageModel, and calls StaffPageModel.NewOperationKey.

The post-implementation report records changed files, the mechanical
preservation claims, simplification dispositions, exact command results, and
any deviation. proof.md records the same checks on merged dev.

## Risks / open questions

- Static member access through CaseMutationPageModel is expected C# inheritance
  behaviour; the Step 2 build is the early stop check. If it does not compile,
  update the Razor calls to StaffPageModel.NewOperationKey without changing
  generated values.
- Inline call sites combine actor resolution with other conditions. Replace only
  the factory clause and retain operand order so short-circuit behaviour stays
  unchanged.
- The root must stay metadata-free. Authorization remains explicit or supplied
  by Program.cs fallback; architecture coverage protects the anonymous
  exception.
- The source inventory may change before execution. Re-run both rg inventories
  in the task worktree before editing and add any new application caller to this
  same mechanical slice; stop if it represents a materially different policy.
- No open user question remains; complete consolidation was selected on
  2026-08-20.
