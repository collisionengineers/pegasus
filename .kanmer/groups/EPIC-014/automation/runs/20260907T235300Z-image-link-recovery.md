# Supplemental run — INTK-063

Run: 20260907T235300Z-image-link-recovery. Frozen roster: INTK-063 only.
Controller: codex-v1-remediation-root. Original 218-ticket roster unchanged.

## State — 2026-09-08T03:30:00Z

Implementing under pack_reconcile in .worktrees/intk-063, branch
INTK-063-image-link-recovery, exact accepted base
cdaa02584c38ecc27d3bd24784f59da189138bc1. Root fully read and approved
plan7e7246481a9a46bd/files9bc24e63a2b10a38. No required credential is missing.

Reuse current CaseMatchIndex, the existing pairing owner and staged timer.
Persisted reasoned manual association is distinct from automatic identity;
every merge rechecks the current association transactionally. All registered
group members must be linked before the one merge. Persisted expected member
count preserves the existing single-image versus multiple-image ambiguity
rule. Failures remain eligible for bounded recovery, without permanently
excluding a later-corrected match. Existing acceptance, custody and restricted
Worker fixtures prove the actual callers; no new host or stress suite.

Author stops at frozen source for root's sole-owner focused commands, followed
by independent review and exact-merge proof. No runtime, provider or deployment
PASS is claimed yet. INTK-064 waits for these shared files to be released.
Historical claims/workspaces remain preserved, not absorbed or transferred.
