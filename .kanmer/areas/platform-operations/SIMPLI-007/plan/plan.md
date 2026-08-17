# Plan — SIMPLI-007: move the QDOS alpha acceptance gate out of application composition

Diff estimate: ~6 files, roughly +60 / −480 (one Core file emptied to its marker, one Web line, one test file deleted, one script edited, two doc passages).

## Approach

Delete the gate from Core and Web — it is registered-only and test-only, and its capability roster is stale — and make `scripts/Invoke-QdosAlphaAcceptance.ps1` the single owner of offline-candidate validation. The one check the C# gate added that the script lacked (does the manifest cover every alpha capability?) is retained in the script by **reading `docs/capabilities.md`** for rows at the target version, so the register stays the only source and cannot drift from a hard-coded list again. No ADR is superseded (none placed the gate in composition; ADR-0013 speaks of acceptance gates only generically). No deployment, no live claim.

Governing docs: [ADR-0013](../../../docs/adr/0013-qdos-alpha-implementation-contract.md) (QDOS alpha implementation contract) — unchanged; `docs/runbook.md` and `docs/operations.md` are the working docs updated. Reuses: the script's existing `Assert-OfflineCandidatePrerequisites` / `Assert-LocalRunEvidence`; the register's version column.

## Steps

1. **Core + Web deletion.** `src/Pegasus.Core/CoreAssembly.cs` → keep only the marker `public static class CoreAssembly;` (drop the `using` and lines 10–316). `src/Pegasus.Web/Program.cs` → remove the `AddSingleton<QdosAlphaAcceptanceGate>()` line. Delete `tests/Pegasus.IntegrationTests/QdosAlphaAcceptanceGateTests.cs`. Build must be 0/0 (`TreatWarningsAsErrors`).
2. **Script owns validation.** In `Invoke-QdosAlphaAcceptance.ps1` `OfflineCandidate`: replace the env-var plumbing + `dotnet test` gate invocation (`:381-390`, `:414-427`, `:479-490`) with a script function `Assert-AlphaCapabilityCoverage` that (a) parses `docs/capabilities.md` rows whose version column equals the target (`0.1.0-alpha.1`), (b) checks the manifest's capability observations cover each ID (accepted / externally completed for OPS-10/24/25 / deferred-with-reason where the register marks a permanent boundary), and (c) fails closed with the same "blockers" wording the C# gate used. Keep `dotnet test --filter 'Category=QdosAlphaAcceptance'` as the acceptance test lane (it still selects Recovery/Triage tests) — but as a test run, not a gate call. Remove the `PEGASUS_QDOS_ACCEPTANCE_MANIFEST`/`_SOURCE_REVISION` env contract if nothing else reads it (grep). Fix the stale hashed-file list at `:505-514` only if it is inside a function already being edited.
3. **Docs.** `docs/runbook.md:677-716` — validation is script-owned; drop the "Web-host gate compares … `/diagnostics/version`" sentence; describe the register-derived roster. `docs/operations.md:58,67-79` — reword; keep `QdosAlphaAcceptance` in the trait list.
4. **Verify.** Locked restore; Release build 0/0; Core tests; Architecture tests (esp. `DependencyDirectionTests` — marker survives); IntegrationTests filter `Category=QdosAlphaAcceptance` (still selects and passes the Recovery/Triage set); run `scripts/Invoke-QdosAlphaAcceptance.ps1 -Profile OfflineCandidate` against a deliberately incomplete manifest → fails closed naming missing IDs, and against a synthetic complete one → passes (no live/corpus inputs); `rg -n "QdosAlphaAcceptanceGate|IQdosAlphaAcceptanceGate|AcceptanceManifestKind|PEGASUS_QDOS_ACCEPTANCE" src tests scripts docs` → only intended script/doc hits.
5. **Simplification pass** over the diff (proportional: one combined-lens check), findings appended here; post-implementation report; PR to `dev`.
6. **Independent plan-vs-diff review; CI green; merge; verify on merged `dev`; proof; closeout.**

## Verification (acceptance)

- Web composition registers no `QdosAlphaAcceptanceGate`; Core has no gate types; the interface is gone.
- `Invoke-QdosAlphaAcceptance.ps1 -Profile OfflineCandidate` still fails closed on a manifest missing an alpha capability, and the roster it enforces equals `docs/capabilities.md` rows at `0.1.0-alpha.1` (131 today), not a hard-coded list.
- Recovery/Triage tests under the `QdosAlphaAcceptance` trait still run and pass.

## Risks / stop rules

- Stop if any production code path (page, endpoint, MCP tool, Worker function) turns out to resolve the gate — the survey found none, but the build/grep in step 4 is the proof.
- Do not touch `CiPressure`, `qdos-pressure.yml`, or PerformanceTests staging.
- Do not claim any change to the release status of OPS-10/24/25.

## Simplification pass — 2026-08-17 (one combined-lens `code-simplifier` check over the 6-file / +279 −516 diff, before the PR)

**Applied**
- `Assert-AlphaCapabilityCoverage`: each present external gate is validated **once** into a per-gate blocker list; the offline and release verdicts then only ask which required gates are absent — the earlier shape re-ran approval + evidence hashing for the two offline gates in both loops.
- Blocker ordering is **ordinal** (`Get-OrdinalSorted` → `[Array]::Sort` with `StringComparer.Ordinal`), restoring the deleted C# gate's contract; the culture-collation sort had put `capability:null` before `capability:OPS-10:…`. Membership/count unchanged.
- `Get-AlphaCapabilityIds` reads the **"Target release" column by index** (`$cells[4]`, asserting the eight-field split) instead of matching any cell — a Notes cell containing the bare version can no longer false-positive. Same 131 IDs.
- Dead tolerance removed: numeric outcomes (`'1'`/`'2'`, a relic of the deleted `JsonStringEnumConverter`) are now `invalid-outcome`; outcome tokens compared case-sensitively (`-ceq`) like the script's other contract checks; the two accepted tokens are named in the runbook. This is a deliberate fail-closed tightening — no producer emits numbers.
- `Test-LowerHex`: dead `$null -ne` guard and `[AllowNull()]` dropped (`[string]` coerces null to empty).
- Constants: contract constants (`$acceptanceManifestKind`, outcome tokens, gate/capability lists) sit directly above their first reader (`Assert-OfflineCandidatePrerequisites`); register path stays with the path block. `$externallyCompletedCapabilityIds` → `$externalGateCapabilityIds` (the set is right; "completed" read oddly against OPS-24 "Required and accepted").
- `docs/runbook.md`: the coverage-check text is its own paragraph after the local-manifest paragraph (the insertion had split "…`run-manifest.json`. That local manifest…" by 90 words).

**Confirmed clean** — no `QdosAlpha*` / `PEGASUS_QDOS_ACCEPTANCE_*` residue anywhere; all four newly hashed acceptance-lane test files exist and carry the trait; `CoreAssembly.cs` marker file is correct where it is; `docs/operations.md:81-83` trait list still names `QdosAlphaAcceptance`.

**Considered, left alone** — evidence-file hash cache (≈140 small distinct files per real run; a cache is state for no gain).

Harness (`scratchpad/test-007-coverage.ps1`) re-run after the pass: roster 131 (no DOC-06, has MCP-01); incomplete manifest fails closed with the expected blockers; complete manifest → offline accepted, release not (10 blockers).
