 # Pegasus-focused Codex configuration

  ## Summary and audit findings

  Apply the configuration globally to this Windows Codex installation, as requested. Disable
  unnecessary capabilities while retaining Pegasus development, browser testing, Azure operations
  and OpenAI documentation support.

  The audit covered user and Pegasus configuration, local skill directories, cached plugin
  manifests, bundled MCP definitions and the effective codex mcp list. It found:

  - 43 skills advertised in this session: five Pegasus/Razor skills, 35 Azure skills and three Kusto
    graph/IRQL skills.

  - Existing Azure disable entries target version 1.2.40, while the installed plugin is 1.2.47.
    Those entries no longer target the loaded skills.

  - OpenAI Docs is installed but explicitly disabled.
  - Four MCP servers are configured as enabled: azure, chrome-devtools, cua_repl and node_repl.
    Configuration does not prove each server is connected successfully.

  - Both Azure plugins bundle the same Azure MCP definition.
  - Application Insights and Log Analytics are implemented in Pegasus; retain their supporting
    capabilities.

  - Artifact plugins and app connectors are disabled. Cached plugins are not necessarily active.
  - pegasus-dev-mcp is a ChatGPT integration, as clarified by the operator. Explicitly disable its
    Codex exposure.

  - Kanmer and vehicle-image-sorter cache directories contain no current plugin implementation;
    find-skills has no skill file.

  No configuration changes have been made.

  ## Skill decisions

  ### Retain or enable

   Skills              Decision and purpose
  ━━━━━━━━━━━━━━━━━━  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   pegasus-release,    Retain the established operational procedures.
   pegasus-wipe-
   intake-data
  ──────────────────  ──────────────────────────────────────────────────────────────────────────────
   razor-pages-ui-     Retain all three for Pegasus’s web interface.
   design, razor-
   pages-ui-
   implementation,
   razor-pages-ui-
   review
  ──────────────────  ──────────────────────────────────────────────────────────────────────────────
   openai-docs         Enable for accurate Codex and OpenAI guidance.
  ──────────────────  ──────────────────────────────────────────────────────────────────────────────
   review-agent        Retain the installed review helper; its presence does not authorize
                       automatic delegation.
  ──────────────────  ──────────────────────────────────────────────────────────────────────────────
   azure:appinsight    Retain telemetry, log querying and troubleshooting.
   s-
   instrumentation,
   azure:azure-
   kusto,
   azure:azure-
   diagnostics
  ──────────────────  ──────────────────────────────────────────────────────────────────────────────
   azure:azure-ai      Retain for Document Intelligence OCR.
  ──────────────────  ──────────────────────────────────────────────────────────────────────────────
   azure:azure-        Retain storage and Microsoft identity integration guidance.
   storage,
   azure:entra-app-
   registration
  ──────────────────  ──────────────────────────────────────────────────────────────────────────────
   azure:azure-        Retain estate inspection and architecture mapping.
   resource-lookup,
   azure:azure-
   resource-
   visualizer
  ──────────────────  ──────────────────────────────────────────────────────────────────────────────
   azure:azure-        Retain spending and capacity checks.
   cost,
   azure:azure-
   quotas
  ──────────────────  ──────────────────────────────────────────────────────────────────────────────
   azure:azure-        Retain for explicitly requested operational assessments and hosting changes.
   compliance,
   azure:azure-
   reliability,
   azure:azure-
   upgrade

  ### Disable

  The following names use the azure: prefix unless stated otherwise.

   Skills                                            Reason
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   airunway-aks-setup, azure-kubernetes, azure-      No Kubernetes or AI Runway workload.
   kubernetes-app-deploy, azure-kubernetes-
   automatic-readiness
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   azure-compute, azure-enterprise-infra-planner,    No current VM, enterprise landing-zone or
   azure-cloud-migrate                               cross-cloud migration requirement.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   azure-aigateway, entra-agent-id                   No corresponding AI gateway or agent identity
                                                     system.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   microsoft-foundry, finetuning, deploy-model,      No Foundry-hosted model deployment or training
   capacity, customize, preset                       workflow.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   azure-messaging                                   Pegasus does not use Service Bus or Event
                                                     Hubs.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   python-appservice-deploy                          Pegasus is a .NET application.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   azure-app-onboard, azure-app-onboard-prereq,      Generic onboarding/deployment workflows
   azure-prepare, azure-deploy, azure-validate       overlap with Pegasus’s established release
                                                     procedure. Include the onboarding plugin’s
                                                     nested prepare, scaffold and deploy documents
                                                     in the disabled family.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   All three azure-kusto-graph-skills:* skills       No Kusto graph or IRQL investigation workflow.
                                                     Disable their whole plugin.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   User-local microsoft-foundry and its five         Keep the duplicate Foundry installation
   nested skills                                     disabled.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   System imagegen, plugin-creator, skill-           Keep disabled; no current requirement for
   creator, skill-installer                          generated imagery or extension authoring/
                                                     installing.

  Keep all artifact/template skills disabled with their parent plugins, including documents, pdf,
  spreadsheets, excel-live-control, presentations, template-creator, plugin-management and all 20
  cached artifact-template-* skills.

  ## Plugin, MCP and connector decisions

   Component                                         Target state
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   azure@azure-skills                                Enabled, with the selected skills and MCP tool
                                                     filter below.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   azure-kusto-graph-skills@azure-skills             Disabled, including its duplicate Azure MCP
                                                     contribution.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   browser@openai-bundled, chrome@openai-bundled,    Retain together: local browser testing,
   unified-computer-use@openai-bundled               authenticated Chrome inspection and their
                                                     shared runtime.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   cua_repl, node_repl                               Retain the app-managed definitions and runtime
                                                     settings. Browser manifests reference
                                                     node_repl; do not treat it as removable
                                                     duplication.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   Standalone chrome-devtools MCP                    Disable. Retain the app-managed browser route.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   openaiDeveloperDocs MCP                           Add the official documentation server at
                                                     https://developers.openai.com/mcp. Official
                                                     setup.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   documents, pdf, spreadsheets, presentations,      Keep disabled, as selected by the operator.
   template-creator plugins
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   visualize, codex-app-tools, openai-templates,     Keep disabled or explicitly mark inactive
   plugin-management                                 cached registrations disabled.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   Cached GitHub plugin                              Keep disabled; Git and the installed gh CLI
                                                     cover repository work.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   Cached Box plugin and its old legal-workflow      Keep disabled; Pegasus’s Box integration does
   skill overrides                                   not require a general Codex Box connector.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   pegasus-dev-mcp                                   Explicitly disable the Codex plugin and app ID
                                                     asdk_app_6aa0d4e603d8819184c72150696fc606.
                                                     Preserve its ChatGPT connection and service.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   Other app connectors                              Keep features.apps = false, set the default
                                                     app enablement to false, and preserve existing
                                                     explicit disables.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   Azure plugin hooks                                Preserve their existing disabled state.
  ────────────────────────────────────────────────  ────────────────────────────────────────────────
   Empty Kanmer, vehicle-image-sorter and find-      Leave inactive. Do not reinstall or delete
   skills directories; old cache backups             them as part of this change.

  ### Azure MCP tool filter

  Under plugins."azure@azure-skills".mcp_servers.azure, use enabled_tools to expose these existing
  tool names:

  advisor, applens, applicationinsights, appservice, arm,
  bicepschema, documentation, extension_azqr,
  functionapp, functions, get_azure_bestpractices,
  group_list, group_resource_list, insights, keyvault,
  monitor, optimization, policy, pricing, quota,
  resilience, resourcehealth, role, sql, storage,
  subscription_list, wellarchitectedframework, workbooks

  All other currently exposed Azure tool groups are omitted. In particular, exclude Kubernetes, VMs,
  unrelated databases, Foundry, AI Search, Speech, messaging services and generic deployment tools.

  Retain the Kusto skill for KQL guidance; use Monitor/Application Insights tools for Pegasus’s
  logs. The dedicated Azure Data Explorer tool group is unnecessary.

  These filters control tool exposure; they do not make retained tools read-only or grant cloud-
  write authorization. OpenAI documents separate plugin enablement, bundled-server controls and tool
  allowlists. Configuration reference.

  ## Implementation and update handling

  1. Back up the global C:/Users/Alex/.codex/config.toml and record the current plugin/skill/MCP
     inventory.

  2. Edit the global configuration with the decisions above. Preserve unrelated model, approval,
     sandbox, browser-runtime and agent settings.

  3. Replace obsolete Azure 1.2.40 skill overrides with exact installed 1.2.47 paths. Use full paths
     —not session aliases such as r0.

  4. Use [[skills.config]] with enabled = false for individual skills, plugin enabled = false for
     whole unwanted plugins, and MCP enabled = false for the standalone DevTools server. Do not edit
     plugin cache contents. Skill configuration.

  5. Remove the disabling override for OpenAI Docs and add its documentation MCP registration.
  6. Explicitly disable the ChatGPT-only Pegasus connector in Codex without disconnecting it at
     account level.

  7. Treat plugin upgrades as requiring a skill-path recheck. Exact cache paths are version-
     sensitive; do not claim that these disables automatically follow future versions. Reconcile
     against the retained skill list after upgrades.

  8. Restart Codex and verify in a fresh Pegasus session. OpenAI’s skill guidance requires a restart
     after configuration changes. Restart guidance.

  No application API, schema, package, deployment or repository behavior changes are required.

  ## Acceptance and rollback

  - Codex parses the configuration without errors.
  - A fresh Pegasus session advertises the five project skills and the 13 retained Azure skills;
    unwanted Azure and graph skills are absent.

  - OpenAI Docs is available and successfully searches and retrieves an official page.
  - review-agent remains available through its supported review context.
  - Azure exposes only the approved tool groups; a read-only resource-list operation succeeds.
  - The in-app browser opens a local page and captures its state; Chrome inspection still works.
  - Standalone Chrome DevTools and pegasus-dev-mcp are absent from the fresh session’s tools.
  - Artifact plugins and app connectors remain disabled.
  - A fresh session outside Pegasus confirms the global defaults.
  - No Azure resources, Outlook mailboxes, Box files or ChatGPT connector settings are changed
    during verification.

  If configuration loading or retained browser functionality fails, restore the saved configuration
  and restart Codex. Report the failed check before revising the configuration.
