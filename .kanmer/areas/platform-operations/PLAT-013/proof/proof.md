# Proof — PLAT-013

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #438), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Live production readback post-deploy: **zero** "exited with code 134" traces and **zero** exceptions in App Insights since the release (ingestion confirmed flowing); both worker polls completing on schedule (sent 12:29:15Z clean, inbox 12:29:45Z). This deployment itself ran an `azd provision` — the exact window that previously triggered the abort loop — without a single abort.
- Verification lane at the cut: `BoxCustodyOptions` parsing deferred to first Box use in **both** composition roots (`Func<IServiceProvider, BoxCustodyOptions>` factories; `Box` removed from eager `ProductionExternalOptions`); an unresolved `@Microsoft.KeyVault(` placeholder is named in the error and fails the work item into retry/poison instead of killing the host; tests `AnUnresolvedBoxSecretFailsTheFirstBoxUseNotHostBuild` + `ConfigurationNamesAnUnresolvedKeyVaultReferenceDirectly`.
- Longer observation window rides normal operations; the known App Insights daily cap thins telemetry but did not block this evidence.
- Full transcript: DELIV-013 scratch.
