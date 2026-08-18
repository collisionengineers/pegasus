## Review — 2026-08-18 (independent reviewer agent; claude-code applied dispositions)

PR #403 reviewed at `db3f57db`; verdict NEEDS-CHANGES → fixed in `3f836469`.

### Changes (reviewer's reading)
- `src/Pegasus.Web/Mcp/AutomationMcp.cs`, `Program.cs`: only the DevelopmentOffline-only guard removed; every other `TryCreate` validation unchanged; SendToAi/LocalIntake/LocalDocumentCustody gates untouched; no test asserted the old MCP throw.
- Bicep: `automationMcpClientSecretUri` plumbed main → module → parameters; secret entry mirrors `box-client-secret`; `AutomationMcp__PublicOrigin` computed as `https://<prefix>-web-<suffix>.<defaultDomain>/` (correct; app cannot self-reference its ingress FQDN); `Features__AutomationMcp=true` renders on every Web provision once the URI is supplied. `Test-AzureDeploymentPlan.ps1` has no Web env census.
- ADR-0026 frontmatter valid, one decision. Operations addition dated/factual/content-safe. FRD-10 untouched.

### Comments and dispositions
1. **blocking — fixed-in-PR**: ADR-0021 was flipped to `superseded` wholesale though only one clause changes and 0026 said the rest "remain in force". Restored 0021 `accepted` (added a `## Status` line naming the amendment), 0026 `supersedes: []` with an explicit "amends ADR-0021 decision 1 and its final consequence only" paragraph; index rows in numeric order (0021 back in the current table, 0026 after 0025).
2. non-blocking — fixed-in-PR: stale DevelopmentOffline transport comment in `AutomationMcpExtensions.cs` reworded to the TLS-at-ingress fact.
3. non-blocking — won't-do (documented instead): a `bool automationMcpEnabled` bicep param. The switch-off paths are now stated in ADR-0026 consequences (provision without the settings; Administrator kill switch for immediate effect). A parameter that only one deployment sets is a flag added for a single caller — simplicity rail.
4. non-blocking — won't-do here: a unit test for `TryCreate` in a Production-shaped configuration. The `AutomationMcpIngressTests` gate/token tests and the deployed smoke + live evidence for this ticket exercise the same path; noted as a candidate follow-up in the report.
5. non-blocking — fixed-in-PR: `docs/operations.md:119` stale "accepted only in DevelopmentOffline" sentence qualified as pre-ADR-0026 with a pointer to Production environment.
6. non-blocking — fixed-in-PR: index ordering; ADR-0021 `## Status` body line added.

### Verdict
PASS after fixes; merge once CI is green on `3f836469`.
