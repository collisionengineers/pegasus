# Checklist — ENG-035 (2026-09-03, gpt-5.6-terra xhigh; revised after plan review)

- [ ] Step 1 — Add every pinned vocabulary path, the `Json` field type and the `damage.impacts` normalizer, the D45-consistent zone/severity codes, the derived impact rules, and the raised `MaximumFieldsPerSave`.
- [ ] Step 2 — Persist and clear Core-derived impact rows through the existing assessment store.
- [ ] Step 3 — Generate the serialized check-constraint migration, update the model snapshot, and append the generated migration name to the census in `IntakePersistenceIntegrationTests.cs`.
- [ ] Step 4 — Extend the Core report snapshot and projection (including D41 equity and the Core-owned report display text), update every positional construction site, and bump `TemplateVersion` to `rendererref1-v2`.
- [ ] Step 5 — Render vehicle, damage, restraint, and settlement report sections without explanatory copy and without a second Infrastructure label list.
- [ ] Step 6 — Add policy, projection, rendering, persistence, MCP, save-bound, payload-version, and PDF assertions.
- [ ] Step 7 — Run the simplification pass and record its dated dispositions **before** the final verification run; confirm excluded files remain untouched.
- [ ] Refresh with `git merge --no-edit origin/dev` before opening the PR (DOCS-017 signatory block and PLAT-068 migration land in the same files/lock).
- [ ] Run `./scripts/Test-MigrationGrants.ps1`.
- [ ] Run `dotnet restore ./Pegasus.slnx --locked-mode`.
- [ ] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` (canonical gate; do not exclude `Category=Browser`).
- [ ] Post-implementation report written.
- [ ] PR opened with Kanmer: ENG-035.
