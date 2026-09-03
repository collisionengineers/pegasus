# Checklist — CASE-043

- [ ] Step 1: extend the Core vehicle contract, validations, allow-list, and projections.
- [ ] Step 2: persist and extract the ten optional instruction-draft fields as facts.
- [ ] Step 3: add non-overwriting automatic lookup facts for supported fields only.
- [ ] Step 4: add the additive migration, generated metadata, and Worker-role grant evidence.
- [ ] Step 5: complete focused round-trip, provenance, non-overwrite, and migration tests.
- [ ] Run `dotnet restore ./Pegasus.slnx --locked-mode`.
- [ ] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`.
- [ ] Run `./scripts/Test-MigrationGrants.ps1`.
- [ ] If a routed Razor page changed, run `./scripts/Update-TestUiSnapshots.ps1`, `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`, and `./scripts/Test-UiCatalogue.ps1`.
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: CASE-043
