# Bulk Data Utilisation — Markdown Pack Index

This ZIP contains separate markdown notes for each distinct concept, implementation idea, use case, or governance control identified from the Phase 5 bulk data plan and the wider Collision Engineers project context.

The starting plan already identifies MOT history, DVLA vehicle data, accident statistics, salvage auction prices, weather/traffic data, and governance as relevant. This pack expands those into specific implementable modules and separates each concept into its own file.

## Recommended read order

1. `01_research_sources_and_constraints.md` — factual baseline from public sources and internal context.
2. `02_bulk_data_layer_architecture.md` — common architecture everything else should sit on.
3. `03_mot_bulk_delta_ingestion.md` — the first technical ingestion layer to build.
4. `04_mileage_estimation_engine.md` and `05_mileage_anomaly_review.md` — highest-value MOT-derived use cases.
5. `08_dvla_vrm_attribute_cache.md` and `09_vehicle_identity_normalisation_and_conflict_resolution.md` — vehicle identity and extraction validation.
6. `10_market_valuation_evidence_store.md` and `11_salvage_benchmarking.md` — valuation-supporting data products.
7. `17_eva_report_mining_and_sentry_bulk_sync.md`, `18_engineer_pack_data_enrichment.md`, and `19_document_mapper_learning_loop.md` — integration with current CE tooling.
8. `23_governance_privacy_licensing_controls.md` and `24_implementation_roadmap.md` — controls and staged rollout.

## Files in this pack

- `01_research_sources_and_constraints.md`
- `02_bulk_data_layer_architecture.md`
- `03_mot_bulk_delta_ingestion.md`
- `04_mileage_estimation_engine.md`
- `05_mileage_anomaly_review.md`
- `06_maintenance_condition_score.md`
- `07_mot_failure_pattern_repair_costs.md`
- `08_dvla_vrm_attribute_cache.md`
- `09_vehicle_identity_normalisation_and_conflict_resolution.md`
- `10_market_valuation_evidence_store.md`
- `11_salvage_benchmarking.md`
- `12_prior_total_loss_and_previous_claim_review.md`
- `13_duplicate_evidence_and_reused_image_review.md`
- `14_weather_context_enrichment.md`
- `15_road_safety_open_data_damage_context.md`
- `16_traffic_and_road_network_context.md`
- `17_eva_report_mining_and_sentry_bulk_sync.md`
- `18_engineer_pack_data_enrichment.md`
- `19_document_mapper_learning_loop.md`
- `20_job_sheet_and_operations_analytics.md`
- `21_client_principal_management_intelligence.md`
- `22_data_quality_confidence_and_explainability.md`
- `23_governance_privacy_licensing_controls.md`
- `24_implementation_roadmap.md`
- `25_bulk_data_schema_contracts.md`
- `26_use_case_backlog_prioritisation.md`

## Main recommendation

Treat bulk data as an evidence and decision-support layer, not as an automatic engineering decision-maker. The strongest first products are:

1. MOT bulk/delta ingestion.
2. Mileage estimation and anomaly review.
3. DVLA/DVSA vehicle identity validation.
4. Valuation evidence store using approved commercial sources.
5. EVA/report mining to build CE’s own historic claim intelligence.
6. Case-level data-quality and confidence scoring.

These can be built around the current CE Document Mapper and EVA workflow before a full replacement platform exists.


## Source notes

Internal sources used: `phase_bulk_data.md`, `claudechat.md`, `handover.docx`, `EVA User Guide.pdf`, `Sentry_API_Complete_Guide.md`, `evaapidocs.pdf`, and the two Collision Engineers context packs.

Public/researched sources used across this pack:

- DVSA new MOT history API documentation: https://documentation.history.mot.api.gov.uk/
- DVSA bulk/delta download documentation: https://documentation.history.mot.api.gov.uk/mot-history-api/download-vehicle-mot-history-data/
- DVSA MOT history API authentication: https://documentation.history.mot.api.gov.uk/mot-history-api/authentication/
- DVSA MOT history API rate limits: https://documentation.history.mot.api.gov.uk/mot-history-api/rate-limits/
- GOV.UK MOT history service: https://www.gov.uk/check-mot-history
- DVLA Vehicle Enquiry Service API Guide: https://developer-portal.driver-vehicle-licensing.api.gov.uk/apis/vehicle-enquiry-service/vehicle-enquiry-service-description.html
- DfT Road Safety Open Data: https://www.gov.uk/government/statistical-data-sets/road-safety-open-data
- DfT Road Traffic Statistics data downloads: https://roadtraffic.dft.gov.uk/downloads
- OS Open Roads: https://www.ordnancesurvey.co.uk/products/os-open-roads
- Met Office Weather DataHub: https://datahub.metoffice.gov.uk/
- Met Office observations overview: https://datahub.metoffice.gov.uk/docs/g/category/observations/overview
- ICO anonymisation guidance: https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/data-sharing/anonymisation/
- ICO personal data guidance: https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/personal-information-what-is-it/what-is-personal-data/what-is-personal-data/
- National Archives Open Government Licence `Later`/`unallocated`.0: https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/
- Auto Trader Connect valuations overview: https://help.autotrader.co.uk/hc/en-gb/articles/21923133513117-Introduction-to-Current-Valuations
- Auto Trader Historic Valuations overview: https://help.autotrader.co.uk/hc/en-gb/articles/21945683119389-Introduction-to-Historic-Valuations
- Glass's / Autovista API overview: https://glass.co.uk/product/autovista-api/
