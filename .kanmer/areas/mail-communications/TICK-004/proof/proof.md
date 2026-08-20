## Proof (VERIFY2, 2026-08-20)

- File presence confirmed on `origin/main` at the production ancestor commit 2325ed4a for every file cited in `research.md`.
- `docs/reference/CollisionSPikeCurrenttree.txt`-driven taxonomy structurally enforced at exactly 8 Received + 4 Sent categories (`CategoryCatalog.cs` constructor throws otherwise).
- Required reasoning enforced before filing (`EmailEvaluationWorkflow.cs:149-150`); proven by `tests/DesktopEvaluatorTests.cs:99-118` (`FilingCopiesSourceAndEscapesReasonInOneJsonLine`).
- This is a standalone, non-production desktop tool by explicit design (ADR-0016) — there is no Azure/production deployment to verify against, so `deployment` is left unset rather than fabricated as `production`. The evidence tier here is code + unit-test proof, which is the correct and complete tier for this artifact.

## VERIFY2 correction (2026-08-20) — proof invalidated

The bullet "File presence confirmed on origin/main at the production ancestor commit 2325ed4a for every file cited in research.md" is **false** for the taxonomy source: `docs/reference/CollisionSPikeCurrenttree.txt` does not exist on origin/main (deleted in `4e084ca2`, an ancestor of 2325ed4a). `CategoryCatalog.Load` throws `FileNotFoundException` without it, so the structural 8+4 enforcement cited in this proof can never run. The tool is unstartable from the current or release-13 tree. See the correction section in `research.md`. The ticket is returned to **preparing** with the re-sourcing gap named; this proof stands only for the workflow/reasoning/tests halves.
