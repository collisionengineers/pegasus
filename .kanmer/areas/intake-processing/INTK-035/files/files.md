# Files — INTK-035

Branch `task/intk-035-open-triage`, worktree
`C:\Users\Alex\Documents\GitHub\pegasus-worktrees\intk-035-open-triage`,
based on `task/intk-033-triage-from-intake` @ `e6144344`.

## Changed

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | `IsTriageRequest` `internal` → `public`. One word. The Web surface needs the recorded classification read by its single owner rather than re-derived. |
| `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs` | Inject `ICreateTriageFromIntake`, `ITriageQueries`, `ReconcileUnidentifiedDestinations`. Add `Triage` + `CanOpenTriage` state, loaded beside the existing Image-intake load. Add `OnPostOpenTriageAsync`. |
| `src/Pegasus.Web/Pages/Intake/Details.cshtml` | One panel gated on `CanOpenTriage`, beside "Register Image intake". Heading/label/input/button only — no guidance sentence (the necessary-copy list is closed). |
| `tests/Pegasus.IntegrationTests/…` | End-to-end test of the whole operator rule. |

## Read, not changed (the owners being reused)

| File | Why it stays as it is |
| --- | --- |
| `src/Pegasus.Core/Triage/TriageLifecycle.cs` | `CreateTriageFromIntake` + `TriageLifecycleRules.ValidateCreate` are the creation owner. Unchanged: the new caller supplies exactly what the existing contract asks for. |
| `src/Pegasus.Core/Intake/ReconcileUnidentifiedDestinations.cs` | Already owns the supersession, including the Triage branch INTK-033 added. Called, not copied. |
| `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs` | Idempotency per origin receipt and the retained-evidence re-check both already hold for a staff caller. |
| `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` (`EfImageIntakeOriginResolver`) | Resolves the origin for any processed receipt; not image-specific in behaviour. Reused as-is. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeLifecycle.cs` (`NormalizeRegistrationInput`) | The one owner of staff registration input → normalized form. Reused. |
| `src/Pegasus.Core/Identity/StaffAuthorization.cs` | `Require(actor, PerformCasework)` is the authorization owner. Called from the handler. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | `ICreateTriageFromIntake` (:129), `ITriageQueries` (:124) and `ReconcileUnidentifiedDestinations` (:121) are all already `AddScoped`. **No DI change needed.** |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | The automatic path stays exactly as INTK-033 left it. |
| `docs/operator-notes.md` | Protected. Source of the rule. Not edited. |

## Deliberately not touched

- `OnPostCorrectDraftAsync` / `IResolveIntake` / `EfIntakeMutationStore` — the trap (see research). Using them would push a triage request back into case allocation.
- Any MCP surface — out of scope by decision.
- `docs/current-architecture.md` / `docs/operations.md` — no deploy in this ticket.
