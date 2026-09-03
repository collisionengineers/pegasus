# Checklist — CASE-022

- [ ] [pre-review] Step 1 — Replace the request upload's legacy
  `StoreAsync` call with a complete persisted
  `ManagedDocumentContentAddress` and `StoreVersionAsync` write.
- [ ] [pre-review] Step 1 — Prove positive document/occurrence ordinal,
  custody-root identity, Box flat-file retention, exactly-once SQL state, replay,
  failure cleanup, and safe retry with the focused storage tests.
- [ ] [pre-review] Step 2 — Add and register the public-upload request telemetry
  initializer through the existing Application Insights composition.
- [ ] [pre-review] Step 2 — Prove upload GET/POST URLs lose token, query, and
  fragment while request identity, correlation, unrelated telemetry, and
  production composition remain intact.
- [ ] [pre-review] Step 3 — Update `docs/current-architecture.md` and
  `docs/operations.md` with the managed repository path, observed live
  failure, zero-success census, healthy revision, and explicit undeployed state.
- [ ] [pre-review] Step 4 — Run focused integration tests and retain exit
  evidence without weakening assertions.
- [ ] [pre-review] Step 4 — Run the independent simplification lenses, apply
  behaviour-preserving findings, and append every disposition to
  `plan/plan.md` under a dated `Simplification pass` heading.
- [ ] [pre-review] Step 4 — Run locked restore, Release build, the full
  non-Corpus solution tests, `git diff --check`, and final status/scope
  inspection with exit evidence.
- [ ] [pre-review] Step 4 — Confirm the final diff contains only declared files,
  no token/secret, no dependency or lock change, no schema/IaC change, and no
  Test UI snapshot churn.
- [ ] [pre-review] Step 4 — Commit the bounded branch, open its PR to `dev`,
  and stop for independent `kanmer-review` without merging or deploying.
- [ ] [post-merge] Step 4 — After separately authorised release/deployment,
  verify a disposable permitted upload reaches Box and SQL exactly once,
  telemetry is token-free, revoke the link, and refresh both current-state
  documents at the exact deployed SHA.

## Progress notes

Append execution evidence; do not rewrite completed history.
