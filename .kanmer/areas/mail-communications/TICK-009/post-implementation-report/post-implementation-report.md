# Post-implementation report — TICK-009

*The report. Not the proof — this is the author's **claim**, written before merge; proof is **evidence**, gathered after.*

The reviewers' brief: every change and why. Gates **Implementing → Review**.

## Summary

MAIL-21's QDOS classification policy (`qdos_mail_classification` v3) was already on `dev`. This slice closes the local acceptance-cohort evidence gap: the harness now discovers a flat `corpus/*.eml` dump (including from a git worktree via the primary checkout) and skips labelled accuracy when that tree is absent. A 2026-08-17 volume run over 256 local EML files is recorded as dated evidence. No policy predicates, schema, or callers changed. Deployment and live verification remain unclaimed.

## Changes
| File | Change | Why |
| --- | --- | --- |
| `tests/Pegasus.IntegrationTests/QdosEmailCohortTests.cs` | Volume roots fall back to a flat corpus; worktree-aware corpus discovery; labelled facts skip without that tree | The previous `IsPresent` check was false on this machine and in worktrees |
| `docs/operations.md` | Dated volume-cohort observation (counts only) | Operations owns dated evidence; must not claim acceptance |
| `docs/capabilities.md` | MAIL-21 activation note names local volume cohort vs holdout/deploy/live | Keep the inventory evidence states distinct |

## Governing docs
How this meets each linked PRD/FRD/ADR (`refs`). Call out anything modified with explicit authorization, or a new ADR written for a design decision.

- **FRD-08** — Unchanged. The existing QDOS policy still records versioned predicates, explicit ambiguity, and fail-closed unclassified. This ticket only produces the local cohort evidence state the capability row still owed.
- No governing doc was modified. No new ADR.

## Risks / follow-ups
Anything deferred, or a risk a reviewer should weigh. Link follow-up tickets.

- Labelled holdout and operator acceptance remain parked (need a machine with `extraction-corpus/QDOS/{audits,...}` and an operator review).
- Deployment and live verification remain separate evidence states (approval required).
- Staff confirmation / correction / folder / queue work stays on MAIL-04/05/02/23 and UI-10/14 ([[TICK-010]] is the taxonomy persist slice, not this).

## Verification hand-off
What `kanmer-verify` should run on merged `main` (commands, expected results, screenshots to capture for UI work).

```
dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --filter "FullyQualifiedName~QdosMailClassificationPolicyTests|FullyQualifiedName~ProcessIntakeTests.Classification|FullyQualifiedName~ProcessIntakeTests.AmbiguousClassification"
dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~QdosEmailCohortTests"
```

Expected: Core filter 29 passed. Cohort: labelled facts skip when those folders are absent; volume fact passes (`processed > 0`) when any discovered corpus has `.eml`. Do not require `corpus/` in CI. Confirm `docs/operations.md` still contains only counts, no filenames or PII.
