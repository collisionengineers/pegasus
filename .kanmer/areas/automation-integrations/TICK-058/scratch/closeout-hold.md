# HELD — EPIC-011 closeout board walk, 2026-08-29

## Verdict: TICK-058 does NOT reach Done. Left in `review`.

Checked against merged `dev` at `450b9234a6f5626f21adea3c4da244550a3bdace`
(2026-08-29 18:03:20 +0100).

**No `proof` document was written for this ticket, deliberately.** Writing one
would satisfy the only remaining `enter-done` gate and let a later agent walk
this ticket to Done on a gate technicality. The finding is recorded here, in
scratch, which is never gated. When the hold clears, whoever clears it writes
the proof.

## The work IS merged, and is not the problem

PR #594 merged as `0d985c9e` (2026-08-29 15:24:42 +0100);
`git merge-base --is-ancestor 0d985c9e 450b9234` → exit 0. It carried 36+
files: `Core/ProviderApi/{ProviderInstruction,ProviderInstructionJson,
ProviderInstructionPolicy,ProviderSubmission}.cs` (1,264 new lines),
`Infrastructure/Intake/ProviderApiIntakeSourceReader.cs`,
`Persistence/EfProviderSubmissionStore.cs`, the
`20260828111707_ProviderSubmissions` migration, FRD-09 and the bootstrap
script. The solution builds green at `450b9234` (0 warnings, 0 errors, exit 0)
and `ProviderSubmissionTests` passes (inside the 49-test focused Core run,
0 failed).

## The problem: `Features:ProviderApi` is CLOSED in the deployed estate

The entire Provider API surface is behind one startup composition flag:

```
src/Pegasus.Web/ProviderApi/ProviderApi.cs:14
    public const string FeatureFlag = "Features:ProviderApi";

src/Pegasus.Web/Program.cs:289
    var providerApiEnabled = builder.Configuration.GetValue<bool>(ProviderApi.FeatureFlag);
src/Pegasus.Web/Program.cs:711   if (providerApiEnabled) { builder.Services.AddPegasusProviderApi(); }
src/Pegasus.Web/Program.cs:1078  if (providerApiEnabled) { app.MapPegasusProviderApi(); }
```

Where is that flag ever set to true? Exactly one place in the whole
repository:

```
git grep -rn "Features__ProviderApi\|Features:ProviderApi" 450b9234
  docs/frd/frd-09-…:70                      <- prose
  src/Pegasus.Web/ProviderApi/ProviderApi.cs:14        <- the constant
  src/Pegasus.Web/ProviderApi/ProviderApiEndpoints.cs:38  <- a doc comment
  tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs:33
      factory.WithWebHostBuilder(builder => builder.UseSetting("Features:ProviderApi", "true"));
```

**A test. Only a test.** It is set in no `appsettings*.json`, no Bicep, no
workflow, no deployment parameter file —
`git grep -rn "ProviderApi" 450b9234 -- '*.json' '*.bicep' '*.yml' '*.ps1'`
returns nothing. The only feature flag rendered in `infra/` is
`infra/modules/platform.bicep:467` `Features__AutomationMcp = 'true'`.

`docs/operations.md:121` still reads: *"Provider API | **Not implemented: no
endpoint, client, credential, or caller** | Settled actor/client/authentication
contract, real caller evidence, and separately approved activation."*

## Why that bars Done

Three separate rules land on the same conclusion:

- **AGENTS.md rule 14** — *"registered-but-unreachable or **test-only** code is
  not done."* The endpoint composes only under a flag that only a test sets.
- **D21 table** — *"Capability behind a composition/feature gate that is CLOSED
  in the deployed estate → **No**."* This is that row exactly.
- **D26** — *"`Features:ProviderApi` (TICK-058) … [is] opened together in the
  release, under a single approval conversation … nothing behind a closed gate
  is claimed as delivered until the gate is open in the deployed estate and
  `docs/operations.md` records it."*

The ticket's own **What** names the deliverable as "**API-01**: Principal-scoped
provider submission API" — the whole capability, and all of it sits behind the
closed gate. There is no reachable half to carry it, unlike PLAT-048 where the
Service health half is genuinely wired.

## Contrast, so the distinction is not lost later

TICK-077 (EXT-04, EVA API) **was** walked to Done in this same pass, and the
difference is real, not a double standard:

- TICK-077's transport is registered **unconditionally**
  (`Infrastructure/DependencyInjection.cs:635`, no `if`), its route
  `/Cases/{caseId:guid}/Eva/Send` composes in every environment, and it is
  **deployed in production release 36**. Its per-Principal `EvaManualSubmission`
  toggle is operator *data*, switchable through a deployed admin page with no
  redeploy — the D21 "conditionally disabled with a named condition → **Yes**"
  row.
- TICK-058's `Features:ProviderApi` is a **startup composition flag** that no
  deployed configuration sets. Nothing behind it can be reached in any
  environment without a redeploy — the D21 "**No**" row.

## What clears the hold

Per D26, the activation batches into the wave-5 release under a single approval
conversation:

1. `Features__ProviderApi` is rendered true in `infra/modules/platform.bicep`
   alongside the release-37 deploy.
2. `docs/operations.md` records the gate as open, with dated live evidence and
   the `20260828111707_ProviderSubmissions` migration + grants applied.
3. Then this ticket's proof is written against that evidence and it walks
   `review` → `verifying` → `done`.

Also note the ticket is `blockedBy` **TICK-061** (API-04 provider credentials),
itself still in `verifying` — the credential is what makes the endpoint
authenticable, and **D8** ties the Principal "Pegasus API key" to this
endpoint's delivery.

Related open residuals, already ticketed and unaffected by this hold:
**AUTO-012** (the accept path is not atomic across its four writes) and
**AUTO-013** (provider principal absent from the case-data snapshot; paused
credentials read the body).

## Board note

Stage left at `review`. Two of 22 checklist items remain unticked, which is
consistent with the hold. No source file, branch or worktree was touched by
this walk.
