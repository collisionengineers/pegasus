# Sentry API Documentation

> Normalized transcription of `eva-api-docs.pdf`. No API data has been
> intentionally omitted. Repeated page headers and printed page numbers are
> represented by the source-page headings below.

## Source metadata

| Property | Value |
| --- | --- |
| Title | Sentry API Documentation |
| Author | Minotaur Software Ltd |
| Document version | V1.2 |
| PDF pages | 99 |
| Created | 2026-05-08 09:27:09 +01:00 |
| Modified | 2026-05-08 09:27:09 +01:00 |
| Source SHA-256 | `FB6C66F4DCDC2452EF477F79881FFD675D4C14AA077F681A4351638033E9D7D5` |

## Source page 1

_No extractable text on this page._

![Source page 1 image](eva-api-docs-assets/page-001-image-1.jpeg)

![Source page 1 image](eva-api-docs-assets/page-001-image-2.png)

## Source page 2

### Table of Contents

### Sentry API Documentation - Version 1.0

1. Overview - Purpose and scope of the Sentry API
2. Authentication - How to connect using JSON Web Tokens (JWT)
3. Instruct Claim
4. Claim Location Update
5. Authority Status Update
6. Submit Note
7. Claim Update
8. Submit Report
9. Retrieve Report
    1. Retrievable Report List
    2. Retrieve Report

## Source page 3

### Overview

The Sentry API facilitates the secure and efficient transmission of data related to
vehicles involved in Road Traffic Accidents (RTAs) or other forms of damage that
require engineering assessment, reporting, or authorization.
This API enables authorized external partners - such as insurers, claims management
companies, and repair networks - to exchange claim-related data quickly and reliably.
### Supported Data Types

The Sentry API supports the transmission and management of the following data types:
- Instructions - details and requests relating to the handling of a vehicle claim.
- Notes - internal or external commentary related to a claim's progress or
observations.
- Claim Updates - status changes, progress updates, and lifecycle events.
- Location Updates - geolocation or tracking information relevant to the
vehicle or case.
- Repair Authority Updates - communication of repair authorizations,
amendments, or rejections.
### Purpose

The Sentry API is designed to make the process of sending and receiving vehicle claim-
related data:
- Quick - streamlined data exchange between systems.
- Efficient - minimal manual intervention or duplication.
- Simple to Integrate - clear structure and straightforward implementation for any
external service provider.

## Source page 4

### Authentication

All Sentry API endpoints require valid authentication using a JSON Web Token (JWT).
External users must first obtain a token using their assigned Client ID and Client
Secret credentials.
Tokens are short-lived and must be refreshed periodically.
### Authentication Endpoint

POST /Connect/token
### Request Format

The request must use the application/x-www-form-urlencoded content type and
include the following fields:
```text
 Field        Type  Required Description
 Client_Id    string Yes Yes  Unique identifier assigned to the client.
 Client_Secret string Yes Yes  Secret key assigned to the client for
                           authentication.
```

### Example Request

POST /api/Connect/token
Content-Type: application/x-www-form-urlencoded
Client_Id=partner123&Client_Secret=secretKeyValue

## Source page 5

### Example Responses

Yes Success (200 OK)
```json
{
"access_token": "JWT string",
"expires_in": 5
}
```
- access_token: The JWT string used for authenticating subsequent requests.
- expires_in: Time remaining (in minutes) before the token expires (default: 5
minutes).
No Failure (401 Unauthorized)
```json
{
"error": "unauthorized_client",
"error_description": "Invalid Client ID or Secret"
}
```
### Usage

All subsequent API requests must include the token in the HTTP Authorization header:
Authorization: Bearer {access_token}
Tip: Because tokens expire every 5 minutes, it's recommended to automate token
refreshes using your integration middleware or API client.
### Base API URL

https://sentry.evasoftware.co.uk/api/

## Source page 6

### Instruct Claim

Endpoint: POST /Instruction/Inspection
Purpose: Submit an instruction to the vehicle assessor for a claim. The more complete
the information provided, the more efficiently assessors can begin actioning the claim.
### Request Model

```text
 Field                Type      Required Description
 RequestFrom           string    Yes      Contact code supplied by us with
                     (max 40)           your credentials.
 ExternalRef           string    Yes      External reference for the claim
                                       or instruction. (This is your own
                                       reference)
 Agent                string    No       Agent code, if applicable.
                     (max 10)
 ProvEng              string    No       Provisional engineer code, if
                     (max 10)           applicable.
 VehReg               string    Yes      Vehicle registration number.
                     (max 20)
 PrivateHireLicenceNo   string    No       Licence number for private hire
                     (max 20)           vehicles.
 PrivateHireLicenceAuth string     No       Private hire licence authorization
                     (max 50)           reference.
 DtPlateExpire         DateTime   No       Expiry date of the vehicle plate.
 ClmNo                string    Yes      Claim number.
                     (max 24)
 PolNo                string    No       Policy number.
                     (max 24)
 InsName              string    Yes      Name of the insurer.
                     (max 60)
```

## Source page 7

```text
 TPName               string    No       Name of the third party.
                     (max 60)
 PrincipalName         string    No       Principal party name.
                     (max 24)
 VehDesc              string    No       Vehicle description.
                     (max 40)
 RepairName           string    No       Name of the repairer.
                     (max 40)
 RepairAdd            string    No       Address of the repairer.
                     (max 40)
 RepairTown           string    No       Town of the repairer.
                     (max 250)
 RepairCity           string    No       City of the repairer.
                     (max 250)
 RepairCounty          string    No       County of the repairer.
                     (max 250)
 RepairPCode           string    No       Postcode of the repairer.
                     (max 10)
 RepairTel            string    No       Contact number of the repairer.
                     (max 18)
 RepairEmail           String    No       Repairer email address.
                     (max 255)
 RepairerNetworkCode    string    No       Network code for the repairer.
                     (max 35)
 ApprovedRepairer      bool      No       Whether the repairer is approved.
 EstRec               decimal    No       Estimated recovery cost.
 EstLab               decimal    No       Estimated labour cost.
 EstMat               decimal    No       Estimated materials cost.
 EstPts               decimal    No       Estimated parts cost.
 EstNet               decimal    No       Estimated net cost.
 DtIncident           DateTime   No       Date of the incident.
```

## Source page 8

```text
 InspLocName           string    No       Name of the inspection location.
                     (max 40)
 InspLocAdd           string    No       Address of the inspection
                     (max 40)           location.
 InspLocTown           string    No       Town of inspection location.
                     (max 250)
 InspLocCity           string    No       City of inspection location.
                     (max 250)
 InspLocCounty         string    No       County of inspection location.
                     (max 250)
 InspLocPCode          string    No       Postcode of inspection location.
                     (max 10)
 InspLocTel           string    No       Contact number for inspection
                     (max 18)           location.
 InspLocEmail          string    No       Email of inspection location.
                     (max 250)
 InspLocCont           string    No       Contact person at inspection
                     (max 18)           location.
 InspType             string    Yes      Type of inspection. Accepted
                     (max 25)           values:
  Vehicle Damage
                                            Inspection
  Inspect and Authorise
  Inspect Only
  WOP Inspection
  Rectification work
  Quality/Audit Inspection
  Post Repair
  Low Velocity Inspection
  Desktop
  Other
 VehDriveable          string    No       Whether vehicle is driveable.
                     (max 9)            Accepted values:
  Yes
  No
  Not Known
```

## Source page 9

```text
 InUse                string    Yes      Whether the vehicle is in use.
                     (max 9)            Accepted values:
  Yes
  No
  Not Known
 ClmAdd               string    Yes      Claim address.
                     (max 40)
 ClmTown              string    No       Claim town.
                     (max 250)
 ClmCity              string    No       Claim city.
                     (max 250)
 ClmCounty            string    No       Claim county.
                     (max 250)
 ClmPCode             string    No       Claim postcode.
                     (max 10)
 ClmTelNo             string    No       Claim contact number.
                     (max 18)
 ClmAltTelNo           string    No       Alternative claim contact.
                     (max 30)
 ClmMobileTelNo        string    No       Mobile number of claim contact.
                     (max 18)
 ClmEmail             string    No       Email of claim contact.
                     (max 250)
 CoverType            string    Yes      Type of insurance cover.
                     (max 5)            Accepted values:
  'Comp' - Comprehensive
  'TBA' - TBA
  'TP' - Third Party
  'TPFT' - Third Party, Fire &
                                            Theft
  'WOP' - WOP
 Excess               string    No       Policy excess.
                     (max 10)
 VatStat              string    No       VAT status. Accepted values:
                     (max 3)
  Yes
```

## Source page 10

- No
- n%
```text
 InOrder              string    No       Order reference. Accepted
                     (max 17)           values:
  Yes
  No
 SumInsured           decimal    No       Sum insured value.
 Cause                string    No       Cause of incident.
                     (max 250)
 InstEmail            string    Yes      Email address to send
                     (max 250)          instruction.
 ObviousTotalLoss      string    No       Flag if vehicle is obvious total
                     (max 3)            loss.
 Urgent               bool      No       Whether the instruction is urgent.
 WorkType             string    No       Type of work required.
                     (max 50)
 Roadworthy           string    No       Roadworthy status. Accepted
                     (max 3)            values:
  Yes
  No
 NotesStr             string    No       Additional notes.
 Files                List      No       Files attached to the instruction.
```

### File Model

```text
 Field     Type  Description
 Name      string File name.
 Extension string File extension (e.g., .jpg, .pdf).
 Data      byte[] Base64-encoded file content.
```

## Source page 11

### Example 'Instruct Claim' JSON Request

```json
{
"RequestFrom": "Provided Sender Code",
"ExternalRef": "ACME-CLM-2025-00981",
"Agent": "",
"ProvEng": "",
"VehReg": "AB12CDE",
"PrivateHireLicenceNo": "PHL-00921",
"PrivateHireLicenceAuth": "City of London Licensing",
"DtPlateExpire": "2026-01-31T00:00:00Z",
"ClmNo": "CLM20251022001",
"PolNo": "POL998877",
"InsName": "Acme Insurance Group",
"TPName": "John Doe",
"PrincipalName": "Jane Smith",
"PrincipalClmNo": "JS0019",
"VehDesc": "2020 BMW 320d M Sport",
"RepairName": "Example Repairs",
"RepairAdd": "15 High Street",
"RepairTown": "Watford",
"RepairCity": "London",
"RepairCounty": "Hertfordshire",
"RepairPCode": "WD17 1AA",
"RepairTel": "02*********",
"RepairerNetworkCode": "ELTBOD01",
"ApprovedRepairer": true,
"EstRec": 300.00,
"EstLab": 500.00,
```

## Source page 12

"EstMat": 250.00,
"EstPts": 100.00,
"EstNet": 1150.00,
"DtIncident": "2025-10-15T14:30:00Z",
"InspLocName": "Example Repairers",
"InspLocAdd": "15 High Street",
"InspLocTown": "Watford",
"InspLocCity": "London",
"InspLocCounty": "Hertfordshire",
"InspLocPCode": "WD17 1AA",
"InspLocTel": "02*********",
"InspLocEmail": "example@email.com",
"InspLocCont": "Sarah Mills",
"InspType": "Vehicle Damage Inspection",
"VehDriveable": "Yes",
"InUse": "No",
"ClmAdd": "22 Park Avenue",
"ClmTown": "Watford",
"ClmCity": "London",
"ClmCounty": "Hertfordshire",
"ClmPCode": "WD18 7RT",
"ClmTelNo": "02*********",
"ClmAltTelNo": "02*********",
"ClmMobileTelNo": "07*********",
"ClmEmail": "example@email.com",
"CoverType": "COMP",
"Excess": "250",
"VatStat": "20%",

## Source page 13

"InOrder": "Yes",
"SumInsured": 18000.00,
"Cause": "Rear-end collision with another vehicle at traffic lights.",
"InstEmail": "example@email.com",
"ObviousTotalLoss": "No",
"Urgent": false,
"WorkType": "Accident Damage",
"Roadworthy": "No",
"NotesStr": "Customer requests inspection to be expedited.",
```json
"Files": [
{
"Name": "damage_photo_1.jpg",
"Extension": ".jpg",
"Data": "base64stringofimage1=="
},
{
"Name": "policy_document.pdf",
"Extension": ".pdf",
"Data": "base64stringofpdf=="
}
]
```
}
### Possible Responses

```text
 Status      Description
 Code
 200 Yes     Success - instruction created. Response includes Id for the assessor
            system.
 400 No      Bad Request - invalid data submitted.
```

## Source page 14

```text
 401 No      Unauthorized - missing or invalid JWT token.
 409 No      Conflict - duplicate or conflicting instruction.
 500 No      Internal Server Error - unexpected error processing request.
```

### Response Model

All response types use the same structure as other Sentry API endpoints.
```text
 Field      Type          Description
 StatusCode HttpStatusCode The HTTP status returned.
 Message     string        A description or explanation of the result.
 Id         string        Populated on success; represents the unique Note
                         ID generated in the assessor's system.
```

### Example Standard JSON response

```json
{
"StatusCode": 200,
"Message": "Instruction received successfully.",
"Id": "123"
}
```

## Source page 15

### Claim Location Update

Endpoint: POST /Claim/LocationUpdate
Purpose: Submit an update to the location associated with a vehicle claim. This helps
ensure assessors, repairers, and other stakeholders have accurate and up-to-date
location information for the claim.
### Request Model

```text
 Field           Type    Required     Description
 EVARef          string   Conditionally Reference to a related file (used in
                        Yes          combination with VehReg to identify
                                    the claim).
 VehReg          string   Conditionally Vehicle registration number. Must be
                (max    Yes          included with either FileRef or
                20)                 ClmNo.
 ClmNo           string   Conditionally Claim number. Must be included
                (max    Yes          with VehReg.
                24)
 LocationName     string   No          Name of the location being updated.
                (max                 E.g. Claimant's home, Claimant's
                40)                 work etc
 Address          string   No          Address of the location.
                (max
                40)
 Town            string   No          Town of the location.
                (max
                40)
 City            string   No          City of the location.
                (max
                40)
 County          string   No          County of the location.
                (max
                40)
```

## Source page 16

```text
 Postcode         string   No          Postcode of the location.
                (max
                10)
 Telephone        string   No          Contact number for the location.
                (max
                18)
 Email           string   No          Email address for the location.
                (max
                255)
 ContactName      string   No          Name of the contact person at the
                (max                 location.
                20)
 LocationType     string   Yes          Type of location. Accepted values:
  REPAIRER
  INSPECTION
  INSURED
  THIRDPARTY
 ApprovedRepairer bool     Conditionally Required only if LocationType is
                        Yes          REPAIRER. Indicates if the repairer is
                                    approved.
```

### Validation Logic

To identify the target claim, the API will match either of the following field
combinations:
- ClmNo + VehReg
- EvaRef + VehReg
If the claim cannot be found using these combinations, a 404 response will be returned.

## Source page 17

    Example 'Location Update' JSON Request
```json
{
"VehReg": "AB12CDE",
"ClmNo": "CLM20251022001",
"LocationName": "Example Repairers",
"Address": "15 High Street",
"Town": "Watford",
"City": "London",
"County": "Hertfordshire",
"Postcode": "WD17 1AA",
"Telephone": "02*********",
"Email": "example@email.com",
"ContactName": "Sarah Mills",
"LocationType": "REPAIRER",
"ApprovedRepairer": true
}
```

## Source page 18

### Possible Responses

```text
 Status      Description
 Code
 200 Yes     Success - location updated.
 400 No      Bad Request - invalid data submitted.
 404 No      Claim not found - the claim could not be located using the data
            provided.
 500 No      Internal Server Error - unexpected error processing request.
```

### Response Model

All response types use the same structure as other Sentry API endpoints.
```text
 Field      Type          Description
 StatusCode HttpStatusCode The HTTP status returned.
 Message     string        A description or explanation of the result.
 Id         string        Not used in this response
Tip: Ensure the correct combination of FileRef/ClmNo with VehReg is included;
otherwise, a 404 will be returned. Include ApprovedRepairer only for repairer locations.
```

## Source page 19

### Authority Status Update

Endpoint: POST /Claim/AuthorityStatusUpdate
Purpose: Update the repair authority status on a claim. This informs the assessor
whether the repairer is authorized to begin repairs on the vehicle.
### Request Model

```text
 Field      Type     Required      Description
 VehReg     string   Conditionally Vehicle registration number. Required with
           (max 20) Yes          either ClmNo or FileRef to identify the
                                claim.
 EVARef     string   Conditionally Reference ID for the claim in the external
                   Yes          system.
 ClmNo      string   Conditionally Claim number. Required with VehReg or
           (max 24) Yes          FileRef to identify the claim.
 AuthStatus string    No           Current authority status for the repair.
           (max 50)              Accepted values:
  'Yes' - Repairer is authorized
  'No' - Repairer is not authorized
  'Other' - Alternative status
  'After Instruction' - Authority granted
                                     after instruction submission
 Comment    string   No           Optional comment or notes regarding the
                                authority status update.
 Files      List     No           Optional list of files (documents or images)
                                related to the authority update.
```

### File Model

```text
 Field     Type  Description
 Name      string File name.
 Extension string File extension (e.g., .jpg, .pdf).
 Data      byte[] Base64-encoded file content.
```

## Source page 20

### Validation Logic

To identify the target claim, the API will match either of the following field
combinations:
- ClmNo + VehReg
- EvaRef + VehReg
If the claim cannot be found using these combinations, a 404 response will be returned.
    Example 'Authority Status Update' JSON Request
```json
{
"VehReg": "AB12CDE",
"ClmNo": "CLM20251022001",
"AuthStatus": "Yes",
"Comment": "Repair authorised following assessment review.",
"Files": [
{
"Name": "authority_letter.pdf",
"Extension": ".pdf",
"Data": "base64stringofpdf=="
}
]
}
```

## Source page 21

### Possible Responses

```text
 Status     Description
 Code
 200 Yes    Success - authority status updated. Response includes Id for the
           assessor system.
 400 No     Bad Request - invalid data submitted.
 404 No     Claim not found - the claim could not be located using the data
           provided.
 500 No     Internal Server Error - unexpected error processing request.
Response Model
All response types use the same structure as other Sentry API endpoints.
 Field      Type          Description
 StatusCode HttpStatusCode The HTTP status returned.
 Message     string        A description or explanation of the result.
 Id         string        Not used in this response
Tip: Include either ClmNo or FileRef with VehReg to correctly identify the claim.
Attach relevant supporting files to streamline assessor processing.
```

## Source page 22

### Submit Note

Endpoint: POST /Note/SubmitNote
Description:
This endpoint allows external partners (such as insurers or claims management
companies) to send messages containing important information, general queries, or
supporting details to the assessing company.
It also supports the submission of related files, helping assessors complete their
reports more accurately and efficiently.
### Request Model

```text
 Field   Type  Required      Description
 EvaRef  string Conditionally Used with VehReg or ClmNo to identify the claim
              Yes          within the assessor's system.
 ClmNo   string Conditionally The claim number; required when paired with
              Yes          VehReg if EvaRef is not supplied.
 VehReg string Conditionally   Vehicle registration number; required when used
              Yes          with ClmNo or EvaRef.
 Note    string Yes          The message text or details being submitted.
 Files   List  No           Optional list of file attachments (e.g., images,
                           documents, or reports).
```

### File Model

```text
 Field     Type  Description
 Name      string File name.
 Extension string File extension (e.g., .jpg, .pdf).
 Data      byte[] Base64-encoded file content.
```

## Source page 23

### Validation Logic

To identify the target claim, the API will match either of the following field
combinations:
- ClmNo + VehReg
- EvaRef + VehReg
If the claim cannot be found using these combinations, a 404 response will be returned.
### Example 'Submit Note' JSON Request

```json
{
"ClmNo": "CLM20251022001",
"VehReg": "AB12CDE",
"Note": "Please confirm if additional photos are required before authorisation.",
"Files": [
{
"Name": "damage_closeup.jpg",
"Extension": ".jpg",
"Data": "base64stringofimage=="
}
]
}
```

## Source page 24

### Possible Responses

```text
 HTTP Code           Description
 200 - Success        The note and any attached files were received successfully.
 404 - Not Found      No matching claim could be found for the provided
                    reference details.
 409 - Conflict       A conflict occurred (e.g., duplicate submission or invalid
                    state).
 400 - Bad Request     The request model was invalid or missing required fields.
 500 - Internal Server An unexpected server error occurred while processing the
 Error               note.
```

### Response Model

All response types use the same structure as other Sentry API endpoints.
```text
 Field      Type          Description
 StatusCode HttpStatusCode The HTTP status returned.
 Message     string        A description or explanation of the result.
 Id         string        Not used in this response
```

## Source page 25

### Claim Update

Endpoint: POST /Claim/Update
Description:
This endpoint allows external partners to submit updates to existing claims.
Currently, the endpoint supports the updating of the Excess and Claimant VAT Status
fields, ensuring the assessor has the latest financial information to proceed efficiently
with claim evaluation.
Note: The update model can be extended in the future to include additional
updatable claim fields on request.
### Request Model

```text
 Field      Type     Required      Description
 FileRef    string   Conditionally Internal file reference used by the
                    Yes          assessor. Required with VehReg if ClmNo
                                is not provided.
 VehReg     string   Conditionally Vehicle registration number. Used with
                    Yes          either FileRef or ClmNo to locate the
                                claim.
 ClmNo      string   Conditionally The claim number. Required with VehReg if
                    Yes          FileRef is not supplied.
 Excess     string   No           Updated claim excess value (e.g. "250" or
           (max 10)              "£250").
 ClmVatStat String    No           VAT status. Accepted values:
           (max 3)
  Yes
  No
  n%
```

## Source page 26

### Validation Logic

To locate the target claim, one of the following field combinations must be provided:
- FileRef + VehReg
- ClmNo + VehReg
If a valid claim cannot be found using these combinations, a 404 response will be
returned.
### Example 'Claim Update' JSON Request

```json
{
"VehReg": "AB12CDE",
"ClmNo": "CLM20251022001",
"Excess": "350",
"ClmVatStat": "20%"
}
```

## Source page 27

### Possible Responses

```text
 HTTP Code              Description
 200 - Success          The claim has been successfully updated.
 404 - Not Found        No claim record matched the provided identifying data.
 400 - Bad Request       The submitted data was invalid or incomplete.
 500 - Internal Server Error An error occurred while processing the update request.
```

### Response Model

Same structure used across all Sentry API endpoints.
```text
 Field      Type          Description
 StatusCode HttpStatusCode The HTTP status code returned.
 Message     string        A brief description of the result.
 Id         string        Not used in this response
```

## Source page 28

### Submit Report

Endpoint: POST / Report/SubmitReport
Description:
This endpoint is used by external engineers or assessors to submit a comprehensive
vehicle inspection report to the EVA System.
The report includes all available inspection, vehicle, valuation, and repair data - along
with any supporting documentation or images.
### Request Model

```text
 Field                  Type     Required   Description
 InspectEngineer          string    Yes       The name or code of the
                        (max 12)            inspecting engineer.
 EvaRef                  string    Yes (if    The EVA reference number
                                ClmNo is   identifying the assessment.
                                used with
                                it)
 VehReg                  string    Yes       Vehicle registration.
 ClmNo                  string    Yes       Claim number.
 InsuredName             string    No        Name of the insured party.
 ThirdPartyName           string    No        Name of the third party
                                          involved (if applicable).
 ClaimType               string    No        Type of claim. Accepted
                                          values:
  Cash-In-Lieu
  Diminution
  Other
  Post Repair
  Repair
  T/Loss
  Repudiation
 IncidentDate            DateTime  Yes       Date of the incident.
 InspectionDate           DateTime  No        Date the vehicle was
                                          inspected.
```

## Source page 29

```text
 RepairsAuthorisedDate     DateTime  No        Date repairs were authorised.
 SuppAuthorisedDate       DateTime  No        Date supplementary
                                          authorisation was given.
 EstimateRecievedDate      DateTime  No        Date the repair estimate was
                                          received.
 ReportDate              DateTime  Yes       Date the report was created
                                          or submitted.
 RepairerEstimateAgreed    String    No        Indicates if the repairer's
                                          estimate was agreed.
                                          Accepted values:
  Yes
  No
  N/A
 InspLocName             string    No        Name of the inspection
                        (max 40)            location.
 InspLocAdd              string    No        Address line of the
                        (max 40)            inspection location.
 InspLocTown             string    No        Town of the inspection
                        (max 250)           location.
 InspLocCity             string    No        City of the inspection
                        (max 250)           location.
 InspLocCounty            string    No        County of the inspection
                        (max 250)           location.
 InspLocPCode            string    No        Postcode of the inspection
                        (max 10)            location.
 InspLocTel              string    No        Telephone number of the
                        (max 18)            inspection location.
 InspLocEmail            string    No        Email of the inspection
                        (max 250)           location.
 InspLocCont             string    No        Contact name at inspection
                        (max 18)            location.
 InspectionType           string    Yes       Type of inspection
                        (max 25)            performed. Accepted values:
```

## Source page 30

- Vehicle Damage
    Inspection
- Rectification Work
- Quality/Audit Inspection
- Low Velocity Inspection
- Desktop
- Other
- Cold Call
- Consistency
- Images Only
- Forensic
```text
 ReportType              string    No        Type of report. Accepted
                        (max 27)            values:
  Cold Call Report
  Desktop Report
  Full Report
  Letter
  Post-Inspection
  Post-Repair Audit
  Post-Repair
                                               Complaint
  Roadworthy
  Simple Low Speed
                                               Inspection
  Small Claim
  Telephone
 RepairDuration           string    No        Estimated repair duration in
                        (max 10)            days.
 VehRoadWorthy            string    No        Indicates if the vehicle is
                        (max 10)            roadworthy. Accepted
                                          values:
  Yes
  No
  N/A
  Subject To
 VehNotRoadWorthyReason string       No        Reason vehicle is not
                        (max 250)           roadworthy.
 VehDriveable            string    No        Indicates if the vehicle is
                        (max 9)            drivable. Accepted values:
  Yes
  No
```

## Source page 31

- Not Known
```text
 VehInUse                string    No        Indicates if the vehicle is still
                        (max 9)            in use. Accepted values:
  Yes
  No
  Not Known
 VehAreaOfRepair          string    No        General area of repair.
                        (max 250)
 LightCondAtInsp          string    No        Lighting conditions at
                        (max 250)           inspection.
 InspCondition            string    No        General condition at
                        (max 250)           inspection.
 TempRepairs             string    No        Indicates if temporary repairs
                        (max 3)            were done. Accepted values:
  Yes
  No
  N/A
 TempRepairsHow           string    No        Description of temporary
                        (max 250)           repairs.
 TempRepairsCost          decimal   No        Cost of temporary repairs.
 ValMarket               decimal   No        Market value of the vehicle.
 ValRetail               decimal   No        Retail value.
 ValTrade                decimal   No        Trade-in value.
 ValMid                  decimal   No        Mid-point valuation.
 ValSalvage              decimal   No        Salvage value.
 ValDisposal             decimal   No        Disposal value.
 ValPrivate              decimal   No        Private sale value.
 ValResearch             string    No        Supporting valuation
                                          research text. Accepted
                                          values:
  Internet
```

## Source page 32

- Magazines
- Main Dealer
- Other
```text
 MileageAdjust            decimal   No        Adjustment for mileage.
 ConditionAdjust          decimal   No        Adjustment for condition.
 OtherAdjust             decimal   No        Other valuation adjustments.
 ReportDelayed            bool     No        Indicates if report
                                          submission was delayed.
 ReportDelayedReason      string    No        Reason for delay. Accepted
                        (max 35)            values:
  Awaiting estimate
                                               Owner
  Awaiting estimate
                                               Repairer
  Client requested
                                               inspection date
  Getting parts prices
  Problem contacting
                                               owner
  Vehicle being stripped
  Awaiting images
  Repairer carrying out pre
                                               strip
  Customer unresponsive
  Repairer unresponsive
  Awaiting further info(See
                                               comments)
  Vehicle not available
  Valuation dispute
 PresentAtInsp            string    No        Individuals present during
                        (max 250)           the inspection.
 PrivateHireLicNo         string    No        Private hire licence number.
                        (max 20)
 EngineStarts            string    No        Indicates if engine starts.
                        (max 9)            Accepted values:
  Yes
  No
  Not Known
```

## Source page 33

```text
 EngineFailReason         string    No        Reason for engine failure (if
                        (max 250)           applicable).
 DoorsSecured            string    No        Indicates if vehicle doors are
                        (max 9)            secured. Accepted values:
  Yes
  No
  Not Known
 DoorsNotSecuredReason     string    No        Reason vehicle doors are not
                        (max 250)           secured.
 Diminution              string    No        Indicates if vehicle has a
                        (max 3)            diminution in value.
                                          Accepted values:
  Yes
  No
  N/A
 DiminutionPAVPercent      decimal   No        Diminution percentage of
                                          pre-accident value.
 ClaimantVatLiability      decimal   No        VAT liability for the claimant.
 ClaimantTotLiability      decimal   No        Total liability of the claimant.
 RepairDelays            bool     No        Indicates if there were repair
                                          delays.
 RepairDelaysReason       string    No        Reason for repair delays.
                        (max 50)
 Excess                  string    No        Claim excess amount.
                        (max 10)
 ClaimantVatStatus        string    No        Claimant VAT registration
                        (max 8)            status. Accepted values:
  Yes
  No
  n%
 AuthorityStatus          string    No        Authority status for repair.
                        (max 17)            Accepted values:
  Yes
  No
```

## Source page 34

```text
 Cause                  string    No        Cause of the damage.
                        (max 250)
 SumInsured              decimal   No        Total sum insured.
 Settled                 string    No        Indicates if the claim is
                        (max 21)            settled. Accepted values:
  Yes
  No
  Disputed
  N/A
  Subject To
 DtAttemptedSettle        DateTime  No        Date settlement was
                                          attempted.
 SalLocName              string    No        Name of salvage company
                        (max 40)
 SalLocAdd               string    No        Salvage company first line of
                        (max 40)            address
 SalLocTown              string    No        Salvage company town
                        (max 250)
 SalLocCity              string    No        Salvage company city
                        (max 250)
 SalLocCounty            string    No        Salvage company county
                        (max 250)
 SalLocPCode             string    No        Salvage company postcode
                        (max 10)
 SalLocTelNo             string    No        Salvage company contact
                        (max 18)            number
 SalLocEmail             string    No        Salvage company email
                        (max 250)           address
 ABICat                  string    No        Salvage category. Accepted
                                          values:
  A-Scrap
  B-Breaker
  C-Rep Costs > PAMV
  D-Constructive T/L
  X-Other
  S-Structural
  N-Non-Structural
  No category
                                               breaker/scrap
 OwnerRetainSalvage       Bool     No        Is the owner retaining the
                                          salvage?
 FinalStorage            Decimal   No        Final storage costs
```

## Source page 35

```text
 CurrentStore            Decimal   No        Current storage costs
 SalvageRef              string    No        Salvage reference
                        (max 8)
 DtSalvageMoved           Datetime  No        Date salvage was moved
 StorageRate             Decimal   No        Daily storage rate
 Inherited               String    No        Inherited charges
                        (max 10)
 DtStoreStart            DateTime  No        Date storage of vehicle
                                          started
 SalvageMoved            Bool     No        Was the salvage moved?
 VATOnSalv               String    No        Should vat be applied to
                                          salvage? Accepted values:
  Yes
  No
 VATOnOther              Bool     No        Should vat be applied to
                                          other?
 VATOnPaint              Bool     No        Should vat be applied to
                                          paint?
 VATOnParts              Bool     No        Should vat be applied to
                                          parts?
 Rectification            String    No        Rectification work required
 RectificationIssues      String    No        Rectification issues
 VehImpactAsDescribed      bool     No        Is the impact as described?
 GlassCode               string    No        Glass's guide code
                        (max 6)
 GlassModelId            String    No        Glass's model Id
                        (max 9)
 VehClass                String    No        Vehicle class. Accepted
                        (max 10)            values:
  Agric
  Car
  Caravan
  HGV
  LCV
  M/Cycle
  Misc
  Plant
  PSV
  Trailer
  Tractor
  Machinery
 VehMake                 String    No        Vehicle Make
                        (max 20)
 VehModel                String    No        Vehicle Model
                        (max 20)
```

## Source page 36

```text
 VehDescription           String    No        Vehicle Make & Model
                        (max 40)
 VehEngineCC             String    No        Engine CC
                        (max 8)
 VehEngineBHP            String    No        Engine BHP
                        (max 4)
 VehBody                 String    No        Vehicle body type. Accepted
                        (max 14)            values:
  2 Door
  3 Door
  4 Door
  5 Door
  6 Door
  Agricultural
  Articulated
  Contractors
  P.S.V.
  Private
  Rigid
  Trailer
 VehBodyDescription       String    No        Vehicle Body Description.
                        (max 30)            Accepted values:
  3 Wheel Scooter
  Access Platform
  Ambulance
  ATV
  Backhoe Digger
  Baler
  Beavertail
  Bicycle
  Box Lorry
  Box Trailer
  Box-Van
  Bubble Car
  Bus
  Caged Tipper
  Camper Van
  Car Derived Van
  Caravan
  Catering Unit
  Cement Mixer
  Cherry Picker
  Coach
  Combine
  Compressor
```

## Source page 37

- Convertible
- Coupe
- Crane
- Crew-cab
- Crewcab Tipper
- Crop Fertilizer
- Crop Sprayer
- Curtainsider
- Digger
- Double Decker Bus
- Drophead
- Dropside
- Dumper
- Estate
- Excavator
- Fire Engine
- Flatbed
- Food Truck
- Forager
- Fork-Lift
- Front End Loader
- Fuel Tanker
- Grab Truck
- Gritter
- Hatchback
- Hay Turner
- Hedgetrimmer
- Hook Loader
- Horsebox
- Ice Cream Van
- Kombi
- Light Van
- Limousine
- Livestock Carrier
- Low Loader
- Luton Van
- M.P.V.
- Machinery
- Mini Excavator
- Minibus
- Mobile Home
- Motor Home
- Motorcycle
- Mower
- Muck Spreader

## Source page 38

- Omnibus
- Other
- P.S.V.
- Panel Van
- Pantechnicon
- Parkhome
- Pickup
- Plough
- Quad-Bike
- Rake
- Recovery Truck
- Refrigerated Box Lorry
- Refrigerated Box Van
- Refrigerated Van
- Refuse Collection
    Vehicle
- Road Sweeper
- Rotavator
- Saloon
- Scooter
- Self Propelled Sprayer
- Silage Trailer
- Skelatal
- Skid Steer
- Skip Loader
- Static
- Station Wagon
- Tandem Trailer
- Tanker
- Tarmac Hot Box
- Taxi
- Tedder
- Telehandler
- Tipper
- Toilet Block
- Touring
- Tractor
- Tractor Unit
- Trailer
- Tri Axle Trailer
- Trike
- Ute
- Vacuum Tanker
- Van
- Vehicle Transporter

## Source page 39

- Wagon
- Welfare Unit
```text
 VehFirstRegistered       String    No        Month & Year vehicle was
                        (max 10)            first registered. Format:
                                          MMM/yyyy
 VehOdometer             String    No        Odometer reading
                        (max 10)
 VehOdometerUnit          string    No        Odometer reading units.
                                          Accepted values:
  Hrs
  Km
  Miles
 VehColour               String    No        Colour of vehicle
                        (max 40)
 VehCondition            String    No        Condition of vehicle
                        (max 15)
 VehBrakes               String    No        Condition of brakes on
                        (max 250)           vehicle
 VehSteering             String    No        Condition of steering on
                        (max 250)           vehicle
 VehInCarEntertainment     String    No        Any in car entertainment in
                        (max 40)            the vehicle
 VehExtras               String    No        Any extras on the vehicle
                        (max 40)
 VehMods                 String    No        Any additional mods of the
                        (max 40)            vehicle
 VehTaxExpire            String    No        Tax expiration date. Format:
                        (max 10)            MMM/yyyy
 VehVin                  String    No        Vehicle Identification
                        (max 22)            Number (VIN)
 VehRhfTyre              String    No        Tyre tread depths on right-
                        (max 11)            hand front tyre
 VehLhfTyre              String    No        Tyre tread depths on left-
                        (max 11)            hand front tyre
 VehRhrTyre              String    No        Tyre tread depths on right-
                        (max 11)            hand rear tyre
 VehLhrTyre              String    No        Tyre tread depths on left-
                        (max 11)            hand rear tyre
 VehSpareTyre            String    No        Tyre tread depths on spare
                        (max 11)            tyre
 VehRhfSBelt             String    No        Status of right-hand front
                        (max 12)            seat belt. Accepted values:
  Damaged
  Deployed
  Fitted
```

## Source page 40

- No Access
- None Fitted
- Not Tested
```text
 VehLhfSBelt             String    No        Status of left-hand front seat
                        (max 12)            belt. Accepted values:
  Damaged
  Deployed
  Fitted
  No Access
  None Fitted
  Not Tested
 VehRhrSBelt             String    No        Status of right-hand rear seat
                        (max 12)            belt. Accepted values:
  Damaged
  Deployed
  Fitted
  No Access
  None Fitted
  Not Tested
 VehLhrSBelt             String    No        Status of left-hand rear seat
                        (max 12)            belt. Accepted values:
  Damaged
  Deployed
  Fitted
  No Access
  None Fitted
  Not Tested
 VehCenSBelt             String    No        Status of center seat belt.
                        (max 12)            Accepted values:
  Damaged
  Deployed
  Fitted
  No Access
  None Fitted
  Not Tested
 VehAirBagDeployed        String    No        Did the airbags deploy?
                        (max 25)
 VehTransmission          String    No        Vehicle transmission.
                        (max 20)            Accepted values:
```

## Source page 41

- Automatic
- CVT
- DSG
- EGS
- Manual
- N/A
- Semi-Auto
- Sequential
- Sequential Automatic
- Sequential Manual
- Unknown
```text
 VehFuelType             String    No        Vehicle fuel type. Accepted
                        (max 20)            values:
  Biofuel
  Compressed Natural
                                               Gas
  Diesel
  Dual
  Electric
  Hybrid
  Hydrogen
  LPG
  Petrol
 DamageSeverity           String    No        First damage severity.
                        (max 18)            Accepted Values:
  Very Heavy
  Heavy
  Moderate to Heavy
  Moderate
  Light to Moderate
  Light
  Very Light
 DamageType              String    No        First damage type.
                        (max 22)            Accepted values:
  Accidental Damage
  Chemical
                                               Contamination
  Collision/Impact
  Electrical Failure
  Fire (Cause Unknown)
  Fire (Electrical)
  Fire (External)
  Fire (Fuel)
  Ground Damage
  Mechanical Failure
```

## Source page 42

- No Damage
- Rodent Damage
- Storm Damage
- Theft or Attempted
- Unrecovered Theft
- Vandalism
- Water/Flood/Damp
```text
 DamageArea              String    No        First damage area. Accepted
                        (max 8)            values:
  Front
  LH Front
  LH Rear
  LH Side
  Rear
  RH Front
  RH Rear
  RH Side
 DamageLocation           String    No        First damage location.
                        (max 20)            Accepted values:
  Bonnet
  Boot
  Brake Lever
  Bumper
  Complete Vehicle
  Door
  Engine Bay
  Fairing
  Foot Peg
  Frame
  Front Fork
  Front Panel
  Front to Rear
  Gear Lever
  Handlebars
  Interior
  Luggage
                                               Compartment
  Mirror
  Mud Guard
  Panniers
  Quarter Panel
  Rear Panel
  Rear to Front
  Roof
  Swing Arm
```

## Source page 43

- Tyre
- Underside
- Wheel
- Wing
```text
 DamageSeverity2          String    No        Second damage severity.
                        (max 18)            Accepted Values:
  Very Heavy
  Heavy
  Moderate to Heavy
  Moderate
  Light to Moderate
  Light
  Very Light
 DamageType2             String    No        Second damage type.
                        (max 22)            Accepted values:
  Accidental Damage
  Chemical
                                               Contamination
  Collision/Impact
  Electrical Failure
  Fire (Cause Unknown)
  Fire (Electrical)
  Fire (External)
  Fire (Fuel)
  Ground Damage
  Mechanical Failure
  No Damage
  Rodent Damage
  Storm Damage
  Theft or Attempted
  Unrecovered Theft
  Vandalism
  Water/Flood/Damp
 DamageArea2             String    No        Second damage area.
                        (max 8)            Accepted values:
  Front
  LH Front
  LH Rear
  LH Side
```

## Source page 44

- Rear
- RH Front
- RH Rear
- RH Side
```text
 DamageLocation2          String    No        Second damage location.
                        (max 20)            Accepted values:
  Bonnet
  Boot
  Brake Lever
  Bumper
  Complete Vehicle
  Door
  Engine Bay
  Fairing
  Foot Peg
  Frame
  Front Fork
  Front Panel
  Front to Rear
  Gear Lever
  Handlebars
  Interior
  Luggage
                                               Compartment
  Mirror
  Mud Guard
  Panniers
  Quarter Panel
  Rear Panel
  Rear to Front
  Roof
  Swing Arm
  Tyre
  Underside
  Wheel
  Wing
 DamageSeverity3          String    No        Third damage severity.
                        (max 18)            Accepted Values:
  Very Heavy
```

## Source page 45

- Heavy
- Moderate to Heavy
- Moderate
- Light to Moderate
- Light
- Very Light
```text
 DamageType3             String    No        Third damage type.
                        (max 22)            Accepted values:
  Accidental Damage
  Chemical
                                               Contamination
  Collision/Impact
  Electrical Failure
  Fire (Cause Unknown)
  Fire (Electrical)
  Fire (External)
  Fire (Fuel)
  Ground Damage
  Mechanical Failure
  No Damage
  Rodent Damage
  Storm Damage
  Theft or Attempted
  Unrecovered Theft
  Vandalism
  Water/Flood/Damp
 DamageArea3             String    No        Third damage area. Accepted
                        (max 8)            values:
  Front
  LH Front
  LH Rear
  LH Side
  Rear
  RH Front
  RH Rear
  RH Side
 DamageLocation3          String    No        Third damage location.
                        (max 20)            Accepted values:
```

## Source page 46

- Bonnet
- Boot
- Brake Lever
- Bumper
- Complete Vehicle
- Door
- Engine Bay
- Fairing
- Foot Peg
- Frame
- Front Fork
- Front Panel
- Front to Rear
- Gear Lever
- Handlebars
- Interior
- Luggage
    Compartment
- Mirror
- Mud Guard
- Panniers
- Quarter Panel
- Rear Panel
- Rear to Front
- Roof
- Swing Arm
- Tyre
- Underside
- Wheel
- Wing
```text
 RepairName              String    No        Repairer name
                        (max 40)
 RepairAdd               String    No        Repairer address line 1
                        (max 40)
 RepairTown              String    No        Repairer town
                        (max 250)
 RepairCity              String    No        Repairer city
                        (max 250)
 RepairCounty            String    No        Repairer county
                        (max 250)
```

## Source page 47

```text
 RepairPCode             String    No        Repairer postcode
                        (max 10)
 RepairTel               String    No        Repairer contact number
                        (max 18)
 RepairEmail             String    No        Repairer email
                        (max 250)
 RepairCont              String    No        Repairer contact name
                        (max 20)
 RepairerVatStatus        String    No        Repairer Vat Status.
                        (max 3)            Accepted values:
  Yes
  No
  N%
 EstimatedRecovery        Decimal   No        Estimated recovery cost
 EstimatedLabour          Decimal   No        Estimated labour cost
 EstimatedMaterials       Decimal   No        Estimated materials cost
 EstimatedSundries        Decimal   No        Estimated sundries cost
 EstimatedEpa            Decimal   No        Estimated epa cost
 EstimatedParts           Decimal   No        Estimated parts cost
 EstimatedOtherTotal      Decimal   No        Estimated other cost
 EstimatedNet            Decimal   No        Estimated net cost
 EstimatedVat            Decimal   No        Estimated vat cost
 EstimatedGross           Decimal   No        Estimated gross cost
 EstimatedPartsDiscount    Decimal   No        Estimated parts discount
 EstimatedLabourRate      Decimal   No        Estimated labour rate
 LabourRate              Decimal   No        Assessed labour rate
 RecoveryDiscountPercent   Decimal   No        Assessed recovery discount
                                          percentage
 LabourDiscountPercent     Decimal   No        Assessed labour discount
                                          percentage
 MaterialDiscountPercent   Decimal   No        Assessed material discount
                                          percentage
 SundryDiscountPercent     Decimal   No        Assessed sundry discount
                                          percentage
 EPADiscountPercent       Decimal   No        Assessed epa discount
                                          percentage
 PartsDiscountPercent      Decimal   No        Assessed parts discount
                                          percentage
 RecoverySavingsNet       Decimal   No        Recovery savings net
                                          between estimated and
                                          assessed recovery charge
 RecoverySavingsGross      Decimal   No        Recovery savings gross
                                          between estimated and
                                          assessed recovery charge
```

## Source page 48

```text
 LabourSavingsNet         Decimal   No        Labour savings net between
                                          estimated and assessed
                                          labour charge
 LabourSavingsGross       Decimal   No        Labour savings gross
                                          between estimated and
                                          assessed labour charge
 MaterialSavingsNet       Decimal   No        Materials savings net
                                          between estimated and
                                          assessed materials charge
 MaterialSavingsGross      Decimal   No        Materials savings gross
                                          between estimated and
                                          assessed materials charge
 PartsSavingsNet          Decimal   No        Parts savings net between
                                          estimated and assessed
                                          parts charge
 PartsSavingsGross        Decimal   No        Parts savings gross between
                                          estimated and assessed
                                          parts charge
 OtherSavingsNet          Decimal   No        Other savings net between
                                          estimated and assessed
                                          other charge
 OtherSavingsGross        Decimal   No        Other savings gross between
                                          estimated and assessed
                                          other charge
 DeleteSavingsNet         Decimal   No        Deleted estimate items
                                          savings net between
                                          estimated and assessed
 DeleteSavingsGross       Decimal   No        Deleted estimate items
                                          savings gross between
                                          estimated and assessed
 TotalSavingsNet          Decimal   No        Total savings net between
                                          estimated and assessed
 TotalSavingsGross        Decimal   No        Total savings gross between
                                          estimated and assessed
 VatSavings              Decimal   No        Vat savings between
                                          estimated and assessed
 PrincipalSavings         Decimal   No        Savings for principal between
                                          estimated assessed
 BettermentRetail         Decimal   No        Betterment retail figure
 BettermentDiscount       Decimal   No        Betterment discount figure
 BettermentNet            Decimal   No        Betterment net figure
 BettermentVat            Decimal   No        Betterment vat figure
 BettermentGross          Decimal   No        Betterment gross figure
 TotalExcess             Decimal   No        Total excess
 ContractRep             Decimal   No        Contract Repair charge
 Reserve                 Decimal   No        Reserve figure
```

## Source page 49

```text
 Balance                 Decimal   No        Balance
 RepairSettlement         Decimal   No        Repair settlement figure
 OriginalMaterialNet      Decimal   No        Original material net figure
 RecoveryRetail           String    No        Retail recovery costs.
                                          Accepts the cost or the value
                                          'TBA'
 RecoveryNet             Decimal   No        Assessed recovery net figure
 RecoveryVat             Decimal   No        Assessed recovery vat figure
 RecoveryGross            Decimal   No        Assessed recovery gross
                                          figure
 LabourRetail            Decimal   No        Assessed recovery net figure
 LabourDiscountAmount      Decimal   No        Assessed labour discount
                                          figure
 LabourNet               Decimal   No        Assessed labour net figure
 LabourVat               Decimal   No        Assessed labour vat figure
 LabourGross             Decimal   No        Assessed labour gross figure
 MaterialRetail           Decimal   No        Assessed material retail
                                          figure
 MaterialDiscount         Decimal   No        Assessed material discount
                                          figure
 MaterialNet             Decimal   No        Assessed material net figure
 MaterialVat             Decimal   No        Assessed material vat figure
 MaterialGross            Decimal   No        Assessed material gross
                                          figure
 SundryNet               Decimal   No        Assessed sundry net figure
 SundryVat               Decimal   No        Assessed sundry vat figure
 SundryGross             Decimal   No        Assessed sundry gross figure
 EpaNet                  Decimal   No        Assessed epa net figure
 EpaVat                  Decimal   No        Assessed epa vat figure
 EpaGross                Decimal   No        Assessed epa gross figure
 PartsRetail             Decimal   No        Assessed parts retail figure
 PartsDiscountAmount      Decimal   No        Assessed parts discount
                                          figure
 PartsNet                Decimal   No        Assessed parts net figure
 PartsVat                Decimal   No        Assessed parts vat figure
 PartsGross              Decimal   No        Assessed parts gross figure
 OtherRetail             Decimal   No        Assessed other retail figure
 OtherDiscountAmount      Decimal   No        Assessed other discount
                                          figure
 OtherNet                Decimal   No        Assessed other net figure
 OtherVat                Decimal   No        Assessed other vat figure
 OtherGross              Decimal   No        Assessed other gross figure
 TotalRetail             Decimal   No        Assessed total retail figure
```

## Source page 50

```text
 TotalDiscountAmount      Decimal   No        Assessed total discount
                                          figure
 TotalNet                Decimal   No        Assessed total net figure
 TotalVat                Decimal   No        Assessed total vat figure
 TotalGross              Decimal   No        Assessed total gross figure
 ReportText              String    No        Narrative content of the
                                          engineer's report.
 IsSupplementary          Bool     No        Indicates if the report is a
                                          supplementary submission.
 ImpactImage             List     No        Directional impact details
                                          (see below).
 Parts                  List     No        List of parts involved (see
                                          table below).
 Files                  List     No        Associated files (photos,
                                          documents, etc.).
```

## Source page 51

### Example 'Report' JSON Request

```json
{
"InspectEngineer": "DEMOENG1",
"EvaRef": "123456",
"VehReg": "AB12CDE",
"ClmNo": "CLM987654",
"InsuredName": "John Smith",
"ThirdPartyName": "Jane Doe",
"ClaimType": "Repair",
"IncidentDate": "2025-09-15T00:00:00Z",
"InspectionDate": "2025-09-18T00:00:00Z",
"RepairsAuthorisedDate": "2025-09-20T00:00:00Z",
"SuppAuthorisedDate": "2025-09-22T00:00:00Z",
"EstimateRecievedDate": "2025-09-17T00:00:00Z",
"ReportDate": "2025-09-23T00:00:00Z",
"RepairerEstimateAgreed": "Yes",
"InspLocName": "AutoFix Garage",
"InspLocAdd": "123 Main Street",
"InspLocTown": "Nottingham",
"InspLocCity": "Nottingham",
"InspLocCounty": "Nottinghamshire",
"InspLocPCode": "NG1 4AA",
"InspLocTel": "01151234567",
"InspLocEmail": "info@autofixgarage.co.uk",
"InspLocCont": "Mark Taylor",
"InspectionType": "Vehicle Damage Inspection",
"ReportType": "Full Report",
"RepairDuration": "5 Days",
```

## Source page 52

"VehRoadWorthy": "No",
"VehNotRoadWorthyReason": "Front-end structural damage",
"VehDriveable": "No",
"VehInUse": "No",
"VehAreaOfRepair": "Front Bumper, Bonnet, Left Headlamp",
"LightCondAtInsp": "Daylight",
"InspCondition": "Vehicle assessed at garage under natural light",
"TempRepairs": "No",
"TempRepairsHow": "",
"TempRepairsCost": 0.0,
"ValMarket": 12500.0,
"ValRetail": 13000.0,
"ValTrade": 11000.0,
"ValMid": 12000.0,
"ValSalvage": 2000.0,
"ValDisposal": 150.0,
"ValPrivate": 11800.0,
"ValResearch": "Internet",
"MileageAdjust": -200.0,
"ConditionAdjust": -300.0,
"OtherAdjust": 0.0,
"ReportDelayed": false,
"ReportDelayedReason": "",
"PresentAtInsp": "Repairer representative and engineer present",
"PrivateHireLicNo": "",
"EngineStarts": "Yes",
"EngineFailReason": "",
"DoorsSecured": "Yes",

## Source page 53

"DoorsNotSecuredReason": "",
"Diminution": "No",
"DiminutionPAVPercent": 0.0,
"ClaimantVatLiability": 20.0,
"ClaimantTotLiability": 300.0,
"RepairDelays": false,
"RepairDelaysReason": "",
"Excess": "250",
"ClaimantVatStatus": "20%",
"AuthorityStatus": "Yes",
"Cause": "Rear-ended another vehicle",
"SumInsured": 15000.0,
"Settled": "No",
"DtAttemptedSettle": "2025-09-28T00:00:00Z",
"SalLocName": "ABC Salvage Ltd",
"SalLocAdd": "1 Recovery Way",
"SalLocTown": "Leeds",
"SalLocCity": "Leeds",
"SalLocCounty": "Yorkshire",
"SalLocPCode": "LS1 4BB",
"SalLocTelNo": "01134567890",
"SalLocEmail": "contact@abcsalvage.co.uk",
"ABICat": "N",
"OwnerRetainSalvage": false,
"FinalStorage": 250.0,
"CurrentStore": 100.0,
"SalvageRef": "SAL20251022",
"DtSalvageMoved": "2025-09-25T00:00:00Z",

## Source page 54

"StorageRate": 20.0,
"Inherited": "No",
"DtStoreStart": "2025-09-15T00:00:00Z",
"SalvageMoved": true,
"VATOnSalv": "Yes",
"VATONother": false,
"VATOnPaint": true,
"VATOnParts": true,
"Rectification": "Panel adjustment required",
"RectificationIssues": "",
"VehImpactAsDescribed": true,
"GlassCode": "GLS001",
"GlassModelId": "MDL12345",
"VehClass": "Car",
"VehMake": "Toyota",
"VehModel": "Corolla",
"VehDescription": "Toyota Corolla 1.6 VVT-i Icon Tech",
"VehEngineCC": "1600",
"VehEngineBHP": "132",
"VehBody": "5 Door",
"VehBodyDescription": "Hatchback",
"VehFirstRegistered": "2021-03-10",
"VehOdometer": "26500",
"VehOdometerUnit": "Miles",
"VehColour": "Silver",
"VehCondition": "Good",
"VehBrakes": "ABS, fully functional",
"VehSteering": "Power-assisted, No issues",

## Source page 55

"VehInCarEntertainment": "Touchscreen infotainment system",
"VehExtras": "Rear camera, cruise control",
"VehMods": "None",
"VehTaxExpire": "MAR/2026",
"VehVin": "JTNB1234567890001",
"VehRhfTyre": "5",
"VehLhfTyre": "6",
"VehRhrTyre": "7",
"VehLhrTyre": "8",
"VehSpareTyre": "No spare",
"VehRhfSBelt": "Fitted",
"VehLhfSBelt": "Fitted",
"VehRhrSBelt": "Fitted",
"VehLhrSBelt": "Fitted",
"VehCenSBelt": "Fitted",
"VehAirBagDeployed": "Driver and passenger front",
"VehTransmission": "Automatic",
"VehFuelType": "Petrol",
"DamageSeverity": "Moderate",
"DamageType": "Collision/Impact",
"DamageArea": "Rear",
"DamageLocation": "Bumper",
"RepairName": "AutoFix Garage",
"RepairAdd": "123 Main Street",
"RepairTown": "Nottingham",
"RepairCity": "Nottingham",
"RepairCounty": "Nottinghamshire",
"RepairPCode": "NG1 4AA",

## Source page 56

"RepairTel": "01151234567",
"RepairEmail": "repairs@autofixgarage.co.uk",
"RepairCont": "Mark Taylor",
"RepairerVatStatus": "20%",
"EstimatedRecovery": 150.0,
"EstimatedLabour": 500.0,
"EstimatedMaterials": 300.0,
"EstimatedSundries": 50.0,
"EstimatedEpa": 30.0,
"EstimatedParts": 600.0,
"EstimatedOtherTotal": 0.0,
"EstimatedNet": 1630.0,
"EstimatedVat": 326.0,
"EstimatedGross": 1956.0,
"EstimatedPartsDiscount": 5.0,
"EstimatedLabourRate": 50.0,
"LabourRate": 48.0,
"RecoveryDiscountPercent": 0.0,
"LabourDiscountPercent": 2.0,
"MaterialDiscountPercent": 0.0,
"SundryDiscountPercent": 0.0,
"EPADiscountPercent": 0.0,
"PartsDiscountPercent": 5.0,
"RecoverySavingsNet": 0.0,
"RecoverySavingsGross": 0.0,
"LabourSavingsNet": 10.0,
"LabourSavingsGross": 12.0,
"MaterialSavingsNet": 0.0,

## Source page 57

"MaterialSavingsGross": 0.0,
"PartsSavingsNet": 30.0,
"PartsSavingsGross": 36.0,
"OtherSavingsNet": 0.0,
"OtherSavingsGross": 0.0,
"DeleteSavingsNet": 0.0,
"DeleteSavingsGross": 0.0,
"TotalSavingsNet": 40.0,
"TotalSavingsGross": 48.0,
"VatSavings": 8.0,
"PrincipalSavings": 0.0,
"BettermentRetail": 0.0,
"BettermentDiscount": 0.0,
"BettermentNet": 0.0,
"BettermentVat": 0.0,
"BettermentGross": 0.0,
"TotalExcess": 250.0,
"ContractRep": 0.0,
"Reserve": 0.0,
"Balance": 0.0,
"RepairSettlement": 1800.0,
"OriginalMaterialNet": 0.0,
"RecoveryRetail": "TBA",
"RecoveryNet": 0.0,
"RecoveryVat": 0.0,
"RecoveryGross": 0.0,
"LabourRetail": 0.0,
"LabourDiscountAmount": 0.0,

## Source page 58

"LabourNet": 0.0,
"LabourVat": 0.0,
"LabourGross": 0.0,
"MaterialRetail": 0.0,
"MaterialDiscount": 0.0,
"MaterialNet": 0.0,
"MaterialVat": 0.0,
"MaterialGross": 0.0,
"SundryNet": 0.0,
"SundryVat": 0.0,
"SundryGross": 0.0,
"EpaNet": 0.0,
"EpaVat": 0.0,
"EpaGross": 0.0,
"PartsRetail": 0.0,
"PartsDiscountAmount": 0.0,
"PartsNet": 0.0,
"PartsVat": 0.0,
"PartsGross": 0.0,
"OtherRetail": 0.0,
"OtherDiscountAmount": 0.0,
"OtherNet": 0.0,
"OtherVat": 0.0,
"OtherGross": 0.0,
"TotalRetail": 0.0,
"TotalDiscountAmount": 0.0,
"TotalNet": 0.0,
"TotalVat": 0.0,

## Source page 59

"TotalGross": 0.0,
"ReportText": "Vehicle sustained severe front-end damage. Recommended full
bumper and bonnet replacement.",
"IsSupplementary": false,
```json
"ImpactImage": [
{
"Start": 1,
"End": 7
},
{
"Start": 2,
"End": 4
}
],
```
```json
"Parts": [
{
"Description": "Rear Bumper",
"Quantity": 1,
"PartType": "New",
"LabourTime": 2.5,
"PaintTime": 1.0,
"MaterialCost": 50.0,
"Price": 350.0
},
{
"Description": "Bonnet",
"Quantity": 1,
"PartType": "Repair",
```

## Source page 60

"LabourTime": 3.0,
"PaintTime": 1.5,
"MaterialCost": 80.0,
"Price": 420.0
}
],
```json
"Files": [
{
"Name": "RearDamagePhoto",
"Extension": "jpg",
"Data": "Base64EncodedStringHere"
},
{
"Name": "EstimateDocument",
"Extension": "pdf",
"Data": "Base64EncodedStringHere"
}
]
```
}

## Source page 61

### Impact Image

```text
 Field     Type      Required         Description
 Start     Int       Yes             Start point of impact.
 End       Int       Yes             End point of impact.
The impact image has 8 points that reference 8 different locations on the impact image.
Example image above shows point locations. The Impact Image is made up of a list of
start and end locations that relate to the above impact image. The below example image
shows the output from the example Impact Image section of the JSON. This image will
be outputted on the final report PDF once the report has been compiled in EVA.
```

### Example 'ImpactImage' JSON

```json
"ImpactImage": [
{
"Start": 1,
"End": 7
},
{
"Start": 2,
"End": 4
}
]
```

![Source page 61 image](eva-api-docs-assets/page-061-image-1.jpeg)

![Source page 61 image](eva-api-docs-assets/page-061-image-2.jpeg)

## Source page 62

### Parts

```text
 Field          Type      Required    Description
 Description     string    Yes        Part name or description.
 Quantity        int       Yes        Quantity of the part.
 PartType        string    Yes        Type of part. Accepted values:
  Blend
  New
  Paint
  R & R
  Repair
  Specialist
  Unknown
  Check
 LabourTime      float     No         Labour time in hours.
 PaintTime       float     No         Paint time in hours.
 MaterialCost    decimal    No         Cost of materials for the part.
 Price          decimal    No         Price of the part.
```

## Source page 63

### Example 'Parts' JSON

```json
"Parts" :[
{
"Description": "Rear Bumper",
"Quantity" : 1,
"PartType": "New",
"LabourTime": 2.25,
"Price": 250.75
},
{
"Description": "Front Bumper",
"Quantity" : 1,
"PartType": "Paint",
"PaintTime": 1.75,
"Price": 124.85
}
]
```

## Source page 64

### File Model

```text
 Field     Type  Description
 Name      string File name.
 Extension string File extension (e.g., .jpg, .pdf).
 Data      byte[] Base64-encoded file content.
```

### Example 'Files' JSON

```json
"Files": [
{
"Name": "damage_closeup.jpg",
"Extension": ".jpg",
"Data": "base64stringofimage=="
}
]
```

## Source page 65

### Retrievable Report List

Endpoint: GET /Report/GetAvailableReports
Description:
This endpoint allows external partners to get a list of retrievable reports.
Currently, the results that are returned are ordered by the releasedDate descending
(newest first).
Note: Returns a 200 - Success, even when no reports are available to download.
### Possible Responses

```text
 HTTP Code              Description
 200 - Success          The List has been successfully retrieved
 401 - Unauthorized      The user is unauthorised to access the endpoint
 500 - Internal Server Error An error occurred while processing the update request.
             Example 'GetAvailableReports' JSON Response
```
```json
[
{
"id": 123,
"registration": "AB12CDE",
" releasedDate": "2026-05-06T10:30:00Z ",
}
]
```

### Response Model

```text
 Field                   Nullable   Description
               Type
 id                      No        Unique identifier for the report
               int
 registration             Yes        Vehicle registration
               string
 releasedDate             No        Date the report was released
               datetime
```

## Source page 66

### Retrieve Report

Endpoint: GET /Report/ GetReport?id={id}
Description:
This endpoint allows external partners to retrieve a reports data from the list of released
reports using the ID taken from the previous endpoint.
### Possible Responses

```text
 Name     Type     Required    Description
 Id       Int      Yes        Unique identifier of report
```

### Possible Responses

```text
 HTTP Code              Description
 200 - Success          The report has been successfully retrieved
 400 - Bad Request       An invalid report id has been sent
 401 - Unauthorized      The user is unauthorised to access the endpoint
 404 - Not Found        The report has not been found
 500 - Internal Server Error An error occurred while processing the update request.
```

### Example 'Full Report' JSON Response

```json
{
"InspectEngineer": "DEMOENG1",
"EvaRef": "123456",
"VehReg": "AB12CDE",
"ClmNo": "CLM987654",
"InsuredName": "John Smith",
"ThirdPartyName": "Jane Doe",
```

## Source page 67

"ClaimType": "Repair",
"IncidentDate": "2025-09-15T00:00:00Z",
"InspectionDate": "2025-09-18T00:00:00Z",
"RepairsAuthorisedDate": "2025-09-20T00:00:00Z",
"SuppAuthorisedDate": "2025-09-22T00:00:00Z",
"EstimateRecievedDate": "2025-09-17T00:00:00Z",
"ReportDate": "2025-09-23T00:00:00Z",
"RepairerEstimateAgreed": "Yes",
"InspLocName": "AutoFix Garage",
"InspLocAdd": "123 Main Street",
"InspLocTown": "Nottingham",
"InspLocCity": "Nottingham",
"InspLocCounty": "Nottinghamshire",
"InspLocPCode": "NG1 4AA",
"InspLocTel": "01151234567",
"InspLocEmail": "info@autofixgarage.co.uk",
"InspLocCont": "Mark Taylor",
"InspectionType": "Vehicle Damage Inspection",
"ReportType": "Full Report",
"RepairDuration": "5 Days",
"VehRoadWorthy": "No",
"VehNotRoadWorthyReason": "Front-end structural damage",
"VehDriveable": "No",
"VehInUse": "No",
"VehAreaOfRepair": "Front Bumper, Bonnet, Left Headlamp",
"LightCondAtInsp": "Daylight",
"InspCondition": "Vehicle assessed at garage under natural light",
"TempRepairs": "No",

## Source page 68

"TempRepairsHow": "",
"TempRepairsCost": 0.0,
"ValMarket": 12500.0,
"ValRetail": 13000.0,
"ValTrade": 11000.0,
"ValMid": 12000.0,
"ValSalvage": 2000.0,
"ValDisposal": 150.0,
"ValPrivate": 11800.0,
"ValResearch": "Internet",
"MileageAdjust": -200.0,
"ConditionAdjust": -300.0,
"OtherAdjust": 0.0,
"ReportDelayed": false,
"ReportDelayedReason": "",
"PresentAtInsp": "Repairer representative and engineer present",
"PrivateHireLicNo": "",
"EngineStarts": "Yes",
"EngineFailReason": "",
"DoorsSecured": "Yes",
"DoorsNotSecuredReason": "",
"Diminution": "No",
"DiminutionPAVPercent": 0.0,
"ClaimantVatLiability": 20.0,
"ClaimantTotLiability": 300.0,
"RepairDelays": false,
"RepairDelaysReason": "",
"Excess": "250",

## Source page 69

"ClaimantVatStatus": "20%",
"AuthorityStatus": "Yes",
"Cause": "Rear-ended another vehicle",
"SumInsured": 15000.0,
"Settled": "No",
"DtAttemptedSettle": "2025-09-28T00:00:00Z",
"SalLocName": "ABC Salvage Ltd",
"SalLocAdd": "1 Recovery Way",
"SalLocTown": "Leeds",
"SalLocCity": "Leeds",
"SalLocCounty": "Yorkshire",
"SalLocPCode": "LS1 4BB",
"SalLocTelNo": "01134567890",
"SalLocEmail": "contact@abcsalvage.co.uk",
"ABICat": "N",
"OwnerRetainSalvage": false,
"FinalStorage": 250.0,
"CurrentStore": 100.0,
"SalvageRef": "SAL20251022",
"DtSalvageMoved": "2025-09-25T00:00:00Z",
"StorageRate": 20.0,
"Inherited": "No",
"DtStoreStart": "2025-09-15T00:00:00Z",
"SalvageMoved": true,
"VATOnSalv": "Yes",
"VATONother": false,
"VATOnPaint": true,
"VATOnParts": true,

## Source page 70

"Rectification": "Panel adjustment required",
"RectificationIssues": "",
"VehImpactAsDescribed": true,
"GlassCode": "GLS001",
"GlassModelId": "MDL12345",
"VehClass": "Car",
"VehMake": "Toyota",
"VehModel": "Corolla",
"VehDescription": "Toyota Corolla 1.6 VVT-i Icon Tech",
"VehEngineCC": "1600",
"VehEngineBHP": "132",
"VehBody": "5 Door",
"VehBodyDescription": "Hatchback",
"VehFirstRegistered": "2021-03-10",
"VehOdometer": "26500",
"VehOdometerUnit": "Miles",
"VehColour": "Silver",
"VehCondition": "Good",
"VehBrakes": "ABS, fully functional",
"VehSteering": "Power-assisted, No issues",
"VehInCarEntertainment": "Touchscreen infotainment system",
"VehExtras": "Rear camera, cruise control",
"VehMods": "None",
"VehTaxExpire": "MAR/2026",
"VehVin": "JTNB1234567890001",
"VehRhfTyre": "5",
"VehLhfTyre": "6",
"VehRhrTyre": "7",

## Source page 71

"VehLhrTyre": "8",
"VehSpareTyre": "No spare",
"VehRhfSBelt": "Fitted",
"VehLhfSBelt": "Fitted",
"VehRhrSBelt": "Fitted",
"VehLhrSBelt": "Fitted",
"VehCenSBelt": "Fitted",
"VehAirBagDeployed": "Driver and passenger front",
"VehTransmission": "Automatic",
"VehFuelType": "Petrol",
"DamageSeverity": "Moderate",
"DamageType": "Collision/Impact",
"DamageArea": "Rear",
"DamageLocation": "Bumper",
"RepairName": "AutoFix Garage",
"RepairAdd": "123 Main Street",
"RepairTown": "Nottingham",
"RepairCity": "Nottingham",
"RepairCounty": "Nottinghamshire",
"RepairPCode": "NG1 4AA",
"RepairTel": "01151234567",
"RepairEmail": "repairs@autofixgarage.co.uk",
"RepairCont": "Mark Taylor",
"RepairerVatStatus": "20%",
"EstimatedRecovery": 150.0,
"EstimatedLabour": 500.0,
"EstimatedMaterials": 300.0,
"EstimatedSundries": 50.0,

## Source page 72

"EstimatedEpa": 30.0,
"EstimatedParts": 600.0,
"EstimatedOtherTotal": 0.0,
"EstimatedNet": 1630.0,
"EstimatedVat": 326.0,
"EstimatedGross": 1956.0,
"EstimatedPartsDiscount": 5.0,
"EstimatedLabourRate": 50.0,
"LabourRate": 48.0,
"RecoveryDiscountPercent": 0.0,
"LabourDiscountPercent": 2.0,
"MaterialDiscountPercent": 0.0,
"SundryDiscountPercent": 0.0,
"EPADiscountPercent": 0.0,
"PartsDiscountPercent": 5.0,
"RecoverySavingsNet": 0.0,
"RecoverySavingsGross": 0.0,
"LabourSavingsNet": 10.0,
"LabourSavingsGross": 12.0,
"MaterialSavingsNet": 0.0,
"MaterialSavingsGross": 0.0,
"PartsSavingsNet": 30.0,
"PartsSavingsGross": 36.0,
"OtherSavingsNet": 0.0,
"OtherSavingsGross": 0.0,
"DeleteSavingsNet": 0.0,
"DeleteSavingsGross": 0.0,
"TotalSavingsNet": 40.0,

## Source page 73

"TotalSavingsGross": 48.0,
"VatSavings": 8.0,
"PrincipalSavings": 0.0,
"BettermentRetail": 0.0,
"BettermentDiscount": 0.0,
"BettermentNet": 0.0,
"BettermentVat": 0.0,
"BettermentGross": 0.0,
"TotalExcess": 250.0,
"ContractRep": 0.0,
"Reserve": 0.0,
"Balance": 0.0,
"RepairSettlement": 1800.0,
"OriginalMaterialNet": 0.0,
"RecoveryRetail": "TBA",
"RecoveryNet": 0.0,
"RecoveryVat": 0.0,
"RecoveryGross": 0.0,
"LabourRetail": 0.0,
"LabourDiscountAmount": 0.0,
"LabourNet": 0.0,
"LabourVat": 0.0,
"LabourGross": 0.0,
"MaterialRetail": 0.0,
"MaterialDiscount": 0.0,
"MaterialNet": 0.0,
"MaterialVat": 0.0,
"MaterialGross": 0.0,

## Source page 74

"SundryNet": 0.0,
"SundryVat": 0.0,
"SundryGross": 0.0,
"EpaNet": 0.0,
"EpaVat": 0.0,
"EpaGross": 0.0,
"PartsRetail": 0.0,
"PartsDiscountAmount": 0.0,
"PartsNet": 0.0,
"PartsVat": 0.0,
"PartsGross": 0.0,
"OtherRetail": 0.0,
"OtherDiscountAmount": 0.0,
"OtherNet": 0.0,
"OtherVat": 0.0,
"OtherGross": 0.0,
"TotalRetail": 0.0,
"TotalDiscountAmount": 0.0,
"TotalNet": 0.0,
"TotalVat": 0.0,
"TotalGross": 0.0,
"ReportText": "Vehicle sustained severe front-end damage. Recommended full
bumper and bonnet replacement.",
"GlassMonth": "",
"SupplementaryCount": 1,
"FeeNet": 0.0,
"FeeVat": 0.0,
"FeeGross": 0.0,

## Source page 75

"IsSupplementary": false,
```json
"ImpactImage": [
{
"Start": 1,
"End": 7
},
{
"Start": 2,
"End": 4
}
],
```
```json
"Parts": [
{
"Description": "Rear Bumper",
"Quantity": 1,
"PartType": "New",
"LabourTime": 2.5,
"PaintTime": 1.0,
"MaterialCost": 50.0,
"Price": 350.0
},
{
"Description": "Bonnet",
"Quantity": 1,
"PartType": "Repair",
"LabourTime": 3.0,
"PaintTime": 1.5,
```

## Source page 76

"MaterialCost": 80.0,
"Price": 420.0
}
],
```json
"Files": [
{
"Name": "RearDamagePhoto",
"Extension": "jpg",
"Data": "Base64EncodedStringHere"
},
{
"Name": "EstimateDocument",
"Extension": "pdf",
"Data": "Base64EncodedStringHere"
}
]
```
}
### Request Model

```text
 Field                  Type     Required   Description
 InspectEngineer          string    Yes       The name or code of the
                        (max 12)            inspecting engineer.
 EvaRef                  string    Yes (if    The EVA reference number
                                ClmNo is   identifying the assessment.
                                used with
                                it)
 VehReg                  string    Yes       Vehicle registration.
 ClmNo                  string    Yes       Claim number.
```

## Source page 77

```text
 InsuredName             string    No        Name of the insured party.
 ThirdPartyName           string    No        Name of the third party
                                          involved (if applicable).
 ClaimType               string    No        Type of claim. Possible
                                          values:
  Cash-In-Lieu
  Diminution
  Other
  Post Repair
  Repair
  T/Loss
  Repudiation
 IncidentDate            DateTime  Yes       Date of the incident.
 InspectionDate           DateTime  No        Date the vehicle was
                                          inspected.
 RepairsAuthorisedDate     DateTime  No        Date repairs were authorised.
 SuppAuthorisedDate       DateTime  No        Date supplementary
                                          authorisation was given.
 EstimateRecievedDate      DateTime  No        Date the repair estimate was
                                          received.
 ReportDate              DateTime  Yes       Date the report was created
                                          or submitted.
 RepairerEstimateAgreed    String    No        Indicates if the repairer's
                                          estimate was agreed.
                                          Possible values:
  Yes
  No
  N/A
 InspLocName             string    No        Name of the inspection
                        (max 40)            location.
 InspLocAdd              string    No        Address line of the
                        (max 40)            inspection location.
 InspLocTown             string    No        Town of the inspection
                        (max 250)           location.
```

## Source page 78

```text
 InspLocCity             string    No        City of the inspection
                        (max 250)           location.
 InspLocCounty            string    No        County of the inspection
                        (max 250)           location.
 InspLocPCode            string    No        Postcode of the inspection
                        (max 10)            location.
 InspLocTel              string    No        Telephone number of the
                        (max 18)            inspection location.
 InspLocEmail            string    No        Email of the inspection
                        (max 250)           location.
 InspLocCont             string    No        Contact name at inspection
                        (max 18)            location.
 InspectionType           string    Yes       Type of inspection
                        (max 25)            performed. Possible values:
  Vehicle Damage
                                               Inspection
  Rectification Work
  Quality/Audit Inspection
  Low Velocity Inspection
  Desktop
  Other
  Cold Call
  Consistency
  Images Only
  Forensic
 ReportType              string    No        Type of report. Possible
                        (max 27)            values:
  Cold Call Report
  Desktop Report
  Full Report
  Letter
  Post-Inspection
  Post-Repair Audit
  Post-Repair
                                               Complaint
  Roadworthy
  Simple Low Speed
                                               Inspection
  Small Claim
```

## Source page 79

- Telephone
```text
 RepairDuration           string    No        Estimated repair duration in
                        (max 10)            days.
 VehRoadWorthy            string    No        Indicates if the vehicle is
                        (max 10)            roadworthy. Possible values:
  Yes
  No
  N/A
  Subject To
 VehNotRoadWorthyReason string       No        Reason vehicle is not
                        (max 250)           roadworthy.
 VehDriveable            string    No        Indicates if the vehicle is
                        (max 9)            drivable. Possible values:
  Yes
  No
  Not Known
 VehInUse                string    No        Indicates if the vehicle is still
                        (max 9)            in use. Possible values:
  Yes
  No
  Not Known
 VehAreaOfRepair          string    No        General area of repair.
                        (max 250)
 LightCondAtInsp          string    No        Lighting conditions at
                        (max 250)           inspection.
 InspCondition            string    No        General condition at
                        (max 250)           inspection.
 TempRepairs             string    No        Indicates if temporary repairs
                        (max 3)            were done. Possible values:
  Yes
  No
  N/A
 TempRepairsHow           string    No        Description of temporary
                        (max 250)           repairs.
```

## Source page 80

```text
 TempRepairsCost          decimal   No        Cost of temporary repairs.
 ValMarket               decimal   No        Market value of the vehicle.
 ValRetail               decimal   No        Retail value.
 ValTrade                decimal   No        Trade-in value.
 ValMid                  decimal   No        Mid-point valuation.
 ValSalvage              decimal   No        Salvage value.
 ValDisposal             decimal   No        Disposal value.
 ValPrivate              decimal   No        Private sale value.
 ValResearch             string    No        Supporting valuation
                                          research text. Possible
                                          values:
  Internet
  Magazines
  Main Dealer
  Other
 MileageAdjust            decimal   No        Adjustment for mileage.
 ConditionAdjust          decimal   No        Adjustment for condition.
 OtherAdjust             decimal   No        Other valuation adjustments.
 ReportDelayed            bool     No        Indicates if report
                                          submission was delayed.
 ReportDelayedReason      string    No        Reason for delay. Possible
                        (max 35)            values:
  Awaiting estimate
                                               Owner
  Awaiting estimate
                                               Repairer
  Client requested
                                               inspection date
  Getting parts prices
  Problem contacting
                                               owner
  Vehicle being stripped
  Awaiting images
```

## Source page 81

- Repairer carrying out pre
    strip
- Customer unresponsive
- Repairer unresponsive
- Awaiting further info(See
    comments)
- Vehicle not available
- Valuation dispute
```text
 PresentAtInsp            string    No        Individuals present during
                        (max 250)           the inspection.
 PrivateHireLicNo         string    No        Private hire licence number.
                        (max 20)
 EngineStarts            string    No        Indicates if engine starts.
                        (max 9)            Possible values:
  Yes
  No
  Not Known
 EngineFailReason         string    No        Reason for engine failure (if
                        (max 250)           applicable).
 DoorsSecured            string    No        Indicates if vehicle doors are
                        (max 9)            secured. Possible values:
  Yes
  No
  Not Known
 DoorsNotSecuredReason     string    No        Reason vehicle doors are not
                        (max 250)           secured.
 Diminution              string    No        Indicates if vehicle has a
                        (max 3)            diminution in value. Possible
                                          values:
  Yes
  No
  N/A
 DiminutionPAVPercent      decimal   No        Diminution percentage of
                                          pre-accident value.
 ClaimantVatLiability      decimal   No        VAT liability for the claimant.
 ClaimantTotLiability      decimal   No        Total liability of the claimant.
```

## Source page 82

```text
 RepairDelays            bool     No        Indicates if there were repair
                                          delays.
 RepairDelaysReason       string    No        Reason for repair delays.
                        (max 50)
 Excess                  string    No        Claim excess amount.
                        (max 10)
 ClaimantVatStatus        string    No        Claimant VAT registration
                        (max 8)            status. Possible values:
  Yes
  No
  n%
 AuthorityStatus          string    No        Authority status for repair.
                        (max 17)            Possible values:
  Yes
  No
 Cause                  string    No        Cause of the damage.
                        (max 250)
 SumInsured              decimal   No        Total sum insured.
 Settled                 string    No        Indicates if the claim is
                        (max 21)            settled. Possible values:
  Yes
  No
  Disputed
  N/A
  Subject To
 DtAttemptedSettle        DateTime  No        Date settlement was
                                          attempted.
 SalLocName              string    No        Name of salvage company
                        (max 40)
 SalLocAdd               string    No        Salvage company first line of
                        (max 40)            address
 SalLocTown              string    No        Salvage company town
                        (max 250)
 SalLocCity              string    No        Salvage company city
                        (max 250)
 SalLocCounty            string    No        Salvage company county
                        (max 250)
```

## Source page 83

```text
 SalLocPCode             string    No        Salvage company postcode
                        (max 10)
 SalLocTelNo             string    No        Salvage company contact
                        (max 18)            number
 SalLocEmail             string    No        Salvage company email
                        (max 250)           address
 ABICat                  string    No        Salvage category. Possible
                                          values:
  A-Scrap
  B-Breaker
  C-Rep Costs > PAMV
  D-Constructive T/L
  X-Other
  S-Structural
  N-Non-Structural
  No category
                                               breaker/scrap
 OwnerRetainSalvage       Bool     No        Is the owner retaining the
                                          salvage?
 FinalStorage            Decimal   No        Final storage costs
 CurrentStore            Decimal   No        Current storage costs
 SalvageRef              string    No        Salvage reference
                        (max 8)
 DtSalvageMoved           Datetime  No        Date salvage was moved
 StorageRate             Decimal   No        Daily storage rate
 Inherited               String    No        Inherited charges
                        (max 10)
 DtStoreStart            DateTime  No        Date storage of vehicle
                                          started
 SalvageMoved            Bool     No        Was the salvage moved?
 VATOnSalv               String    No        Should vat be applied to
                                          salvage? Possible values:
  Yes
  No
 VATOnOther              Bool     No        Should vat be applied to
                                          other?
 VATOnPaint              Bool     No        Should vat be applied to
                                          paint?
 VATOnParts              Bool     No        Should vat be applied to
                                          parts?
 Rectification            String    No        Rectification work required
 RectificationIssues      String    No        Rectification issues
 VehImpactAsDescribed      bool     No        Is the impact as described?
 GlassCode               string    No        Glass's guide code
                        (max 6)
```

## Source page 84

```text
 GlassModelId            String    No        Glass's model Id
                        (max 9)
 VehClass                String    No        Vehicle class. Possible
                        (max 10)            values:
  Agric
  Car
  Caravan
  HGV
  LCV
  M/Cycle
  Misc
  Plant
  PSV
  Trailer
  Tractor
  Machinery
 VehMake                 String    No        Vehicle Make
                        (max 20)
 VehModel                String    No        Vehicle Model
                        (max 20)
 VehDescription           String    No        Vehicle Make & Model
                        (max 40)
 VehEngineCC             String    No        Engine CC
                        (max 8)
 VehEngineBHP            String    No        Engine BHP
                        (max 4)
 VehBody                 String    No        Vehicle body type. Possible
                        (max 14)            values:
  2 Door
  3 Door
  4 Door
  5 Door
  6 Door
  Agricultural
  Articulated
  Contractors
  P.S.V.
  Private
  Rigid
  Trailer
 VehBodyDescription       String    No        Vehicle Body Description.
                        (max 30)            Possible values:
  3 Wheel Scooter
  Access Platform
  Ambulance
```

## Source page 85

- ATV
- Backhoe Digger
- Baler
- Beavertail
- Bicycle
- Box Lorry
- Box Trailer
- Box-Van
- Bubble Car
- Bus
- Caged Tipper
- Camper Van
- Car Derived Van
- Caravan
- Catering Unit
- Cement Mixer
- Cherry Picker
- Coach
- Combine
- Compressor
- Convertible
- Coupe
- Crane
- Crew-cab
- Crewcab Tipper
- Crop Fertilizer
- Crop Sprayer
- Curtainsider
- Digger
- Double Decker Bus
- Drophead
- Dropside
- Dumper
- Estate
- Excavator
- Fire Engine
- Flatbed
- Food Truck
- Forager
- Fork-Lift
- Front End Loader
- Fuel Tanker
- Grab Truck
- Gritter
- Hatchback

## Source page 86

- Hay Turner
- Hedgetrimmer
- Hook Loader
- Horsebox
- Ice Cream Van
- Kombi
- Light Van
- Limousine
- Livestock Carrier
- Low Loader
- Luton Van
- M.P.V.
- Machinery
- Mini Excavator
- Minibus
- Mobile Home
- Motor Home
- Motorcycle
- Mower
- Muck Spreader
- Omnibus
- Other
- P.S.V.
- Panel Van
- Pantechnicon
- Parkhome
- Pickup
- Plough
- Quad-Bike
- Rake
- Recovery Truck
- Refrigerated Box Lorry
- Refrigerated Box Van
- Refrigerated Van
- Refuse Collection
    Vehicle
- Road Sweeper
- Rotavator
- Saloon
- Scooter
- Self Propelled Sprayer
- Silage Trailer
- Skelatal
- Skid Steer
- Skip Loader

## Source page 87

- Static
- Station Wagon
- Tandem Trailer
- Tanker
- Tarmac Hot Box
- Taxi
- Tedder
- Telehandler
- Tipper
- Toilet Block
- Touring
- Tractor
- Tractor Unit
- Trailer
- Tri Axle Trailer
- Trike
- Ute
- Vacuum Tanker
- Van
- Vehicle Transporter
- Wagon
- Welfare Unit
```text
 VehFirstRegistered       String    No        Month & Year vehicle was
                        (max 10)            first registered. Format:
                                          MMM/yyyy
 VehOdometer             String    No        Odometer reading
                        (max 10)
 VehOdometerUnit          string    No        Odometer reading units.
                                          Accepted values:
  Hrs
  Km
  Miles
 VehColour               String    No        Colour of vehicle
                        (max 40)
 VehCondition            String    No        Condition of vehicle
                        (max 15)
 VehBrakes               String    No        Condition of brakes on
                        (max 250)           vehicle
 VehSteering             String    No        Condition of steering on
                        (max 250)           vehicle
 VehInCarEntertainment     String    No        Any in car entertainment in
                        (max 40)            the vehicle
 VehExtras               String    No        Any extras on the vehicle
                        (max 40)
```

## Source page 88

```text
 VehMods                 String    No        Any additional mods of the
                        (max 40)            vehicle
 VehTaxExpire            String    No        Tax expiration date. Format:
                        (max 10)            MMM/yyyy
 VehVin                  String    No        Vehicle Identification
                        (max 22)            Number (VIN)
 VehRhfTyre              String    No        Tyre tread depths on right-
                        (max 11)            hand front tyre
 VehLhfTyre              String    No        Tyre tread depths on left-
                        (max 11)            hand front tyre
 VehRhrTyre              String    No        Tyre tread depths on right-
                        (max 11)            hand rear tyre
 VehLhrTyre              String    No        Tyre tread depths on left-
                        (max 11)            hand rear tyre
 VehSpareTyre            String    No        Tyre tread depths on spare
                        (max 11)            tyre
 VehRhfSBelt             String    No        Status of right-hand front
                        (max 12)            seat belt. Possible values:
  Damaged
  Deployed
  Fitted
  No Access
  None Fitted
  Not Tested
 VehLhfSBelt             String    No        Status of left-hand front seat
                        (max 12)            belt. Possible values:
  Damaged
  Deployed
  Fitted
  No Access
  None Fitted
  Not Tested
 VehRhrSBelt             String    No        Status of right-hand rear seat
                        (max 12)            belt. Possible values:
  Damaged
  Deployed
  Fitted
  No Access
  None Fitted
  Not Tested
 VehLhrSBelt             String    No        Status of left-hand rear seat
                        (max 12)            belt. Possible values:
  Damaged
```

## Source page 89

- Deployed
- Fitted
- No Access
- None Fitted
- Not Tested
```text
 VehCenSBelt             String    No        Status of center seat belt.
                        (max 12)            Possible values:
  Damaged
  Deployed
  Fitted
  No Access
  None Fitted
  Not Tested
 VehAirBagDeployed        String    No        Did the airbags deploy?
                        (max 25)
 VehTransmission          String    No        Vehicle transmission.
                        (max 20)            Possible values:
  Automatic
  CVT
  DSG
  EGS
  Manual
  N/A
  Semi-Auto
  Sequential
  Sequential Automatic
  Sequential Manual
  Unknown
 VehFuelType             String    No        Vehicle fuel type.
                        (max 20)            Possible values:
  Biofuel
  Compressed Natural
                                               Gas
  Diesel
  Dual
  Electric
  Hybrid
  Hydrogen
  LPG
  Petrol
 DamageSeverity           String    No        First damage severity.
                        (max 18)            Possible Values:
```

## Source page 90

- Very Heavy
- Heavy
- Moderate to Heavy
- Moderate
- Light to Moderate
- Light
- Very Light
```text
 DamageType              String    No        First damage type.
                        (max 22)            Possible values:
  Accidental Damage
  Chemical
                                               Contamination
  Collision/Impact
  Electrical Failure
  Fire (Cause Unknown)
  Fire (Electrical)
  Fire (External)
  Fire (Fuel)
  Ground Damage
  Mechanical Failure
  No Damage
  Rodent Damage
  Storm Damage
  Theft or Attempted
  Unrecovered Theft
  Vandalism
  Water/Flood/Damp
 DamageArea              String    No        First damage area.
                        (max 8)            Possible values:
  Front
  LH Front
  LH Rear
  LH Side
  Rear
  RH Front
  RH Rear
  RH Side
 DamageLocation           String    No        First damage location.
                        (max 20)            Possible values:
  Bonnet
  Boot
  Brake Lever
  Bumper
  Complete Vehicle
  Door
```

## Source page 91

- Engine Bay
- Fairing
- Foot Peg
- Frame
- Front Fork
- Front Panel
- Front to Rear
- Gear Lever
- Handlebars
- Interior
- Luggage
    Compartment
- Mirror
- Mud Guard
- Panniers
- Quarter Panel
- Rear Panel
- Rear to Front
- Roof
- Swing Arm
- Tyre
- Underside
- Wheel
- Wing
```text
 DamageSeverity2          String    No        Second damage severity.
                        (max 18)            Possible values:
  Very Heavy
  Heavy
  Moderate to Heavy
  Moderate
  Light to Moderate
  Light
  Very Light
 DamageType2             String    No        Second damage type.
                        (max 22)            Possible values:
  Accidental Damage
  Chemical
                                               Contamination
  Collision/Impact
  Electrical Failure
  Fire (Cause Unknown)
  Fire (Electrical)
```

## Source page 92

- Fire (External)
- Fire (Fuel)
- Ground Damage
- Mechanical Failure
- No Damage
- Rodent Damage
- Storm Damage
- Theft or Attempted
- Unrecovered Theft
- Vandalism
- Water/Flood/Damp
```text
 DamageArea2             String    No        Second damage area.
                        (max 8)            Possible values:
  Front
  LH Front
  LH Rear
  LH Side
  Rear
  RH Front
  RH Rear
  RH Side
 DamageLocation2          String    No        Second damage location.
                        (max 20)            Possible values:
  Bonnet
  Boot
  Brake Lever
  Bumper
  Complete Vehicle
  Door
  Engine Bay
  Fairing
  Foot Peg
  Frame
  Front Fork
  Front Panel
  Front to Rear
  Gear Lever
  Handlebars
```

## Source page 93

- Interior
- Luggage
    Compartment
- Mirror
- Mud Guard
- Panniers
- Quarter Panel
- Rear Panel
- Rear to Front
- Roof
- Swing Arm
- Tyre
- Underside
- Wheel
- Wing
```text
 DamageSeverity3          String    No        Third damage severity.
                        (max 18)            Possible values:
  Very Heavy
  Heavy
  Moderate to Heavy
  Moderate
  Light to Moderate
  Light
  Very Light
 DamageType3             String    No        Third damage type.
                        (max 22)            Possible values:
  Accidental Damage
  Chemical
                                               Contamination
  Collision/Impact
  Electrical Failure
  Fire (Cause Unknown)
  Fire (Electrical)
  Fire (External)
  Fire (Fuel)
  Ground Damage
  Mechanical Failure
  No Damage
  Rodent Damage
```

## Source page 94

- Storm Damage
- Theft or Attempted
- Unrecovered Theft
- Vandalism
- Water/Flood/Damp
```text
 DamageArea3             String    No        Third damage area.
                        (max 8)            Possible values:
  Front
  LH Front
  LH Rear
  LH Side
  Rear
  RH Front
  RH Rear
  RH Side
 DamageLocation3          String    No        Third damage location.
                        (max 20)            Possible values:
  Bonnet
  Boot
  Brake Lever
  Bumper
  Complete Vehicle
  Door
  Engine Bay
  Fairing
  Foot Peg
  Frame
  Front Fork
  Front Panel
  Front to Rear
  Gear Lever
  Handlebars
  Interior
  Luggage
                                               Compartment
  Mirror
  Mud Guard
  Panniers
  Quarter Panel
```

## Source page 95

- Rear Panel
- Rear to Front
- Roof
- Swing Arm
- Tyre
- Underside
- Wheel
- Wing
```text
 RepairName              String    No        Repairer name
                        (max 40)
 RepairAdd               String    No        Repairer address line 1
                        (max 40)
 RepairTown              String    No        Repairer town
                        (max 250)
 RepairCity              String    No        Repairer city
                        (max 250)
 RepairCounty            String    No        Repairer county
                        (max 250)
 RepairPCode             String    No        Repairer postcode
                        (max 10)
 RepairTel               String    No        Repairer contact number
                        (max 18)
 RepairEmail             String    No        Repairer email
                        (max 250)
 RepairCont              String    No        Repairer contact name
                        (max 20)
 RepairerVatStatus        String    No        Repairer Vat Status.
                        (max 3)            Possible values:
  Yes
  No
  N%
 EstimatedRecovery        Decimal   No        Estimated recovery cost
 EstimatedLabour          Decimal   No        Estimated labour cost
 EstimatedMaterials       Decimal   No        Estimated materials cost
 EstimatedSundries        Decimal   No        Estimated sundries cost
 EstimatedEpa            Decimal   No        Estimated epa cost
 EstimatedParts           Decimal   No        Estimated parts cost
 EstimatedOtherTotal      Decimal   No        Estimated other cost
 EstimatedNet            Decimal   No        Estimated net cost
 EstimatedVat            Decimal   No        Estimated vat cost
 EstimatedGross           Decimal   No        Estimated gross cost
 EstimatedPartsDiscount    Decimal   No        Estimated parts discount
 EstimatedLabourRate      Decimal   No        Estimated labour rate
```

## Source page 96

```text
 LabourRate              Decimal   No        Assessed labour rate
 RecoveryDiscountPercent   Decimal   No        Assessed recovery discount
                                          percentage
 LabourDiscountPercent     Decimal   No        Assessed labour discount
                                          percentage
 MaterialDiscountPercent   Decimal   No        Assessed material discount
                                          percentage
 SundryDiscountPercent     Decimal   No        Assessed sundry discount
                                          percentage
 EPADiscountPercent       Decimal   No        Assessed epa discount
                                          percentage
 PartsDiscountPercent      Decimal   No        Assessed parts discount
                                          percentage
 RecoverySavingsNet       Decimal   No        Recovery savings net
                                          between estimated and
                                          assessed recovery charge
 RecoverySavingsGross      Decimal   No        Recovery savings gross
                                          between estimated and
                                          assessed recovery charge
 LabourSavingsNet         Decimal   No        Labour savings net between
                                          estimated and assessed
                                          labour charge
 LabourSavingsGross       Decimal   No        Labour savings gross
                                          between estimated and
                                          assessed labour charge
 MaterialSavingsNet       Decimal   No        Materials savings net
                                          between estimated and
                                          assessed materials charge
 MaterialSavingsGross      Decimal   No        Materials savings gross
                                          between estimated and
                                          assessed materials charge
 PartsSavingsNet          Decimal   No        Parts savings net between
                                          estimated and assessed
                                          parts charge
 PartsSavingsGross        Decimal   No        Parts savings gross between
                                          estimated and assessed
                                          parts charge
 OtherSavingsNet          Decimal   No        Other savings net between
                                          estimated and assessed
                                          other charge
 OtherSavingsGross        Decimal   No        Other savings gross between
                                          estimated and assessed
                                          other charge
 DeleteSavingsNet         Decimal   No        Deleted estimate items
                                          savings net between
                                          estimated and assessed
```

## Source page 97

```text
 DeleteSavingsGross       Decimal   No        Deleted estimate items
                                          savings gross between
                                          estimated and assessed
 TotalSavingsNet          Decimal   No        Total savings net between
                                          estimated and assessed
 TotalSavingsGross        Decimal   No        Total savings gross between
                                          estimated and assessed
 VatSavings              Decimal   No        Vat savings between
                                          estimated and assessed
 PrincipalSavings         Decimal   No        Savings for principal between
                                          estimated assessed
 BettermentRetail         Decimal   No        Betterment retail figure
 BettermentDiscount       Decimal   No        Betterment discount figure
 BettermentNet            Decimal   No        Betterment net figure
 BettermentVat            Decimal   No        Betterment vat figure
 BettermentGross          Decimal   No        Betterment gross figure
 TotalExcess             Decimal   No        Total excess
 ContractRep             Decimal   No        Contract Repair charge
 Reserve                 Decimal   No        Reserve figure
 Balance                 Decimal   No        Balance
 RepairSettlement         Decimal   No        Repair settlement figure
 OriginalMaterialNet      Decimal   No        Original material net figure
 RecoveryRetail           String    No        Retail recovery costs.
                                          Accepts the cost or the value
                                          'TBA'
 RecoveryNet             Decimal   No        Assessed recovery net figure
 RecoveryVat             Decimal   No        Assessed recovery vat figure
 RecoveryGross            Decimal   No        Assessed recovery gross
                                          figure
 LabourRetail            Decimal   No        Assessed recovery net figure
 LabourDiscountAmount      Decimal   No        Assessed labour discount
                                          figure
 LabourNet               Decimal   No        Assessed labour net figure
 LabourVat               Decimal   No        Assessed labour vat figure
 LabourGross             Decimal   No        Assessed labour gross figure
 MaterialRetail           Decimal   No        Assessed material retail
                                          figure
 MaterialDiscount         Decimal   No        Assessed material discount
                                          figure
 MaterialNet             Decimal   No        Assessed material net figure
 MaterialVat             Decimal   No        Assessed material vat figure
 MaterialGross            Decimal   No        Assessed material gross
                                          figure
 SundryNet               Decimal   No        Assessed sundry net figure
```

## Source page 98

```text
 SundryVat               Decimal   No        Assessed sundry vat figure
 SundryGross             Decimal   No        Assessed sundry gross figure
 EpaNet                  Decimal   No        Assessed epa net figure
 EpaVat                  Decimal   No        Assessed epa vat figure
 EpaGross                Decimal   No        Assessed epa gross figure
 PartsRetail             Decimal   No        Assessed parts retail figure
 PartsDiscountAmount      Decimal   No        Assessed parts discount
                                          figure
 PartsNet                Decimal   No        Assessed parts net figure
 PartsVat                Decimal   No        Assessed parts vat figure
 PartsGross              Decimal   No        Assessed parts gross figure
 OtherRetail             Decimal   No        Assessed other retail figure
 OtherDiscountAmount      Decimal   No        Assessed other discount
                                          figure
 OtherNet                Decimal   No        Assessed other net figure
 OtherVat                Decimal   No        Assessed other vat figure
 OtherGross              Decimal   No        Assessed other gross figure
 TotalRetail             Decimal   No        Assessed total retail figure
 TotalDiscountAmount      Decimal   No        Assessed total discount
                                          figure
 TotalNet                Decimal   No        Assessed total net figure
 TotalVat                Decimal   No        Assessed total vat figure
 TotalGross              Decimal   No        Assessed total gross figure
 ReportText              String    No        Narrative content of the
                                          engineer's report.
 IsSupplementary          Bool     No        Indicates if the report is a
                                          supplementary submission.
 GlassMonth              String    No        This is the date the valuation
                                          of the vehicle
 SupplementaryCount       Integer   No        This is the count of
                                          supplementary completed
 FeeNet                  Decimal   No        This is the last live invoice
                                          nett value
 FeeVat                  Decimal   No        This is the last live invoice vat
                                          value
 FeeGross                Decimal   No        This is the last live invoice
                                          gross value
 ImpactImage             List     No        Directional impact details
                                          (see below).
 Parts                  List     No        List of parts involved (see
                                          table below).
```

## Source page 99

```text
 Files                  List     No        Associated files (photos,
                                          documents, etc.).
```

### Parts Model

```text
 Field           Type      Required    Description
 Description     string     Yes        Part name or description.
 Quantity        int       Yes        Quantity of the part.
 PartType        string     Yes        Type of part. Possible values:
  Blend
  New
  Paint
  R & R
  Repair
  Specialist
  Unknown
  Check
 LabourTime      float     No         Labour time in hours.
 PaintTime       float     No         Paint time in hours.
 MaterialCost     decimal    No         Cost of materials for the part.
 Price           decimal    No         Price of the part.
```

### File Model

```text
 Field     Type          Description
 Name      string        File name.
 Extension string         File extension (e.g., .jpg, .pdf).
 Data      byte[]        Base64-encoded file content.
```

## PDF link annotations

1. Source page 2 -> source page 4.
2. Source page 2 -> source page 6.
3. Source page 2 -> source page 15.
4. Source page 2 -> source page 19.
5. Source page 2 -> source page 22.
6. Source page 2 -> source page 25.
7. Source page 2 -> source page 28.
8. Source page 2 -> source page 28.
9. Source page 2 -> source page 28.
