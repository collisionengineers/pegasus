# Checklist — PLAT-007

- [x] Existing Azure IaC and release workflow build the integrated renderer with matched Chromium/native dependencies (`platform.bicep` Chromium base image, ADR-0028, release 12 evidence).
- [x] A deployed render completes and emits telemetry (Report draft action renders real Chromium PDF; container turned Healthy on first pull per `docs/operations.md`).
- [ ] Retry, timeout, restart, duplicate delivery, and unavailable-renderer behavior proven fail-closed **for an automatic/durable trigger** — this remains DOCS-001's not-yet-built scope; infrastructure-level unavailable-renderer fail-closed (Liveness/Readiness restart) IS in place.
- [x] No standalone CollisionRenderer Azure service/API/MCP deployment exists (single in-process DI registration confirmed).
