# Plan

Committed in `1a86f5db`. Deployment is Phase 5 of the release plan.

## Root cause

30–60s of processing was a chain of minute-granularity hops:

- inbox poll `45 * * * * *` — once a minute;
- dispatch `*/15`;
- reconciliation `30 * * * * *` — once a minute;
- **and** `host.json` set no queue `maxPollingInterval`, so each queue hop idled back off
  to the Azure Functions 60s default. There are two hops (`intake-work`, `external-work`),
  so that alone was up to 120s of pure waiting.

Box folder creation sat behind the reconciliation tick *and* a second queue hop, which is
why it lagged furthest.

## The change

`maxPollingInterval` to 2s, and the three timers tightened to `*/15`, `*/10`, `*/5`.
Mirrored in `local.settings.example.json` and `Invoke-LocalDevelopment.ps1` so local and
deployed stay identical.

## What was deliberately not done

**No always-ready instance** — operator decision. Cold start therefore remains, and is
stated here rather than hidden: the first request after an idle period still pays it.
This ticket removes the *idle back-off*, which is a different and larger cost.

No Graph webhooks — same decision, same reason.

## Cost

Flex Consumption bills per execution and these are cheap no-op ticks. Going from ~1/min
to ~4/min on the inbox poll, ~6/min on reconciliation and ~12/min on dispatch adds roughly
20 executions a minute of no-op work. Against the £75 budget alert that is immaterial, and
the alert is the backstop if that estimate is wrong.

## Acceptance

- Bicep, `host.json`, local settings and the dev script all carry the same values. ✅
- Deployed worker settings match after provision — Phase 5.
- Measured received-to-case-visible and received-to-Box-folder timings, before and after,
  in numbers — Phase 6. `docs/operations.md` records the expected window so the next
  regression report has a baseline.

## Simplification pass

2026-08-21. Configuration only; no code. The one judgement made was to set
`maxPollingInterval` rather than add an always-ready instance — the cheaper fix that
addresses the actual measured cause. No findings deferred.
