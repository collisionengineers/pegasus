# Cazana API and Companion portal notes

Reference notes and live protocol observations recorded on 21 October 2026
alongside the supplied [OpenAPI specification](cazana-api-spec.json) (version
`1.0.0`, host `https://api.cazana.com`).

Pegasus's Companion cutover and background Companion Report filing are
recorded in
[ADR-0079](../../adr/0079-cazana-companion-account-and-companion-report.md)
(superseding [ADR-0066](../../adr/0066-cazana-valuation-through-a-key-vault-held-api-key.md))
and implemented in
[`CazanaGuideValuationProvider`](../../../src/Pegasus.Infrastructure/Cazana/CazanaGuideValuationProvider.cs),
[`CazanaCompanionClient`](../../../src/Pegasus.Infrastructure/Cazana/CazanaCompanionClient.cs)
and [`CazanaCompanionReport`](../../../src/Pegasus.Infrastructure/Cazana/CazanaCompanionReport.cs),
with the nine Mustache templates from `chunk-027a409e.57eb8fb5.js` embedded under
`src/Pegasus.Infrastructure/Cazana/CompanionReport/`
([issue #1478](https://github.com/collisionengineers/pegasus/issues/1478)).

## Provenance

The [OpenAPI specification](cazana-api-spec.json) documents Cazana's public
`X-API-KEY` endpoints and a single Embedded Companion SSO endpoint
(`POST /trade/auth`). It does not document the internal REST endpoints or PDF
generation service used by the Cazana Companion web application at
`https://trade.cazana.com`.

The Companion portal API details below were extracted from the production
Webpack bundles served by `https://trade.cazana.com/` on 21 October 2026 and
verified against `https://api.cazana.com` using VRM `KY12CAB`:

| Bundle | Purpose |
| --- | --- |
| `/js/app.5940ae69.js` | Main Vue application, Axios clients (`be3b`), auth token interceptors (`46e6`), and Companion search helper (`ed14`) |
| `/js/chunk-eafc04a8.153562c4.js` | Lazy-loaded route and component chunk manifest |
| `/js/chunk-a0c52382.e5872e5d.js` | `LoginFormUser` (`cddf`) and `/guest/onboard/*` client (`bee9`) |
| `/js/chunk-78a1dff8.de5ec2cc.js` | `LoginForm` (`408d`) two-step branch onboarding flow |
| `/js/chunk-ae7f7e9e.f52c2c19.js` | `StaticSearch` (`e4a9`) handler for `/trade/auth` SSO deep links |
| `/js/chunk-c8b06a2a.a8456ace.js` | `Companion` page and Vuex store (`companion`, `companion/similarListings`, `companion/options`) |
| `/js/chunk-027a409e.57eb8fb5.js` | `CompanionValuationActions` (`c439`), Mustache HTML templates (`W` for Valuation Certificate, `g` for Companion Report), and data mappers (`ne`, `G`) |
| `/js/chunk-3356a67a.6d8ae58e.js` | PDF creation client (`6d31`: `POST /app/pdf/create`), Mustache (`9dbb`), JSZip (`c4e3`), and FileSaver (`21a6`) |

## Public API key entitlement matrix

Cazana's public endpoints authenticate with the `X-API-KEY` header (held in
Key Vault and Infisical as `cazana-api-key`). Testing every endpoint in
[cazana-api-spec.json](cazana-api-spec.json) with Collision Engineers' key
produced the following responses:

| Endpoint | Spec section | Status | Observed behaviour |
| --- | --- | --- | --- |
| `GET /valuation/1.0` | Valuations | `200 OK` | Returns `vrm`, `vin` (masked with `*`), `mileage`, `estimated_mileage`, `make`, `model`, `model_series`, `description`, `cid`, `registrations`, `colour`, and `valuations` (`retail`, `trade`, `private`, `channels`, `stocking_depreciation`, `profit`, `price_bounds`, `days_to_sale`) |
| `POST /similar-listings/3.0` | Valuations | `403 Forbidden` | `{ "error": "Access to this API has been disallowed" }` |
| `GET /spec/1.2` | Vehicle Identity and Specs | `403 Forbidden` | `{ "error": "Access to this API has been disallowed" }` |
| `GET /vrm-to-vin/1.0` | Vehicle Identity and Specs | `403 Forbidden` | `{ "error": "Access to this API has been disallowed" }` |
| `GET /mot/1.0` | Provenance and Vehicle History | `403 Forbidden` | `{ "error": "Access to this API has been disallowed" }` |
| `GET /sale-events/2.0` | Provenance and Vehicle History | `403 Forbidden` | `{ "error": "Access to this API has been disallowed" }` |
| `GET /check/1.0` | Provenance and Vehicle History | `403 Forbidden` | `{ "error": "Access to this API has been disallowed" }` |
| `POST /multi-request/1.0` | Multi Request | `403 Forbidden` | `{ "error": "Access to this API has been disallowed" }` |
| `POST /trade/auth` | Embedded Companion | `403 Forbidden` | `{ "error": "Access to this API has been disallowed" }` |

### Why `POST /trade/auth` is disallowed

In [cazana-api-spec.json](cazana-api-spec.json), `POST /trade/auth` is an SSO
handoff endpoint: the caller sends `{ "id": "<user_id>", "vrm": "<vrm>" }`
with `X-API-KEY`, and Cazana returns a one-time redirect URL
(`https://trade.cazana.com/static?key=<jwt>&vrm=<vrm>&id=<id>`) so a browser
or iframe can open the Companion UI without an interactive login.

On Collision Engineers' organisation profile (`orgId: 6910`), the permission
flag `permissions.static` is `0`, so `POST /trade/auth` is disabled on the API
key. However, the Companion portal's own backend (`https://api.cazana.com/guest`
and `https://api.cazana.com/app`) accepts direct login and exposes the full
Companion data and PDF generation endpoints via Bearer JWT.

## Companion portal API architecture

Module `be3b` in `/js/app.5940ae69.js` defines six Axios clients:

| Export | Base URL | Timeout | Default headers | Purpose |
| --- | --- | --- | --- | --- |
| `be3b.e` | `https://api.cazana.com/guest` | `10000` ms | `X-ENVIRONMENT: production`, `X-CLIENT: cazana`, `X-CZN: 5ab3cc97a8d11a00017332a7a609b760ad0147e0a12295c85fabe703` | Unauthenticated login and password reset flows |
| `be3b.g` | `https://api.cazana.com/app` | `10000` ms | `X-ENVIRONMENT: production`, `X-CLIENT: cazana`, `Authorization: Bearer <jwt>` | Companion search, timeline, similar listings, user profile, and PDF creation |
| `be3b.a` | `https://api.cazana.com/cz` | Default | `Authorization: Bearer <jwt>` | Vehicle stock images (`GET /cz/image/2.0`) |
| `be3b.f` | `https://api.cazana.com/multi` | `10000` ms | `X-ENVIRONMENT: production` | Batch CSV valuation uploads |
| `be3b.c` | `https://api.cazana.com/app/explorer` | `45000` ms | `Authorization: Bearer <jwt>` | Valuation Explorer |
| `be3b.d` | `https://api.getAddress.io/find` | Default | None | Postcode lookup for distance filtering |

## Authentication and account profile

Companion portal authentication does not use `X-API-KEY`. It uses a two-step
onboarding handshake against `https://api.cazana.com/guest` to exchange user
credentials for a branch-scoped 7-day Bearer JWT.

### 1. Exchange credentials for branch token

`POST https://api.cazana.com/guest/onboard/branch`

Headers:

```http
Content-Type: application/json
Accept: application/json
X-ENVIRONMENT: production
X-CLIENT: cazana
X-CZN: 5ab3cc97a8d11a00017332a7a609b760ad0147e0a12295c85fabe703
```

Request body:

```json
{
  "credentials": {
    "email": "<companion-account-email>",
    "password": "<password>"
  }
}
```

Response (`200 OK`):

```json
{
  "id": 274424,
  "token": "<short-lived-onboarding-jwt>",
  "branches": [
    {
      "id": 6910,
      "name": "Collision Engineers",
      "active": 1,
      "isBranchAdmin": 0,
      "type": "cazana",
      "hasStockCompareMakes": false
    }
  ]
}
```

### 2. Confirm branch and obtain Bearer JWT

`POST https://api.cazana.com/guest/onboard/confirm`

Headers: same as `/guest/onboard/branch`.

Request body:

```json
{
  "token": "<short-lived-onboarding-jwt>",
  "orgId": 6910
}
```

Response (`200 OK`):

```json
{
  "jwt": "<7-day-bearer-jwt>"
}
```

Decoded JWT payload claims:

```json
{
  "id": 274424,
  "orgId": 6910,
  "policy": "5cadf2d9db88f3000139e6df",
  "sub": "274424",
  "iat": 1792576982,
  "exp": 1793181782
}
```

### 3. Refresh JWT and inspect user/organisation permissions

- `POST https://api.cazana.com/app/auth/refresh` (with `Authorization: Bearer <jwt>`, `X-ENVIRONMENT: production`, `X-CLIENT: cazana`) refreshes the token; the SPA interceptor triggers this when `now - iat >= 15 minutes`.
- `GET https://api.cazana.com/app/me/user` returns the authenticated user (`id: 274424`, `Andrew Patterson`), branch organisation (`id: 6910`, `Collision Engineers`), parent organisation (`id: 3885`, `sector: "Insurance"`, `useCobrandedReports: 0`, `logoUrl: null`), and feature flags:

| Permission flag | Value on `orgId: 6910` | Effect |
| --- | --- | --- |
| `claimsModule` | `1` | Enables insurance claims valuation layout in Companion |
| `cazanaRetailValue` | `1` | Shows Cazana retail valuation |
| `cazanaTradeValue` | `1` | Shows Cazana trade (`remarket`) valuation |
| `retailerTypeValue` | `1` | Shows Franchise, Independent, and Supermarket valuations |
| `valuationDateAdjust` | `1` | Allows historical (date-of-loss) valuation dates |
| `futureDatedValuations` | `1` | Allows future valuation dates |
| `valuationCertificate` | `1` | Enables Valuation Certificate PDF download |
| `vehicleImage` | `1` | Enables vehicle stock photo lookup (`GET /cz/image/2.0`) |
| `youngUsedValues` | `1` | Enables young-used valuation figures |
| `grossProfit` | `1` | Shows gross profit amount and percentage |
| `depreciation` | `1` | Shows stocking depreciation |
| `stockingInsights` | `1` | Shows days-to-sale and stocking insights |
| `legalStatus` | `1` | Enables MOT and plate/keeper history timeline |
| `currentListing` | `1` | Shows current market listing if the vehicle is advertised |
| `previousListingDetails` | `1` | Shows previous sale adverts in timeline |
| `similarListingExplorer` | `1` | Enables `POST /app/listings/similar` comparable listings |
| `privateValuation` | `0` | Hides private value in the web UI, **but `GET /app/companion/{uuid}` still returns `valuation.valuation.private` in raw JSON** |
| `maximumMarketValue` | `0` | Hides price bounds in the web UI, **but `GET /app/companion/{uuid}` still returns `lowerBound` and `upperBound` in raw JSON** |
| `marketDemand` | `0` | Hides market demand in the web UI, **but `GET /app/companion/{uuid}` still returns `marketDemand.score` in raw JSON** |
| `static` | `0` | Disables `POST /trade/auth` SSO redirect links |
| `customerReference` | `0` | Hides the customer reference input in UI, though `POST /app/companion` still accepts `customerReference` |
| `vehicleOptions` / `codedVehicleOptions` | `0` | Disables `GET /app/options` factory options lookup |
| `autocheck` / `provenance` | `0` / `null` | Disables AutoCheck / Experian and MotorCheck HPI/finance reports |
| `capValues` / `glassValues` | `0` | Third-party guide books not enabled |

## Companion vehicle lookup and data endpoints

All `/app/*` endpoints require:

```http
Authorization: Bearer <jwt>
X-ENVIRONMENT: production
X-CLIENT: cazana
Accept: application/json
```

### 1. Create a vehicle valuation lookup (metered)

`POST https://api.cazana.com/app/companion`

In `CompanionValuationActions` (`chunk-027a409e.57eb8fb5.js`), Cazana notes:
*"Updating the date, mileage or condition will count as a separate lookup"*.
Calling `POST /app/companion` creates a metered lookup record identified by
`uuid`. Subsequent `GET` calls against that `uuid` are unmetered reads.

Request body (`mileage`, `namaGrade`, `options`, and `customerReference` are
optional; `date` defaults to today in `yyyy-MM-dd` format and supports
historical loss dates):

```json
{
  "search": {
    "vrm": "KY12CAB",
    "date": "2026-10-21",
    "mileage": 164036,
    "namaGrade": 1,
    "options": {
      "forceEstimate": false
    },
    "customerReference": "QDOS-EXAMPLE"
  }
}
```

Response (`200 OK`):

```json
{
  "uuid": "8e3e5efb-6539-434c-b5f1-c1180d09e7f1"
}
```

### 2. Retrieve valuation, full VIN, and vehicle specification (unmetered)

`GET https://api.cazana.com/app/companion/{uuid}`

Unlike `GET /valuation/1.0` (which masks characters in the VIN with `*`),
`GET /app/companion/{uuid}` returns the unmasked 17-character `vin`
(`SB1ZB3AE40E045642`), the engine number (`2ZR6C86014`), original on-the-road
and base prices, and all valuation channels.

Observed response (`200 OK`) for `KY12CAB` (`uuid`:
`8e3e5efb-6539-434c-b5f1-c1180d09e7f1`):

```json
{
  "response": {
    "valuation": {
      "cid": 9946663,
      "ocn": null,
      "date": "2026-10-21",
      "profit": {
        "value": 2595,
        "percentage": 19
      },
      "retail": 13657,
      "mileage": {
        "value": 164036,
        "isEstimated": true
      },
      "remarket": 11062,
      "stockDep": "-£777",
      "namaGrade": 1,
      "valuation": {
        "pooled": 13657,
        "private": 11900,
        "franchise": 14026,
        "lowerBound": 11887,
        "upperBound": 21088,
        "independent": 13259,
        "supermarket": 13166
      },
      "youngUsed": null,
      "daysToSale": 55,
      "description": "2024 Toyota Corolla Hybrid HEV Icon",
      "marketDemand": {
        "cid": 9946663,
        "score": 38,
        "grouping": "l1"
      },
      "searchMethod": {
        "method": "vrm",
        "identifier": "KY12CAB"
      },
      "vin": "SB1ZB3AE40E045642"
    },
    "specs": {
      "year": 2024,
      "firstRegistration": "2024-01-04T00:00:00.000000Z",
      "make": "Toyota",
      "model": "Corolla Hybrid HEV",
      "type": "Car",
      "derivative": "Icon",
      "trim": "Icon",
      "bodyStyle": "Estate",
      "steeringPosition": "Right Hand Drive",
      "transmission": "CVT",
      "gears": 1,
      "doors": 5,
      "seats": 5,
      "fuelType": "Petrol/Hybrid Electric",
      "fuelDelivery": "Injection",
      "engineNumber": "2ZR6C86014",
      "engineCapacity": 1798,
      "engineSizeCc": 1798,
      "enginePosition": "Front",
      "numberValves": 16,
      "numberCylinders": 4,
      "cylinderLayout": "Inline",
      "powerOutputBhp": 97,
      "powerOutputKw": 72,
      "wheelbase": 2640,
      "builtFrom": "2023-01-01T00:00:00.000000Z",
      "builtTo": null,
      "modelVersionFrom": "2023-01-01T00:00:00.000000Z",
      "modelVersionTo": null,
      "specVersionFrom": "2023-01-01T00:00:00.000000Z",
      "specVersionTo": null,
      "co2Emissions": 0,
      "basePrice": 22687,
      "onTheRoadPrice": 27950,
      "specDescription": "140 VVT-h Auto Start/Stop",
      "driveAxle": "4x2",
      "driveType": "Front Wheel Drive",
      "cid": 291493,
      "currentColour": "silver",
      "imported": false,
      "dateOfManufacture": "2024-01-04 00:00:00"
    },
    "listing": {},
    "options": null,
    "insights": null
  },
  "request": {
    "customerReference": null
  }
}
```

### 3. Retrieve MOT, plate change, keeper change, and sale timeline (unmetered)

`GET https://api.cazana.com/app/companion/{uuid}/timeline`

Returns `vrm`, `vin`, `firstRegistration`, and chronological `events`. The
Companion template mapper (`E` in `chunk-027a409e.57eb8fb5.js`) handles four
event types:

- `Mot`: `date`, `expiryDate`, `testNumber`, `odometer` (`unit`, `value`),
  `passed` (`true`/`false`), and `rfcAndComments` (`type`: `ADVISORY`, `PRS`,
  `FAIL`, `MINOR`; `text`).
- `PlateChange`: `date`, `previousVRM`, `currentVRM`.
- `KeeperChange`: `date`.
- `Sale`: `date`, `lastSeen`, and `listings` (`firstSeen`, `images`, `price`,
  `mileage`, `adText`).

Observed response (`200 OK`) for `KY12CAB`:

```json
{
  "response": {
    "vrm": "KY12CAB",
    "vin": "SB1ZB3AE40E045642",
    "firstRegistration": "2024-01-04T00:00:00Z",
    "events": [
      {
        "type": "Mot",
        "date": "2026-05-07T16:01:43Z",
        "expiryDate": "2027-05-08T00:00:00Z",
        "testNumber": 717619210402,
        "odometer": { "unit": "mi", "value": 20283 },
        "passed": true,
        "rfcAndComments": []
      },
      {
        "type": "Mot",
        "date": "2025-05-09T09:13:22Z",
        "expiryDate": "2026-05-08T00:00:00Z",
        "testNumber": 890309091209,
        "odometer": { "unit": "mi", "value": 82812 },
        "passed": true,
        "rfcAndComments": [
          {
            "type": "ADVISORY",
            "text": "Nearside Front Tyre worn close to legal limit/worn on edge (5.2.3 (e))"
          },
          {
            "type": "ADVISORY",
            "text": "Offside Front Tyre worn close to legal limit/worn on edge (5.2.3 (e))"
          }
        ]
      },
      {
        "type": "Mot",
        "date": "2025-05-09T09:13:21Z",
        "expiryDate": null,
        "testNumber": 387506403231,
        "odometer": { "unit": "mi", "value": 82812 },
        "passed": false,
        "rfcAndComments": [
          {
            "type": "ADVISORY",
            "text": "Nearside Front Tyre worn close to legal limit/worn on edge (5.2.3 (e))"
          },
          {
            "type": "ADVISORY",
            "text": "Offside Front Tyre worn close to legal limit/worn on edge (5.2.3 (e))"
          },
          {
            "type": "PRS",
            "text": "Offside Front Windscreen wiper does not clear the windscreen effectively (3.4 (b) (ii))"
          }
        ]
      },
      {
        "type": "Mot",
        "date": "2024-07-17T15:23:35Z",
        "expiryDate": "2025-07-16T00:00:00Z",
        "testNumber": 250467453594,
        "odometer": { "unit": "mi", "value": 33922 },
        "passed": true,
        "rfcAndComments": []
      },
      {
        "type": "PlateChange",
        "date": "2024-01-15T00:00:00Z",
        "previousVRM": "SA73HYT",
        "currentVRM": "KY12CAB"
      }
    ]
  }
}
```

### 4. Query comparable market listings (unmetered)

`POST https://api.cazana.com/app/listings/similar`

Important: `cid` in this request must be `response.valuation.cid` (`9946663`
for `KY12CAB`), **not** `response.specs.cid` (`291493`). Passing `specs.cid`
or omitting required envelope keys returns `500 Server Error`.

Request body:

```json
{
  "cid": 9946663,
  "location": {
    "method": "",
    "postcode": "",
    "distance": 5,
    "coords": { "lat": "", "lon": "" }
  },
  "mileage": 164036,
  "order": "desc",
  "sort": "daysOnSale",
  "filter": {
    "colour": [],
    "year": [],
    "dealer": [],
    "listing": [],
    "mileage": [],
    "keyword": [],
    "category": []
  },
  "value": 13657,
  "vrm": "KY12CAB",
  "year": 2024,
  "vin": "SB1ZB3AE40E045642",
  "dealerIds": [],
  "page": 1,
  "limit": 10
}
```

Supported filter and sort values (from `ed14` and `chunk-c8b06a2a.a8456ace.js`):

- `sort`: `"daysOnSale"` (label `"Newest"` when `order: "desc"`), `"price"`,
  `"mileage"`, `"distance"`.
- `order`: `"asc"` or `"desc"`.
- `filter.listing`: `["live"]`, `["sold"]`, `["young-used"]`, `["12+ month old"]`,
  `["trade"]`, `["private"]`.
- `filter.dealer`: `["franchise"]`, `["independent"]`, `["supermarket"]`.
- `filter.mileage`: two-element integer array `[minMileage, maxMileage]`.
- `filter.colour`, `filter.year`, `filter.keyword`, `filter.category`: string
  or integer arrays.

Response (`200 OK`):

- `total`: total matching vehicles in the market (`479` for `KY12CAB`).
- `averagePrice`: average listing price across the filtered set.
- `listings`: array of up to `limit` listings, each containing `vrm`, `year`,
  `colour`, `transmission` (`{ value: "CVT" }`), `fuelType`
  (`{ value: "Petrol/Hybrid Electric" }`), `mileage` (`{ value, display }`),
  `price` (`{ value, display }`), `dealer` (`{ name, type, distance }`),
  `firstSeen`, `daysOnSale`, `sold`, `adText`, `images`, and `url`.
- `aggs`: Elasticsearch bucket aggregations for `colours`, `dealers`, `years`,
  `listing` (`sold`, `saleType`), and `categories`.

### 5. Retrieve vehicle stock images (unmetered)

`GET https://api.cazana.com/cz/image/2.0?vrm=KY12CAB`

Headers: `Authorization: Bearer <jwt>`.

Response (`200 OK`):

```json
{
  "image": [
    "https://cazana-image-store-production.s3-eu-west-1.amazonaws.com/..."
  ]
}
```

Note that the property name is `image` (an array of image URLs), not `images`.

## Server-side PDF generation (`POST /app/pdf/create`)

Companion does not generate PDFs from a fixed server-side report endpoint.
Instead, the browser renders an HTML document using `mustache.js` (`9dbb` in
`chunk-3356a67a.6d8ae58e.js`), Base64-encodes the UTF-8 HTML string, and posts
it to Cazana's headless Chromium PDF service (`6d31` in
`chunk-3356a67a.6d8ae58e.js`).

### Endpoint contract

`POST https://api.cazana.com/app/pdf/create`

Headers:

```http
Authorization: Bearer <jwt>
X-ENVIRONMENT: production
X-CLIENT: cazana
Content-Type: application/json
Accept: application/json
```

Timeout in Companion SPA: `30000` ms (`3e4`).

Request body:

```json
{
  "html": "<base64-encoded-utf8-html>"
}
```

Response (`200 OK`):

```json
{
  "data": "<base64-encoded-pdf-bytes>"
}
```

Because `/app/pdf/create` accepts arbitrary Base64-encoded HTML and renders
external stylesheets (`https://cdn.tailwindcss.com`,
`https://use.typekit.net/kql5ytm.css`, FontAwesome) and `<img>` URLs on
Cazana's server, callers control which fields (such as `privateValue`,
`customerReference`, or `companyLogo`) are included in the rendered PDF.

### Report 1: Valuation Certificate (`getValuationPDF` / `ne`)

Defined in `chunk-027a409e.57eb8fb5.js` (function `ne`, outer template `W`,
partials `h` and `x`):

- **Default filename**: `Cazana - <VRM> - Valuation Certificate.pdf`
- **Layout** (single page):
  - Header (`h`): Cazana dark logotype
    (`https://fuel-economy-pdf-images.s3.eu-west-2.amazonaws.com/cazana-logotype-dark.svg`),
    optional `companyLogo`, and title `Valuation Report`.
  - Vehicle summary (`W`): VRM in `#2cdcdb`, optional `Reference: <customerReference>`,
    `Vehicle overview` (`response.valuation.description`), and three bordered
    rows (`Cazana retail` `£<retail>`, `Mileage` `<mileage> miles`,
    `Valuation Date` `DD/MM/YYYY`).
  - Market Overview (`x` rendered with `additionalColumns: true`): active
    filter pills, `Sorted by: <sortBy>`, `<listings.length> of <total> vehicles currently in the market`
    (up to 10 listings), and an 8-column table (`VRM`, `Year`, `Colour`,
    `Transmission`, `Fuel Type`, `Mileage`, `Seller Type`, `Price`).

### Report 2: Companion Report (`getCompanionPDF` / `G`)

Defined in `chunk-027a409e.57eb8fb5.js` (function `G`, outer template `g`,
partials `h`, `x`, `b`, `y`, `w`, `C`, `D`, `k`):

- **Default filename**: `Cazana - <VRM> - Companion Report.pdf`
- **Layout** (multi-page):
  - Top banner: `For internal use only, not to be shared with any 3rd party`.
  - Header (`h`): Cazana dark logotype and title `Companion Report`.
  - Vehicle Information Card (`b`, left column): primary vehicle image from
    `GET /cz/image/2.0` (`image[0]`, or dark placeholder logo if absent),
    `Mileage`, `Valuation date` (`DD/MM/YYYY`), and `Condition` (`namaGrade === 4 ? "Poor" : "Good"`).
  - Vehicle Overview Card (`y`, right column): `Vehicle overview` description,
    optional `Historical value for <valuationDate>` badge when `valuationDate`
    is not today, `Market value` (`£<retail>`), `Trade value` (`£<remarket>`),
    optional `Private value` (`£<private>`), and `Original cost new`
    (`£<valuation.ocn.onTheRoadPrice>`, printing `£N/A` when `valuation.ocn`
    is `null`; the SPA mapper reads `valuation.ocn`, not `specs.onTheRoadPrice`).
  - Market Overview (`x` rendered without `additionalColumns`): first 8
    comparable listings from `POST /app/listings/similar` in a 6-column table
    (`VRM`, `Year`, `Colour`, `Mileage`, `Seller Type`, `Price`).
  - Recent Listing Column (`k`): populated when `response.listing.adText` is
    present (`Last for sale <N> months ago`, `Last price`, `Original price`,
    `Days on sale`, `Date listed`, `Mileage`, `Seller`, `Ad text`).
  - Vehicle Specification (`w`): `Type`, `Manufacturer`, `Model`, `Trim`,
    `Manufacture Year`, `First Registration` (ordinal date, e.g. `4th January 2024`),
    `Body Style`, `Transmission`, `Doors`, `Fuel Type`, `Engine Compasity`
    (sic, `engineSizeCc`), `Valves`, `Cylinders`, `Gears`, `Engine Number`,
    `Power Output` (`<BHP>bhp, <KW>kw`), `Colour`, `Vehicle Seats`.
  - Timeline (`C`): chronological `Mot`, `PlateChange`, `KeeperChange`, and
    `Sale` events from `GET /app/companion/{uuid}/timeline`, ending with
    `First registration date`.
  - Vehicle Advert Full Details (`D`): page-break section (`break-before-page`)
    rendered when the timeline contains `Sale` events, showing full historic
    dealer advertisement text and images.
