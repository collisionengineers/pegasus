# UIIMP-017 files

- src/Pegasus.Web/Pages/Administration/Health.cshtml — five instants use existing OfficeTime.
- tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs — existing populated Health route asserts London consistently.
- tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs — exact default state predicate and small same-owner selector regression.
- docs/design/test-ui/catalogue.json — name the actual populated mailbox default scenario.
- docs/design/test-ui/pages/administration-health--default.html — focused generated output.
- docs/design/test-ui/index.html — existing generated catalogue only if changed.

Context: docs/design/README.md one-clock rule; docs/frd/frd-12-operator-experience.md;
existing OperatorLabels.OfficeTime; scripts/Update-TestUiSnapshots.ps1.
No page-model, CSS/JS, shared clock/culture or unrelated template changes.
TICK035 currently owns the generated index; do not take/modify until its
snapshot changes are integrated/released or a precise disjoint handoff exists.
