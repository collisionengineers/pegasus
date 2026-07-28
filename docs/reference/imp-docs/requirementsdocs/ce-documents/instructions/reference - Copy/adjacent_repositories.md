# Adjacent Repository Review

## `../cedocumentmapper`

Legacy monolith. It combines Tkinter UI, provider config, document parsing, OCR, MSG handling, and export behavior in one app. Treat it as behavior reference, not code to import wholesale.

## `../cedocumentmapper_v2.0`

Contract-first rewrite scaffold. Useful concepts include domain models, reader protocols, rule protocols, contracts, and tickets. Production app behavior is not implemented.

## `../collisionautomation`

React/Vite prototype with useful UI, matching, schemas, runtime gating, and testing patterns. It is not parser-first.

## `../collisionpdf`

Closest parser-first reference. It has FastAPI parser service modules for native extraction, OCR fallback, classification, matching, field extraction, and schema validation. Its warning still applies: synthetic fixture tests do not prove real-production accuracy.
