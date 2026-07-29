***PEGASUS***

**System Plan — Collision Engineers Ltd**

**Prepared for:** Andrew (owner), Alex (developer), and the CE team

**Date:** 26 July 2026    **Status:** Draft for discussion

# **1\. What Pegasus is**

Pegasus is Collision Engineers’ own case management and reporting system. It replaces EVA as the place where jobs are created, assessed, reported and tracked, and it replaces the spreadsheets and manual steps that currently sit around EVA. Box remains the file storage behind it.

The design rests on one idea: capture the job data once, in a structured form, and every output — the assessment report, the fee note, an audit report, a diminution report, an addendum, a query response, an invoice, a management statistic — is simply a different rendering of that same data. Nothing is retyped, so nothing can disagree.

Pegasus is also AI-native. Claude is built into the workflow — reading incoming instructions, drafting assessments from images, suggesting query responses — with an engineer reviewing and approving everything that goes out. The reports themselves are produced by the deterministic generator agreed and locked in July 2026, so every figure is computed once and every report looks identical.

# **2\. Why we are building it**

Collision Engineers currently handles 1,000–1,200 jobs a month. Around that volume, the present workflow has three problems:

* **Manual admin load.** Two to three admin staff spend most of their time logging instructions and images on spreadsheets, chasing the missing half of a job, downloading images from WhatsApp, dragging files into EVA, creating Box folders by hand, and working out the next reference number — with the occasional mistake that manual work invites.

* **End-of-job faff for engineers.** After the expert work is done, the engineer saves out PDFs, files them in Box, hunts through mailboxes for the original instruction email, sends, deletes, and marks the job complete — none of which needs an engineer.

* **No autonomy.** EVA’s vendor is slow to make changes. Ideas that would take days to build in our own system (a contract-repair percentage slider, a one-click diminution report, a Claude assessment button) wait indefinitely or never happen.

Owning the system converts those ideas from feature requests into afternoon jobs, and converts most of the admin role into monitoring exceptions.

# **3\. A job through Pegasus, start to finish**

Instruction arrives by API (larger work providers) or into the single instruction inbox. Pegasus reads it, extracts the details, allocates the next reference number, creates the job and its Box folder, and places it in the holding pen. Images arrive through the upload portal (or occasionally by WhatsApp, dragged in manually) and are paired to the job. The moment both halves are present, Pegasus notifies the team: job complete, ready to go.

Valuations are pulled from CAP, Glass’s and Cazana by API; mileage and any notes for the engineer are added; the job is flagged ready. The engineer opens it, builds the repair specification through one of three routes, makes the expert decisions (value, deductions, outcome, category, salvage, roadworthiness), and generates the report. One click sends it back on the original instruction thread with the right people copied in, files everything to Box, marks the job complete, and logs the management information.

If a query comes back later, it lands on the job’s correspondence thread, Claude drafts a response in house style for the engineer to approve, and any addendum or diminution report generates from the data already held.

# **4\. The modules**

## **4.1 Intake and the pairing engine**

A job needs two pieces: the instruction and the images. Either can arrive first. Every instruction and every image set goes into a holding pen the moment it arrives, and Pegasus pairs them automatically — when the second half turns up, the team is notified that the job is complete. The pen replaces the spreadsheet: nothing can be forgotten because unpaired items are always visible, with age and chase status against each one.

Intake channels are deliberately narrowed to three:

* **API** for the larger work providers — instructions arrive structured, no email reading at all.

* **One single instruction inbox** for everyone else, replacing the current spread of mailboxes. Automation logs each email and reads the instruction (most PDF instructions are already mapped, so client details, accident date and principal auto-populate).

* **An image upload portal** for clients, bodyshops and storage yards — we steer everyone to it with a web link. WhatsApp remains an occasional fallback: someone drags the images into the portal, which is tolerable now and then rather than the routine it is today.

Chasing tools live here too: send an upload link to a client, log a bodyshop chase, see at a glance which jobs are waiting on which half.

## **4.2 Job setup — automated**

What admin does by hand today, Pegasus does on its own: allocate the next reference number (no guessing, no duplicates), create the job, create the Box folder and file the instruction, images and notes into it, populate the job from the mapped instruction, and pull guide values from the CAP, Glass’s and Cazana APIs — potentially pre-fetched from the registration before anyone opens the job. A DVLA lookup on the registration auto-fills make, model, year, engine and fuel. Admin adds mileage and any engineer notes (unroadworthy, customer prefers total loss, and so on) and the job is flagged ready.

The target is that one team member monitors the queue and handles exceptions — unmatched images, unmapped instructions, odd cases — rather than two to three people doing data entry.

## **4.3 The engineer workspace**

The engineer’s screen holds the expert decisions and nothing else. The repair specification can be built through any of three doors:

* **Glass’s** — the traditional integrated route, as in EVA today (an external dependency; see section 6).

* **Audatex** — built in the Audatex software, printed to PDF and dropped onto the job; Pegasus maps the PDF into the standard specification format.

* **The Claude button** — Claude assesses the vehicle from the images already on the job and produces the repair specification for the engineer to review and approve. This workflow already exists and is working well for clear total losses.

The contract repair slider is the model for how owned software pays off: set the cap (say 80% of the pre-accident value), press the Claude button, and the assessment comes back already targeted to that figure — no manual raising and lowering to hit the percentage.

The engineer then completes the decision fields agreed in the report specification: final value (with any deduction, e.g. previous total loss), outcome (total loss / repairable / cash in lieu / contract repair), salvage category and value, roadworthy or not with the reason. Everything else on the report is composed or computed — the engineer never types a settlement figure or a narrative sentence that the system can derive.

## **4.4 The report engine**

Reports are produced by the deterministic generator designed and locked in July 2026: computed-once figures, validation before render, fixed layout in the CE house style, four outcome variants, fee note page included. On top of the core assessment report, the same engine produces:

* **Audit reports.** One job, two repair specifications — the conservative report and the maximised audit report — both rendered from the same job data. Pegasus records the uplift between the two for the management statistics.

* **Diminution in value reports.** All the information is already in the original job. The engineer types a percentage, clicks generate, done.

* **Addendum reports.** Generated from the job data with the amendment applied — never retyped.

* **Fee notes and the itemised repair specification breakdown**, filed to Box automatically alongside the report.

## **4.5 Sending**

Sending happens from inside Pegasus, and because Pegasus ingested the instruction, it already knows which email thread (or API) the report goes back on — no more scouring mailboxes. Each principal has a profile: saved CC lists (with suggested dropdowns at send time), delivery preferences such as “report and images as separate attachments”, and any standing notes. One click sends the report and fee note, files the sent items to Box, marks the job complete, and stamps the management information.

## **4.6 Queries and correspondence**

After the report goes out, the job stays alive. All correspondence on a case — the full email chain and history — is displayed on the job. When a query arrives (a defendant engineer’s challenge, a dispute, a requested adjustment), Claude reads it against the job data and drafts a response in the CE house style, on letterhead, ready to send. The engineer reads it, sends it as-is or amends it first. The cost-defence rebuttal format and house-style rules are already codified from earlier work, so drafts arrive court-ready.

Each query is tagged by type (supplementary request, repair cost challenge, valuation dispute, and so on) — which is what makes the training statistics in the next section possible.

## **4.7 Management information, accounts and access**

The principle is: track everything possible. Because every event happens inside Pegasus, the statistics are a by-product rather than a chore:

* **Per engineer:** reports per day, jobs completed, query rate, query types, audit-report uplift. This is a coaching tool — an engineer attracting supplementaries may need training on hidden damage behind the bumper; one attracting repair-cost challenges may be too heavy-handed.

* **Per principal:** how many reports, of which types, over which period — feeding straight into invoice generation for accounts.

* **Operational:** holding pen ages, time from instruction to images, time from ready to sent, turnaround overall.

Every engineer and admin team member has their own login. Accounts information and management statistics are visible only to the superuser account (Andrew).

## **4.8 Storage — Box**

Box stays as the archive and file store. Pegasus creates the case folder via the Box API and files everything into it automatically — instruction, images, notes, reports, fee notes, breakdowns, sent correspondence. Nobody touches Box by hand; it becomes the audit-proof library behind the system.

# **5\. What already exists**

Pegasus is not a blank sheet. Substantial pieces are already built and proven:

| Asset | What it gives Pegasus |
| :---- | :---- |
| **Report generator (locked July 2026\)** | The entire report engine: deterministic PDF output, computed-once figures, validation, all four outcome variants, fee note page. Includes the full variables walkthrough mapping every field to a dashboard input type — effectively the data-entry screen specification. |
| **Job data schema** | The structured job format (JSON) the generator renders from — the natural core data model for Pegasus. |
| **Claude assessment workflow** | Images in, Audatex-style structured assessment out. Working today for clear total losses; becomes the Claude button. |
| **Cost-defence report skill** | Court-addressed cost justification documents in fixed house style — the backbone of the queries module’s formal responses. |
| **CE house style rules** | Codified tone, wording and banned terms for every outbound letter, email and rebuttal Claude drafts. |
| **CE design system** | Brand tokens, fonts, letterhead and document layout — so Pegasus screens and outputs look right from day one. |

# **6\. External dependencies — chase these early**

These conversations should start now, because their answers shape the build:

| Dependency | What we need to find out |
| :---- | :---- |
| **Glass’s repair estimate** | Can we integrate directly, outside EVA? Licensing, API or embedded access, cost. This is the biggest unknown; if unavailable, engineers still have the Audatex and Claude routes. |
| **CAP / Glass’s / Cazana valuations** | API access and terms in our own system rather than through EVA’s integration. |
| **Work provider APIs** | Which of the larger providers can send instructions by API, and in what format. |
| **Box** | API access for folder creation and filing — straightforward; Box’s API is mature. |
| **Vehicle data** | DVLA lookup for vehicle details; provider API (e.g. Experian AutoCheck) for the mandatory history check. |
| **Audatex PDF format** | Confirm our mapping covers the variants engineers produce, so drag-in import is reliable. |

# **7\. Suggested build order**

The guiding rule: each phase must be useful on its own, alongside EVA, before the next begins. EVA runs in parallel until Pegasus covers the whole flow, then work migrates provider by provider. Alex leads the build; Claude assists with development throughout.

| Phase | Build | Why this order |
| :---- | :---- | :---- |
| **0** | Dependency enquiries; confirm the job data model (already largely done via the report spec). | The Glass’s and valuation-API answers change design decisions; ask before building. |
| **1** | Intake: single inbox automation, image portal, holding pen and pairing engine, notifications, chase tools. | Kills the spreadsheet and the biggest source of forgotten jobs immediately — and it works alongside EVA with zero risk, because paired jobs can still be set up in EVA while the rest is built. |
| **2** | Job setup: auto reference numbers, Box folder creation and filing, instruction mapping, valuation APIs, DVLA lookup. | Removes most of the remaining admin work. Jobs are now born in Pegasus. |
| **3** | Engineer workspace \+ report engine: decision fields, the three repair-spec routes (Claude button first, Audatex import second, Glass’s when resolved), report generation, sending with principal profiles. | The generator already exists, so this phase is mostly screens and wiring. From here, jobs can run end-to-end in Pegasus — start with clear total losses, where the Claude route is proven. |
| **4** | Queries module, addenda, diminution reports, audit reports, contract repair slider. | The high-value extras, each small once the core exists. |
| **5** | MI dashboards, accounts and invoicing, full logins and roles. | Data has been accumulating since Phase 1; this phase surfaces it. Basic logins exist earlier; fine-grained roles and superuser reporting complete here. |

# **8\. Risks and open questions**

* **Glass’s access.** If direct integration is refused or priced out, the Glass’s door closes — mitigated by the Audatex and Claude routes, but worth knowing early.

* **Valuation API terms.** CAP, Glass’s and Cazana may licence differently outside EVA; budget and contracts need confirming.

* **Engineer sign-off on AI work.** Every Claude-produced assessment, response or report must be reviewed and approved by a named engineer before it leaves the building — the reports carry an expert’s signature and statement of truth. The workflow must make approval explicit, logged and attributable.

* **Data protection.** Client personal data and vehicle images flow through email, the portal, Claude and Box; the design should cover UK GDPR basics — retention, access control, processor agreements.

* **Parallel running.** Running EVA and Pegasus together during migration means some double-keying for a period; migrating one work provider or job type at a time keeps it short.

* **Alex’s capacity.** One developer plus Claude is a workable team for this, but the phases should be sized honestly and EVA kept until each phase is genuinely stable.

* **Wording placeholders from the report spec.** Still owed: salvage paragraphs for Categories N, A, B and N/A; the recovery & storage paragraph; final statement of truth wording; qualifications for E Mawdsley and N O’Reilly.

# **9\. Team and volumes**

Volume: 1,000–1,200 jobs per month.

| Person | Role |
| :---- | :---- |
| **Andrew** | Owner and head engineer; Pegasus superuser |
| **Ed** | Senior engineer |
| **Neil** | Engineer |
| **Patrick** | Junior engineer |
| **Jake** | Trainee engineer |
| **Ben** | Admin (senior) |
| **Lisa** | Admin |
| **Fay** | Admin (part time) |
| **Alex** | Developer and automations engineer — Pegasus build lead |

*The intended end state: the admin function becomes one person monitoring an exception queue; engineers spend their time on judgement, not filing; and every idea on this list — and the next list — is ours to build the week we think of it.*