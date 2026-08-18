# Checklist — DELIV-002

Derived from `plan.md`; each box is independently checkable.

- [ ] Replace the merge-commit release guidance in `docs/engineering.md` with
  the canonical non-force exact-SHA procedure and its exclusions.
- [ ] Align `AGENTS.md`'s authorization and allowed-operation rules with the
  canonical procedure without duplicating its command sequence.
- [ ] Change `scripts/Test-MainBranchHistory.ps1` from a two-parent predicate
  to append-only and release-branch ancestry validation.
- [ ] Fetch `origin/dev`, pass it to the revised guard, and preserve the
  existing CI path-classification wiring in `.github/workflows/ci.yml`.
- [ ] Update `MainBranchHistoryGuardTests.cs` for fast-forward acceptance,
  later-`dev` ancestry, direct-main rejection, synthetic-merge rejection, and
  the existing invalid-history cases.
- [ ] Run the four-lens simplification pass over the task diff and record dated
  dispositions in `plan.md`; open the reviewed single PR to `dev`.
- [ ] Run `dotnet restore`, the Release build, focused architecture tests, and
  documentation-link validation; record their results in the
  post-implementation report.

## Progress notes

- 2026-08-18: [[DELIV-003]] was filed and blocked by this ticket. It owns the
  one-time shared-branch convergence and first remote promotion after this
  policy change reaches `dev` and the user grants exact `MERGE AUTH GRANTED`.
