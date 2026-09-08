# Checklist — DELIV-054

- [x] Step 1 — Replace the two glob-based ZIP calls with root-preserving ZipFile construction.
- [x] Step 2 — Require Worker .azurefunctions and Web .playwright roots in existing artifact validation.
- [x] Step 3 — Replace placeholder ZIP fixtures with valid hidden-directory ZIP archives and root-free negative cases.
- [ ] Step 4 — Hand off the scoped change for parent-arranged independent verification; do not run it locally.

## Progress notes

2026-09-08: Completed Steps 1–3 in
`DELIV-054-hidden-runtime-zips`. Static diff and whitespace inspection are
clean; no test, build, packaging, browser/capture host, or verification script
was run. D6 remains the first immutable actual-release package build.
