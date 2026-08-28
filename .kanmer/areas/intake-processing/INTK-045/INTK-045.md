---
id: INTK-045
type: ticket
title: >-
  Share one retryable-concurrency predicate that unwraps every exception layer
  across the six stores that still unwrap only DbUpdateException
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels:
  - allocation
  - concurrency
  - follow-up
links:
  - INTK-044
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-27T16:55:16.185Z'
updated: '2026-08-27T16:55:16.185Z'
---

## Problem

[[INTK-044]] found that EF's non-retrying execution strategy wraps a SQL 1205 deadlock as `InvalidOperationException → DbUpdateException → SqlException`, and a predicate that unwraps only `DbUpdateException` lets the deadlock escape the retry loop. `EfCaseAcceptanceStore` was fixed in PR #572; `EfIntakeReceiptStore` already unwrapped every layer.

Six other stores still unwrap only `DbUpdateException` and can swallow the same wrapped deadlock: `EfCaseReportSentEvidenceStore`, `EfIntakeSubmissionGroupStore`, `EfIntakeWorkStore`, `EfLinkedCaseReplacementStore`, `EfVehicleWorkflowStore`, `EfOrganizationAdministration`.

## Required outcome

One shared retryable-concurrency predicate in `Pegasus.Infrastructure` (one list per concept) that unwraps every inner-exception layer, used by every store above and by the two already fixed; no behaviour change other than the wrapped deadlock now being retried. Filed from the INTK-044 simplification pass (finding 6) and review.
