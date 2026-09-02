# Checklist — TICK-060

- [ ] Reuse the existing API-01 GET route and result query; add no parallel
      route, query, store, or projection.
- [ ] Keep submission ownership bound to the authenticated Principal.
- [ ] Return empty 202 for owned unfinished work.
- [ ] Return only `caseReference` for an actual active Case link.
- [ ] Return generic 422 for failed or completed-without-link work.
- [ ] Preserve indistinguishable 404, paused reads, and revoked/invalid 401.
- [ ] Remove public processing detail and update focused Core/integration tests.
- [ ] Update FRD-09 and capabilities; add no unrelated current-state or
      deployment claim.
- [ ] Run and record the simplification pass with dispositions.
- [ ] Pass locked restore, Release build, and non-Corpus solution tests.
- [ ] Obtain independent review and integrate the PR into `dev`.
- [ ] Verify at the exact `main` SHA, write proof, close out, and release the
      ticket workspace.

## Progress notes

- 2026-09-02: Replanned after baseline review found API-01 already supplies the
  route, Core port, persistence seams, authentication, throttling, telemetry,
  and production caller required by API-03.
