# Phase 2 – New Case‑Intake System

The long‑term vision for Collision Engineers is a fully integrated case‑intake and management platform that reduces manual work and provides a clear view of all cases.  The **collisionautomation** repository contains detailed feature specifications and context for a demo system.  This phase adapts those ideas into a production‑grade system tailored for Collision Engineers.

## 2.1 Principles and Scope

* **Single source of truth** – All case data, images, documents and notes should reside in one system with appropriate access controls.  This eliminates the current scattering across Outlook, Excel, network drives and Box.

* **Human‑in‑the‑loop** – Automation should assist staff but not remove their oversight.  The system must clearly highlight where information is missing or ambiguous and allow humans to intervene.

* **Modular architecture** – Build the system as a set of services (inbox ingestion, document parsing, case management, EVA integration, valuation, notifications) that communicate via APIs.  This allows individual components to be replaced without rewriting the whole system.

## 2.2 Core Features

Drawing from the feature specification document【167206420730503†L73-L132】 and the company context【63166266483183†L17-L39】, the new system should provide:

1. **Dashboard / Holding Pen** – A central dashboard that displays the queue of cases awaiting triage.  Flags should indicate missing elements (images, estimate, client ID), duplicate cases and potential issues (e.g., mismatched VRM between instruction and estimate).  The design should mirror the current two‑section spreadsheet: “awaiting information” and “ready for EVA”.

2. **Upload & New Intake** – A drag‑and‑drop interface to upload PDFs, DOCX files, emails and images.  On upload, the system classifies the document type (instruction, estimate, engineer report) and extracts fields using the upgraded parser.  Users can correct fields via a form and confirm before committing the case.

3. **Evidence Matching** – When estimates or images arrive separately, the system should match them to the correct instruction using VRM, claim number and heuristics.  If multiple potential matches exist, show suggestions to the user with confidence scores.  In demo mode, the spec uses deterministic rules【38492142418804†L8-L23】; for production, AI models (string matching, fuzzy search) can be incorporated.

4. **Case Detail Page** – A page showing all extracted fields, documents, images, notes and valuations.  Users can edit fields, upload additional files and trigger re‑parsing.  A timeline of actions (e.g., email received, images matched, valuation obtained) provides auditability.

5. **Missing Information / Chaser** – For cases lacking images or other data, provide buttons to send templated chase emails or WhatsApp messages to garages or providers.  The system should track when chasers were sent and whether responses were received.  This directly automates the manual chase process described in the company context【63166266483183†L17-L39】.

6. **Engineer Pack Generator** – Once a case is complete, generate a concise pack for the engineer containing the instruction, images in the correct order, valuation report, MOT history and a summary of extracted fields.  This can be a PDF or interactive web view.  Engineers can add notes, mark the inspection as complete and upload their report.

7. **Settings & Provider Management** – Provide an administrative interface to manage provider codes, mapping rules (similar to `providers.json`), garage contact details and valuations services.  This centralises configuration that is currently scattered across spreadsheets and JSON files.

## 2.3 Data Model

Implement a data model that reflects the objects described in the feature spec:

* **Case** – Primary entity containing VRM, claim number, principal, vehicle details, dates (instruction, incident, inspection), status, valuation(s), mileage and links to files.
* **Document** – Represents any uploaded file, with metadata (type, provider, upload date).  Associate documents with cases via foreign keys.
* **Image** – Sub‑type of Document with additional fields for order (first two preview images vs rest), orientation and detection flags (e.g., full vehicle vs damage close‑up).
* **Provider** and **Garage** – Entities loaded from the Principals and Garages sheets, including EVA/Box codes, contact methods and image retrieval rules【188662199067677†screenshot】【173726810529521†screenshot】.
* **Valuation** – Records value, salvage value, source (Glass’s, Autotrader connector) and whether an uplift was applied.
* **AuditLog** – Tracks all system actions for compliance.

## 2.4 Technical Architecture

* **Backend** – Consider using Python (e.g., FastAPI or Django) or Node.js for the API layer.  A relational database (PostgreSQL) stores case data, while a message queue (e.g., RabbitMQ) coordinates asynchronous tasks (e.g., parsing, valuations, EVA calls).

* **Frontend** – Use a modern web framework (React or Vue) to build the dashboard and case detail pages.  Integrate with Microsoft Graph to display Outlook messages and send chase emails.

* **File Storage** – Continue using Box as the primary document store.  The system should manage folder creation, file uploads and metadata tagging through the Box API.  Locally cache temporary files only for processing.

* **Integration Services** – Each integration (Outlook, EVA/Sentry API, DVLA/DVSA MCP, valuation providers) runs as a microservice with clear interfaces.  Use token refresh mechanisms where necessary.

## 2.5 Roadmap and Migration

1. **Prototype (MVP)** – Start with a minimal version of the dashboard, upload/intake form and EVA integration.  Migrate a small subset of providers and documents.  Use this to gather feedback from admin staff and engineers.

2. **Incremental Migration** – Gradually onboard remaining providers.  Maintain the legacy spreadsheet and Box workflow during the transition by syncing data between the systems.  Provide export/import scripts to ensure nothing is lost.

3. **Deprecate Spreadsheet** – Once the new system fully replicates the functionality of the job sheet (missing vs ready sections, counters, provider instructions), retire the spreadsheet to read‑only mode.

4. **Expand AI Capabilities** – In later phases, integrate AI modules for classification, extraction and summarisation (see Phase 4).  The system architecture should be designed to plug in these services without major refactoring.

## Expected Benefits

* **Unified case management** – All case information will be centralised, eliminating duplication and reducing the risk of lost data.

* **Improved visibility** – Dashboards will show real‑time status of all cases, outstanding tasks and resource allocation.

* **Scalability** – The modular design enables additional services (e.g., new valuation providers or additional report types) to be added easily.

* **Better user experience** – Staff and engineers will have a single interface rather than juggling Outlook, Excel, the document mapper and EVA.

By adopting this phased approach, Collision Engineers can evolve from a series of manual processes to a cohesive, modern case‑intake platform.