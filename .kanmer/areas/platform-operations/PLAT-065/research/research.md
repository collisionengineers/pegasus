# Research — PLAT-065: activate existing qualified OCR

## Question

What exact minimal infrastructure activates TICK-041's existing Worker OCR
without another runtime, credential path or business-policy owner?

## Findings

Read-only observation: 2026-09-07, approximately 23:04–23:10 UTC. Source inspected:
TICK-041 commit 890f656be, including accepted ADR-0040. EPIC-014's current user
request supersedes old live-provider exclusions; EPIC-011 is preserved as a
historical group. get_sources returned no declarations.

- Exact tenant 858cf5b3-aa0a-47a6-9b40-4851fd0afa94, subscription
  e6076573-23a5-46a8-acef-7e22d264e5db (Enabled), rg-pegasus-prod, uksouth.
  Scoped Resource Graph inventory and direct cognitiveservices account list
  both found no Cognitive Services accounts. Provider is Registered.
- Existing Worker pegasus-prod-worker-252ow37gij uses user-assigned identity
  pegasus-prod-worker-id-252ow37gij, client
  d7d9a0ad-a309-467d-9102-56a002fb0edc, principal
  4f4d9606-3634-4c21-a1ee-3238351cfc69. Its selected app-setting read returned
  Production and this client ID, but no DocumentIntelligence__Endpoint.
  Web's distinct principal is f3b032cc-7591-4ea8-bd68-d165578c576f.
  Existing assignments for both identities contain no Cognitive Services role.
- FormRecognizer list-skus in UKSOUTH returns S0/Standard with no restrictions.
  ARM checkDomainAvailability returned isSubdomainAvailable=true for
  pegasus-prod-ocr-252ow37gij. This name follows platform.bicep's prefix/suffix;
  availability must be rechecked before provision, not treated as a reservation.
- Existing infra/main.bicep delegates the estate to infra/modules/platform.bicep.
  The latter owns Worker identity, scoped assignments and Worker appSettings.
  Add the account and assignment there, then the endpoint only to Worker.
  No new module, identity, Web permission, secret or configuration flag is needed.
- WorkerDependencyInjection already registers AzureDocumentIntelligenceOcr and
  ProcessIntakeOcr when the Production endpoint is configured. The existing
  WorkerAzureClientFactory uses DefaultAzureCredential with every other
  credential source excluded: the configured Worker managed identity is the
  sole usable source. DevelopmentOffline rejects this setting. The adapter
  pins prebuilt-layout / GA API 2024-11-30 and existing credential scope
  https://cognitiveservices.azure.com/.default; no SDK dependency is needed.
- Microsoft documents custom subdomains for Entra authentication and
  Cognitive Services User for Document Intelligence. Live role definition ID
  a97b65f3-24c7-4388-baec-2e87135dc908 agrees. It grants Cognitive Services
  data actions and also listkeys; it is not an analyze-only custom role.
  Resource-only scope plus disableLocalAuth=true is the approved built-in
  approach. Do not grant Contributor, Web access or account-side storage roles:
  the existing Worker reads retained bytes and posts them.
  Sources: [authentication](https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/versioning/sdk-overview-v4-0?view=doc-intel-4.0.0),
  [built-in role](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/ai-machine-learning#cognitive-services-user).
- Official price-page categorization includes Layout within prebuilt models.
  Retail API query: armRegionName eq 'uksouth' and productName eq
  'Azure Document Intelligence' and meterName eq 'S0 Pre-built Pages'.
  Returned consumption S0 meter ddaa022c-eaf7-542d-8766-d86879934f9a,
  unit 1K, tierMinimumUnits 0: USD10.00; GBP7.3624 reference.
  Meter effectiveStartDate 2021-08-01 and isPrimaryMeterRegion=false are
  retained honestly; this is the returned regional meter, not a private quote.
  GBP is reference-only per Microsoft, not a promised invoice conversion.
  Six selected YL69YFO pages imply USD0.06 / approximately GBP0.0442 in OCR
  page charges; monthly cost is selected pages times that unit rate, not a
  guessed volume. No commitment, training, query-fields, add-on or batch SKU.
  Existing £75 estate budget remains unchanged.
  Sources: [model pricing](https://azure.microsoft.com/en-gb/pricing/details/document-intelligence/),
  [retail API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices).
- Live provider metadata advertises accounts ARM GA 2026-07-01; public template
  navigation currently documents GA 2026-05-01. Public 2026-07-01 reference
  fetch was unavailable. Use the current advertised GA with the established
  account fields, and report Bicep schema/compiler incompatibility instead of
  silently switching API/package or adding infrastructure.
- First check-domain attempt failed HTTP400 because az.cmd stripped JSON
  quotes. Repeating the same read-only check with correctly quoted JSON
  returned available. First obsolete product/service price filter returned no
  items; the exact Document Intelligence product/meter query above succeeded.
  No cloud write, provider analysis, token/key retrieval, build or test ran.

## Implications

The resource is FormRecognizer/S0, name and customSubDomainName
pegasus-prod-ocr-252ow37gij, endpoint
https://pegasus-prod-ocr-252ow37gij.cognitiveservices.azure.com/.
Enable public HTTPS consistent with the current estate; no private networking.
Set disableLocalAuth=true. One deterministic role assignment scoped exactly to
this account names only the Worker principal. Worker depends on it and uses the
endpoint. Existing main outputs can expose account ID and endpoint for release
readback; no new input parameter is needed.

TICK-041 owns OCR source, recovery and confidence behavior. TICK-085 owns the
genuine PDF parser/canonical import caller; PLAT-065 must not invent either.
Provision/configuration is not live application acceptance. The YL69YFO
canary requires the integrated canonical caller and retained original source.

## Open questions

No new product or provider decision is needed. Execution remains unclaimed;
root must select the exact reviewed release SHA/manifest and schedule the
existing authorized release route. Recheck names, role authority, current
settings and preview at that point. No claim of current activation is made.
