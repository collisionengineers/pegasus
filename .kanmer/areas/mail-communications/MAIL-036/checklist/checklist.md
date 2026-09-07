# Checklist — MAIL-036

- [x] Record a wipe cutoff atomically with SQL deletion and require a stopped Worker.
- [x] Preserve cutoff through poll refresh and reject old notified MIME.
- [x] Process only the exact notified message; release lease without a recovery scan/cursor advance.
- [x] Prove SQL missing/existing state, scope refresh and Graph old/new reset paths; update workflow docs.

Focused evidence: 72 initial integration cases, 27 notification Core cases and 3 updated SQL cases passed; no live wipe.

## Closeout — MAIL-036

- [x] PR merge verified (PR678, 2026-09-07T20:50:31Z).
- [x] proof.md finalised and whole-file readback matched.
- [x] Moved to final stage with PASS.
- [x] Outcome recorded in ticket body (PR link, follow-ups).
- [x] cd out of worktree; remove exact implementation and verification trees.
- [x] Delete merged MAIL-036-wipe-boundary branch locally and remotely.
- [x] Fetch/prune reviewed ticket refs.
- [x] Release claim after Git cleanup.

Exact clean ticket and verification trees removed, merged local/remote branch deleted, refs fetched; claim released. Integrated change remains in Git and proof remains on board.
