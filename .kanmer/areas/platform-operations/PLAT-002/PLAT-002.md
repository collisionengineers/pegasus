---
id: PLAT-002
type: ticket
title: Give the Web pages one staff-actor root (TryGetActor / NewOperationKey)
status: done
area: platform-operations
order: 1430
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-20T08:42:52.536Z'
  review: '2026-08-20T09:21:03.098Z'
  verifying: '2026-08-20T09:33:45.453Z'
  done: '2026-08-20T12:46:22.730Z'
labels:
  - simplify
  - web
  - follow-up
links:
  - SIMPLI-011
commits:
  - 62502995a7b452977f596c5bd72b44296f3710ec
prs:
  - '467'
deployment: production
archived: false
created: '2026-08-17T14:19:26.387Z'
updated: '2026-08-26T14:34:45.170Z'
---

## Why

The staff-actor resolution (`StaffActorFactory.TryCreate` from the `ClaimTypes.NameIdentifier` / role claims → `TryGetActor`) and `NewOperationKey()` are now carried by two abstract Web bases — `Pages/Cases/CaseMutationPageModel.cs` (from [[SIMPLI-011]]) and `Pages/Administration/AdministrationPageModel.cs` — plus private copies in `Operations/Index`, `Triage/Details`, `Cases/Assessment/Index`, `Account/PasswordChange`, `Cases/Documents/Download`, `Uploads/Request`. The simplicity rail says one list per concept; the copies were pre-existing and were moved verbatim by SIMPLI-011, which deferred the consolidation here.

## Scope

- One root (e.g. `Pages/StaffPageModel.cs`) owning `TryGetActor` (the `[NotNullWhen(true)] out ActionActor?` signature from `AdministrationPageModel` is the tidier one) and `NewOperationKey`.
- Both existing bases derive from it; the private page copies are deleted and the pages inherit the root.
- No behaviour change: same claims, same factory, same operation-key shape. `Pages/Cases/Documents/Download` and `Uploads/Request` are GET/anonymous-adjacent — check each page's authorization before it inherits a staff root.

## How to verify

`dotnet build` 0/0; `Pegasus.ArchitectureTests`; the Web integration filter for the touched pages (`CaseDetailsWebTests`, `AdministrationSearchAccountWebTests`, `OperationsWebTests`, `QdosCustodialWebTests`, `ImageIntakeWebTests`); `rg "StaffActorFactory.TryCreate" src/Pegasus.Web` returns the one root.

## Outcome
