# Post-implementation report — MAIL-10

## Outcome

The retained-message page supports deliberate search/review/reasoned link, reasoned unlink and separate replacement search. The lease-first confirmation now reaches Core replay for exact successful resubmissions, rejects changed fingerprints, and binds its protected authority to the exact message, server-derived receipt and Link/Unlink action. Cross-message and cross-action transfers fail before mutation. A transient compensation-release failure surfaces a same-confirmation retry and retains bounded authority until the existing release port confirms resolution.

## Exact branch file inventory

- docs/capabilities.md — MAIL-10 schedule/capability wording from the original implementation.
- docs/current-architecture.md — as-built Mail association caller from the original implementation.
- src/Pegasus.Web/Pages/Mail/Message.cshtml.cs — search/review, prepare/final association orchestration, protected exact authority, Core replay path and bounded definitive-failure compensation.
- src/Pegasus.Web/Pages/Mail/Message.cshtml — deliberate link/unlink confirmations and complete accessible Case-result anchors, gated to exact prepared message/receipt/action.
- src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml — optional hidden fields shared by the concrete Link and Unlink confirmation callers.
- tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs — authenticated journeys for link/unlink/replacement, replay/conflict/history, lease consumption/compensation, accessibility, cross-message/cross-action refusal and fail-once release retry.

No Core, Infrastructure, EF model/store, schema, migration, Graph/Box/cloud/deployment/permission or generic action framework changed.

## Verification

- Exact focused authority/recovery/established replay tests — 3/3 passed.
- Full MailWorkspaceWebTests — 35/35 passed.
- dotnet restore Pegasus.slnx --locked-mode — passed.
- dotnet build Pegasus.slnx --configuration Release --no-restore — passed, 0 warnings/errors.
- git diff --check — passed (line-ending notices only).
- Four-lens disposition is recorded in the plan; no unapplied findings.

## Delivery references

- Original implementation: d4c951f5.
- PR-048..050 correction: 6b7c62a4.
- PR-051/052 correction: 563bb2ec.
- Pull request: #490 — https://github.com/collisionengineers/pegasus/pull/490
- Target/disposition: dev, open in Review for independent re-review; not self-reviewed or merged.
- Evidence: disposable local SQL and authenticated local Web only; no external/live write.
