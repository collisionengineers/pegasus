# Checklist — SIMPLI-001

- [ ] Freeze the exact Pegasus source and create a transfer manifest.
- [ ] Create and push filtered history to private `collisionengineers/ai-centre`.
- [ ] Scrub approved cross-repository and data references from the standalone repository.
- [ ] Validate the standalone source without data, Docker, model, connector, or cloud operations.
- [ ] Remove the authorised AI Centre workspace, Pegasus CI steps, and textual references.
- [ ] Run zero-reference searches and Pegasus verification.
- [ ] Commit, push, obtain independent review, and record proof.

Progress 2026-08-13: created private `collisionengineers/ai-centre`; filtered transfer matched all 266 source files and all eleven protected packages byte-for-byte. Published standalone `main` at `e75b8543`; locked restore, Release build/test, and skill-pack test passed.

Verification 2026-08-13: Pegasus `dotnet restore` and Release build passed; `Pegasus.Core.Tests` passed 572/572. `Pegasus.ArchitectureTests` had one unrelated failure in `WorkerActivationReleaseContractTests.LocalDeploymentPlanRejectsAppendedRogueHardCodedWorkerSetting` (PowerShell exception instead of its expected diagnostic); not counted as a pass.
