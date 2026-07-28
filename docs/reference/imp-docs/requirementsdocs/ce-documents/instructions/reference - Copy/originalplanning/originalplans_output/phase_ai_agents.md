# Phase 4 – AI Agents for Workflow Assistance

Beyond individual tools, Collision Engineers can benefit from autonomous agents that orchestrate tasks across services.  These agents operate within defined boundaries and always defer critical decisions to humans.  The following proposals outline possible agents and their roles.

## 4.1 Inbox Triage Agent

**Purpose:** Monitor the Outlook inboxes, classify incoming messages and initiate processing.

* **Email classification** – Determine whether an email is an instruction, a reply with images, an estimate or other correspondence.  Use the document type classifier described in Phase 3.

* **Case association** – Match the email to an existing case based on VRM, claim number or subject line.  If no match exists, create a new pending case in the system.

* **Attachment processing** – Forward attachments to the document parser and image processor.  If attachments are large images, compress and standardise them (e.g., JPEG at 80 % quality) to save storage.

* **Notifications** – When the agent requires human input (e.g., ambiguous VRM), send a notification to the admin dashboard or via chat.  Provide a suggested classification and allow a one‑click confirmation.

## 4.2 Missing Information Agent

**Purpose:** Proactively follow up on cases lacking images, estimates or other critical data.

* **Monitoring** – Scan the case database for entries in the “missing information” state, along with the date of the last chase message (mirroring the “Sent Mino” column【173726810529521†screenshot】).

* **Chase scheduling** – If no response has been received after a certain period (e.g., 24 hours), generate a new chase message using the natural‑language skill (Phase 3) and send it to the appropriate contact via email or WhatsApp.

* **Escalation** – After multiple unanswered chases, flag the case for manual escalation (e.g., phone call) and display it prominently on the dashboard.

* **Feedback loop** – Record responses (or lack thereof) and use them to refine follow‑up timing and wording.

## 4.3 Valuation and Uplift Agent

**Purpose:** Automate the process of obtaining valuations and recommending uplifted values.

* **Trigger** – When a new instruction arrives and valuations are missing, call the valuation agent skill to obtain a baseline value.  Use DVLA data to fill missing fields (make, model, year).  If the provider has requested uplift, compute the recommended uplift.

* **Review** – Present the valuation and evidence to a human reviewer.  Allow them to accept, modify or reject the proposed value.  Once approved, update the case and prepare the companion report for EVA.

## 4.4 Engineer Support Agent

**Purpose:** Assist engineers during inspections and report writing.

* **Pre‑inspection briefing** – Summarise the case details, highlight any discrepancies (e.g., VRM mismatch, missing mileage), show valuation ranges and display adverse history results.  Provide recommended photo order guidelines.

* **On‑site support** – Through a mobile interface, allow engineers to ask questions (e.g., “Where is the ADAS module located on this vehicle?”) and receive quick answers from a knowledge base.  The agent can also log notes via voice dictation.

* **Post‑inspection report drafting** – Convert the engineer’s notes into a structured report that conforms to EVA’s `Submit Report` requirements【859874972426525†L517-L576】.  Insert images in the correct order and include valuations.

## 4.5 Continuous Learning Agent

**Purpose:** Continuously analyse system data to propose process improvements.

* **Pattern detection** – Analyse case data to identify common bottlenecks (e.g., providers that frequently omit images, garages with slow responses).  Recommend targeted training or process changes.

* **Provider performance scores** – Compute metrics such as average time to supply images, error rates in instructions and frequency of valuation uplifts.  Share these scores with management to prioritise outreach.

* **Data quality monitoring** – Detect anomalies (e.g., unusually high mileage for a given model year) and flag them for manual review.  Use MOT datasets and other benchmarks to set thresholds.

By deploying these agents, Collision Engineers can delegate repetitive tasks to software while empowering staff and engineers to focus on higher‑value work.