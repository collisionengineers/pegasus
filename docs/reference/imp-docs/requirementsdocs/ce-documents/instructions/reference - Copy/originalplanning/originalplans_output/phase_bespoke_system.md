# Phase 6 – Bespoke End‑to‑End System Creation

After automating existing workflows and introducing AI tools, the ultimate aim is to build a bespoke platform that seamlessly manages case intake, document storage, valuations and reporting.  This phase outlines high‑level requirements and architectural considerations for such a system.

## 6.1 Objectives

1. **End‑to‑end workflow** – From receiving an instruction to issuing the final engineer report, all steps should be orchestrated by the system, minimising context switching.

2. **Regulatory compliance** – The system must adhere to data protection regulations (GDPR), maintain audit trails for all actions and ensure secure storage and transmission of documents.

3. **Extensibility** – Design the system so that new types of reports (e.g., electric vehicle battery checks, ADAS calibration) or external services (new valuation providers) can be added with minimal friction.

## 6.2 Key Components

### 6.2.1 Communication Layer

* **Inbox module** – Connects to Outlook via Graph API, Slack/WhatsApp via Twilio or Microsoft Teams connectors.  Handles message retrieval, classification and sending chasers.

* **Notification service** – Sends alerts to staff via email, push notifications or chat when tasks require attention (e.g., missing data, valuations ready).  Integrate with existing ticketing systems if present.

### 6.2.2 Parsing and Extraction Layer

* **Document parser** – Centralises PDF, DOCX, email and image parsing.  Utilises a combination of rule‑based extraction (provider presets), AI models (Phase 3) and OCR for scanned documents.

* **Image processor** – Standardises image formats, orders them according to the rules (vehicle overview first, damage close‑up second) and generates thumbnails for preview.  Detects irrelevant images using computer vision.

### 6.2.3 Case Management Core

* **Case database** – Stores all case entities, provider rules and garage contacts as described in Phase 2.  Enables queries by VRM, claim number, provider, status and engineer.

* **Workflow engine** – Implements the state machine governing case progression (e.g., New → Awaiting Images → Ready for EVA → Assigned → In Progress → Completed).  Triggers actions (e.g., send chase, post to EVA) when state changes.

* **User roles and permissions** – Define roles (admin, intake staff, engineer, manager) with appropriate access.  Use single sign‑on (e.g., Azure AD) for authentication.

### 6.2.4 Integration Layer

* **EVA API client** – Encapsulates all interactions with the Sentry API (Instruct Claim, Location Update, Note, Report) and manages token refresh.  Provide asynchronous queues to avoid blocking the user interface.

* **Box API client** – Handles folder creation, file upload, metadata tagging and retrieval.  Provides signed URLs to engineers for viewing documents without granting full Box access.

* **Valuation and DVLA services** – Interfaces to Autotrader connector, Glass’s API and the DVLA/DVSA MCP server.  Caches responses and manages rate limits.

### 6.2.5 Presentation Layer

* **Web portal** – A responsive web application that serves both desktop and mobile users.  Presents dashboards, case lists, case detail views and engineer packs.  Includes a built‑in PDF viewer and image carousel.

* **Mobile application** – A lightweight app (or PWA) for engineers to view case packs, capture photos on site and upload reports.  Supports offline mode with later sync.

## 6.3 Build vs Buy Considerations

* **Low‑code vs custom** – Evaluate low‑code platforms (e.g., Power Apps, Appian) for rapid development.  These can handle form building, workflows and integration connectors.  However, the complexity of provider rules and AI integrations may require custom development.

* **Cost and maintenance** – Custom development provides flexibility but requires long‑term maintenance.  A hybrid approach could use low‑code for the UI and custom microservices for parsing and AI tasks.

* **Staff adoption** – A new system must be intuitive.  Early prototypes should be tested with staff to ensure acceptance.  Provide training and documentation.

## 6.4 Implementation Roadmap

1. **Architecture design** – Define the data model, state machine and integration patterns.  Choose the tech stack (see Phase 2).  Draft API contracts for each service.

2. **Pilot modules** – Start with the inbox module and case database.  Use the existing document mapper as a backend service while building the new parser.  Deploy for internal use and iterate.

3. **Incremental rollout** – Add EVA and Box clients, then migrate valuations and DVLA services.  Roll out the engineer mobile interface last, after the case management core is stable.

4. **Decommission legacy systems** – Once the bespoke system fully replaces the spreadsheet and the document mapper, phase out these tools.  Maintain read‑only access for historical cases.

5. **Continuous improvement** – Incorporate feedback loops, integrate new AI models and monitor system performance.

By investing in a bespoke end‑to‑end system, Collision Engineers can future‑proof their operations, improve turnaround times and provide superior service to clients and insurers.