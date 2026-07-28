# Scratchpad – Provider-Specific Notes

This scratchpad compiles observations about the different **principals** (work providers) and how their instructions are processed.  Much of this information comes from the **Principals** sheet of the job workbook and the `providers.json` file from the document mapper.

## Overview of Providers

* Each work provider has a unique **EVA code** (also called the *principal*) and often an associated **Box code**.  For instance, **Abrahams Solicitors** uses EVA code `ABRAHAMS` and its instructions usually arrive as email attachments【188662199067677†screenshot】.

* Providers may supply instructions in different formats (PDF, Word, etc.) and have preferences for receiving images:
  * **Accident Specialists (ACSP)** – instructions are attached to email; images are often provided by the Accident Specialist group or from the garage via WhatsApp【173726810529521†screenshot】.
  * **AMS, AX, ALISON, etc.** – The `providers.json` file contains extraction rules for each provider.  For example, the **ALISON** preset uses `two_labels` for the VRM field (delimited by “Vehicle Reg:” and “Accident Date:”)【115463040094822†L3-L16】 and manual input for the work provider name “ALISON”.  The **ALS** preset uses `single_label_offset` to find the VRM a fixed number of lines after a marker (“Third Party Vehicle || +2”)【115463040094822†L66-L83】.

* Some providers (labelled with `engineer_report: true` in the presets) supply “Engineer Reports” as companion documents.  The CE Document Mapper has logic to merge an engineer report with the corresponding instruction: only one instruction and one engineer report may be processed together【399092012268091†L87-L103】.

* Providers may require additional steps:
  * **Mileage estimation** – Several provider instructions lack mileage data.  Staff estimate mileage using MOT history or other sources.  This is a candidate for automation using DVSA/DVLA APIs.
  * **Valuation uplift** – Some providers (e.g., valuations via the Autotrader connector) ask to uplift the vehicle value when market prices suggest the original valuation is too low.  This has been partially automated via a ChatGPT Autotrader connector.

## Data Fields Per Provider

From the `providers.json` rules, each provider defines mapping methods for the standard fields used by the mapper.  Methods include:

| Method        | Description                                                                 | Example Provider                               |
|---------------|-----------------------------------------------------------------------------|-----------------------------------------------|
| `manual_input`| Field is not extracted; user must type it in.                              | Work provider names (e.g., “ALISON”, “ALS”)【115463040094822†L3-L16】 |
| `single_label`| Extract the value following a single label.                                 | **AMS** uses “Vehicle Reg:” for VRM【115463040094822†L129-L140】 |
| `two_labels`  | Extract text between two labels.                                            | **ALISON** uses “Vehicle Reg: || Accident Date:” for VRM【115463040094822†L3-L16】 |
| `single_label_offset`| Extract a value a set number of lines after a label.               | **ALS** uses “Third Party Vehicle || +2” for VRM【115463040094822†L66-L83】 |
| `fixed_position` | Use a fixed line number in the document.                                 | **ALS** sets the instruction date at position 1【115463040094822†L95-L100】 |
| `fixed_position_label` | Use a label at a fixed position relative to another label.          | **AMS** uses “5 || Date:” to extract the instruction date【115463040094822†L158-L161】 |

These rules highlight the heterogeneity of incoming documents and underline why a robust parsing mechanism is required.

## Provider‑Specific Image Rules

The Principals sheet also includes columns such as **Drag in to EVA?**, **Sent Mino** and **Images location**【173726810529521†screenshot】.  Notes here indicate whether instructions should be dragged into EVA, whether the provider uses the “Minotaur” (PDF parser) service and where images usually reside (e.g., in the Accident Specialist group, in the solicitor’s network, via WhatsApp, etc.).  Automating image retrieval must respect these rules.

## Provider Codes and Counters

The internal case/po number is formed by appending a numeric counter to the provider code (principal).  The counter resets each year.  Automating this process will require tracking the highest existing counter per provider and generating the next number.  The job sheet’s bottom section shows these case numbers but does not explicitly expose the counter; therefore the automation will need to query EVA or a central database to find the latest reference.

These observations will guide the design of provider‑specific modules in the automation plan.