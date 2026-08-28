# Checklist — KANMER-005

*Refreshed 2026-08-28 against the post-CASE-024 plan. Each box is
independently observable in the ticket's recorded worktree.*

- [x] [pre-review] CASE-024 (PR 581) merged at `1f2cf4a6`; branch
  `task/kanmer-005-lease-exclusivity` started from that `origin/dev` in
  `../pegasus-worktrees/kanmer-005-lease-exclusivity`.
- [x] [pre-review] Core owns one holder matcher (`CaseEditAuthority.IsHolder`);
  `RequireLease` and the descriptor take the retained kind; Core tests pin
  same-subject/different-kind, kind-less holder, and disclosure by kind.
- [x] [pre-review] `EditLeaseHolderKind` retained on claim, cleared with the
  tuple, compared on replay/renew/heartbeat/release/write through
  `CaseMutationGuard`, projected by the case and operations queries. Lock,
  isolation, token and version handling unchanged.
- [x] [pre-review] Migration `20260828110108_CaseEditLeaseHolderKind`: one
  nullable `nvarchar(40)` column, column-only `Down`, snapshot updated,
  committed-migration census extended. No backfill, default or constraint
  (plan step 4).
- [x] [pre-review] Details, Assessment, Triage and `CaseMutationPageModel`
  call the Core matcher; no copy or control added.
- [x] [pre-review] SqlServer tests: both actor directions, rejected
  claim/write/renew/heartbeat/release leave every retained column unchanged,
  holder heartbeats then saves (lease consumed) or renews then releases;
  same-subject impostor with the live token.
- [x] [pre-review] Web test: Automation-held workspace read-only, no claim
  control, posted claim refused. Ingress tests over real HTTP: staff-held
  lease refuses `pegasus_case_edit_begin`, `pegasus_assessment_update`,
  `pegasus_case_edit_end`; Automation-held lease refuses the staff claim and
  renders read-only; holder ends; staff claim.
- [x] [pre-review] `dotnet build ./Pegasus.slnx --configuration Release`
  exit 0 on the final tree (2026-08-28).
- [ ] [pre-review] Test run — not run by the implementer by instruction; the
  EPIC-011 orchestrator runs the wave loop (restore/build/test filters,
  snapshots, migration grants).
- [x] [pre-review] Simplification pass recorded in `plan.md` (dated section);
  post-implementation report written; PR to `dev` opened.
- [x] [pre-review] Stop with the PR open: no merge, no proof, no next ticket.

## Progress notes

- 2026-08-28 Root cause verified read-only on dev: the lease retained only
  the subject; kind ignored at `CaseEditAuthority.cs:68`,
  `EfCaseWorkflowStore.cs:177/1244`, and four Web self-holder checks. The
  claim-path `IsHeld` refusal predates the incident (`012b3864`, 2026-08-05).
- 2026-08-28 Commits `2ab02db3` (fix), `4a91c5c1` (tests), `8218b3f3`
  (simplification).
