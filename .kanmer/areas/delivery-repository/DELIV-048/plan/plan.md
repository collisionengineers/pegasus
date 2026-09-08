# DELIV-048 plan

Diff estimate: 14 files, about 200 changed lines including focused script
assertions and ADR; one release-tooling correction, no CI redesign.

## Objective

Develop and release using native Windows x64 or Linux x64 PowerShell 7 while
Web/Worker deployed runtime and OCI image remain Linux x64.

## Starting state

origin/dev 1d972f05c0f10c2ecf804f271a4fd3155242f1ef; fresh isolated worktree.
Evidence: research/research.md@aa46acab1cdb3b64 and
files/files.md@a6eace140ea512ca record builder/validator and current consumers. The original
DELIV-048 Linux-only CI audit remains historical in the ticket. EPIC-014 and
the current operator request replace that premise. Profile fix matches release
consequence. No open questions or extra approval required.

## Governing docs

New ADR-0039 supersedes ADR-0037's Linux-only workstation choice on explicit
operator authority. Preserve ADR-0007 direct terminal order/approval and
ADR-0014 environment boundary. Current working docs and as-built tooling agree;
no operational/deployed state is asserted.

## Required changes

Use Get-PegasusPlatform through one small Get-PegasusMigrationBundle helper
with actual x64 guard and host runtime/name pair. Builder uses it for the EF
bundle and schema3 manifest; keep all Linux Web/Worker publish and OCI checks.
Validator compares both fields to that host's expected pair before artifact
hash checks and invokes Unix mode API only on Linux. Existing complete census,
hash, approved-manifest, OCI platform and live-environment gates stay intact.
Update only corresponding source assertions; remove obsolete Windows bans.
Add bounded existing-script acceptance with isolated temporary fixtures and
stubbed external OCI calls; no actual build, Docker, Azure or database call.
Preserve LocalDB assertions on Windows and explicitly skip just those on Linux.
Update docs, supersession status and forwarding skill, not two procedures.

## Expected files

| Action | Path | Purpose |
| --- | --- | --- |
| Modify | scripts/PegasusPlatform.ps1 | Existing platform helper; one release bundle identity. |
| Modify | scripts/Build-ReleaseArtifacts.ps1 | Host-matching EF bundle; keep Linux deployed artifacts. |
| Modify | scripts/Test-AzureDeploymentPlan.ps1 | Host/pair validation, Linux-only mode check; keep safety gates. |
| Modify | scripts/Test-PegasusPlatform.ps1 | Cheap existing script acceptance, actual/mocked supported host cases. |
| Modify | tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs | Validator fixture imports required platform file. |
| Modify | AGENTS.md | Portable release convention, outside managed section only. |
| Modify | docs/runbook.md | Native Windows/Linux release path and exact bundle selection. |
| Modify | docs/current-architecture.md | Repository tooling paragraph, no deployment claim. |
| Modify | docs/adr/0037-linux-authorised-release-workstation.md | Supersession metadata/status only. |
| Add | docs/adr/0039-windows-and-linux-release-workstations.md | User-approved workstation decision; no new deploy route. |
| Modify | docs/adr/README.md | Decision index. |
| Modify | .agents/skills/pegasus-release/SKILL.md | Canonical current release procedure. |
| Modify | .agents/skills/pegasus-release/references/database-migration.md | Manifest-named migration executable. |
| Modify | .codex/skills/pegasus-release/SKILL.md | Preserve canonical forwarding; name portable route. |

## Do not modify

- src/**
- infra/**
- .github/**
- docs/operator-notes.md
- docs/operations.md
- docs/design/test-ui/**

## Constraints

One native platform per release run. No architecture/platform invention,
Windows containers, feature flags, packages, extra CI lane or live write.
Root is sole heavy verifier; PLAT-028 remains frozen. Shared checkout and
other worktrees preserved. Canonical .agents skill remains source; .codex
continues forwarding. ADR-0039 is reserved for this lane.

## Ordered steps

1. Extend existing platform helper, builder and validator; update the existing
   copied validator fixture with its required platform dependency.
2. Add focused acceptance to the existing platform script for Windows/Linux
   identity, invalid names/runtime/host, artifact hash and Linux executable
   bit checks using temporary non-domain files and stubbed ORAS only.
3. Supersede ADR0037, add0039 and align current AGENTS/runbook/as-built/skills.
4. Run cheap parse/diff/script checks; freeze for root's focused architecture
   checks and exact-release artifact validation. Record limits and evidence.

## Acceptance checks

Existing Build-ReleaseArtifacts and Test-AzureDeploymentPlan are the callers;
both bootstrap consumers retain Artifact validation. Matching Windows pair is
win-x64/efbundle.exe; Linux pair linux-x64/efbundle. Wrong or crossed pairs and
other-host bundle fail; Linux missing owner execute fails only on Linux.
Web/Worker RID and inspected OCI linux/amd64 remain required. No copy of the
superseded Linux-only active rule remains; historical ADR body stays intact.

## Commands

Implementation-owned: PowerShell AST parse of changed scripts;
pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1; git diff --check.
Root-owned: focused existing WorkerActivationReleaseContractTests; existing
Test-AzureDeploymentPlan -Mode Local; clean exact-SHA Build-ReleaseArtifacts
and Artifact validation when packaging the integrated release, once only.
No UI/snapshot capture or separate .NET build by this lane.

## Failure and deviation rules

Report check failures and expand only with explicit parent coordination.
Do not claim host-mocked checks prove native Linux artifact execution.
Any unrelated deferred-Azure-service assertion change belongs to root OCR lane.

## Stop condition

Freeze source and report commands/filters to root for verification. Do not
commit, open PR or move to Review until root supplies evidence. Independent
review owns merge; no cloud write or production deployment in this ticket.

## Exact-merge correction — 2026-09-08

Root's full-read proof2462b18082379607 records FAIL implementation at PR681
merge1c1d7a0a45555604bafd3e732bd606bf083b804a. Reuse the same recorded
DELIV-048-portable-release branch/.worktrees/deliv-048; no second claim.
After clean recorded-identity checks, normally merge current accepted dev;
preserve the original squash-reviewed source and all newer integrated changes.
Root authors only the existing ORAS Windows repair hint and regression in
Test-PegasusPlatform's existing two-host mapping loop (supply Kind in its mock).
Both hosts point to the existing official pinned ORAS installation guidance.
No new commands/conventions, dependencies or installer; AGENTS already records
ADR0039, so this repairs text rather than changes that convention. Existing
file map already includes both files. Run that cheap script and native hint
check only; original artifact validation remains pending. New exact-head
independent review is required; prior proof FAIL stays in history and no
Done/deployment claim is allowed from this correction alone.
