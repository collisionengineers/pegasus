# Proof — INTK-013

Type: command-log + visual. Released in **release 14** (`d91fd7d7…`, PR #456), production smoke passed 2026-08-20; production serves the SHA (`/diagnostics/version`).

- Live (signed-in browser pass, 2026-08-20 ~13:32 London): `/Triage` Not-ready badge **10** = rows **10** (QDOS26001/26002 + AU17SEO-01..07 + G6KDL-01); Dashboard Not-ready tile and Queues rail badge agree.
- Independent verification lane at the release cut: `EfDashboardQueries.GetCaseStageCountsAsync` adds unmerged `AwaitingInstruction` ImageIntakes via `EfImageIntakeStore.ToCode` reuse; regression test `NotReadyBadgeCountMatchesRowsAcrossBothOrigins` present.
- Full route + verification transcript: DELIV-013 scratch.
