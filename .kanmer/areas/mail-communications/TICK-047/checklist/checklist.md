# Checklist — TICK-047

- [ ] Extend `RetainedMailDetail` and `GetRetainedMail` with the read-only MAIL-23 policy/binding-derived exact-folder recommendation and fail-closed unavailable result.
- [ ] Render recommendation/provenance or an accessible unavailable state on authenticated exact-message detail, with no input or mutation control.
- [ ] Add focused Core tests for configured, fail-closed, re-derived, and valid No action outcomes using existing fakes.
- [ ] Add focused Web caller evidence and reconcile `docs/design/README.md` plus `docs/capabilities.md` to the local evidence tier.
- [ ] Run proportional restore/build/tests and the four-lens simplification pass; apply findings and record dispositions.
- [ ] Commit and push the ticket branch, write the post-implementation report, open a PR to `dev`, and move TICK-047 to Review.

## Progress notes

Implementation starts from merged `origin/dev` `fb42ce15802d6bfa35ada3d26b006ba164c595f1`; no Outlook, Graph, Azure, or other external write is authorized or required.
