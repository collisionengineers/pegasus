# Files — ENG-018

- `src/Pegasus.Core/Eva/CaseEvaMapping.cs`: remove the activation acceptance type/check while retaining mapping identity/version in successful export data.
- `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` and `src/Pegasus.Infrastructure/DependencyInjection.cs`: remove the acceptance dependency and pass only case evidence plus export date.
- `src/Pegasus.Web/Program.cs` and `infra/modules/platform.bicep`: remove the obsolete configuration registration and environment settings.
- Existing Core, integration, browser, and architecture tests that construct or configure `EvaMappingAcceptance`: update them and add regression coverage for configuration-free Export.
- `docs/frd/frd-07-eva-and-external-engineering-handoff.md`, `docs/current-architecture.md`, and `docs/operations.md`: describe Review as the sole readiness gate and record the correction.

Verified on current `origin/main`: production configuration supplies mapping version 2 while the deployed Core source expects version 1; Azure reports the deployed Web revision healthy and running.
