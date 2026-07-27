# Phase 3 – AI Tools and Skills

This phase focuses on developing discrete AI‑powered tools and skills that augment the case‑intake process and assist engineers.  These tools can be invoked via the bespoke system or by staff through chat interfaces.  They build on data sources like the DVLA/DVSA MCP server, Autotrader connector and large datasets.

## 3.1 Valuation Agent Skill

The user has already created a proof‑of‑concept **Valuation Agent** that queries the Autotrader connector to estimate vehicle values.  This skill operates in two modes: (1) derive a fair market value by comparing similar vehicles; and (2) attempt to “uplift” the valuation when market values exceed the initial estimate.  To industrialise this skill:

* **Formalise search criteria** – Define a consistent query template based on vehicle registration (VRM), make, model, mileage and year.  Use results from the DVLA service to populate these parameters.  Filter listings by region and condition (e.g., similar mileage range).

* **Statistical modelling** – Instead of returning a single listing’s price, compute the mean and standard deviation of prices for the top matches and return a range.  Identify outliers and exclude them.  When the client requests an uplift, add a configurable percentage (e.g., 5–10%) or reference the 75th percentile of the distribution.

* **Provide evidence** – Return a companion report summarising the listings used for valuation.  Include key fields (vehicle name, year, mileage, price, listing URL) and attach this as part of the case’s valuation record.

* **Integration with EVA** – When populating the `Value` fields in EVA (via `Submit Report`), allow users to choose between the standard and uplifted valuations.  If an uplift is used, add a note explaining why (e.g., “market prices exceed initial estimate by X%”).

## 3.2 DVLA/DVSA Data Utilisation

The MCP server built by the developer allows AIs to query the DVLA vehicle enquiry service and DVSA MOT history.  Potential applications:

* **Mileage estimation** – When instructions lack mileage (a common occurrence), call the MOT history API to retrieve the last recorded mileage.  Use linear interpolation between MOT dates to estimate current mileage based on the time elapsed.  Flag the mileage as estimated and provide the MOT record as evidence.

* **Vehicle specification** – Use the DVLA vehicle enquiry service to retrieve make, model, engine size and fuel type.  Cross‑check the extracted vehicle model from the instruction.  Highlight discrepancies for review.

* **Adverse history and write‑offs** – Use the DVLA “high‑risk” data (if available) or commercial services to check if the vehicle has outstanding finance or previous write‑offs.  Integrate this information into the engineer pack and valuations, ensuring that such flags are considered before recommending an uplift.

## 3.3 Document and Image Classification

While the deterministic rules from the current document mapper cover many cases, AI models can improve accuracy and reduce rule maintenance:

* **Document type classifier** – Train a lightweight model (e.g., logistic regression or a transformer like BERT) to classify whether a file is an instruction, estimate, engineer report, invoice, or other.  Use labelled data from the `docs/Instructions` samples as training data.  The classifier can serve as a first step before applying provider‑specific parsing rules.

* **Field extraction model** – For unstructured or poorly formatted documents, fine‑tune an LLM or use a sequence‑to‑sequence model to extract required fields.  Use the existing JSON outputs as ground truth.  Start with high‑impact fields like VRM, claim number and provider name.

* **Image content analysis** – Use computer vision to verify that the first image is a full vehicle view and the second is a damage close‑up.  A simple CNN or transfer learning on a small dataset could achieve this.  Flag images that do not meet quality standards (e.g., too dark, blurred, irrelevant).  Also detect ADAS equipment (sensors, cameras) to help engineers plan for calibration, as suggested in the demo spec【167206420730503†L73-L132】.

## 3.4 Natural‑Language Skills for Staff and Engineers

Develop conversational skills accessible via chat (e.g., Slack, Teams or an internal portal) to assist staff:

* **“Case summary” skill** – Given a case number or VRM, return a concise summary of the case’s status, missing information, latest actions and deadlines.  Pull data from the new system’s API.

* **“Generate chase email/WhatsApp” skill** – Compose a polite message to a garage or solicitor requesting images or missing documents.  Use the provider’s name, VRM, claim number and previous chase history.

* **“Explain valuation” skill** – When an uplifted valuation is proposed, generate a narrative explaining how the value was derived (e.g., “based on comparable vehicles on Autotrader within 50 mi, the average price is £X, therefore an uplift of Y% is justified”).  Reference the companion report.

* **“AI literacy” training modules** – Provide interactive tutorials for staff to learn about AI capabilities and limitations.  Topics could include data privacy, bias, when to trust AI recommendations and how to override them.  This aligns with the user’s plan to improve AI literacy in the organisation.

## 3.5 Tooling and Deployment

* **Model hosting** – Host models behind an internal API or via the existing MCP server.  Use caching to reduce API calls (e.g., store DVLA responses for 24 hours).  For sensitive models (e.g., valuations), restrict access and log all invocations.

* **Data collection** – Create anonymised datasets of documents and images (with personal data removed) to train and evaluate the models.  Seek consent from clients where necessary.

* **Evaluation and feedback loop** – For each AI skill, track accuracy and user satisfaction.  Provide a feedback button in the UI.  Use corrections (e.g., when a user edits an extracted field) as labelled data for retraining.

By incrementally introducing these AI tools, Collision Engineers can progressively enhance automation and decision support while retaining human oversight.