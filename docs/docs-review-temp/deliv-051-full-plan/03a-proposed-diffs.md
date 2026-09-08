# Exact implementation diffs

Temporary working reference for DELIV-051. Amended from the operator answers on 2026-09-08. Canonical documents now carry the requirements; this folder is not an additional authority.

The following patches describe the implemented text changes against `af1625fae8ac8018054c95e988907f6c44fa4639`. They are review artifacts, not commands to apply again. Binary/supplied-file changes are recorded by hashes in baseline-and-patches.json and in Git.

| File | Exact diff |
| --- | --- |
| `.agents/skills/pegasus-release/SKILL (2).md` | [001-SKILL (2).md.patch](patches/001-SKILL (2).md.patch) |
| `.agents/skills/pegasus-release/SKILL.md` | [002-SKILL.md.patch](patches/002-SKILL.md.patch) |
| `.agents/skills/pegasus-release/references/troubleshooting.md` | [003-troubleshooting.md.patch](patches/003-troubleshooting.md.patch) |
| `.agents/skills/pegasus-wipe-intake-data/SKILL (2).md` | [004-SKILL (2).md.patch](patches/004-SKILL (2).md.patch) |
| `.agents/skills/razor-pages-ui-implementation/SKILL.md` | [005-SKILL.md.patch](patches/005-SKILL.md.patch) |
| `AGENTS.md` | [046-AGENTS.md.patch](patches/046-AGENTS.md.patch) |
| `CONTEXT.md` | [047-CONTEXT.md.patch](patches/047-CONTEXT.md.patch) |
| `README.md` | [048-README.md.patch](patches/048-README.md.patch) |
| `docs/adr/0002-dotnet-modular-monolith-on-azure.md` | [049-0002-dotnet-modular-monolith-on-azure.md.patch](patches/049-0002-dotnet-modular-monolith-on-azure.md.patch) |
| `docs/adr/0003-pdfpig-for-first-qdos-slice.md` | [050-0003-pdfpig-for-first-qdos-slice.md.patch](patches/050-0003-pdfpig-for-first-qdos-slice.md.patch) |
| `docs/adr/0005-multiformat-intake-assets.md` | [051-0005-multiformat-intake-assets.md.patch](patches/051-0005-multiformat-intake-assets.md.patch) |
| `docs/adr/0006-provider-neutral-intake-with-contained-qdos-policy.md` | [052-0006-provider-neutral-intake-with-contained-qdos-policy.md.patch](patches/052-0006-provider-neutral-intake-with-contained-qdos-policy.md.patch) |
| `docs/adr/0007-direct-terminal-azure-deployment.md` | [053-0007-direct-terminal-azure-deployment.md.patch](patches/053-0007-direct-terminal-azure-deployment.md.patch) |
| `docs/adr/0008-separate-direct-provider-and-intermediary-email-policies.md` | [054-0008-separate-direct-provider-and-intermediary-email-policies.md.patch](patches/054-0008-separate-direct-provider-and-intermediary-email-policies.md.patch) |
| `docs/adr/0009-adopt-pegasus-monorepo-workspaces.md` | [055-0009-adopt-pegasus-monorepo-workspaces.md.patch](patches/055-0009-adopt-pegasus-monorepo-workspaces.md.patch) |
| `docs/adr/0013-qdos-alpha-implementation-contract.md` | [056-0013-qdos-alpha-implementation-contract.md.patch](patches/056-0013-qdos-alpha-implementation-contract.md.patch) |
| `docs/adr/0015-host-web-on-container-apps-consumption.md` | [057-0015-host-web-on-container-apps-consumption.md.patch](patches/057-0015-host-web-on-container-apps-consumption.md.patch) |
| `docs/adr/0018-provider-inspection-mode-database-setting.md` | [058-0018-provider-inspection-mode-database-setting.md.patch](patches/058-0018-provider-inspection-mode-database-setting.md.patch) |
| `docs/adr/0019-in-process-onnx-vrm-recognition.md` | [059-0019-in-process-onnx-vrm-recognition.md.patch](patches/059-0019-in-process-onnx-vrm-recognition.md.patch) |
| `docs/adr/0021-automation-actor-direct-write-assessment-contract.md` | [060-0021-automation-actor-direct-write-assessment-contract.md.patch](patches/060-0021-automation-actor-direct-write-assessment-contract.md.patch) |
| `docs/adr/0024-stable-approved-mailbox-identity-and-explicit-baseline.md` | [061-0024-stable-approved-mailbox-identity-and-explicit-baseline.md.patch](patches/061-0024-stable-approved-mailbox-identity-and-explicit-baseline.md.patch) |
| `docs/adr/0026-enable-automation-mcp-by-explicit-deployment-configuration.md` | [062-0026-enable-automation-mcp-by-explicit-deployment-configuration.md.patch](patches/062-0026-enable-automation-mcp-by-explicit-deployment-configuration.md.patch) |
| `docs/adr/0027-authorization-code-for-external-mcp-connectors.md` | [063-0027-authorization-code-for-external-mcp-connectors.md.patch](patches/063-0027-authorization-code-for-external-mcp-connectors.md.patch) |
| `docs/adr/0029-image-initiated-case-projection.md` | [064-0029-image-initiated-case-projection.md.patch](patches/064-0029-image-initiated-case-projection.md.patch) |
| `docs/adr/0030-non-additive-schema-changes-before-cutover.md` | [065-0030-non-additive-schema-changes-before-cutover.md.patch](patches/065-0030-non-additive-schema-changes-before-cutover.md.patch) |
| `docs/adr/0031-automation-actor-contract-without-eva-export-tools.md` | [066-0031-automation-actor-contract-without-eva-export-tools.md.patch](patches/066-0031-automation-actor-contract-without-eva-export-tools.md.patch) |
| `docs/adr/0032-near-real-time-durable-intake-triggering.md` | [067-0032-near-real-time-durable-intake-triggering.md.patch](patches/067-0032-near-real-time-durable-intake-triggering.md.patch) |
| `docs/adr/0035-ai-job-ledger.md` | [068-0035-ai-job-ledger.md.patch](patches/068-0035-ai-job-ledger.md.patch) |
| `docs/adr/0036-outbound-mail-via-approved-mailbox.md` | [069-0036-outbound-mail-via-approved-mailbox.md.patch](patches/069-0036-outbound-mail-via-approved-mailbox.md.patch) |
| `docs/adr/0037-linux-authorised-release-workstation.md` | [070-0037-linux-authorised-release-workstation.md.patch](patches/070-0037-linux-authorised-release-workstation.md.patch) |
| `docs/adr/0039-windows-and-linux-release-workstations.md` | [071-0039-windows-and-linux-release-workstations.md.patch](patches/071-0039-windows-and-linux-release-workstations.md.patch) |
| `docs/adr/0041-persistent-automation-keys-and-grant-attribution.md` | [072-0041-persistent-automation-keys-and-grant-attribution.md.patch](patches/072-0041-persistent-automation-keys-and-grant-attribution.md.patch) |
| `docs/adr/0042-staff-send-operation-journal.md` | [073-0042-staff-send-operation-journal.md.patch](patches/073-0042-staff-send-operation-journal.md.patch) |
| `docs/adr/0043-per-engineer-vendor-credential-protection.md` | [074-0043-per-engineer-vendor-credential-protection.md.patch](patches/074-0043-per-engineer-vendor-credential-protection.md.patch) |
| `docs/adr/0044-mail-occurrence-and-business-identity.md` | [075-0044-mail-occurrence-and-business-identity.md.patch](patches/075-0044-mail-occurrence-and-business-identity.md.patch) |
| `docs/adr/0045-document-custody-and-derived-caches.md` | [076-0045-document-custody-and-derived-caches.md.patch](patches/076-0045-document-custody-and-derived-caches.md.patch) |
| `docs/adr/README.md` | [077-README.md.patch](patches/077-README.md.patch) |
| `docs/boundaries.md` | [078-boundaries.md.patch](patches/078-boundaries.md.patch) |
| `docs/capabilities.md` | [079-capabilities.md.patch](patches/079-capabilities.md.patch) |
| `docs/current-architecture.md` | [080-current-architecture.md.patch](patches/080-current-architecture.md.patch) |
| `docs/design/README.md` | [081-README.md.patch](patches/081-README.md.patch) |
| `docs/engineering.md` | [082-engineering.md.patch](patches/082-engineering.md.patch) |
| `docs/engineering/configuration.md` | [083-configuration.md.patch](patches/083-configuration.md.patch) |
| `docs/frd/README.md` | [105-README.md.patch](patches/105-README.md.patch) |
| `docs/frd/frd-01-case-identity-and-lifecycle.md` | [106-frd-01-case-identity-and-lifecycle.md.patch](patches/106-frd-01-case-identity-and-lifecycle.md.patch) |
| `docs/frd/frd-02-intake-and-source-identity.md` | [107-frd-02-intake-and-source-identity.md.patch](patches/107-frd-02-intake-and-source-identity.md.patch) |
| `docs/frd/frd-03-triage.md` | [108-frd-03-triage.md.patch](patches/108-frd-03-triage.md.patch) |
| `docs/frd/frd-04-parties-accounts-and-access.md` | [109-frd-04-parties-accounts-and-access.md.patch](patches/109-frd-04-parties-accounts-and-access.md.patch) |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | [110-frd-05-documents-extraction-and-custody.md.patch](patches/110-frd-05-documents-extraction-and-custody.md.patch) |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | [111-frd-06-vehicle-and-engineering-evidence.md.patch](patches/111-frd-06-vehicle-and-engineering-evidence.md.patch) |
| `docs/frd/frd-07-eva-and-external-engineering-handoff.md` | [112-frd-07-eva-and-external-engineering-handoff.md.patch](patches/112-frd-07-eva-and-external-engineering-handoff.md.patch) |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | [113-frd-08-email-mailbox-and-background-processing.md.patch](patches/113-frd-08-email-mailbox-and-background-processing.md.patch) |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | [114-frd-09-provider-and-intermediary-routes.md.patch](patches/114-frd-09-provider-and-intermediary-routes.md.patch) |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | [115-frd-10-mcp-automation-and-actor-boundary.md.patch](patches/115-frd-10-mcp-automation-and-actor-boundary.md.patch) |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | [116-frd-11-reports-correspondence-and-reviewed-proposals.md.patch](patches/116-frd-11-reports-correspondence-and-reviewed-proposals.md.patch) |
| `docs/frd/frd-12-operator-experience.md` | [117-frd-12-operator-experience.md.patch](patches/117-frd-12-operator-experience.md.patch) |
| `docs/index.md` | [118-index.md.patch](patches/118-index.md.patch) |
| `docs/json-extraction-parity/Final-Format-Example-02.json` | [119-Final-Format-Example-02.json.patch](patches/119-Final-Format-Example-02.json.patch) |
| `docs/json-extraction-parity/ap.QDOS26015/old-extraction-working/QDOS_NX14AXY.json` | [122-QDOS_NX14AXY.json.patch](patches/122-QDOS_NX14AXY.json.patch) |
| `docs/json-extraction-parity/ap.QDOS26015/pegasus-output/EVA-ap.QDOS26015/EVA-ap.QDOS26015.json` | [124-EVA-ap.QDOS26015.json.patch](patches/124-EVA-ap.QDOS26015.json.patch) |
| `docs/json-extraction-parity/ap.QDOS26015/pegasus-output/EVA-ap.QDOS26015/manifest.sha256` | [130-manifest.sha256.patch](patches/130-manifest.sha256.patch) |
| `docs/json-extraction-parity/ap.QDOS26015/pegasus-output/EVA-ap.QDOS26015/provenance.json` | [131-provenance.json.patch](patches/131-provenance.json.patch) |
| `docs/json-extraction-parity/eva-api-docs.md` | [136-eva-api-docs.md.patch](patches/136-eva-api-docs.md.patch) |
| `docs/open-decisions.md` | [138-open-decisions.md.patch](patches/138-open-decisions.md.patch) |
| `docs/operations.md` | [139-operations.md.patch](patches/139-operations.md.patch) |
| `docs/operator-notes.md` | [140-operator-notes.md.patch](patches/140-operator-notes.md.patch) |
| `docs/prd/README.md` | [141-README.md.patch](patches/141-README.md.patch) |
| `docs/prd/pegasus-product.md` | [142-pegasus-product.md.patch](patches/142-pegasus-product.md.patch) |
| `docs/principal-rules-and-mappings/README.md` | [143-README.md.patch](patches/143-README.md.patch) |
| `docs/runbook.md` | [144-runbook.md.patch](patches/144-runbook.md.patch) |
| `reference/EVA/EVA_API_SCHEMA.md` | [145-EVA_API_SCHEMA.md.patch](patches/145-EVA_API_SCHEMA.md.patch) |
| `reference/README.md` | [146-README.md.patch](patches/146-README.md.patch) |
| `scripts/Invoke-QdosAlphaAcceptance.ps1` | [147-Invoke-QdosAlphaAcceptance.ps1.patch](patches/147-Invoke-QdosAlphaAcceptance.ps1.patch) |
| `scripts/Test-DocumentationLinks.ps1` | [148-Test-DocumentationLinks.ps1.patch](patches/148-Test-DocumentationLinks.ps1.patch) |
| `scripts/Test-MarkdownPlacement.ps1` | [149-Test-MarkdownPlacement.ps1.patch](patches/149-Test-MarkdownPlacement.ps1.patch) |
| `scripts/Test-TestMarkdownPlacement.ps1` | [150-Test-TestMarkdownPlacement.ps1.patch](patches/150-Test-TestMarkdownPlacement.ps1.patch) |
