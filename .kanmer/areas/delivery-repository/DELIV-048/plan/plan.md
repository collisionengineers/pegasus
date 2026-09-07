# DELIV-048 plan

Diff estimate: 14 files, about 200 changed lines including focused script
assertions and ADR; one release-tooling correction, no CI redesign.

## Objective

Develop and release using native Windows x64 or Linux x64 PowerShell 7 while
Web/Worker deployed runtime and OCI image remain Linux x64.

## Starting state

origin/dev 1d972f05c0f10c2ecf804f271a4fd3155242f1ef; fresh isolated worktree.
Research records builder/validator and all current consumers. The original
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

Use the exact 14 repository-relative paths in files/files.md.

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
