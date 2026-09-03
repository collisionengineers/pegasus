---
id: PLAT-013
type: ticket
title: Stop the Functions worker SIGABRT crash loop (dotnet exit 134)
status: done
area: platform-operations
order: 1610
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-20T03:35:48.885Z'
  review: '2026-08-20T03:48:34.058Z'
  verifying: '2026-08-20T03:53:38.497Z'
  done: '2026-08-20T12:47:00.719Z'
labels:
  - defect
  - worker
  - production
  - stability
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - ab443909
prs:
  - '438'
deployment: production
archived: false
created: '2026-08-20T03:25:25.690Z'
updated: '2026-09-03T09:06:50.994Z'
---

## What

Production App Insights (48 h to 2026-08-20): **344 aborts of the Functions .NET isolated worker** ("dotnet exited with code 134 (0x86)" = SIGABRT; 555 rethrows) plus 393 `JobHost.StopAsync` failures ("The host has not yet started"). The worker `pegasus-prod-worker-252ow37gij` is crash-looping continuously.

Diagnose the root cause (candidates: native library abort — ONNX/SkiaSharp — under memory pressure on the consumption plan; unhandled fatal in a timer function; host shutdown race) and fix it.

## Why

A crash-looping worker delays every queue-driven outcome. The 2026-08-20 grouped upload had 3 members burn their first attempt with no FailureCode recorded — the signature of a host death mid-batch — pushing them onto 60 s retries and into Unidentified.

## Verification

- [ ] Root cause named with production evidence (stack/trace correlation, not speculation).
- [ ] After the fix deploys: exit-134 aborts stop in App Insights over a multi-hour window.
- [ ] Grouped uploads complete on attempt 1.
