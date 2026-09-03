# Checklist — ENG-035 (2026-09-03, gpt-5.6-terra xhigh)

- [ ] Step 1 — Add and validate the D45-consistent Core vocabulary and derived impact rules.
- [ ] Step 2 — Persist and clear Core-derived impact rows through the existing assessment store.
- [ ] Step 3 — Generate the serialized check-constraint migration and obtain the out-of-scope migration-census update.
- [ ] Step 4 — Extend the Core report snapshot and projection, including D41 equity.
- [ ] Step 5 — Render vehicle, damage, restraint, and settlement report sections without explanatory copy.
- [ ] Step 6 — Add policy, projection, rendering, persistence, MCP, and PDF assertions.
- [ ] Step 7 — Record the dated simplification pass and confirm excluded files remain untouched.
- [ ] Run `./scripts/Test-MigrationGrants.ps1`.
- [ ] Run `dotnet restore ./Pegasus.slnx --locked-mode`.
- [ ] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`.
- [ ] Post-implementation report written.
- [ ] PR opened with Kanmer: ENG-035.
