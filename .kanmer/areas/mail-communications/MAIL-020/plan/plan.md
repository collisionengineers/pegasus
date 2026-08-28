# MAIL-020 — plan

## Premises (verified read-only, 2026-08-27)

- The daily caps are **not** declared in `infra/modules/platform.bicep`: the
  `Microsoft.Insights/components` resource has no `pricingPlans`, the
  workspace has no `workspaceCapping`. Both 0.1 GB caps are live-only state.
- The Worker registers `AddApplicationInsightsTelemetryWorkerService()` with
  defaults, so `DependencyTrackingTelemetryModule` reports every SQL command.
  `host.json` excludes `Dependency` from host sampling, so nothing thins it.
- Live facts from the ticket (not re-derived): ~100 MB in 00:00–05:30Z,
  `AppDependencies` 64.7 MB. Extrapolated to a full day the estate emits
  roughly 450 MB, ~300 MB of it dependencies — filtering alone does not fit
  0.1 GB, so the cap must also rise.
- Bicep has no decimal literal; a fractional GB is expressed as
  `json('0.5')`. `az bicep build --file infra/main.bicep` exit 0 with both
  caps bound to the one variable (checked).

## Steps

1. **Worker: drop successful SQL dependency telemetry.**
   `src/Pegasus.Worker/SqlDependencyTelemetryFilter.cs`, an
   `ITelemetryProcessor` that swallows `DependencyTelemetry` with
   `Type == "SQL"` and `Success != false`. Failed SQL calls, HTTP dependencies
   (Graph, Box, DVLA/DVSA), requests, exceptions and traces pass untouched.
   Registered in `Program.cs` through the SDK's own
   `AddApplicationInsightsTelemetryProcessor<T>()` — reuse, no new
   abstraction, no package.
2. **Bicep: declare the caps once.** `var telemetryDailyCapGb = json('0.5')`
   feeds `workspaceCapping.dailyQuotaGb` on the workspace and `cap` on a new
   `Microsoft.Insights/components/pricingPlans@2017-10-01` child (`current`,
   `warningThreshold: 90`). Reset times are read-only (component 00:00Z,
   workspace 03:00Z) and cannot be aligned; the shared number is the "one
   clear limit".
3. **Docs.** `docs/operations.md`: Monitoring/cost bullet states the new
   shape; the release-19 paragraph records the decision taken and that the
   live caps stay 0.1 GB until applied.
4. **Verify.** `dotnet restore --locked-mode`, `dotnet build -c Release
   --no-restore`, `dotnet test -c Release --no-build --filter
   "Category!=Corpus"` (log `artifacts/mail-020/test-full.log`),
   `az bicep build --file infra/main.bicep`.
5. **PR** to `dev`, post-implementation report, move to review.

## Operator approval required (not executed by this ticket)

Live writes, exact targets, need explicit approval (sub
`e6076573-23a5-46a8-acef-7e22d264e5db`, rg `rg-pegasus-prod`):

- Component `pegasus-prod-appi-252ow37gij`: daily cap 0.1 → 0.5 GB
  (`az monitor app-insights component billing update --app
  pegasus-prod-appi-252ow37gij -g rg-pegasus-prod --cap 0.5`).
- Workspace `pegasus-prod-logs-252ow37gij`: daily quota 0.1 → 0.5 GB
  (`az monitor log-analytics workspace update -n pegasus-prod-logs-252ow37gij
  -g rg-pegasus-prod --quota 0.5`).
- Or the next release's `azd provision` applies both from bicep once merged.
  Ceiling ≈ 0.4 GB/day extra pay-as-you-go ingestion, a few pounds a month
  against the £75 budget.

## Acceptance

- Worker filter has a named production caller (`Program.cs`).
- `az bicep build` exit 0; canonical gate exit 0; CI green.
- `docs/operations.md` updated.

## Simplification pass

(filled before the PR)

### 2026-08-27 — findings (code-simplifier agent, four lenses, over the branch diff)

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | Incident narrative repeated in class comment, bicep comment and docs | Applied — comments trimmed; `docs/operations.md` owns the history |
| 2 | `host.json` already exempts `Dependency` from host sampling — relationship unrecorded | Recorded here: un-excluding `Dependency` would sample failed calls too; the processor keeps every failure, so the host setting stays as is |
| 3 | `Success: not false` also dropped unknown-success records while docs said "successful" | Applied — filter now matches `Success: true` |
| 4 | Processor runs after SDK sampling | Rejected — no effect on outcome, negligible cost |
| 5 | `pricingPlans` PUT without `planType` may reset the plan | Applied — `planType: 'Basic'` declared explicitly; what-if against the prod rg is the release's pre-provision step |
| 6 | `stopSendNotificationWhenHitCap: false` reads like its inverse | Kept (notifications stay on); docs now say so |
| 7 | `json('0.5')` convention | Rejected — already the file's convention (`cpu: json('1.0')`) |
| 8 | Monitoring/cost bullet stated declared value as live | Applied — live 0.1 GB noted inline |
| 9 | `current-architecture.md` / `open-decisions.md` still say 0.1 GB | Rejected — they are as-built and still true; the release that provisions refreshes them |
| 10 | Reset-time text adjacent to the earlier 03:00Z belief without acknowledgement | Applied — paragraph says it corrects the one above |
| 11 | Web also emits SQL dependencies unfiltered | Deferred — outside the brief; follow-up ticket if Web volume becomes material |
