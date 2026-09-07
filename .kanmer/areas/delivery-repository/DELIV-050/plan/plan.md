# Plan

## Objective

Restore the release bootstrap's explicit migration census without changing permissions.

## Starting state

origin/dev baafa29e0f7002b8235aa43bf333f5d9bb172828. PLAT-065 Local validation failed because the report-input grant migration ID was missing from bootstrap text. Root and independent read-only audit confirm all five grant entries already match. No unresolved choices.

## Governing docs

ADR-0007: preserve the existing checked terminal deployment route; no new mechanism or authority.

## Required changes

Name 20260907210000_ReportInputInvalidationPermissions in the existing report grant comment, using the convention of surrounding migration annotations.

## Expected files

- scripts/Invoke-AzureDatabaseBootstrap.ps1

## Do not modify

- src/**
- tests/**
- infra/**
- scripts/Test-AzureDeploymentPlan.ps1

## Constraints

Comment-only. No new test harness, build, schema, grant or cloud change. Keep both PLAT-065 failed attempts recorded.

## Ordered steps

1. Replace the existing comment with the migration ID and its current description.
2. Run the unmodified Local deployment-plan check and diff whitespace check; independent review, then exact-merge Local check.

## Acceptance checks

Only the explanatory comment changes; the five migration grants and bootstrap entries remain identical. Local validation exits zero.

## Commands

- pwsh -NoProfile -File ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local
- git diff --check

## Failure and deviation rules

Record any unrelated failure without widening this change. Reconcile a changed baseline before continuing.

## Stop condition

Push one independently reviewable PR to dev with the comment-only change and truthful check results; stop for independent review.
