# Plan — PLAT-028: One customer identity in Principals

## Objective

Administrator creates and manages one customer identity from Principals, without a parent organisation or separate owner workflow.

## Starting state

Source: origin/dev 3da60bd0c270111d5168dc17246dc831882108ea. The 7 September appended research and rewritten files map supersede the old organisation-detail plan. EPIC-014 is current authority; prior EPIC-011 organisation backing identity is still usable internally. Sources registry has no declarations.

## Governing docs

Modify FRD-04 to reflect the operator's explicit single customer identity correction. Meet FRD-09 by consuming existing provider credential and route policies unchanged. Current manual-only EVA supersedes the historical PLAT-050 two-toggle request.

## Required changes

Create customer Name and Code together using existing Core/EF owner and permanent idempotency. List/paginate customer Principals, show Name, Code, State and Settings. Remove owner hierarchy and organisation selection; remove separate Organizations routes. Reuse existing settings UI for manual EVA/default location and existing provider command controls with reason/version/key, exact secret only in immediate noncacheable POST response. Settings use Principal ID, and code replacement stays on that same backing customer. Keep genuine repairer/location directory unchanged.

## Expected files

Use the exact paths and globs in files/files.md, including scoped generated Test UI artifacts owned by root verification. No schema changes.

## Do not modify

- docs/operator-notes.md
- src/Pegasus.Core/Intake/**
- src/Pegasus.Worker/**
- src/Pegasus.Infrastructure/Persistence/Migrations/**
- corpus/**

## Constraints

No new package, directory, credential policy, role model, sender-domain activation, compatibility URL or test framework. Existing customer/reference identity and code replacement history remain permanent. Creation has one backing directory identity, not a selectable parent. Root owns builds/tests/capture. All new literals use the existing presentation label owner where shared.

## Ordered steps

1. Replace the create request's organisation selector with Name, normalize through existing policy, create both records in the existing serializable transaction, and add direct paginated/detail customer projections. Update known callers.
2. Make Principals the one entry/list/create experience; fold manual EVA/default location/provider controls into Settings with current dialog primitives; remove obsolete Organizations hierarchy routes and successor organisation selection.
3. Add focused existing tests for creation/replay/duplicates, single-row list and no owner selector, actual settings forms, provider lifecycle and show-once nonretention, and authorization. Update FRD-04/design wording.
4. Hand changed files and exact filters/capture scope to root verifier. Record honest focused/final evidence and independent simplification dispositions before PR/Review. Root may collect snapshots in this worktree.

## Acceptance checks

The real Razor routes call current Core commands and EF store; customer create cannot leave an orphan backing identity after duplicate-code failure and identical POST replays once. List is principal-paged and settings need no parent selection. Provider controls reach existing API credential owner, generated text is never in TempData/session/URL/logs; refresh and operation replay cannot redisplay secret. Non-administrators are refused. Replacement preserves current customer's backing identity and lineage. Root can create pegasustest through /Administration/Principals/Create; this lane performs no live write.

## Commands

Root only: focused Core filter FullyQualifiedName~Cases.OrganizationAdministrationTests; integration filter FullyQualifiedName~OrganizationAdministrationWebTests|FullyQualifiedName~OrganizationAdministrationPersistenceTests|FullyQualifiedName~PrincipalCredentialPersistenceTests|FullyQualifiedName~ProviderApiSubmissionTests. Then scoped Principals/retired-Organizations Test UI capture and catalogue verification; one final coordinated solution rail. Worker uses git diff --check and static caller searches only.

## Failure and deviation rules

Report new schema requirements, shared-file conflicts, failing checks and evidence gaps. No scope expansion or silent test weakening.

## Stop condition

Stop with implementation and focused tests ready for root verification, keeping claim/worktree intact. Do not build, run tests, capture snapshots, create a PR, move to Review or perform external writes until root supplies verification and next handoff.
