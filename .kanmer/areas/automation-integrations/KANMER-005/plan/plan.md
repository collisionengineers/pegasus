# Plan — KANMER-005: exclusive edit leases across staff and Automation Actors

*Refreshed 2026-08-28 on `origin/dev` `1f2cf4a6` (CASE-024 merged). Replaces
the pre-merge plan; the diff estimate is ~250 production lines plus tests.*

## Objective

The one server-owned case edit lease identifies its holder as
`(ActorKind, SubjectId)` — the identity `ActionActor` already owns — at claim,
replay, renew, heartbeat, release, every write boundary, and every page and
MCP projection. A staff actor and an Automation Actor can never be the same
holder; the claim path's existing actor-agnostic refusal of any unexpired
lease is unchanged, so a competing claim never replaces a holder.

## Governing docs

- **Meets** `docs/frd/frd-01-case-identity-and-lifecycle.md` (one lease,
  wrong-holder refused, no takeover, same guard for Web and MCP).
- **Meets** `docs/frd/frd-10-…` and `frd-11-…` (MCP uses the same Core
  commands and guard as staff).
- **Meets** `docs/adr/0011-…` and `0031-…` (Automation is a distinct durable
  actor; the retained kind stops inferring it from subject shape).
- No governing document changes.

## Required changes (reuse named per step)

1. **Core owner.** `CaseEditAuthority.IsHolder(ActorKind? retainedKind, string? retainedHolder, ActionActor actor)` is the one rule. `RequireLease` takes the `ActionActor` and the retained kind; refusal order (expired → conflict) and the fixed-time token check are unchanged; a missing or unrecognised retained kind with a retained holder is a conflict, never a match. `IDescribeCaseEditAuthorityHolder` takes the retained kind: Automation is disclosed as itself, a Staff GUID is looked up, everything else is `Unnamed`.
2. **Snapshot.** `CaseEditLeaseSnapshot` gains `ActorKind? HolderKind`. `CaseEditLease` is unchanged (callers know their own actor; the MCP result schema stays as is).
3. **Persistence.** `CaseWorkflowEntity.EditLeaseHolderKind` (nullable, 40). Claim writes `Actor.Kind`; `CaseMutationGuard.ClearLease` clears it; `CaseMutationGuard.RequireLease` and `ReadLeaseReplayOrThrow` hand the parsed kind to Core. Query and operations stores project it. Same lock, isolation, token, version handling.
4. **Migration.** One generated `CaseEditLeaseHolderKind` migration: add the nullable column; `Down` drops it. No backfill, no default, no check constraint — a lease lives five minutes, the old Web revision keeps writing null kinds until the new package activates, and the new runtime treats a null-kind unexpired lease as an unidentified competing holder (unclaimable via `IsHeld`, unwritable via `IsHolder`) until it expires and the existing locked clear/reclaim path replaces it. Production census on 2026-08-28 found zero retained holders. This supersedes the earlier plan's operation-row backfill, which was ritual for a five-minute window.
5. **Web.** `CaseMutationPageModel.RestoreLeaseState`, Details, Assessment and Triage call `CaseEditAuthority.IsHolder`; the descriptor gets the kind. Existing copy (`EditModeDisplay`) and controls unchanged: an Automation-held case renders "Case locked - AI is editing." and no claim form, exactly as a staff-held one does today.
6. **MCP.** No change: `pegasus_case_edit_begin/renew/end` and the write tools already reach the shared owner and map `CaseEditLeaseConflictException` to the existing refusal text.
7. **Tests.** Core: matcher rules, same-subject/different-kind refused with a matching token, null/unknown kind refused, descriptor by kind. Persistence (SqlServer): Automation holds → staff claim, write, renew, release refused and every lease column unchanged, then Automation heartbeats, saves (lease consumed) or releases; the mirror direction; same-subject/different-kind with the live token refused. Web: Automation-held workspace read-only with no claim form; staff claim POST refused keeps the holder. Ingress: real HTTP `pegasus_case_edit_begin` and `pegasus_assessment_update` refused while staff holds, with the existing message; staff refused while Automation holds. Census: migration added.

## Do not modify

Save-clears-lease, five-minute lease, heartbeat interval, `_EditHeartbeat`,
lock/isolation, MCP schemas, operator copy, governing docs, `corpus/`.

## Ordered steps

1. Core matcher + contracts + Core tests.
2. Entity, config, guard, store, projections; generate migration; census.
3. Web pages.
4. Integration tests (persistence, web, ingress).
5. `dotnet build ./Pegasus.slnx --configuration Release`; merge `origin/dev`; simplification pass; report; PR to `dev`.

Tests are not run by the implementer; the EPIC-011 orchestrator runs the wave
loop (`waves.md`).

## Acceptance checks

- Ticket Verification bullets 1–4 are each pinned by a SqlServer test in both
  actor directions.
- One MCP ingress test per direction.
- CASE-024 heartbeat and save tests unchanged and green.
- Release build exit 0; no new package, grant, constraint, or copy.

## Stop condition

PR open against `dev`; ticket in Review. No merge, no proof, no next ticket.
