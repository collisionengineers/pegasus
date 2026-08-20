# Post-implementation report — PLAT-002

## Summary

Added one metadata-free Web `StaffPageModel` that owns staff claim translation
and operation-key generation, then migrated every current staff actor caller to
that root directly or through the existing administration, case-mutation, and
upload-confirmation bases. The anonymous upload-request page remains outside
staff inheritance and manual-upload receipt identity remains separate. The
result removes 255 lines of duplicate code, adds ownership guards, and preserves
all endpoint behaviour.

## Changes

| File | Change | Why |
|---|---|---|
| docs/current-architecture.md | Modified | Record the as-built staff request-context owner and anonymous/receipt boundaries. |
| src/Pegasus.Web/Pages/StaffPageModel.cs | Added | Provide the sole Web StaffActorFactory adapter and operation-key generator. |
| src/Pegasus.Web/Pages/Administration/AdministrationPageModel.cs | Modified | Inherit the root; retain only administration key validation. |
| src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs | Modified | Inherit the root; retain case-only command, lease, TempData, readiness, and logging mechanics. |
| src/Pegasus.Web/Pages/UploadConfirmationPageModel.cs | Modified | Reuse the root for both existing upload-status descendants without editing them separately. |
| src/Pegasus.Web/Pages/Account/PasswordChange.cshtml.cs | Modified | Reuse shared actor/key methods while preserving local SubjectId-to-Guid parsing. |
| src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs | Modified | Remove local actor/key owners; retain local key validation. |
| src/Pegasus.Web/Pages/Cases/Create.cshtml.cs | Modified | Reuse actor resolution and operation-key generation. |
| src/Pegasus.Web/Pages/Cases/Documents/Download.cshtml.cs | Modified | Reuse actor resolution; preserve explicit authorization and download responses. |
| src/Pegasus.Web/Pages/Cases/Index.cshtml.cs | Modified | Remove its actor helper and inherit the root. |
| src/Pegasus.Web/Pages/ImageIntake/Details.cshtml.cs | Modified | Replace its inline actor factory clause. |
| src/Pegasus.Web/Pages/Index.cshtml.cs | Modified | Replace the dashboard inline actor factory clause. |
| src/Pegasus.Web/Pages/Intake/Details.cshtml.cs | Modified | Replace four actor factory clauses without changing handler conditions/results. |
| src/Pegasus.Web/Pages/Intake/Image.cshtml.cs | Modified | Reconcile the fresh-base inline image caller into the root. |
| src/Pegasus.Web/Pages/Intake/Source.cshtml.cs | Modified | Replace the retained-source actor factory clause. |
| src/Pegasus.Web/Pages/Mail/Index.cshtml.cs | Modified | Replace the list-page actor factory clause. |
| src/Pegasus.Web/Pages/Mail/Message.cshtml.cs | Modified | Replace both message-page actor factory clauses. |
| src/Pegasus.Web/Pages/Operations/Index.cshtml.cs | Modified | Remove local actor/key owners while preserving lease flow. |
| src/Pegasus.Web/Pages/Triage/Details.cshtml.cs | Modified | Reuse shared actor resolution/key generation while retaining local non-empty staff-Guid parsing. |
| src/Pegasus.Web/Pages/Triage/Index.cshtml.cs | Modified | Replace the queue actor factory clause. |
| src/Pegasus.Web/Pages/Unidentified/Details.cshtml.cs | Modified | Replace two clauses while preserving conditional short-circuit order. |
| src/Pegasus.Web/Pages/Upload.cshtml.cs | Modified | Reuse actor resolution while keeping ExternalReceiptToken as a separate intake replay identity. |
| src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs | Modified | Reuse static operation-key generation while remaining AllowAnonymous on PageModel. |
| tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs | Modified | Guard the sole actor owner, exact two GUID-N sites by concept, and anonymous non-staff inheritance boundary. |

## Governing docs

PLAT-002 has no linked PRD, FRD, or ADR, and its chore profile requires none.
The change is behaviour-preserving Web composition cleanup under the repository
one-owner and simplicity rules. No governing requirement or durable
architectural choice changed. The downstream as-built snapshot was refreshed.

## Risks / follow-ups

No follow-up is deferred. The source-based ownership guard intentionally follows
the existing architecture-test convention. It counts exact GUID-N occurrences
and pins their two semantic owners; alternative future generation syntax would
still require review but is not claimed to be statically impossible.

The first combined focused-test command exceeded its 120-second wrapper; its
orphan process was stopped after it ceased meaningful progress. The exact 15
planned classes were then rerun in three clean captured batches, all green.

## Verification hand-off

On merged `main`, run:

- `dotnet restore ./Pegasus.slnx --locked-mode`
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`
- The plan's exact 15-class IntegrationTests filter (one command or equivalent non-overlapping batches).
- `rg -n "StaffActorFactory\.TryCreate" src/Pegasus.Web` — expect only StaffPageModel.cs.
- `rg -n 'Guid\.NewGuid\(\)\.ToString\("N"\)' src/Pegasus.Web/Pages --glob '*.cs'` — expect StaffPageModel.cs and Upload.cshtml.cs only.
- Confirm RequestModel remains `[AllowAnonymous]`, directly derives from PageModel, and calls `StaffPageModel.NewOperationKey`.

Task-branch evidence at commit `62502995a7b452977f596c5bd72b44296f3710ec`:

- Locked restore: passed.
- Release build: passed, 0 warnings and 0 errors.
- Architecture tests: 98 passed, 0 failed.
- Focused integration tests: 114 passed, 6 skipped, 0 failed across all 15 planned classes.
- Inventory and anonymous-boundary checks: matched expected owners.
