# Proof — AUTO-004 (verified on deployed release 16, 2026-08-21)

Type: test-output + command-log. Deployment evidence bundle: [[DELIV-015]] proof.

- Deployed at release 16 (`4111ad29`, PR #470 squash `38c419ca`): the Unidentified tools are registered and the typed Triage tools exist, all delegating to the same Core owners the Web caller uses (IGetTriage/IRecordTriageFinding/…, actor + operation-key + expected-version discipline throughout) — verified in review by reading the delegation sites; no policy lives in the Web MCP layer.
- The governed `/mcp` inventory contract (35 tools, fails if one is omitted) and the real HTTP caller fixtures (`AutomationIntakeParityIngressTests`, `AutomationMcpIngressTests`, `AutomationConnectorAuthorizationTests`) merged green in CI.
- Live fail-closed check: anonymous `GET /mcp` on production → 302 to sign-in; the Automation ingress authorization contract (ADR-0026/0027 connector route) is unchanged by this PR — tool invocation still requires the authorized connector token with the `automation.intake` scope.
- Live tool invocation through the operator's connected Claude connector is operator-driven use, not a release gate; the parity acceptance (same Core owners, same guards, attributable history) is proven by the merged ingress fixtures.
