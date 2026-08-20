# Proof — PLAT-011

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #452), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Verification lane at the cut: `ActorDisplayNames` Core resolver ("Unknown user" fallback) wired through the three query owners; Automation Activity renders `AutomationActorLabel` (configured client name / "Unknown automation client", never a raw GUID); case summary shows `ReportApprovedByDisplayName`; case history, triage details, and mail message render `ActorDisplayName`; repo-wide `.cshtml` grep shows no remaining raw `SubjectId` render. `EfStaffAccountQueries` is UserManager-free so the Worker composes without Identity (the earlier Identity-less-host break fixed and pinned).
- Live: release-14 pages serve these surfaces; the release copy audit independently confirmed this release *removed* two raw-GUID renders.
- Full transcript: DELIV-013 scratch.
