# Figma Inspection: Collision Engineers Whiteboard

- Figma board: https://www.figma.com/board/klGsJGOEG41oP459EbH0ea/Collision-Engineers-Whiteboard?t=1uUOSUp9VtqVuFtN-6
- File key: `klGsJGOEG41oP459EbH0ea`
- Inspection method: Figma `get_figjam` on root node `0:1`
- Inspection status: completed

## Node-Level Workflow Insights

### Admin Intake

- Incoming email is split into instruction, vehicle images, or query.
- Incoming WhatsApp is split into vehicle images or query.
- Email gets an Outlook label based on instruction type and work provider.
- Registration is added to the spreadsheet.
- If both instruction and images are present, the matter goes into Table 2, `Ready to Set Up`.
- If something is missing, the matter goes into Table 1, `Not Ready Yet`.
- For some work providers, Collision Engineers waits for images and repair estimate from the garage.
- For some work providers, staff message the garage on WhatsApp to request images and repair estimate.
- Missing information is chased for up to 4 weeks; after that CE closes the file and informs the work provider.
- On closure, information is backed up to Box.com as a safety net in case the file is reopened later.

### Query Handling

- Non-PCH query emails are moved to the `query` folder.
- PCH query emails get a query label in Outlook.
- Queries can be answered by Ed, Neil, or the engineer who carried out the inspection, depending on query type and job relationship.

### WhatsApp Evidence Handling

- WhatsApp images are downloaded and placed into the relevant folder.
- The registration is added to the spreadsheet.
- Excel creates a folder on the local network and adds a link to it.

### Job Setup

- Staff follow the `EVA Setup Guide` PDF.
- Staff follow SOPs for per-work-provider exceptions.

### Inspection And Delivery

- The engineer completes inspection in the EVA integration of Glass's Repair Estimate, or in standalone Audatex.
- The inspection report is backed up on Box.com.
- The inspection report PDF is sent by Outlook and/or WhatsApp based on the work provider, following the Report Sending SOP.

### Website Form Flow

- A form submission occurs.
- The user is taken to Stripe checkout.
- If checkout succeeds:
  - spreadsheet entry is updated with the Box folder URL;
  - paid status is updated;
  - Box folder is made with `Invoice.pdf`, `Summary.txt` containing form data, and all user attachments;
  - confirmation email is sent to the user with `Invoice.pdf`;
  - new repair estimate request email is sent to CE with all form data and a link to the Box folder.
- Current test recipients are `digital@` and `ed@`.
- Every 5 minutes, a check looks for jobs where payment status is pending, chaser sent is false, and the form submission is at least 15 minutes old. If all three conditions are met, a chaser email is sent and chaser sent changes to true.

## Documentation Implications

- Current intake documentation must distinguish existing repair-estimate portal flow from future portal/API expansion.
- Work item source metadata should support Outlook, WhatsApp, website portal, Box link, payment state, and local-network folder provenance.
- Provider/principal configuration needs delivery channel, query handling, fee-note/report-routing, and SOP exception fields.
- Workflow docs should capture the 4-week missing-information closure rule and Box backup on closure.
- The parser MVP should not own Stripe/payment automation, but the wider CCC roadmap should track payment state as intake/package metadata.
