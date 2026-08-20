# Research — PR-015

The production registration order is the defect: `AddProductionApprovedMailboxResolver` registers `GraphDeletedMailSearchSource` at Program line 183, then `AddPegasusInfrastructure` registers `UnavailableDeletedMailSearchSource` later. Microsoft DI resolves the last single registration. Move the fallback to `TryAdd` semantics so explicit production composition wins and local/unconfigured hosts retain the fallback. Source: current `Program.cs` and `DependencyInjection.cs`.
