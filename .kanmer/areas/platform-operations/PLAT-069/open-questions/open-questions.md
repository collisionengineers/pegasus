# Open questions — PLAT-069 (2026-09-02)

- [x] Does "any query is not current" mean only Partial or Failed, or every
  state other than Current? Resolved 2026-09-03 by the controller: Partial
  or Failed only, as the mockup implements; Running, Configured and
  ReviewRequired are not partial data.
- [x] Two notices when the result-limit condition and the D37 condition both
  hold? Resolved 2026-09-03 by the controller: one label-only notice line
  each; the limit warning's hint sentence is removed (no explanatory copy).
- [x] Sequencing against PLAT-051? Resolved 2026-09-03 by the controller:
  PLAT-069 may merge first; the Administration link is absent, not dead,
  until `Pages/Administration/ServiceHealth` exists (absent vs disabled).

## Parked (explicitly deferred)

None.
