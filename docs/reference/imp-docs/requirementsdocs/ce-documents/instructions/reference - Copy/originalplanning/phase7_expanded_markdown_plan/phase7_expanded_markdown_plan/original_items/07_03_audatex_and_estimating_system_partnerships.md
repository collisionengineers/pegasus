# 7.3 Partnership with Audatex and Other Estimating Systems


## Purpose

Use approved integration routes with Audatex or other estimating systems to reduce manual estimate upload/parsing and improve estimate-data accuracy.

## Core principle

Do not scrape Audatex screens or recreate proprietary estimating logic. Start by parsing supplied PDFs/exports. Move to official integration only if licensed and commercially justified.

## Step-by-step plan

### Step 1 — Confirm current Audatex use

1. Identify whether Collision Engineers creates Audatex estimates directly, receives Audatex PDFs from others, or both.
2. Identify products involved: AudaEnterpriseGold, Qapter, AudaConnect, PlanManager or PDF-only outputs.
3. Confirm whether current clients permit deeper data integration.
4. Confirm whether official API/licensed access is available.

### Step 2 — Strengthen PDF/export parsing first

1. Build template parsers for common Audatex PDFs.
2. Extract VRM, VIN, estimate ref, repairer, insurer/provider, totals, VAT, labour/paint/parts, supplementary markers and key operations.
3. Add flags for ADAS, alignment, SRS, manual/misc lines and high repair-cost review.
4. Preserve source page/evidence snippets.

### Step 3 — Create estimate schema

1. Define an internal `Estimate` model independent of Audatex-specific fields.
2. Store operations, totals and review flags.
3. Link estimates to source documents and work items.
4. Allow multiple estimates per case, including supplementary estimates.

### Step 4 — Run official integration discovery

1. Ask Audatex/Solera or account contacts about AudaConnect/API access.
2. Document endpoints, authentication, permitted operations and licensing terms.
3. Confirm whether parts/labour data can be retrieved, written or only exchanged via authorised workflows.
4. Confirm sandbox/test access.

### Step 5 — Build approved integration adapter

1. Add an estimating-system adapter boundary.
2. Build read-only import first.
3. Map official data to the same internal `Estimate` model used by PDF parsing.
4. Add idempotency using estimate reference + VRM + work item ID.

### Step 6 — Add estimate review pack

1. Summarise estimate totals and operations.
2. List flags requiring engineer review.
3. Compare instruction and estimate identifiers.
4. Include original estimate link/source evidence.
5. Avoid conclusions about reasonableness unless engineer approved.

## Deliverables

- Audatex usage discovery note.
- Estimate parser `0.1.0-alpha.1`.
- Estimate schema.
- AudaConnect/API discovery decision.
- Approved integration adapter if licensed.
- Estimate review pack section.

## Acceptance criteria

- Common estimate PDFs produce structured totals and flags.
- Multiple/supplementary estimates can coexist on one case.
- Identifier mismatches are flagged.
- No proprietary screen scraping is used.
- Any official integration runs through a licensed/tested route.

## Risks and controls

| Risk | Control |
|---|---|
| Licensing breach | Use supplied PDFs or official API only. |
| Parser misreads totals | Evidence snippets and human review. |
| Supplementary confusion | Track estimate version/type and compare. |
| Over-promising | Position as estimate review support, not Audatex replacement. |

## Suggested priority

P1 for PDF parser and review pack. P3 for deep official integration unless access is already available.
