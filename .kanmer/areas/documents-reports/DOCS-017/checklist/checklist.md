# Checklist — DOCS-017 (2026-09-02)

- [ ] Replace the Core fixed-signatory contract with the supplied `ReportSignatory` snapshot tuple and named readiness check.
- [ ] Stop report projection from reading `engineer.name`, `engineer.qualifications`, and `engineer.signature`; record their retirement as an external follow-up.
- [ ] Make the production projection source fail closed with `Sign-off Engineer` until [[CASE-040]] and [[PLAT-068]] supply the tuple.
- [ ] Render supplied signature bytes and media type, remove the Andy embedded resource, and omit the qualification separator when absent.
- [ ] Update the owned Core, persistence, renderer, Web-fixture, and browser-fixture tests for Ed, Neil, and incomplete tuples.
- [ ] Reconcile only FRD-11's D18-era signatory paragraph with D31 and retain its existing event and provenance rules.
- [ ] Record the four-lens simplification pass and dispositions in this plan.
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: DOCS-017
