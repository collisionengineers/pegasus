# Vehicle data and EVA export

Status: **Ready V1 handoff plan — EVA API/replacement and AI Assessor V3+**

## Purpose

Preserve the mandatory vehicle-enrichment and manual EVA hand-off outcomes while withholding vendor and export implementations whose contracts are not yet accepted.

## Feature coverage

Primary feature ownership is: `DATA-02`, `CASE-29`, `EXT-18`, `EXT-01`,
`EXT-02`, `EXT-03`, and `CASE-21`. They are four distinct boundaries: one-time
local reference-data preparation, local deterministic inspection-address
resolution, an external DVLA/DVSA lookup, and a local EVA bundle export. No
external adapter or AI suggestion is implied by the deterministic path.

## Authority and current boundary

- **Authority:** [remaining requirements](../../remaining-requirements.md#6-box-vehicle-data-eva-and-email), [ADR-0002](../../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md), and the authoritative [EVA operator note](../../../operator-notes/systems-and-integrations/eva.md).
- **Policy owner:** case/intake policy owns missing-field need, staff confirmation and export readiness. Future Infrastructure adapters translate an accepted vehicle contract or EVA bundle schema.
- **Current implementation/callers:** no DVLA/DVSA or EVA adapter, export serializer, credential, registered caller or accepted mapping exists.
- **Persistence boundary:** SQL remains authoritative for confirmed typed case data/provenance; custody supplies persisted image identities; EVA remains authoritative for assignment, estimating, valuation and reports.

## Prepare reviewed inspection address reference data

**Evidence state:** Planned — local one-time preparation, not a runtime caller

`DATA-02` is an authorised local procedure that transforms supplied
inspection-address/repairer spreadsheets into reviewed, versioned reference
data. It records source provenance, reviewer and review outcome, version, and
reproducible validation. This procedure is evidence-producing authoring work,
not a product importer: it creates no UI/upload/API surface, background job,
sync, spreadsheet adapter, or new runtime store. Runtime policies consume only
the accepted prepared output.

Malformed source rows, duplicate/conflicting identities, incomplete review, or
failed reproducibility leave the candidate version unaccepted and unavailable
to runtime resolution. Replacing a prepared version requires a separately
reviewed output and preserves the prior provenance for audit/recovery; it never
silently changes confirmed case data.

## Resolve inspection address from reviewed reference data

**Evidence state:** Planned — local deterministic caller

`CASE-29` and `EXT-18` are a planned authenticated intake/case Web caller to
one Core inspection-address policy. Given accepted local reference data and
case/intake evidence, it returns exactly a deterministic **match**, an
operator-visible **ambiguity**, or an explicit **no match**. A match may select
an inspection address or the exact `Image Based Assessment` outcome only where
the reviewed reference data says so; ambiguity and no-match never invent an
address, allocate a case/reference, or overwrite staff-confirmed data.

This is not geocoding, prediction, DVLA/DVSA lookup, EVA integration, OCR, or
AI. It makes no network call and has no external adapter. Focused rule and
integration evidence must prove match/ambiguity/no-match, version provenance,
and zero external calls; genuine local case-shape evidence is required before
claiming the intended caller is locally verified.

## Enrich vehicle data from DVLA and DVSA

**Evidence state:** Planned — external contract/credential gated

### Look up vehicle and MOT data

This compatibility route retains the existing delivery-roadmap link. The
external lookup boundary and its execution requirements are defined by
[Enrich vehicle data from DVLA and DVSA](#enrich-vehicle-data-from-dvla-and-dvsa);
it does not create a second caller, policy, or adapter.

`EXT-01` and `EXT-02` require staff-visible DVLA/DVSA lookup when vehicle
details are absent and mileage estimation when MOT evidence supports it. No
adapter implementation or live call is authorised until the current vendor/API
contract, licence/data permission, identifiers, authoritative response fields,
rate/error policy, credential boundary, and exact target are verified and
accepted.

The intended caller is an authenticated, lease/version-guarded staff action to
one Core vehicle policy and one approved Infrastructure adapter. It may return
typed suggestions and explicit no-result/invalid/transient/unknown outcomes,
but cannot silently overwrite case data. Confirmed values retain lookup
provenance and permanent action history. V1 automatic ordinary-image VRM
reading remains a separate intake plan and must not infer OCR/VLM or merge with
V2 image/damage AI. Valuation remains absent.

## Export the V1 EVA bundle

**Evidence state:** Planned — operator-mapping/readiness gated

`EXT-03` and `CASE-21` require operator-approved structured case JSON plus
stored images for manual transfer into EVA for every active QDOS case type.
Direct EVA API use is conditional V3+ on usable vendor capability; EVA
replacement and AI Assessor are V3+. No export implementation is authorised
until an operator accepts the versioned field mapping, image selection,
readiness/release gate, and recovery procedure. The reference schema alone is
not product authority.

The future authenticated download caller must validate the current case version, pre-assignment review gate and custody-confirmed image IDs; generate deterministic JSON, image files and a manifest with hashes; record actor/revision/outcome; and make no EVA network call. The first successful generation records the stable `First sent to Engineer` event once per case and feeds the `Sent to Engineer` today/week dashboard tile. This first-MVP event is explicitly a proxy: it proves successful export generation, not that EVA or an Engineer received it. It must not estimate, value, generate a report or reconcile EVA automatically, and there is no pre-send report review gate.

## Activation and approval

- **Reference preparation/resolution:** reviewed prepared-data version, reproducible validation, and caller-backed match/ambiguity/no-match evidence; no external approval applies because the path has no external call.
- **Vehicle lookup:** accepted current contract/licence, exact target environment and credential/data-call approval, followed by failure mapping and a separately approved live smoke.
- **EVA bundle:** operator-approved mapping/readiness procedure and genuine case-shape acceptance evidence. A download is local application behavior; any later EVA API call requires a separate ADR and approval.

## Evidence and failure boundary

- [ ] Drive the authenticated case-workspace caller through pre-assignment approval and successful deterministic JSON/image generation; observe one `First sent to Engineer` action-history event and one case counted in the London-day/week tile.
- [ ] Retry or regenerate the bundle and prove the stable first event is not duplicated; retain later generation attempts/revisions separately.
- [ ] Force generation failure and prove no `First sent to Engineer` event or dashboard count is recorded and the operator sees a recoverable failure.
- [ ] Record that local generation evidence does not prove EVA import, Engineer assignment/receipt, production deployment or operator acceptance. When EVA is replaced, the adapter must record the actual Engineer-assignment event while preserving the stable business event contract.
- [ ] Prove an accepted local reference version yields only deterministic match, ambiguity, or no-match (including exact `Image Based Assessment`) and causes zero network/AI/EVA/DVLA/DVSA calls.
- [ ] On external lookup or EVA export failure, retain source/case identity and visible recovery state; do not overwrite confirmed data, duplicate `First sent to Engineer`, or allocate/reuse a reference.

## Deferred-capability impact

- **Named capabilities:** direct EVA API/replacement, estimating, valuation, guided capture, AI/vision address suggestions, and external accounts.
- **Stable seam retained:** reviewed reference-data version/provenance, typed staff-confirmed case fields, stable case/reference/document IDs, the once-per-case `First sent to Engineer` event, and a future versioned export contract.
- **Future migration/replacement:** accepted vendor provenance and export-manifest records may require the single existing migration stream; direct EVA integration replaces only the adapter and changes the evidence source from successful export generation to actual Engineer assignment, not the stable business event or case policy. AI address suggestions are later and cannot replace the deterministic reference-data policy.
- **Deliberately absent:** a runtime spreadsheet importer, vendor client/credentials, export endpoint, serializer, EVA HTTP client, valuation/geocoder/AI dependency, background job, or enablement flag.
