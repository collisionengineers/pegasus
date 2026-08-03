# UI alpha design pass

Design-only build of the Operations-first `0.1.0-alpha.1` shell's visual and
interaction layer, against fixture data only — no Core wiring, no real
mutation, no unresolved-decision invention.

## Scope

Capability IDs: UI-01, UI-02, UI-03, UI-04, UI-05, UI-06, UI-08, UI-09,
UI-11, UI-13 ([capabilities.md](../capabilities.md)).

Excluded and why:

- UI-07 (search/filter) — already in flight under `task/image-led-intake`.
- UI-10 (email-management workspace) — allocated `Next / 0.3.0`
  ([capabilities.md:223](../capabilities.md)), not part of `0.1.0-alpha.1`.
- UI-12 (responsive/mobile) — `Not planned`, permanent boundary
  ([capabilities.md:276](../capabilities.md)).

Also excluded as non-alpha sub-surfaces inside UI-08/UI-09
([design/product/ui-spec.md](../../design/product/ui-spec.md)):

- Report-image selection (future Engineers-screen surface).
- Image readiness advisory (owned by AI-05, `Later / 1.0.0`).
- Email quick preview / mailbox-refresh mechanics (belong to UI-10).
- Request-scoped in-house upload: only the interim bound from
  [open-decisions.md](../open-decisions.md) (10 MB aggregate, hashed
  256-bit token, no case disclosure) is rendered — no invented exact
  limits.

## What changes

Razor views/components under the Web project's existing composition-root
boundary (no new top-level directory, project, or store — see the
architecture invariant), built against `design/product/ui-spec.md`'s
Shared shell/hierarchy, per-component contracts table, and the [requirements
state matrix](../requirements.md#complete-state-matrix):

1. Shared shell: identity/role, navigation, sign-out, permitted-route
   visibility (UI-13 keyboard/screen-reader/focus/contrast baseline built
   in from the start, not retrofitted).
2. Operations dashboard (UI-01/UI-04/UI-05/UI-06): exact `Blocked intake`,
   Due today, New cases today, day/week Sent to Engineer and Reports sent
   metrics; click-through to filtered queues; last-good time and distinct
   current/stale/partial/unavailable/failed states with manual refresh.
3. Case queues — Not ready / Review / Held (UI-02).
4. QDOS-alpha intake queues — Needs sorting / Blocked intake (UI-03).
5. Three-column intake review workbench (UI-08): source identity,
   `All`/`Instructions`/`Images` filter, evidence/candidate, fact vs
   suggestion vs confirmed, provenance/missing/conflict, acceptance path,
   no-case failure consequence.
6. Full case workspace (UI-09): persistent identity header, due/chaser
   panel, inspection-address defaulting/override, provenance-marked
   fields, engineering findings (Roadworthiness/Assessment separate),
   evidence/document panel, named state actions, history — all sections
   from `ui-spec.md`'s Case flow minus the excluded sub-surfaces above.
7. Administration workspace (UI-11): account creation/disable/access
   review/roles, principal successor cutover, configuration, mailbox
   allowlist.

All list/detail data is fixture-backed (repository-provided genuine
material where content resembles real cases/claimants, never fabricated
domain data) and illustrates every required state (loading, empty,
current, stale, partial, unavailable, failed, denied, conflict) rather
than a single happy path. No action performs a real mutation; actions may
be visually present and show their target state without wiring to Core.

## Verification

- `dotnet build --configuration Release` stays green.
- `dotnet test` (focused Web/UI tests, plus full suite before PR) stays
  green; no test asserts a fabricated business outcome as real.
- Manual pass at 1280+, 1024–1279, and 200% zoom per the acceptance
  section (`ui-spec.md`), confirming dense panes vs labelled
  tabs/drawers/ordered sections and that identity/state/actions stay
  first.
- Keyboard-only and screen-reader spot check of the shell, one queue, the
  intake workbench, and the case workspace (UI-13).
- Confirm no fixture view reachable from a real authenticated route
  performs a Core call, Box/Outlook/DVLA/DVSA call, or case/reference
  mutation — grep for the boundary and review call sites.
- Review confirms no capability ID outside the claimed set was touched,
  and none of the excluded sub-surfaces (report-image selection, image
  readiness advisory, email quick preview/mailbox refresh, upload limits
  beyond the interim bound) were implemented.
