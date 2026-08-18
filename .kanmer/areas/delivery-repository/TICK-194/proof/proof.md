# Proof — TICK-194 (verified on merged `main` `f1e116c6`, 2026-08-18)

- Shipped to `main` in #394 (2026-08-17); the guard was subsequently revised by [[DELIV-002]] from "two-parent merge commits only" to "append-only and contained in `dev`" (policy change to fast-forward releases). The current guard is what runs on `main`.
- Real main push (release 9, run 32133221206, `changes` job): step `Require main history to be contained in dev` → `Main history guard passed: 9 new first-parent commit(s); main head is contained in the release branch.`; whole run success.
- `dotnet test tests/Pegasus.ArchitectureTests/… --filter FullyQualifiedName~MainBranchHistoryGuardTests` on `f1e116c6` → 8/8 passed (the six original scenarios plus the DELIV-002 additions); full architecture suite 96/96; `Test-DocumentationLinks.ps1` → 222 files resolve.

PR #377 merged 2026-08-17T05:04:26Z.
