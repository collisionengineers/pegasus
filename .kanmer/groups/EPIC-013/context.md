# WSL-first Pegasus development and release

## Feature outcome

Pegasus development, testing, Kanmer and the authorised release path run from Linux-native WSL storage without Windows executables on PATH.

## Users affected

Developers and release operators. No product-domain behavior or production data changes are introduced by the environment work.

## Acceptance criteria

The branch ancestry invariant is restored; Offline and Cloud Doctor profiles pass on WSL; the local database choice is evidence-backed; Chromium owns the selected automated accessibility evidence; and Linux becomes the release workstation only after equivalence proof.

## Non-goals

Email evaluation and CI redesign are excluded. DELIV-048 is a later audit only. No cloud write is authorised by this group.

## Shared decisions

Keep the authorised main-only test artifacts. Disable Windows PATH import globally while retaining interop. Use nvm Node 24. Use Kanmer v0.4.1 source GUI only for supported board sync and MCP for agents. Qualify the Azure SQL Database private preview before adoption. Accessibility evidence is automation-only and must not claim screen-reader coverage. Linux release conversion is last and must not pause current releases.

## Constraints

All source, tools, caches and runtime data live on the WSL filesystem. Shared branches are never rebased, reset or force-pushed. Secrets remain outside code, board documents and proof. Existing release approvals remain unchanged.

## Risks

The Azure SQL image is private preview with rotating credentials and no BACKUP/RESTORE. Kanmer v0.4.1 reports vulnerable locked transitive dependencies. The WSL instance has enough minimum memory for one SQL container, but database and browser-heavy work must not overlap. Automation-only accessibility reduces assistive-technology coverage.

## Dependency map

[[DELIV-046]] → [[PLAT-073]] → [[PLAT-074]] and [[UIIMP-016]] → [[DELIV-047]] → [[DELIV-048]].

## Rollout & rollback

Environment provisioning is local and reversible except installed packages. The existing SQL Server image and Windows release procedure remain authoritative until their replacement tickets pass. Production release retains its exact approval and rollback boundaries.

## Breakdown

DELIV-046 restores ancestry. PLAT-073 provisions WSL and reconciles tooling. PLAT-074 qualifies SQL. UIIMP-016 changes accessibility evidence. DELIV-047 performs the Linux release conversion. DELIV-048 audits CI afterwards.

## Definition of done

Every member ticket has independent review and exact command proof; the group is complete only after DELIV-047 is production-verified and DELIV-048 has a separately accepted CI plan.
