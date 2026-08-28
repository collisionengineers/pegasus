# Plan — PLAT-023 Operations workspace redesign

Deliver per EPIC-011 §1.11 for the sections dev can drive today; defer the
rest with named reasons (see research "Out-of-scope findings").

## Steps

1. **Labels first (reused surfaces).** Extend `OperatorLabels` with
   `RequestOperationState` (the existing PageModel wording), and the Service
   health maps. Service names rename the two banned-word Core constants for
   display only: "Intake dispatch" → "Receiving dispatch", "Automation
   ingress" → "Automation clients"; mailbox addresses, "Sent evidence",
   "External work", "EVA submissions", "AI jobs" pass through; external kinds
   via the existing `Humanise`. Dependencies: Microsoft Graph, Worker, Box,
   EVA API, AI, Automation client. Add `_StatusChip` tone keys
   `active`→navy, `running`→blue, `review required`→amber.
2. **Page model.** Add optional `GetServiceHealth? getServiceHealth = null`
   (null when `Features:AutomationMcp` is off — the capability is then absent,
   never broken) and load `ServiceHealth` in `OnGetAsync` after the Operations
   query. Reuse: `GetRequestOperations`, `RetryExternalWork`, lease/revoke
   handlers unchanged; `IndexModel.NewOperationKey()` reused for the service
   health Retry form.
3. **Markup.**
   - Header: `page-header` → `page-title` h1 "Operations" + `page-actions`
     with the reused `_FreshnessBanner` (freshness + Refresh; contract §1.11
     draws exactly these).
   - Status message: `notice` (informational) + info icon, `role="status"`,
     text byte-identical.
   - Partial data: `Operations.LimitReached` → `notice notice--warning` with
     label "Partial data" and the existing sentence as the value.
   - Service health panel (only when `ServiceHealth` is not null): head h2 +
     muted "As of HH:MM"; table Area, Service, State chip, Latest evidence
     (`OfficeTime`, "—" when null), Dependency, Retry. Retry = small dark
     button posting to the existing `RetryExternal` handler with
     `workItemId`/`expectedAttemptCount`/`operationKey`. No View control (no
     handler; inert controls are a defect). Rows for uncomposed services are
     already absent by construction of the query.
   - Attention required panel: Case, Work, Attempts, Failure, Retry this work
     (dark + refresh icon, byte-compatible form). Empty → `div.empty` h2
     "No retryable external work" (prototype's rendered empty label).
   - Active upload links panel: Case, Last activity, Accepted, Expires, State
     chip (`OperatorLabels.RequestOperationState`), Withdraw link (danger
     summary + `row-confirm` reason form, byte-compatible fields). Empty →
     `div.empty` h2 "No active upload links".
   - Remove the "AI operations" placeholder section (absent capability must
     be absent; its sentence is explanatory copy).
   - Sections in `div.stack` after the header; `table-wrap no-border` inside
     panels; sr-only captions kept; no inline styles/scripts; no new CSS.
4. **Tests (owned family).** Update `OperationsWebTests`: replace the
   AI-placeholder `Contains` with `DoesNotContain` for the placeholder
   sentence; new test registering a fake-depended `GetServiceHealth`
   (recording store + empty ports) asserting the Service health table, an
   operator-safe Area/Service/Dependency vocabulary, and that its Retry form
   posts through `OnPostRetryExternalAsync` (PRG + recorded command).
5. **Verify.** `dotnet restore ./Pegasus.slnx --locked-mode` then
   `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
   No test/snapshot runs in this lane (orchestrator owns the wave loop).
6. **Simplification pass** over the branch diff; record below.
7. **Commit** in slices `feat(operations): … (PLAT-023)`; push; PR to dev
   "PLAT-023: Redesign the Operations workspace"; stop at the open PR.

## Reuse named

`_FreshnessBanner`, `_StatusChip`, `OperatorLabels.Humanise/OfficeTime/
FileSize`, `panel/notice/empty/table-wrap/no-border/row-confirm` classes,
`IndexModel.NewOperationKey`, `GetServiceHealth`, existing RetryExternal and
RevokeLink handlers. No new partial, CSS, or JS.

## Acceptance

- Header "Operations" (one H1; shell's single `main`).
- Partial-data notice present iff `LimitReached`.
- Attention required + Active upload links restyled, handlers and pinned
  strings byte-compatible ("Retry this work", reason-preserving withdraw,
  status sentences).
- Service health table renders from the merged PLAT-048 query when
  composed; absent when not; banned words absent from markup.
- AI placeholder gone; EVA handoffs not fabricated.
- `OperatorJourneyTests` surfaces untouched semantically (no test edit).

## Commands

```
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
git -C ../pegasus-worktrees/plat-023-operations diff --stat origin/dev
```

## Simplification pass

(Recorded after implementation — dated heading below.)

## 2026-08-28 Simplification pass

Lenses: reuse, simplification, efficiency, altitude (self-review of the
branch diff, no separate agent available in this lane run).

- Reuse: no new CSS/partial/JS; labels went into the existing maps; the
  service-health Retry reuses the existing handler and `NewOperationKey`.
  Finding: none.
- Simplification: the empty-state branch for upload links duplicates the
  Attention-required empty markup shape (two `div.empty` blocks). Extracting
  a partial for two callers was considered and rejected — a second caller
  does not yet exist and the blocks are three lines each (no abstraction
  without a second concrete caller).
- Efficiency: `OnGetAsync` runs `GetRequestOperations` once for the page and
  `GetServiceHealth` once (which re-queries the projection internally when
  composed). Accepted: PLAT-048 owns that shape; the page does not
  double-query on the feature-off path.
- Altitude: service-name renames live in the Web label map rather than
  editing Core constants — Core is outside this lane and the banned-words
  rule binds operator-facing copy at the Web boundary.
- Disposition: no unapplied findings.
