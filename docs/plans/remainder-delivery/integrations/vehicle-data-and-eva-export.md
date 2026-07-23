# Vehicle data and EVA export

## Purpose

Preserve the mandatory vehicle-enrichment and manual EVA hand-off outcomes while withholding vendor and export implementations whose contracts are not yet accepted.

## Authority and current boundary

- **Authority:** [remaining requirements](../../remaining-requirements.md#6-box-vehicle-data-eva-and-email), [ADR-0002](../../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md), and the read-only [EVA operator note](../../../operator-notes/systems-used/eva.md).
- **Policy owner:** case/intake policy owns missing-field need, staff confirmation and export readiness. Future Infrastructure adapters translate an accepted vehicle contract or EVA bundle schema.
- **Current implementation/callers:** no DVLA/DVSA or EVA adapter, export serializer, credential, registered caller or accepted mapping exists.
- **Persistence boundary:** SQL remains authoritative for confirmed typed case data/provenance; custody supplies persisted image identities; EVA remains authoritative for assignment, estimating, valuation and reports.

## Withheld vehicle lookup

The product requires staff-visible DVLA/DVSA lookup when vehicle details are absent and mileage estimation when MOT evidence supports it. No task is emitted until the current vendor/API contract, licence/data permission, identifiers, authoritative response fields, rate/error policy and credential boundary are verified and accepted.

The future caller must be an authenticated, lease/version-guarded staff action. It may return typed suggestions and explicit no-result/invalid/transient/unknown outcomes, but cannot silently overwrite case data. Confirmed values retain lookup provenance and audit. Automated enrichment, valuation, address prediction and VRM OCR/VLM remain absent.

## Withheld EVA bundle

The first release requires operator-approved structured case JSON plus stored images for manual transfer into EVA; direct EVA API use is deferred. No task is emitted until an operator accepts the versioned field mapping, image selection, readiness/release gate and error/recovery procedure. The reference schema alone is not product authority.

The future authenticated download caller must validate the current case version, review gate and custody-confirmed image IDs; generate deterministic JSON, image files and a manifest with hashes; record actor/revision/outcome; and make no EVA network call. It must not assign an Engineer, estimate, value, generate a report or reconcile EVA automatically.

## Activation and approval

- **Vehicle lookup:** accepted current contract/licence, exact target environment and credential/data-call approval, followed by failure mapping and a separately approved live smoke.
- **EVA bundle:** operator-approved mapping/readiness procedure and genuine case-shape acceptance evidence. A download is local application behavior; any later EVA API call requires a separate ADR and approval.

## Deferred-capability impact

- **Named capabilities:** direct EVA API/replacement, estimating, valuation, mapping/address suggestions, guided capture, AI/vision and external accounts.
- **Stable seam retained:** typed staff-confirmed case fields, provenance, stable case/reference/document IDs and a future versioned export contract.
- **Future migration/replacement:** accepted vendor provenance and export-manifest records may require the single existing migration stream; direct EVA integration replaces only the adapter, not case policy.
- **Deliberately absent:** vendor client, credentials, export endpoint, serializer, EVA HTTP client, valuation/geocoder/AI dependency, background job or enablement flag.
