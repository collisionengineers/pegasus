---
id: ADR-0064
status: accepted
date: 2026-10-07
supersedes: [ADR-0048]
superseded_by: []
related_capabilities: [CASE-21, CASE-30, EXT-03, EXT-04, UI-04]
related_frd: [frd-04, frd-07, frd-13, frd-15, frd-16]
tags: [export, principals, work-centre, migration]
---

# ADR-0064: The Case export replaces the EVA routes

## Status

Accepted on 7 October 2026 by operator decision. This record supersedes
ADR-0048's report-generation policy and everything that depends on it. The
Principal report-recipient settings in ADR-0048 survive unchanged.
[FRD-07](../frd/frd-07-case-export.md) owns what staff see.

## Context

Pegasus could hand a Case to EVA, an external engineering system, in three
ways: a ZIP export, a manual API send and an automatic API send when the Case
entered Review. Each Principal chose one route through its report-generation
policy (ADR-0048). EVA is no longer used. The operator kept only the export,
for every standard Case, with no setting.

The first export from Review also moved the Case to With Engineer and wrote a
once-per-Case first-handoff row. The Work Centre counted those rows as "Sent
to Engineer". Assign Engineer already moves a Case into With Engineer, so the
export's move duplicated it.

## Decision

1. **One neutral Case export.** Core `src/Pegasus.Core/CaseExport/` owns the
   13-field mapping, image eligibility and the archive behind
   `IExportCaseBundle`. The download is `{reference}.zip` holding
   `{reference}.json` and `Images/`. It is offered on every standard Case in
   every state, needs no Sign-off Engineer and accepts a Case with no image.
2. **The export has no side effects.** It writes one replay-safe
   `case_exported` history record. It changes no Case state, version or edit
   lease and records no handoff.
3. **The routes are removed.** The EVA API send, the automatic send and its
   Worker timer, the per-Principal `PrincipalReportGenerationPolicy`, the EVA
   service-health area and the `Eva:*` configuration are deleted. The
   Sign-off Engineer form that lived in the EVA dialog, and its Workflow
   route, go with it; the Case card's Sign-off Engineer field is the one
   place it is set.
4. **Sent to Engineer is a workflow fact.** The Work Centre counts each Case
   once, at its first `state_ReportPreparation` workflow event, which Assign
   Engineer writes.
5. **Destructive migration.** `20261007180000_RemoveEva` drops
   `EvaSubmissions`, `AutomaticEvaReviewSubmissions` and
   `EvaFirstHandoffProxies`, and the `Principals.ReportGenerationPolicy`
   column. Their grants go with them. The estate's test data is disposable,
   so nothing is converted.

## Consequences

ADR-0048 keeps only its recipient settings. ADR-0031's rule that automation
has no export tools still holds; the export remains a staff action. A release
of this change follows the destructive-migration procedure (ADR-0046). The
Key Vault EVA secrets and azd `EVA_*` values become unused and are removed
by the operator outside the repository.

## Links

- [ADR-0048 — Principal report-generation policies](0048-principal-report-generation-policies.md)
- [ADR-0046 — Destructive migration runtime shutdown](0046-destructive-migration-runtime-shutdown.md)
- [FRD-07 — Case export](../frd/frd-07-case-export.md)
- [FRD-13 — Assign Engineer](../frd/frd-13-case-lifecycle-and-workflow.md#assign-engineer)
- [FRD-15 — Work Centre](../frd/frd-15-work-centre-queues-and-search.md)
