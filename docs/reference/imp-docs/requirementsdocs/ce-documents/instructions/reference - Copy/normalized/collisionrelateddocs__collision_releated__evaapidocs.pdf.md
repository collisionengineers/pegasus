# Normalized Companion: evaapidocs.pdf

- Raw source: `docs/reference/raw/collisionrelateddocs/collision_releated/evaapidocs.pdf`
- SHA256: `fb6c66f4dcdc2452ef477f79881ffd675d4c14aa077f681a4351638033e9d7d5`
- Extraction method: pypdf-text
- Extraction confidence: partial-review-required
- Blocker: No OCR/layout validation in scaffold pass

This companion is a derivative working copy. The raw source remains the source of truth.

---

## Page 1



## Page 2

1 

1 Sentry API Documentation `0.1.0-alpha.1`.2 
             Table of Contents 
Sentry API Documentation - Version 1.0 
1.         Overview - Purpose and scope of the Sentry API 
2.      Authentication - How to connect using JSON Web Tokens (JWT) 
3.           Instruct Claim 
4.     Claim Location Update 
5.      Authority Status Update 
6.           Submit Note 
7.     Claim Update 
8.    Submit Report 
9.      Retrieve Report  
1.    Retrievable Report List 
2.    Retrieve Report

## Page 3

2 

2 Sentry API Documentation `0.1.0-alpha.1`.2 
     Overview 
The Sentry API facilitates the secure and efficient transmission of data related to 
vehicles involved in Road Traffic Accidents (RTAs) or other forms of damage that 
require engineering assessment, reporting, or authorization. 
This API enables authorized external partners - such as insurers, claims management 
companies, and repair networks - to exchange claim-related data quickly and reliably. 

   Supported Data Types 
The Sentry API supports the transmission and management of the following data types: 
•           Instructions - details and requests relating to the handling of a vehicle claim. 
•    Notes - internal or external commentary related to a claim’s progress or 
observations. 
•     Claim Updates - status changes, progress updates, and lifecycle events. 
•     Location Updates - geolocation or tracking information relevant to the 
vehicle or case. 
•    Repair Authority Updates - communication of repair authorizations, 
amendments, or rejections. 

       Purpose 
The Sentry API is designed to make the process of sending and receiving vehicle claim-
related data: 
• Quick - streamlined data exchange between systems. 
• Efficient - minimal manual intervention or duplication. 
• Simple to Integrate - clear structure and straightforward implementation for any 
external service provider.

## Page 4

3 

3 Sentry API Documentation `0.1.0-alpha.1`.2 
     Authentication 
All Sentry API endpoints require valid authentication using a JSON Web Token (JWT). 
External users must first obtain a token using their assigned Client ID and Client 
Secret credentials. 
Tokens are short-lived and must be refreshed periodically. 

  Authentication Endpoint 
POST /Connect/token 

     Request Format 
The request must use the application/x-www-form-urlencoded content type and 
include the following fields: 
Field Type Required Description 
Client_Id string    Yes Unique identifier assigned to the client. 
Client_Secret string    Yes Secret key assigned to the client for 
authentication. 

       Example Request 
POST /api/Connect/token 
Content-Type: application/x-www-form-urlencoded 

Client_Id=partner123&Client_Secret=secretKeyValue

## Page 5

4 

4 Sentry API Documentation `0.1.0-alpha.1`.2 
     Example Responses 
   Success (200 OK) 
{ 
  "access_token": "JWT string" , 
  "expires_in": 5 
} 
• access_token: The JWT string used for authenticating subsequent requests. 
• expires_in: Time remaining (in minutes) before the token expires (default: 5 
minutes). 
  Failure (401 Unauthorized) 
{ 
  "error": "unauthorized_client" , 
  "error_description": "Invalid Client ID or Secret" 
} 

        Usage 
All subsequent API requests must include the token in the HTTP Authorization header: 
Authorization: Bearer {access_token} 

    Tip: Because tokens expire every 5 minutes, it’s recommended to automate token 
refreshes using your integration middleware or API client. 

  Base API URL 
https://sentry.evasoftware.co.uk/api/

## Page 6

5 

5 Sentry API Documentation `0.1.0-alpha.1`.2 

          Instruct Claim 
Endpoint: POST /Instruction/Inspection 
Purpose: Submit an instruction to the vehicle assessor for a claim. The more complete 
the information provided, the more efficiently assessors can begin actioning the claim. 

   Request Model 
Field Type Required Description 
RequestFrom string 
(max 40) 
   Contact code supplied by us with 
your credentials. 
ExternalRef string    External reference for the claim 
or instruction. (This is your own 
reference) 
Agent string 
(max 10) 
  Agent code, if applicable. 
ProvEng string 
(max 10) 
  Provisional engineer code, if 
applicable. 
VehReg string 
(max 20) 
   Vehicle registration number. 
PrivateHireLicenceNo string 
(max 20) 
  Licence number for private hire 
vehicles. 
PrivateHireLicenceAuth string 
(max 50) 
  Private hire licence authorization 
reference. 
DtPlateExpire DateTime   Expiry date of the vehicle plate. 
ClmNo string 
(max 24) 
   Claim number. 
PolNo string 
(max 24) 
  Policy number. 
InsName string 
(max 60) 
   Name of the insurer.

## Page 7

6 

6 Sentry API Documentation `0.1.0-alpha.1`.2 
TPName string 
(max 60) 
  Name of the third party. 
PrincipalName string 
(max 24) 
  Principal party name. 
VehDesc string 
(max 40) 
  Vehicle description. 
RepairName string 
(max 40) 
  Name of the repairer. 
RepairAdd string 
(max 40) 
  Address of the repairer. 
RepairTown string 
(max 250) 
  Town of the repairer. 
RepairCity string 
(max 250) 
  City of the repairer. 
RepairCounty string 
(max 250) 
  County of the repairer. 
RepairPCode string 
(max 10) 
  Postcode of the repairer. 
RepairTel string 
(max 18) 
  Contact number of the repairer. 
RepairEmail String 
(max 255) 
  Repairer email address. 
RepairerNetworkCode string 
(max 35) 
  Network code for the repairer. 
ApprovedRepairer bool   Whether the repairer is approved. 
EstRec decimal   Estimated recovery cost. 
EstLab decimal   Estimated labour cost. 
EstMat decimal   Estimated materials cost. 
EstPts decimal   Estimated parts cost. 
EstNet decimal   Estimated net cost. 
DtIncident DateTime   Date of the incident.

## Page 8

7 

7 Sentry API Documentation `0.1.0-alpha.1`.2 
InspLocName string 
(max 40) 
  Name of the inspection location. 
InspLocAdd string 
(max 40) 
  Address of the inspection 
location. 
InspLocTown string 
(max 250) 
  Town of inspection location. 
InspLocCity string 
(max 250) 
  City of inspection location. 
InspLocCounty string 
(max 250) 
  County of inspection location. 
InspLocPCode string 
(max 10) 
  Postcode of inspection location. 
InspLocTel string 
(max 18) 
  Contact number for inspection 
location. 
InspLocEmail string 
(max 250) 
  Email of inspection location. 
InspLocCont string 
(max 18) 
  Contact person at inspection 
location. 
InspType string 
(max 25) 
   Type of inspection. Accepted 
values: 
• Vehicle Damage 
Inspection 
• Inspect and Authorise 
• Inspect Only 
• WOP Inspection 
• Rectification work 
• Quality/Audit Inspection 
• Post Repair 
• Low Velocity Inspection 
• Desktop 
• Other 
VehDriveable string 
(max 9) 
  Whether vehicle is driveable. 
Accepted values: 
• Yes 
• No 
• Not Known

## Page 9

8 

8 Sentry API Documentation `0.1.0-alpha.1`.2 
InUse string 
(max 9) 
   Whether the vehicle is in use. 
Accepted values: 
• Yes 
• No 
• Not Known 
ClmAdd string 
(max 40) 
   Claim address. 
ClmTown string 
(max 250) 
  Claim town. 
ClmCity string 
(max 250) 
  Claim city. 
ClmCounty string 
(max 250) 
  Claim county. 
ClmPCode string 
(max 10) 
  Claim postcode. 
ClmTelNo string 
(max 18) 
  Claim contact number. 
ClmAltTelNo string 
(max 30) 
  Alternative claim contact. 
ClmMobileTelNo string 
(max 18) 
  Mobile number of claim contact. 
ClmEmail string 
(max 250) 
  Email of claim contact. 
CoverType string 
(max 5) 
   Type of insurance cover. 
Accepted values: 
• ‘Comp’ - Comprehensive 
• ‘TBA’ - TBA 
• ‘TP’ - Third Party 
• ‘TPFT’ - Third Party, Fire & 
Theft 
• ‘WOP’ - WOP 
Excess string 
(max 10) 
  Policy excess. 
VatStat string 
(max 3) 
  VAT status. Accepted values: 
• Yes

## Page 10

9 

9 Sentry API Documentation `0.1.0-alpha.1`.2 
• No 
• n% 
InOrder string 
(max 17) 
  Order reference. Accepted 
values: 
• Yes 
• No 
SumInsured decimal   Sum insured value. 
Cause string 
(max 250) 
  Cause of incident. 
InstEmail string 
(max 250) 
   Email address to send 
instruction. 
ObviousTotalLoss string 
(max 3) 
  Flag if vehicle is obvious total 
loss. 
Urgent bool   Whether the instruction is urgent. 
WorkType string 
(max 50) 
  Type of work required. 
Roadworthy string 
(max 3) 
  Roadworthy status. Accepted 
values: 
• Yes 
• No 
NotesStr string   Additional notes. 
Files List   Files attached to the instruction. 

File Model 
Field Type Description 
Name string File name. 
Extension string File extension (e.g., .jpg, .pdf). 
Data byte[] Base64-encoded file content.

## Page 11

10 

10 Sentry API Documentation `0.1.0-alpha.1`.2 
   Example ‘Instruct Claim’ JSON Request 
{ 
  "RequestFrom": "Provided Sender Code" , 
  "ExternalRef": "ACME-CLM-2025-00981" , 
  "Agent": "" , 
  "ProvEng": "" , 
  "VehReg": "AB12CDE" , 
  "PrivateHireLicenceNo": "PHL-00921" , 
  "PrivateHireLicenceAuth": "City of London Licensing" , 
  "DtPlateExpire": "2026-01-31T00:00:00Z" , 
  "ClmNo": "CLM20251022001" , 
  "PolNo": "POL998877" , 
  "InsName": "Acme Insurance Group" , 
  "TPName": "John Doe" , 
  "PrincipalName": "Jane Smith" , 
  "PrincipalClmNo": "JS0019" , 
  "VehDesc": "2020 BMW 320d M Sport" , 
  "RepairName": "Example Repairs", 
  "RepairAdd": "15 High Street" , 
  "RepairTown": "Watford" , 
  "RepairCity": "London" , 
  "RepairCounty": "Hertfordshire" , 
  "RepairPCode": "WD17 1AA" , 
  "RepairTel": "02*********", 
  "RepairerNetworkCode": "ELTBOD01" , 
  "ApprovedRepairer": true, 
  "EstRec": 300.00, 
  "EstLab": 500.00,

## Page 12

11 

11 Sentry API Documentation `0.1.0-alpha.1`.2 
  "EstMat": 250.00, 
  "EstPts": 100.00, 
  "EstNet": 1150.00, 
  "DtIncident": "2025-10-15T14:30:00Z" , 
  "InspLocName": "Example Repairers" , 
  "InspLocAdd": "15 High Street" , 
  "InspLocTown": "Watford" , 
  "InspLocCity": "London" , 
  "InspLocCounty": "Hertfordshire" , 
  "InspLocPCode": "WD17 1AA" , 
  "InspLocTel": "02*********", 
  "InspLocEmail": "example@email.com" , 
  "InspLocCont": "Sarah Mills" , 
  "InspType": "Vehicle Damage Inspection" , 
  "VehDriveable": "Yes" , 
  "InUse": "No" , 
  "ClmAdd": "22 Park Avenue" , 
  "ClmTown": "Watford" , 
  "ClmCity": "London" , 
  "ClmCounty": "Hertfordshire" , 
  "ClmPCode": "WD18 7RT" , 
  "ClmTelNo": "02*********", 
  "ClmAltTelNo": "02*********", 
  "ClmMobileTelNo": "07*********", 
  "ClmEmail": "example@email.com" , 
  "CoverType": "COMP" , 
  "Excess": "250" , 
  "VatStat": "20%" ,

## Page 13

12 

12 Sentry API Documentation `0.1.0-alpha.1`.2 
  "InOrder": "Yes" , 
  "SumInsured": 18000.00, 
  "Cause": "Rear-end collision with another vehicle at traffic lights. " , 
  "InstEmail": "example@email.com" , 
  "ObviousTotalLoss": "No" , 
  "Urgent": false, 
  "WorkType": "Accident Damage" , 
  "Roadworthy": "No" , 
  "NotesStr": "Customer requests inspection to be expedited. " , 
  "Files": [ 
    { 
      "Name": "damage_photo_1.jpg", 
      "Extension": " .jpg" , 
      "Data": "base64stringofimage1==" 
    }, 
    { 
      "Name": "policy_document.pdf", 
      "Extension": " .pdf" , 
      "Data": "base64stringofpdf==" 
    } 
  ] 
} 
     Possible Responses 
Status 
Code 
Description 
200    Success - instruction created. Response includes Id for the assessor 
system. 
400   Bad Request - invalid data submitted.

## Page 14

13 

13 Sentry API Documentation `0.1.0-alpha.1`.2 
401   Unauthorized - missing or invalid JWT token. 
409   Conflict - duplicate or conflicting instruction. 
500   Internal Server Error - unexpected error processing request. 
   Response Model 
All response types use the same structure as other Sentry API endpoints. 
Field Type Description 
StatusCode HttpStatusCode The HTTP status returned. 
Message string A description or explanation of the result. 
Id string Populated on success; represents the unique Note 
ID generated in the assessor’s system. 

   Example Standard JSON response 
{ 
  "StatusCode": 200, 
  "Message": "Instruction received successfully. " , 
  "Id": "123" 
}

## Page 15

14 

14 Sentry API Documentation `0.1.0-alpha.1`.2 
    Claim Location Update 
Endpoint: POST /Claim/LocationUpdate 
Purpose: Submit an update to the location associated with a vehicle claim. This helps 
ensure assessors, repairers, and other stakeholders have accurate and up-to-date 
location information for the claim. 

   Request Model 
Field Type Required Description 
EVARef string Conditionally 

Reference to a related file (used in 
combination with VehReg to identify 
the claim). 
VehReg string 
(max 
20) 
Conditionally 

Vehicle registration number. Must be 
included with either FileRef or 
ClmNo. 
ClmNo string 
(max 
24) 
Conditionally 

Claim number. Must be included 
with VehReg. 
LocationName string 
(max 
40) 
  Name of the location being updated. 
E.g. Claimant’s home, Claimant’s 
work etc 
Address string 
(max 
40) 
  Address of the location. 
Town string 
(max 
40) 
  Town of the location. 
City string 
(max 
40) 
  City of the location. 
County string 
(max 
40) 
  County of the location.

## Page 16

15 

15 Sentry API Documentation `0.1.0-alpha.1`.2 
Postcode string 
(max 
10) 
  Postcode of the location. 
Telephone string 
(max 
18) 
  Contact number for the location. 
Email string 
(max 
255) 
  Email address for the location. 
ContactName string 
(max 
20) 
  Name of the contact person at the 
location. 
LocationType string    Type of location. Accepted values:  
• REPAIRER 
• INSPECTION 
• INSURED 
• THIRDPARTY 
ApprovedRepairer bool Conditionally 

Required only if LocationType is 
REPAIRER. Indicates if the repairer is 
approved. 

    Validation Logic 
To identify the target claim, the API will match either of the following field 
combinations: 
• ClmNo + VehReg 
• EvaRef + VehReg 
If the claim cannot be found using these combinations, a 404 response will be returned.

## Page 17

16 

16 Sentry API Documentation `0.1.0-alpha.1`.2 
   Example ‘Location Update’ JSON Request 
{ 
  "VehReg": "AB12CDE" , 
  "ClmNo": "CLM20251022001" , 
  "LocationName": "Example Repairers", 
  "Address": "15 High Street" , 
  "Town": "Watford" , 
  "City": "London" , 
  "County": "Hertfordshire" , 
  "Postcode": "WD17 1AA" , 
  "Telephone": "02*********", 
  "Email": "example@email.com", 
  "ContactName": "Sarah Mills" , 
  "LocationType": "REPAIRER" , 
  "ApprovedRepairer": true 
}

## Page 18

17 

17 Sentry API Documentation `0.1.0-alpha.1`.2 
     Possible Responses 
Status 
Code 
Description 
200    Success - location updated. 
400   Bad Request - invalid data submitted. 
404   Claim not found - the claim could not be located using the data 
provided. 
500   Internal Server Error - unexpected error processing request. 

   Response Model 
All response types use the same structure as other Sentry API endpoints. 
Field Type Description 
StatusCode HttpStatusCode The HTTP status returned. 
Message string A description or explanation of the result. 
Id string Not used in this response 

    Tip: Ensure the correct combination of FileRef/ClmNo with VehReg is included; 
otherwise, a 404 will be returned. Include ApprovedRepairer only for repairer locations.

## Page 19

18 

18 Sentry API Documentation `0.1.0-alpha.1`.2 
     Authority Status Update 
Endpoint: POST /Claim/AuthorityStatusUpdate 
Purpose: Update the repair authority status on a claim. This informs the assessor 
whether the repairer is authorized to begin repairs on the vehicle. 

   Request Model 
Field Type Required Description 
VehReg string 
(max 20) 
Conditionally 

Vehicle registration number. Required with 
either ClmNo or FileRef to identify the 
claim. 
EVARef string Conditionally 

Reference ID for the claim in the external 
system. 
ClmNo string 
(max 24) 
Conditionally 

Claim number. Required with VehReg or 
FileRef to identify the claim. 
AuthStatus string 
(max 50) 
  Current authority status for the repair. 
Accepted values: 
• ‘Yes’ - Repairer is authorized  
• ‘No’ - Repairer is not authorized  
• ‘Other’ - Alternative status  
• ‘After Instruction’ - Authority granted 
after instruction submission 
Comment string   Optional comment or notes regarding the 
authority status update. 
Files List   Optional list of files (documents or images) 
related to the authority update. 

File Model 
Field Type Description 
Name string File name. 
Extension string File extension (e.g., .jpg, .pdf). 
Data byte[] Base64-encoded file content.

## Page 20

19 

19 Sentry API Documentation `0.1.0-alpha.1`.2 
    Validation Logic 
To identify the target claim, the API will match either of the following field 
combinations: 
• ClmNo + VehReg 
• EvaRef + VehReg 
If the claim cannot be found using these combinations, a 404 response will be returned. 

    Example ‘Authority Status Update’ JSON Request 
{ 
  "VehReg": "AB12CDE" , 
  "ClmNo": "CLM20251022001" , 
  "AuthStatus": "Yes" , 
  "Comment": "Repair authorised following assessment review. " , 
  "Files": [ 
    { 
      "Name": "authority_letter.pdf" , 
      "Extension": " .pdf" , 
      "Data": "base64stringofpdf==" 
    } 
  ] 
}

## Page 21

20 

20 Sentry API Documentation `0.1.0-alpha.1`.2 
     Possible Responses 
Status 
Code 
Description 
200    Success - authority status updated. Response includes Id for the 
assessor system. 
400   Bad Request - invalid data submitted. 
404   Claim not found - the claim could not be located using the data 
provided. 
500   Internal Server Error - unexpected error processing request. 

   Response Model 
All response types use the same structure as other Sentry API endpoints. 
Field Type Description 
StatusCode HttpStatusCode The HTTP status returned. 
Message string A description or explanation of the result. 
Id string Not used in this response 

    Tip: Include either ClmNo or FileRef with VehReg to correctly identify the claim. 
Attach relevant supporting files to streamline assessor processing.

## Page 22

21 

21 Sentry API Documentation `0.1.0-alpha.1`.2 
          Submit Note 
Endpoint: POST /Note/SubmitNote 
Description: 
This endpoint allows external partners (such as insurers or claims management 
companies) to send messages containing important information, general queries, or 
supporting details to the assessing company. 
It also supports the submission of related files, helping assessors complete their 
reports more accurately and efficiently. 

   Request Model 
Field Type Required Description 
EvaRef string Conditionally 

Used with VehReg or ClmNo to identify the claim 
within the assessor’s system. 
ClmNo string Conditionally 

The claim number; required when paired with 
VehReg if EvaRef is not supplied. 
VehReg string Conditionally 

Vehicle registration number; required when used 
with ClmNo or EvaRef. 
Note string    The message text or details being submitted. 
Files List   Optional list of file attachments (e.g., images, 
documents, or reports). 

File Model 
Field Type Description 
Name string File name. 
Extension string File extension (e.g., .jpg, .pdf). 
Data byte[] Base64-encoded file content.

## Page 23

22 

22 Sentry API Documentation `0.1.0-alpha.1`.2 
    Validation Logic 
To identify the target claim, the API will match either of the following field 
combinations: 
• ClmNo + VehReg 
• EvaRef + VehReg 
If the claim cannot be found using these combinations, a 404 response will be returned. 

   Example ‘Submit Note’ JSON Request 
{ 
  "ClmNo": "CLM20251022001" , 
  "VehReg": "AB12CDE" , 
  "Note": "Please confirm if additional photos are required before authorisation. " , 
  "Files": [ 
    { 
      "Name": "damage_closeup.jpg" , 
      "Extension": " .jpg" , 
      "Data": "base64stringofimage==" 
    } 
  ] 
}

## Page 24

23 

23 Sentry API Documentation `0.1.0-alpha.1`.2 
     Possible Responses 
HTTP Code Description 
200 - Success The note and any attached files were received successfully. 
404 - Not Found No matching claim could be found for the provided 
reference details. 
409 - Conflict A conflict occurred (e.g., duplicate submission or invalid 
state). 
400 - Bad Request The request model was invalid or missing required fields. 
500 - Internal Server 
Error 
An unexpected server error occurred while processing the 
note. 

   Response Model 
All response types use the same structure as other Sentry API endpoints. 
Field Type Description 
StatusCode HttpStatusCode The HTTP status returned. 
Message string A description or explanation of the result. 
Id string Not used in this response

## Page 25

24 

24 Sentry API Documentation `0.1.0-alpha.1`.2 
    Claim Update 
Endpoint: POST /Claim/Update 
Description: 
This endpoint allows external partners to submit updates to existing claims. 
Currently, the endpoint supports the updating of the Excess and Claimant VAT Status 
fields, ensuring the assessor has the latest financial information to proceed efficiently 
with claim evaluation. 
    Note: The update model can be extended in the future to include additional 
updatable claim fields on request. 

   Request Model 
Field Type Required Description 
FileRef string Conditionally 

Internal file reference used by the 
assessor. Required with VehReg if ClmNo 
is not provided. 
VehReg string Conditionally 

Vehicle registration number. Used with 
either FileRef or ClmNo to locate the 
claim. 
ClmNo string Conditionally 

The claim number. Required with VehReg if 
FileRef is not supplied. 
Excess string 
(max 10) 
  Updated claim excess value (e.g. “250” or 
“£250”). 
ClmVatStat String 
(max 3) 
  VAT status. Accepted values: 
• Yes 
• No 
• n%

## Page 26

25 

25 Sentry API Documentation `0.1.0-alpha.1`.2 
    Validation Logic 
To locate the target claim, one of the following field combinations must be provided: 
• FileRef + VehReg 
• ClmNo + VehReg 
If a valid claim cannot be found using these combinations, a 404 response will be 
returned. 

   Example ‘Claim Update’ JSON Request 
{ 
  "VehReg": "AB12CDE" , 
  "ClmNo": "CLM20251022001" , 
  "Excess": "350" , 
  "ClmVatStat": "20%" 
}

## Page 27

26 

26 Sentry API Documentation `0.1.0-alpha.1`.2 
     Possible Responses 
HTTP Code Description 
200 - Success The claim has been successfully updated. 
404 - Not Found No claim record matched the provided identifying data. 
400 - Bad Request The submitted data was invalid or incomplete. 
500 - Internal Server Error An error occurred while processing the update request. 

   Response Model 
Same structure used across all Sentry API endpoints. 
Field Type Description 
StatusCode HttpStatusCode The HTTP status code returned. 
Message string A brief description of the result. 
Id string Not used in this response

## Page 28

27 

27 Sentry API Documentation `0.1.0-alpha.1`.2 
   Submit Report 
Endpoint: POST / Report/SubmitReport 
Description: 
This endpoint is used by external engineers or assessors to submit a comprehensive 
vehicle inspection report to the EVA System. 
The report includes all available inspection, vehicle, valuation, and repair data - along 
with any supporting documentation or images. 

   Request Model 
Field Type Required Description 
InspectEngineer string 
(max 12) 
   The name or code of the 
inspecting engineer. 
EvaRef string    (if 
ClmNo is 
used with 
it) 
The EVA reference number 
identifying the assessment. 
VehReg string    Vehicle registration. 
ClmNo string    Claim number. 
InsuredName string   Name of the insured party. 
ThirdPartyName string   Name of the third party 
involved (if applicable). 
ClaimType string   Type of claim. Accepted 
values: 
• Cash-In-Lieu 
• Diminution 
• Other 
• Post Repair 
• Repair 
• T/Loss 
• Repudiation 
IncidentDate DateTime    Date of the incident. 
InspectionDate DateTime   Date the vehicle was 
inspected.

## Page 29

28 

28 Sentry API Documentation `0.1.0-alpha.1`.2 
RepairsAuthorisedDate DateTime   Date repairs were authorised. 
SuppAuthorisedDate DateTime   Date supplementary 
authorisation was given. 
EstimateRecievedDate DateTime   Date the repair estimate was 
received. 
ReportDate DateTime    Date the report was created 
or submitted. 
RepairerEstimateAgreed String   Indicates if the repairer’s 
estimate was agreed. 
Accepted values: 
• Yes 
• No 
• N/A 
InspLocName string 
(max 40) 
  Name of the inspection 
location. 
InspLocAdd string 
(max 40) 
  Address line of the 
inspection location. 
InspLocTown string 
(max 250) 
  Town of the inspection 
location. 
InspLocCity string 
(max 250) 
  City of the inspection 
location. 
InspLocCounty string 
(max 250) 
  County of the inspection 
location. 
InspLocPCode string 
(max 10) 
  Postcode of the inspection 
location. 
InspLocTel string 
(max 18) 
  Telephone number of the 
inspection location. 
InspLocEmail string 
(max 250) 
  Email of the inspection 
location. 
InspLocCont string 
(max 18) 
  Contact name at inspection 
location. 
InspectionType string 
(max 25) 
   Type of inspection 
performed. Accepted values:

## Page 30

29 

29 Sentry API Documentation `0.1.0-alpha.1`.2 
• Vehicle Damage 
Inspection 
• Rectification Work 
• Quality/Audit Inspection 
• Low Velocity Inspection 
• Desktop 
• Other 
• Cold Call 
• Consistency 
• Images Only 
• Forensic 
ReportType string 
(max 27) 
  Type of report. Accepted 
values: 
• Cold Call Report 
• Desktop Report 
• Full Report 
• Letter 
• Post-Inspection 
• Post-Repair Audit 
• Post-Repair 
Complaint 
• Roadworthy 
• Simple Low Speed 
Inspection 
• Small Claim 
• Telephone 
RepairDuration string 
(max 10) 
  Estimated repair duration in 
days. 
VehRoadWorthy string 
(max 10) 
  Indicates if the vehicle is 
roadworthy. Accepted 
values: 
• Yes 
• No 
• N/A 
• Subject To 
VehNotRoadWorthyReason string 
(max 250) 
  Reason vehicle is not 
roadworthy. 
VehDriveable string 
(max 9) 
  Indicates if the vehicle is 
drivable. Accepted values: 
• Yes 
• No

## Page 31

30 

30 Sentry API Documentation `0.1.0-alpha.1`.2 
• Not Known 
VehInUse string 
(max 9) 
  Indicates if the vehicle is still 
in use. Accepted values: 
• Yes 
• No 
• Not Known 
VehAreaOfRepair string 
(max 250) 
  General area of repair. 
LightCondAtInsp string 
(max 250) 
  Lighting conditions at 
inspection. 
InspCondition string 
(max 250) 
  General condition at 
inspection. 
TempRepairs string 
(max 3) 
  Indicates if temporary repairs 
were done. Accepted values: 
• Yes 
• No 
• N/A 
TempRepairsHow string 
(max 250) 
  Description of temporary 
repairs. 
TempRepairsCost decimal   Cost of temporary repairs. 
ValMarket decimal   Market value of the vehicle. 
ValRetail decimal   Retail value. 
ValTrade decimal   Trade-in value. 
ValMid decimal   Mid-point valuation. 
ValSalvage decimal   Salvage value. 
ValDisposal decimal   Disposal value. 
ValPrivate decimal   Private sale value. 
ValResearch string   Supporting valuation 
research text. Accepted 
values: 
• Internet

## Page 32

31 

31 Sentry API Documentation `0.1.0-alpha.1`.2 
• Magazines 
• Main Dealer 
• Other 
MileageAdjust decimal   Adjustment for mileage. 
ConditionAdjust decimal   Adjustment for condition. 
OtherAdjust decimal   Other valuation adjustments. 
ReportDelayed bool   Indicates if report 
submission was delayed. 
ReportDelayedReason string 
(max 35) 
  Reason for delay. Accepted 
values: 
• Awaiting estimate - 
Owner 
• Awaiting estimate - 
Repairer 
• Client requested 
inspection date 
• Getting parts prices 
• Problem contacting 
owner 
• Vehicle being stripped 
• Awaiting images 
• Repairer carrying out pre 
strip 
• Customer unresponsive 
• Repairer unresponsive 
• Awaiting further info(See 
comments) 
• Vehicle not available 
• Valuation dispute 
PresentAtInsp string 
(max 250) 
  Individuals present during 
the inspection. 
PrivateHireLicNo string 
(max 20) 
  Private hire licence number. 
EngineStarts string 
(max 9) 
  Indicates if engine starts. 
Accepted values: 
• Yes 
• No 
• Not Known

## Page 33

32 

32 Sentry API Documentation `0.1.0-alpha.1`.2 
EngineFailReason string 
(max 250) 
  Reason for engine failure (if 
applicable). 
DoorsSecured string 
(max 9) 
  Indicates if vehicle doors are 
secured. Accepted values: 
• Yes 
• No 
• Not Known 
DoorsNotSecuredReason string 
(max 250) 
  Reason vehicle doors are not 
secured. 
Diminution string 
(max 3) 
  Indicates if vehicle has a 
diminution in value. 
Accepted values: 
• Yes 
• No 
• N/A 
DiminutionPAVPercent decimal   Diminution percentage of 
pre-accident value. 
ClaimantVatLiability decimal   VAT liability for the claimant. 
ClaimantTotLiability decimal   Total liability of the claimant. 
RepairDelays bool   Indicates if there were repair 
delays. 
RepairDelaysReason string 
(max 50) 
  Reason for repair delays. 
Excess string 
(max 10) 
  Claim excess amount. 
ClaimantVatStatus string 
(max 8) 
  Claimant VAT registration 
status. Accepted values: 
• Yes 
• No 
• n% 
AuthorityStatus string 
(max 17) 
  Authority status for repair. 
Accepted values: 
• Yes 
• No

## Page 34

33 

33 Sentry API Documentation `0.1.0-alpha.1`.2 
Cause string 
(max 250) 
  Cause of the damage. 
SumInsured decimal   Total sum insured. 
Settled string 
(max 21) 
  Indicates if the claim is 
settled. Accepted values: 
• Yes 
• No 
• Disputed 
• N/A 
• Subject To 
DtAttemptedSettle DateTime   Date settlement was 
attempted. 
SalLocName string 
(max 40) 
  Name of salvage company 
SalLocAdd string 
(max 40) 
  Salvage company first line of 
address 
SalLocTown string 
(max 250) 
  Salvage company town 
SalLocCity string 
(max 250) 
  Salvage company city 
SalLocCounty string 
(max 250) 
  Salvage company county 
SalLocPCode string 
(max 10) 
  Salvage company postcode 
SalLocTelNo string 
(max 18) 
  Salvage company contact 
number 
SalLocEmail string 
(max 250) 
  Salvage company email 
address 
ABICat string   Salvage category. Accepted 
values: 
• A-Scrap  
• B-Breaker  
• C-Rep Costs > PAMV  
• D-Constructive T/L 
• X-Other 
• S-Structural  
• N-Non-Structural 
• No category - 
breaker/scrap 
OwnerRetainSalvage Bool   Is the owner retaining the 
salvage? 
FinalStorage Decimal   Final storage costs

## Page 35

34 

34 Sentry API Documentation `0.1.0-alpha.1`.2 
CurrentStore Decimal   Current storage costs 
SalvageRef string 
(max 8) 
  Salvage reference 
DtSalvageMoved Datetime   Date salvage was moved 
StorageRate Decimal   Daily storage rate 
Inherited String 
(max 10) 
  Inherited charges 
DtStoreStart DateTime   Date storage of vehicle 
started 
SalvageMoved Bool   Was the salvage moved? 
VATOnSalv String   Should vat be applied to 
salvage? Accepted values: 
• Yes 
• No 
VATOnOther Bool   Should vat be applied to 
other? 
VATOnPaint Bool   Should vat be applied to 
paint? 
VATOnParts Bool   Should vat be applied to 
parts? 
Rectification String   Rectification work required 
RectificationIssues String   Rectification issues 
VehImpactAsDescribed bool   Is the impact as described? 
GlassCode string 
(max 6) 
  Glass’s guide code 
GlassModelId String 
(max 9) 
  Glass’s model Id 
VehClass String 
(max 10) 
  Vehicle class. Accepted 
values: 
• Agric 
• Car 
• Caravan 
• HGV 
• LCV 
• M/Cycle 
• Misc 
• Plant 
• PSV 
• Trailer 
• Tractor 
• Machinery 
VehMake String 
(max 20) 
  Vehicle Make 
VehModel String 
(max 20) 
  Vehicle Model

## Page 36

35 

35 Sentry API Documentation `0.1.0-alpha.1`.2 
VehDescription String 
(max 40) 
  Vehicle Make & Model 
VehEngineCC String 
(max 8) 
  Engine CC 
VehEngineBHP String 
(max 4) 
  Engine BHP 
VehBody String 
(max 14) 
  Vehicle body type. Accepted 
values: 
• 2 Door 
• 3 Door 
• 4 Door 
• 5 Door 
• 6 Door 
• Agricultural 
• Articulated 
• Contractors 
• P .S. V . 
• Private 
• Rigid 
• Trailer 
VehBodyDescription String 
(max 30) 
  Vehicle Body Description. 
Accepted values: 
• 3 Wheel Scooter 
• Access Platform 
• Ambulance 
• ATV 
• Backhoe Digger 
• Baler 
• Beavertail 
• Bicycle  
• Box Lorry 
• Box Trailer 
• Box-Van 
• Bubble Car 
• Bus 
• Caged Tipper 
• Camper Van 
• Car Derived Van 
• Caravan 
• Catering Unit 
• Cement Mixer 
• Cherry Picker 
• Coach 
• Combine 
• Compressor

## Page 37

36 

36 Sentry API Documentation `0.1.0-alpha.1`.2 
• Convertible 
• Coupe 
• Crane 
• Crew-cab 
• Crewcab Tipper 
• Crop Fertilizer 
• Crop Sprayer 
• Curtainsider 
• Digger 
• Double Decker Bus 
• Drophead 
• Dropside 
• Dumper 
• Estate 
• Excavator 
• Fire Engine 
• Flatbed 
• Food Truck 
• Forager 
• Fork-Lift 
• Front End Loader 
• Fuel Tanker 
• Grab Truck 
• Gritter 
• Hatchback 
• Hay Turner 
• Hedgetrimmer 
• Hook Loader 
• Horsebox 
• Ice Cream Van 
• Kombi 
• Light Van 
• Limousine 
• Livestock Carrier 
• Low Loader 
• Luton Van 
• M.P .V . 
• Machinery 
• Mini Excavator 
• Minibus 
• Mobile Home 
• Motor Home 
• Motorcycle 
• Mower 
• Muck Spreader

## Page 38

37 

37 Sentry API Documentation `0.1.0-alpha.1`.2 
• Omnibus 
• Other 
• P .S. V . 
• Panel Van 
• Pantechnicon 
• Parkhome 
• Pickup 
• Plough 
• Quad-Bike 
• Rake 
• Recovery Truck 
• Refrigerated Box Lorry 
• Refrigerated Box Van 
• Refrigerated Van 
• Refuse Collection 
Vehicle 
• Road Sweeper 
• Rotavator 
• Saloon 
• Scooter 
• Self Propelled Sprayer 
• Silage Trailer 
• Skelatal 
• Skid Steer 
• Skip Loader 
• Static 
• Station Wagon 
• Tandem Trailer 
• Tanker 
• Tarmac Hot Box 
• Taxi 
• Tedder 
• Telehandler 
• Tipper 
• Toilet Block 
• Touring 
• Tractor 
• Tractor Unit 
• Trailer 
• Tri Axle Trailer 
• Trike 
• Ute 
• Vacuum Tanker 
• Van 
• Vehicle Transporter

## Page 39

38 

38 Sentry API Documentation `0.1.0-alpha.1`.2 
• Wagon 
• Welfare Unit 

VehFirstRegistered String 
(max 10) 
  Month & Year vehicle was 
first registered. Format: 
MMM/yyyy 
VehOdometer String 
(max 10) 
  Odometer reading 
VehOdometerUnit string   Odometer reading units. 
Accepted values: 
• Hrs 
• Km 
• Miles 
VehColour String 
(max 40) 
  Colour of vehicle 
VehCondition String 
(max 15) 
  Condition of vehicle 
VehBrakes String 
(max 250) 
  Condition of brakes on 
vehicle 
VehSteering String 
(max 250) 
  Condition of steering on 
vehicle 
VehInCarEntertainment String 
(max 40) 
  Any in car entertainment in 
the vehicle 
VehExtras String 
(max 40) 
  Any extras on the vehicle 
VehMods String 
(max 40) 
  Any additional mods of the 
vehicle 
VehTaxExpire String 
(max 10) 
  Tax expiration date. Format: 
MMM/yyyy 
VehVin String 
(max 22) 
  Vehicle Identification 
Number (VIN) 
VehRhfTyre String 
(max 11) 
  Tyre tread depths on right-
hand front tyre 
VehLhfTyre String 
(max 11) 
  Tyre tread depths on left-
hand front tyre 
VehRhrTyre String 
(max 11) 
  Tyre tread depths on right-
hand rear tyre 
VehLhrTyre String 
(max 11) 
  Tyre tread depths on left-
hand rear tyre 
VehSpareTyre String 
(max 11) 
  Tyre tread depths on spare 
tyre 
VehRhfSBelt String 
(max 12) 
  Status of right-hand front 
seat belt. Accepted values: 
• Damaged 
• Deployed 
• Fitted

## Page 40

39 

39 Sentry API Documentation `0.1.0-alpha.1`.2 
• No Access 
• None Fitted 
• Not Tested 
VehLhfSBelt String 
(max 12) 
  Status of left-hand front seat 
belt. Accepted values: 
• Damaged 
• Deployed 
• Fitted 
• No Access 
• None Fitted 
• Not Tested 
VehRhrSBelt String 
(max 12) 
  Status of right-hand rear seat 
belt. Accepted values: 
• Damaged 
• Deployed 
• Fitted 
• No Access 
• None Fitted 
• Not Tested 
VehLhrSBelt String 
(max 12) 
  Status of left-hand rear seat 
belt. Accepted values: 
• Damaged 
• Deployed 
• Fitted 
• No Access 
• None Fitted 
• Not Tested 
VehCenSBelt String 
(max 12) 
  Status of center seat belt. 
Accepted values: 
• Damaged 
• Deployed 
• Fitted 
• No Access 
• None Fitted 
• Not Tested 
VehAirBagDeployed String 
(max 25) 
  Did the airbags deploy? 
VehTransmission String 
(max 20) 
  Vehicle transmission. 
Accepted values:

## Page 41

40 

40 Sentry API Documentation `0.1.0-alpha.1`.2 
• Automatic 
• CVT 
• DSG 
• EGS 
• Manual 
• N/A 
• Semi-Auto 
• Sequential 
• Sequential Automatic 
• Sequential Manual 
• Unknown 
VehFuelType String 
(max 20) 
  Vehicle fuel type. Accepted 
values: 
• Biofuel 
• Compressed Natural 
Gas 
• Diesel 
• Dual 
• Electric 
• Hybrid 
• Hydrogen 
• LPG 
• Petrol 
DamageSeverity String 
(max 18) 
  First damage severity. 
Accepted Values: 
• Very Heavy  
• Heavy 
• Moderate to Heavy 
• Moderate  
• Light to Moderate 
• Light 
• Very Light 
DamageType String 
(max 22) 
  First damage type. 
Accepted values: 
• Accidental Damage 
• Chemical 
Contamination 
• Collision/Impact 
• Electrical Failure 
• Fire (Cause Unknown) 
• Fire (Electrical) 
• Fire (External) 
• Fire (Fuel) 
• Ground Damage 
• Mechanical Failure

## Page 42

41 

41 Sentry API Documentation `0.1.0-alpha.1`.2 
• No Damage 
• Rodent Damage 
• Storm Damage 
• Theft or Attempted 
• Unrecovered Theft 
• Vandalism 
• Water/Flood/Damp 
DamageArea String 
(max 8) 
  First damage area. Accepted 
values: 
• Front 
• LH Front 
• LH Rear 
• LH Side 
• Rear 
• RH Front 
• RH Rear 
• RH Side 
DamageLocation String 
(max 20) 
  First damage location. 
Accepted values: 
• Bonnet 
• Boot 
• Brake Lever 
• Bumper 
• Complete Vehicle 
• Door 
• Engine Bay 
• Fairing 
• Foot Peg 
• Frame 
• Front Fork 
• Front Panel 
• Front to Rear 
• Gear Lever 
• Handlebars 
• Interior 
• Luggage 
Compartment 
• Mirror 
• Mud Guard 
• Panniers 
• Quarter Panel 
• Rear Panel 
• Rear to Front 
• Roof 
• Swing Arm

## Page 43

42 

42 Sentry API Documentation `0.1.0-alpha.1`.2 
• Tyre 
• Underside 
• Wheel 
• Wing 
DamageSeverity2 String 
(max 18) 
  Second damage severity. 
Accepted Values: 
• Very Heavy  
• Heavy 
• Moderate to Heavy 
• Moderate  
• Light to Moderate 
• Light 
• Very Light 
DamageType2 String 
(max 22) 
  Second damage type. 
Accepted values: 
• Accidental Damage 
• Chemical 
Contamination 
• Collision/Impact 
• Electrical Failure 
• Fire (Cause Unknown) 
• Fire (Electrical) 
• Fire (External) 
• Fire (Fuel) 
• Ground Damage 
• Mechanical Failure 
• No Damage 
• Rodent Damage 
• Storm Damage 
• Theft or Attempted 
• Unrecovered Theft 
• Vandalism 
• Water/Flood/Damp 
DamageArea2 String 
(max 8) 
  Second damage area. 
Accepted values: 
• Front 
• LH Front 
• LH Rear 
• LH Side

## Page 44

43 

43 Sentry API Documentation `0.1.0-alpha.1`.2 
• Rear 
• RH Front 
• RH Rear 
• RH Side 
DamageLocation2 String 
(max 20) 
  Second damage location. 
Accepted values: 
• Bonnet 
• Boot 
• Brake Lever 
• Bumper 
• Complete Vehicle 
• Door 
• Engine Bay 
• Fairing 
• Foot Peg 
• Frame 
• Front Fork 
• Front Panel 
• Front to Rear 
• Gear Lever 
• Handlebars 
• Interior 
• Luggage 
Compartment 
• Mirror 
• Mud Guard 
• Panniers 
• Quarter Panel 
• Rear Panel 
• Rear to Front 
• Roof 
• Swing Arm 
• Tyre 
• Underside 
• Wheel 
• Wing 
DamageSeverity3 String 
(max 18) 
  Third damage severity. 
Accepted Values: 
• Very Heavy

## Page 45

44 

44 Sentry API Documentation `0.1.0-alpha.1`.2 
• Heavy 
• Moderate to Heavy 
• Moderate  
• Light to Moderate 
• Light 
• Very Light 
DamageType3 String 
(max 22) 
  Third damage type. 
Accepted values: 
• Accidental Damage 
• Chemical 
Contamination 
• Collision/Impact 
• Electrical Failure 
• Fire (Cause Unknown) 
• Fire (Electrical) 
• Fire (External) 
• Fire (Fuel) 
• Ground Damage 
• Mechanical Failure 
• No Damage 
• Rodent Damage 
• Storm Damage 
• Theft or Attempted 
• Unrecovered Theft 
• Vandalism 
• Water/Flood/Damp 
DamageArea3 String 
(max 8) 
  Third damage area. Accepted 
values: 
• Front 
• LH Front 
• LH Rear 
• LH Side 
• Rear 
• RH Front 
• RH Rear 
• RH Side 
DamageLocation3 String 
(max 20) 
  Third damage location. 
Accepted values:

## Page 46

45 

45 Sentry API Documentation `0.1.0-alpha.1`.2 
• Bonnet 
• Boot 
• Brake Lever 
• Bumper 
• Complete Vehicle 
• Door 
• Engine Bay 
• Fairing 
• Foot Peg 
• Frame 
• Front Fork 
• Front Panel 
• Front to Rear 
• Gear Lever 
• Handlebars 
• Interior 
• Luggage 
Compartment 
• Mirror 
• Mud Guard 
• Panniers 
• Quarter Panel 
• Rear Panel 
• Rear to Front 
• Roof 
• Swing Arm 
• Tyre 
• Underside 
• Wheel 
• Wing 
RepairName String 
(max 40) 
  Repairer name 
RepairAdd String 
(max 40) 
  Repairer address line 1 
RepairTown String 
(max 250) 
  Repairer town 
RepairCity String 
(max 250) 
  Repairer city 
RepairCounty String 
(max 250) 
  Repairer county

## Page 47

46 

46 Sentry API Documentation `0.1.0-alpha.1`.2 
RepairPCode String 
(max 10) 
  Repairer postcode 
RepairTel String 
(max 18) 
  Repairer contact number 
RepairEmail String 
(max 250) 
  Repairer email 
RepairCont String 
(max 20) 
  Repairer contact name 
RepairerVatStatus String 
(max 3) 
  Repairer Vat Status. 
Accepted values: 
• Yes 
• No 
• N% 
EstimatedRecovery Decimal   Estimated recovery cost 
EstimatedLabour Decimal   Estimated labour cost 
EstimatedMaterials Decimal   Estimated materials cost 
EstimatedSundries Decimal   Estimated sundries cost 
EstimatedEpa Decimal   Estimated epa cost 
EstimatedParts Decimal   Estimated parts cost 
EstimatedOtherTotal Decimal   Estimated other cost 
EstimatedNet Decimal   Estimated net cost 
EstimatedVat Decimal   Estimated vat cost 
EstimatedGross Decimal   Estimated gross cost 
EstimatedPartsDiscount Decimal   Estimated parts discount 
EstimatedLabourRate Decimal   Estimated labour rate 
LabourRate Decimal   Assessed labour rate 
RecoveryDiscountPercent Decimal   Assessed recovery discount 
percentage 
LabourDiscountPercent Decimal   Assessed labour discount 
percentage 
MaterialDiscountPercent Decimal   Assessed material discount 
percentage 
SundryDiscountPercent Decimal   Assessed sundry discount 
percentage 
EPADiscountPercent Decimal   Assessed epa discount 
percentage 
PartsDiscountPercent Decimal   Assessed parts discount 
percentage 
RecoverySavingsNet Decimal   Recovery savings net 
between estimated and 
assessed recovery charge 
RecoverySavingsGross Decimal   Recovery savings gross 
between estimated and 
assessed recovery charge

## Page 48

47 

47 Sentry API Documentation `0.1.0-alpha.1`.2 
LabourSavingsNet Decimal   Labour savings net between 
estimated and assessed 
labour charge 
LabourSavingsGross Decimal   Labour savings gross 
between estimated and 
assessed labour charge 
MaterialSavingsNet Decimal   Materials savings net 
between estimated and 
assessed materials charge 
MaterialSavingsGross Decimal   Materials savings gross 
between estimated and 
assessed materials charge 
PartsSavingsNet Decimal   Parts savings net between 
estimated and assessed 
parts charge 
PartsSavingsGross Decimal   Parts savings gross between 
estimated and assessed 
parts charge 
OtherSavingsNet Decimal   Other savings net between 
estimated and assessed 
other charge 
OtherSavingsGross Decimal   Other savings gross between 
estimated and assessed 
other charge 
DeleteSavingsNet Decimal   Deleted estimate items 
savings net between 
estimated and assessed 
DeleteSavingsGross Decimal   Deleted estimate items 
savings gross between 
estimated and assessed 
TotalSavingsNet Decimal   Total savings net between 
estimated and assessed 
TotalSavingsGross Decimal   Total savings gross between 
estimated and assessed 
VatSavings Decimal   Vat savings between 
estimated and assessed 
PrincipalSavings Decimal   Savings for principal between 
estimated assessed 
BettermentRetail Decimal   Betterment retail figure 
BettermentDiscount Decimal   Betterment discount figure 
BettermentNet Decimal   Betterment net figure 
BettermentVat Decimal   Betterment vat figure 
BettermentGross Decimal   Betterment gross figure 
TotalExcess Decimal   Total excess 
ContractRep Decimal   Contract Repair charge 
Reserve Decimal   Reserve figure

## Page 49

48 

48 Sentry API Documentation `0.1.0-alpha.1`.2 
Balance Decimal   Balance 
RepairSettlement Decimal   Repair settlement figure 
OriginalMaterialNet Decimal   Original material net figure 
RecoveryRetail String   Retail recovery costs. 
Accepts the cost or the value 
‘TBA’ 
RecoveryNet Decimal   Assessed recovery net figure 
RecoveryVat Decimal   Assessed recovery vat figure 
RecoveryGross Decimal   Assessed recovery gross 
figure 
LabourRetail Decimal   Assessed recovery net figure 
LabourDiscountAmount Decimal   Assessed labour discount 
figure 
LabourNet Decimal   Assessed labour net figure 
LabourVat Decimal   Assessed labour vat figure 
LabourGross Decimal   Assessed labour gross figure 
MaterialRetail Decimal   Assessed material retail 
figure 
MaterialDiscount Decimal   Assessed material discount 
figure 
MaterialNet Decimal   Assessed material net figure 
MaterialVat Decimal   Assessed material vat figure 
MaterialGross Decimal   Assessed material gross 
figure 
SundryNet Decimal   Assessed sundry net figure 
SundryVat Decimal   Assessed sundry vat figure 
SundryGross Decimal   Assessed sundry gross figure 
EpaNet Decimal   Assessed epa net figure 
EpaVat Decimal   Assessed epa vat figure 
EpaGross Decimal   Assessed epa gross figure 
PartsRetail Decimal   Assessed parts retail figure 
PartsDiscountAmount Decimal   Assessed parts discount 
figure 
PartsNet Decimal   Assessed parts net figure 
PartsVat Decimal   Assessed parts vat figure 
PartsGross Decimal   Assessed parts gross figure 
OtherRetail Decimal   Assessed other retail figure 
OtherDiscountAmount Decimal   Assessed other discount 
figure 
OtherNet Decimal   Assessed other net figure 
OtherVat Decimal   Assessed other vat figure 
OtherGross Decimal   Assessed other gross figure 
TotalRetail Decimal   Assessed total retail figure

## Page 50

49 

49 Sentry API Documentation `0.1.0-alpha.1`.2 
TotalDiscountAmount Decimal   Assessed total discount 
figure 
TotalNet Decimal   Assessed total net figure 
TotalVat Decimal   Assessed total vat figure 
TotalGross Decimal   Assessed total gross figure 
ReportText String   Narrative content of the 
engineer’s report. 
IsSupplementary Bool   Indicates if the report is a 
supplementary submission. 
ImpactImage List   Directional impact details 
(see below). 
Parts List   List of parts involved (see 
table below). 
Files List   Associated files (photos, 
documents, etc.).

## Page 51

50 

50 Sentry API Documentation `0.1.0-alpha.1`.2 
   Example ‘Report’ JSON Request 
{ 
    "InspectEngineer": "DEMOENG1" , 
    "EvaRef": "123456" , 
    "VehReg": "AB12CDE" , 
    "ClmNo": "CLM987654" , 
    "InsuredName": "John Smith" , 
    "ThirdPartyName": "Jane Doe" , 
    "ClaimType": "Repair" , 
    "IncidentDate": "2025-09-15T00:00:00Z" , 
    "InspectionDate": "2025-09-18T00:00:00Z" , 
    "RepairsAuthorisedDate": "2025-09-20T00:00:00Z" , 
    "SuppAuthorisedDate": "2025-09-22T00:00:00Z" , 
    "EstimateRecievedDate": "2025-09-17T00:00:00Z" , 
    "ReportDate": "2025-09-23T00:00:00Z" , 
    "RepairerEstimateAgreed": "Yes" , 
    "InspLocName": "AutoFix Garage" , 
    "InspLocAdd": "123 Main Street" , 
    "InspLocTown": "Nottingham" , 
    "InspLocCity": "Nottingham" , 
    "InspLocCounty": "Nottinghamshire" , 
    "InspLocPCode": "NG1 4AA" , 
    "InspLocTel": "01151234567" , 
    "InspLocEmail": "info@autofixgarage.co.uk" , 
    "InspLocCont": "Mark Taylor" , 
    "InspectionType": "Vehicle Damage Inspection", 
    "ReportType": "Full Report" , 
    "RepairDuration": "5 Days" ,

## Page 52

51 

51 Sentry API Documentation `0.1.0-alpha.1`.2 
    "VehRoadWorthy": "No" , 
    "VehNotRoadWorthyReason": "Front-end structural damage" , 
    "VehDriveable": "No" , 
    "VehInUse": "No" , 
    "VehAreaOfRepair": "Front Bumper, Bonnet, Left Headlamp" , 
    "LightCondAtInsp": "Daylight" , 
    "InspCondition": "Vehicle assessed at garage under natural light" , 
    "TempRepairs": "No" , 
    "TempRepairsHow": "" , 
    "TempRepairsCost": 0.0, 
    "ValMarket": 12500.0, 
    "ValRetail": 13000.0, 
    "ValTrade": 11000.0, 
    "ValMid": 12000.0, 
    "ValSalvage": 2000.0, 
    "ValDisposal": 150.0, 
    "ValPrivate": 11800.0, 
    "ValResearch": "Internet" , 
    "MileageAdjust": -200.0, 
    "ConditionAdjust": -300.0, 
    "OtherAdjust": 0.0, 
    "ReportDelayed": false, 
    "ReportDelayedReason": "" , 
    "PresentAtInsp": "Repairer representative and engineer present" , 
    "PrivateHireLicNo": "" , 
    "EngineStarts": "Yes" , 
    "EngineFailReason": "" , 
    "DoorsSecured": "Yes" ,

## Page 53

52 

52 Sentry API Documentation `0.1.0-alpha.1`.2 
    "DoorsNotSecuredReason": "" , 
    "Diminution": "No" , 
    "DiminutionPAVPercent": 0.0, 
    "ClaimantVatLiability": 20.0, 
    "ClaimantTotLiability": 300.0, 
    "RepairDelays": false, 
    "RepairDelaysReason": "" , 
    "Excess": "250" , 
    "ClaimantVatStatus": "20%" , 
    "AuthorityStatus": "Yes" , 
    "Cause": "Rear-ended another vehicle" , 
    "SumInsured": 15000.0, 
    "Settled": "No" , 
    "DtAttemptedSettle": "2025-09-28T00:00:00Z" , 
    "SalLocName": "ABC Salvage Ltd" , 
    "SalLocAdd": "1 Recovery Way" , 
    "SalLocTown": "Leeds" , 
    "SalLocCity": "Leeds" , 
    "SalLocCounty": "Yorkshire" , 
    "SalLocPCode": "LS1 4BB" , 
    "SalLocTelNo": "01134567890" , 
    "SalLocEmail": "contact@abcsalvage.co.uk" , 
    "ABICat": "N" , 
    "OwnerRetainSalvage": false, 
    "FinalStorage": 250.0, 
    "CurrentStore": 100.0, 
    "SalvageRef": "SAL20251022" , 
    "DtSalvageMoved": "2025-09-25T00:00:00Z" ,

## Page 54

53 

53 Sentry API Documentation `0.1.0-alpha.1`.2 
    "StorageRate": 20.0, 
    "Inherited": "No" , 
    "DtStoreStart": "2025-09-15T00:00:00Z" , 
    "SalvageMoved": true, 
    "VATOnSalv": "Yes" , 
    "VATONother": false, 
    "VATOnPaint": true, 
    "VATOnParts": true, 
    "Rectification": "Panel adjustment required" , 
    "RectificationIssues": "" , 
    "VehImpactAsDescribed": true, 
    "GlassCode": "GLS001" , 
    "GlassModelId": "MDL12345" , 
    "VehClass": "Car" , 
    "VehMake": "Toyota" , 
    "VehModel": "Corolla" , 
    "VehDescription": "Toyota Corolla 1.6 VVT-i Icon Tech" , 
    "VehEngineCC": "1600" , 
    "VehEngineBHP": "132" , 
    "VehBody": "5 Door" , 
    "VehBodyDescription": "Hatchback" , 
    "VehFirstRegistered": "2021-03-10" , 
    "VehOdometer": "26500" , 
    "VehOdometerUnit": "Miles" , 
    "VehColour": "Silver" , 
    "VehCondition": "Good" , 
    "VehBrakes": "ABS, fully functional" , 
    "VehSteering": "Power-assisted, No issues" ,

## Page 55

54 

54 Sentry API Documentation `0.1.0-alpha.1`.2 
    "VehInCarEntertainment": "Touchscreen infotainment system" , 
    "VehExtras": "Rear camera, cruise control" , 
    "VehMods": "None" , 
    "VehTaxExpire": "MAR/2026" , 
    "VehVin": "JTNB1234567890001" , 
    "VehRhfTyre": "5" , 
    "VehLhfTyre": "6" , 
    "VehRhrTyre": "7" , 
    "VehLhrTyre": "8" , 
    "VehSpareTyre": "No spare" , 
    "VehRhfSBelt": "Fitted" , 
    "VehLhfSBelt": "Fitted" , 
    "VehRhrSBelt": "Fitted" , 
    "VehLhrSBelt": "Fitted" , 
    "VehCenSBelt": "Fitted" , 
    "VehAirBagDeployed": "Driver and passenger front" , 
    "VehTransmission": "Automatic" , 
    "VehFuelType": "Petrol" , 
    "DamageSeverity": "Moderate" , 
    "DamageType": "Collision/Impact" , 
    "DamageArea": "Rear" , 
    "DamageLocation": "Bumper" , 
    "RepairName": "AutoFix Garage" , 
    "RepairAdd": "123 Main Street" , 
    "RepairTown": "Nottingham" , 
    "RepairCity": "Nottingham" , 
    "RepairCounty": "Nottinghamshire" , 
    "RepairPCode": "NG1 4AA" ,

## Page 56

55 

55 Sentry API Documentation `0.1.0-alpha.1`.2 
    "RepairTel": "01151234567" , 
    "RepairEmail": "repairs@autofixgarage.co.uk" , 
    "RepairCont": "Mark Taylor" , 
    "RepairerVatStatus": "20%" , 
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

## Page 57

56 

56 Sentry API Documentation `0.1.0-alpha.1`.2 
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
    "RecoveryRetail": "TBA" , 
    "RecoveryNet": 0.0, 
    "RecoveryVat": 0.0, 
    "RecoveryGross": 0.0, 
    "LabourRetail": 0.0, 
    "LabourDiscountAmount": 0.0,

## Page 58

57 

57 Sentry API Documentation `0.1.0-alpha.1`.2 
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

## Page 59

58 

58 Sentry API Documentation `0.1.0-alpha.1`.2 
    "TotalGross": 0.0, 
    "ReportText": "Vehicle sustained severe front-end damage. Recommended full 
bumper and bonnet replacement. " , 
    "IsSupplementary": false, 
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
    "Parts": [ 
        { 
            "Description": "Rear Bumper" , 
            "Quantity": 1, 
            "PartType": "New" , 
            "LabourTime": 2.5, 
            "PaintTime": 1.0, 
            "MaterialCost": 50.0, 
            "Price": 350.0 
        }, 
        { 
            "Description": "Bonnet" , 
            "Quantity": 1, 
            "PartType": "Repair" ,

## Page 60

59 

59 Sentry API Documentation `0.1.0-alpha.1`.2 
            "LabourTime": 3.0, 
            "PaintTime": 1.5, 
            "MaterialCost": 80.0, 
            "Price": 420.0 
        } 
    ], 
    "Files": [ 
        { 
            "Name": "RearDamagePhoto" , 
            "Extension": "jpg" , 
            "Data": "Base64EncodedStringHere" 
        }, 
        { 
            "Name": "EstimateDocument" , 
            "Extension": "pdf" , 
            "Data": "Base64EncodedStringHere" 
        } 
    ] 
}

## Page 61

60 

60 Sentry API Documentation `0.1.0-alpha.1`.2 
    Impact Image 
Field Type Required Description 
Start Int    Start point of impact. 
End Int    End point of impact. 


The impact image has 8 points that reference 8 different locations on the impact image. 
Example image above shows point locations. The Impact Image is made up of a list of 
start and end locations that relate to the above impact image. The below example image 
shows the output from the example Impact Image section of the JSON. This image will 
be outputted on the final report PDF once the report has been compiled in EVA. 

   Example ‘ImpactImage’ JSON 
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

## Page 62

61 

61 Sentry API Documentation `0.1.0-alpha.1`.2 
    Parts 
Field Type Required Description 
Description string    Part name or description. 
Quantity int    Quantity of the part. 
PartType string    Type of part. Accepted values: 
• Blend 
• New 
• Paint 
• R & R 
• Repair 
• Specialist 
• Unknown 
• Check 
LabourTime float   Labour time in hours. 
PaintTime float   Paint time in hours. 
MaterialCost decimal   Cost of materials for the part. 
Price decimal   Price of the part.

## Page 63

62 

62 Sentry API Documentation `0.1.0-alpha.1`.2 
   Example ‘Parts’ JSON 
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

## Page 64

63 

63 Sentry API Documentation `0.1.0-alpha.1`.2 
File Model 
Field Type Description 
Name string File name. 
Extension string File extension (e.g., .jpg, .pdf). 
Data byte[] Base64-encoded file content. 

   Example ‘Files’ JSON 
"Files": [ 
    { 
      "Name": "damage_closeup.jpg" , 
      "Extension": " .jpg" , 
      "Data": "base64stringofimage==" 
    } 
  ]

## Page 65

64 

64 Sentry API Documentation `0.1.0-alpha.1`.2 
   Retrievable Report List 
Endpoint: GET /Report/GetAvailableReports  
Description: 
This endpoint allows external partners to get a list of retrievable reports. 
Currently, the results that are returned are ordered by the releasedDate descending 
(newest first). 
    Note: Returns a 200 – Success, even when no reports are available to download. 

     Possible Responses 
HTTP Code Description 
200 - Success The List has been successfully retrieved 
401 – Unauthorized The user is unauthorised to access the endpoint 
500 - Internal Server Error An error occurred while processing the update request. 

   Example ‘GetAvailableReports’ JSON Response 
[ 
 { 
  "id": 123, 
  "registration": "AB12CDE", 
  " releasedDate": "2026-05-06T10:30:00Z ", 
} 
] 

   Response Model 
Field Type Nullable Description 
id int   Unique identifier for the report 
registration string    Vehicle registration 
releasedDate datetime   Date the report was released

## Page 66

65 

65 Sentry API Documentation `0.1.0-alpha.1`.2 
   Retrieve Report 
Endpoint: GET /Report/ GetReport?id={id} 
Description: 
This endpoint allows external partners to retrieve a reports data from the list of released 
reports using the ID taken from the previous endpoint. 


     Possible Responses 
Name Type Required Description 
Id Int    Unique identifier of report 


     Possible Responses 
HTTP Code Description 
200 - Success The report has been successfully retrieved  
400 – Bad Request  An invalid report id has been sent 
401 – Unauthorized The user is unauthorised to access the endpoint 
404 – Not Found The report has not been found 
500 - Internal Server Error An error occurred while processing the update request. 

   Example ‘Full Report’ JSON Response 
{ 
    "InspectEngineer": "DEMOENG1" , 
    "EvaRef": "123456" , 
    "VehReg": "AB12CDE" , 
    "ClmNo": "CLM987654" , 
    "InsuredName": "John Smith" , 
    "ThirdPartyName": "Jane Doe" ,

## Page 67

66 

66 Sentry API Documentation `0.1.0-alpha.1`.2 
    "ClaimType": "Repair" , 
    "IncidentDate": "2025-09-15T00:00:00Z" , 
    "InspectionDate": "2025-09-18T00:00:00Z" , 
    "RepairsAuthorisedDate": "2025-09-20T00:00:00Z" , 
    "SuppAuthorisedDate": "2025-09-22T00:00:00Z" , 
    "EstimateRecievedDate": "2025-09-17T00:00:00Z" , 
    "ReportDate": "2025-09-23T00:00:00Z" , 
    "RepairerEstimateAgreed": "Yes" , 
    "InspLocName": "AutoFix Garage" , 
    "InspLocAdd": "123 Main Street" , 
    "InspLocTown": "Nottingham" , 
    "InspLocCity": "Nottingham" , 
    "InspLocCounty": "Nottinghamshire" , 
    "InspLocPCode": "NG1 4AA" , 
    "InspLocTel": "01151234567" , 
    "InspLocEmail": "info@autofixgarage.co.uk" , 
    "InspLocCont": "Mark Taylor" , 
    "InspectionType": "Vehicle Damage Inspection", 
    "ReportType": "Full Report" , 
    "RepairDuration": "5 Days" , 
    "VehRoadWorthy": "No" , 
    "VehNotRoadWorthyReason": "Front-end structural damage" , 
    "VehDriveable": "No" , 
    "VehInUse": "No" , 
    "VehAreaOfRepair": "Front Bumper, Bonnet, Left Headlamp" , 
    "LightCondAtInsp": "Daylight" , 
    "InspCondition": "Vehicle assessed at garage under natural light" , 
    "TempRepairs": "No" ,

## Page 68

67 

67 Sentry API Documentation `0.1.0-alpha.1`.2 
    "TempRepairsHow": "" , 
    "TempRepairsCost": 0.0, 
    "ValMarket": 12500.0, 
    "ValRetail": 13000.0, 
    "ValTrade": 11000.0, 
    "ValMid": 12000.0, 
    "ValSalvage": 2000.0, 
    "ValDisposal": 150.0, 
    "ValPrivate": 11800.0, 
    "ValResearch": "Internet" , 
    "MileageAdjust": -200.0, 
    "ConditionAdjust": -300.0, 
    "OtherAdjust": 0.0, 
    "ReportDelayed": false, 
    "ReportDelayedReason": "" , 
    "PresentAtInsp": "Repairer representative and engineer present" , 
    "PrivateHireLicNo": "" , 
    "EngineStarts": "Yes" , 
    "EngineFailReason": "" , 
    "DoorsSecured": "Yes" , 
    "DoorsNotSecuredReason": "" , 
    "Diminution": "No" , 
    "DiminutionPAVPercent": 0.0, 
    "ClaimantVatLiability": 20.0, 
    "ClaimantTotLiability": 300.0, 
    "RepairDelays": false, 
    "RepairDelaysReason": "" , 
    "Excess": "250" ,

## Page 69

68 

68 Sentry API Documentation `0.1.0-alpha.1`.2 
    "ClaimantVatStatus": "20%" , 
    "AuthorityStatus": "Yes" , 
    "Cause": "Rear-ended another vehicle" , 
    "SumInsured": 15000.0, 
    "Settled": "No" , 
    "DtAttemptedSettle": "2025-09-28T00:00:00Z" , 
    "SalLocName": "ABC Salvage Ltd" , 
    "SalLocAdd": "1 Recovery Way" , 
    "SalLocTown": "Leeds" , 
    "SalLocCity": "Leeds" , 
    "SalLocCounty": "Yorkshire" , 
    "SalLocPCode": "LS1 4BB" , 
    "SalLocTelNo": "01134567890" , 
    "SalLocEmail": "contact@abcsalvage.co.uk" , 
    "ABICat": "N" , 
    "OwnerRetainSalvage": false, 
    "FinalStorage": 250.0, 
    "CurrentStore": 100.0, 
    "SalvageRef": "SAL20251022" , 
    "DtSalvageMoved": "2025-09-25T00:00:00Z" , 
    "StorageRate": 20.0, 
    "Inherited": "No" , 
    "DtStoreStart": "2025-09-15T00:00:00Z" , 
    "SalvageMoved": true, 
    "VATOnSalv": "Yes" , 
    "VATONother": false, 
    "VATOnPaint": true, 
    "VATOnParts": true,

## Page 70

69 

69 Sentry API Documentation `0.1.0-alpha.1`.2 
    "Rectification": "Panel adjustment required" , 
    "RectificationIssues": "" , 
    "VehImpactAsDescribed": true, 
    "GlassCode": "GLS001" , 
    "GlassModelId": "MDL12345" , 
    "VehClass": "Car" , 
    "VehMake": "Toyota" , 
    "VehModel": "Corolla" , 
    "VehDescription": "Toyota Corolla 1.6 VVT-i Icon Tech" , 
    "VehEngineCC": "1600" , 
    "VehEngineBHP": "132" , 
    "VehBody": "5 Door" , 
    "VehBodyDescription": "Hatchback" , 
    "VehFirstRegistered": "2021-03-10" , 
    "VehOdometer": "26500" , 
    "VehOdometerUnit": "Miles" , 
    "VehColour": "Silver" , 
    "VehCondition": "Good" , 
    "VehBrakes": "ABS, fully functional" , 
    "VehSteering": "Power-assisted, No issues" , 
    "VehInCarEntertainment": "Touchscreen infotainment system" , 
    "VehExtras": "Rear camera, cruise control" , 
    "VehMods": "None" , 
    "VehTaxExpire": "MAR/2026" , 
    "VehVin": "JTNB1234567890001" , 
    "VehRhfTyre": "5" , 
    "VehLhfTyre": "6" , 
    "VehRhrTyre": "7" ,

## Page 71

70 

70 Sentry API Documentation `0.1.0-alpha.1`.2 
    "VehLhrTyre": "8" , 
    "VehSpareTyre": "No spare" , 
    "VehRhfSBelt": "Fitted" , 
    "VehLhfSBelt": "Fitted" , 
    "VehRhrSBelt": "Fitted" , 
    "VehLhrSBelt": "Fitted" , 
    "VehCenSBelt": "Fitted" , 
    "VehAirBagDeployed": "Driver and passenger front" , 
    "VehTransmission": "Automatic" , 
    "VehFuelType": "Petrol" , 
    "DamageSeverity": "Moderate" , 
    "DamageType": "Collision/Impact" , 
    "DamageArea": "Rear" , 
    "DamageLocation": "Bumper" , 
    "RepairName": "AutoFix Garage" , 
    "RepairAdd": "123 Main Street" , 
    "RepairTown": "Nottingham" , 
    "RepairCity": "Nottingham" , 
    "RepairCounty": "Nottinghamshire" , 
    "RepairPCode": "NG1 4AA" , 
    "RepairTel": "01151234567" , 
    "RepairEmail": "repairs@autofixgarage.co.uk" , 
    "RepairCont": "Mark Taylor" , 
    "RepairerVatStatus": "20%" , 
    "EstimatedRecovery": 150.0, 
    "EstimatedLabour": 500.0, 
    "EstimatedMaterials": 300.0, 
    "EstimatedSundries": 50.0,

## Page 72

71 

71 Sentry API Documentation `0.1.0-alpha.1`.2 
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

## Page 73

72 

72 Sentry API Documentation `0.1.0-alpha.1`.2 
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
    "RecoveryRetail": "TBA" , 
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

## Page 74

73 

73 Sentry API Documentation `0.1.0-alpha.1`.2 
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
bumper and bonnet replacement. " , 
    "GlassMonth": "", 
    "SupplementaryCount": 1, 
    "FeeNet": 0.0, 
    "FeeVat": 0.0, 
    "FeeGross": 0.0,

## Page 75

74 

74 Sentry API Documentation `0.1.0-alpha.1`.2 

    "IsSupplementary": false, 
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
    "Parts": [ 
        { 
            "Description": "Rear Bumper" , 
            "Quantity": 1, 
            "PartType": "New" , 
            "LabourTime": 2.5, 
            "PaintTime": 1.0, 
            "MaterialCost": 50.0, 
            "Price": 350.0 
        }, 
        { 
            "Description": "Bonnet" , 
            "Quantity": 1, 
            "PartType": "Repair" , 
            "LabourTime": 3.0, 
            "PaintTime": 1.5,

## Page 76

75 

75 Sentry API Documentation `0.1.0-alpha.1`.2 
            "MaterialCost": 80.0, 
            "Price": 420.0 
        } 
    ], 
    "Files": [ 
        { 
            "Name": "RearDamagePhoto" , 
            "Extension": "jpg" , 
            "Data": "Base64EncodedStringHere" 
        }, 
        { 
            "Name": "EstimateDocument" , 
            "Extension": "pdf" , 
            "Data": "Base64EncodedStringHere" 
        } 
    ] 
} 

   Request Model 
Field Type Required Description 
InspectEngineer string 
(max 12) 
   The name or code of the 
inspecting engineer. 
EvaRef string    (if 
ClmNo is 
used with 
it) 
The EVA reference number 
identifying the assessment. 
VehReg string    Vehicle registration. 
ClmNo string    Claim number.

## Page 77

76 

76 Sentry API Documentation `0.1.0-alpha.1`.2 
InsuredName string   Name of the insured party. 
ThirdPartyName string   Name of the third party 
involved (if applicable). 
ClaimType string   Type of claim. Possible 
values: 
• Cash-In-Lieu 
• Diminution 
• Other 
• Post Repair 
• Repair 
• T/Loss 
• Repudiation 
IncidentDate DateTime    Date of the incident. 
InspectionDate DateTime   Date the vehicle was 
inspected. 
RepairsAuthorisedDate DateTime   Date repairs were authorised. 
SuppAuthorisedDate DateTime   Date supplementary 
authorisation was given. 
EstimateRecievedDate DateTime   Date the repair estimate was 
received. 
ReportDate DateTime    Date the report was created 
or submitted. 
RepairerEstimateAgreed String   Indicates if the repairer’s 
estimate was agreed. 
Possible values: 
• Yes 
• No 
• N/A 
InspLocName string 
(max 40) 
  Name of the inspection 
location. 
InspLocAdd string 
(max 40) 
  Address line of the 
inspection location. 
InspLocTown string 
(max 250) 
  Town of the inspection 
location.

## Page 78

77 

77 Sentry API Documentation `0.1.0-alpha.1`.2 
InspLocCity string 
(max 250) 
  City of the inspection 
location. 
InspLocCounty string 
(max 250) 
  County of the inspection 
location. 
InspLocPCode string 
(max 10) 
  Postcode of the inspection 
location. 
InspLocTel string 
(max 18) 
  Telephone number of the 
inspection location. 
InspLocEmail string 
(max 250) 
  Email of the inspection 
location. 
InspLocCont string 
(max 18) 
  Contact name at inspection 
location. 
InspectionType string 
(max 25) 
   Type of inspection 
performed. Possible values: 
• Vehicle Damage 
Inspection 
• Rectification Work 
• Quality/Audit Inspection 
• Low Velocity Inspection 
• Desktop 
• Other 
• Cold Call 
• Consistency 
• Images Only 
• Forensic 
ReportType string 
(max 27) 
  Type of report. Possible 
values: 
• Cold Call Report 
• Desktop Report 
• Full Report 
• Letter 
• Post-Inspection 
• Post-Repair Audit 
• Post-Repair 
Complaint 
• Roadworthy 
• Simple Low Speed 
Inspection 
• Small Claim

## Page 79

78 

78 Sentry API Documentation `0.1.0-alpha.1`.2 
• Telephone 
RepairDuration string 
(max 10) 
  Estimated repair duration in 
days. 
VehRoadWorthy string 
(max 10) 
  Indicates if the vehicle is 
roadworthy. Possible values: 
• Yes 
• No 
• N/A 
• Subject To 
VehNotRoadWorthyReason string 
(max 250) 
  Reason vehicle is not 
roadworthy. 
VehDriveable string 
(max 9) 
  Indicates if the vehicle is 
drivable. Possible values: 
• Yes 
• No 
• Not Known 
VehInUse string 
(max 9) 
  Indicates if the vehicle is still 
in use. Possible values: 
• Yes 
• No 
• Not Known 
VehAreaOfRepair string 
(max 250) 
  General area of repair. 
LightCondAtInsp string 
(max 250) 
  Lighting conditions at 
inspection. 
InspCondition string 
(max 250) 
  General condition at 
inspection. 
TempRepairs string 
(max 3) 
  Indicates if temporary repairs 
were done. Possible values: 
• Yes 
• No 
• N/A 
TempRepairsHow string 
(max 250) 
  Description of temporary 
repairs.

## Page 80

79 

79 Sentry API Documentation `0.1.0-alpha.1`.2 
TempRepairsCost decimal   Cost of temporary repairs. 
ValMarket decimal   Market value of the vehicle. 
ValRetail decimal   Retail value. 
ValTrade decimal   Trade-in value. 
ValMid decimal   Mid-point valuation. 
ValSalvage decimal   Salvage value. 
ValDisposal decimal   Disposal value. 
ValPrivate decimal   Private sale value. 
ValResearch string   Supporting valuation 
research text. Possible 
values: 
• Internet 
• Magazines 
• Main Dealer 
• Other 
MileageAdjust decimal   Adjustment for mileage. 
ConditionAdjust decimal   Adjustment for condition. 
OtherAdjust decimal   Other valuation adjustments. 
ReportDelayed bool   Indicates if report 
submission was delayed. 
ReportDelayedReason string 
(max 35) 
  Reason for delay. Possible 
values: 
• Awaiting estimate - 
Owner 
• Awaiting estimate - 
Repairer 
• Client requested 
inspection date 
• Getting parts prices 
• Problem contacting 
owner 
• Vehicle being stripped 
• Awaiting images

## Page 81

80 

80 Sentry API Documentation `0.1.0-alpha.1`.2 
• Repairer carrying out pre 
strip 
• Customer unresponsive 
• Repairer unresponsive 
• Awaiting further info(See 
comments) 
• Vehicle not available 
• Valuation dispute 
PresentAtInsp string 
(max 250) 
  Individuals present during 
the inspection. 
PrivateHireLicNo string 
(max 20) 
  Private hire licence number. 
EngineStarts string 
(max 9) 
  Indicates if engine starts. 
Possible values: 
• Yes 
• No 
• Not Known 
EngineFailReason string 
(max 250) 
  Reason for engine failure (if 
applicable). 
DoorsSecured string 
(max 9) 
  Indicates if vehicle doors are 
secured. Possible values: 
• Yes 
• No 
• Not Known 
DoorsNotSecuredReason string 
(max 250) 
  Reason vehicle doors are not 
secured. 
Diminution string 
(max 3) 
  Indicates if vehicle has a 
diminution in value. Possible 
values: 
• Yes 
• No 
• N/A 
DiminutionPAVPercent decimal   Diminution percentage of 
pre-accident value. 
ClaimantVatLiability decimal   VAT liability for the claimant. 
ClaimantTotLiability decimal   Total liability of the claimant.

## Page 82

81 

81 Sentry API Documentation `0.1.0-alpha.1`.2 
RepairDelays bool   Indicates if there were repair 
delays. 
RepairDelaysReason string 
(max 50) 
  Reason for repair delays. 
Excess string 
(max 10) 
  Claim excess amount. 
ClaimantVatStatus string 
(max 8) 
  Claimant VAT registration 
status. Possible values: 
• Yes 
• No 
• n% 
AuthorityStatus string 
(max 17) 
  Authority status for repair. 
Possible values: 
• Yes 
• No 
Cause string 
(max 250) 
  Cause of the damage. 
SumInsured decimal   Total sum insured. 
Settled string 
(max 21) 
  Indicates if the claim is 
settled. Possible values: 
• Yes 
• No 
• Disputed 
• N/A 
• Subject To 
DtAttemptedSettle DateTime   Date settlement was 
attempted. 
SalLocName string 
(max 40) 
  Name of salvage company 
SalLocAdd string 
(max 40) 
  Salvage company first line of 
address 
SalLocTown string 
(max 250) 
  Salvage company town 
SalLocCity string 
(max 250) 
  Salvage company city 
SalLocCounty string 
(max 250) 
  Salvage company county

## Page 83

82 

82 Sentry API Documentation `0.1.0-alpha.1`.2 
SalLocPCode string 
(max 10) 
  Salvage company postcode 
SalLocTelNo string 
(max 18) 
  Salvage company contact 
number 
SalLocEmail string 
(max 250) 
  Salvage company email 
address 
ABICat string   Salvage category. Possible 
values: 
• A-Scrap  
• B-Breaker  
• C-Rep Costs > PAMV  
• D-Constructive T/L 
• X-Other 
• S-Structural  
• N-Non-Structural 
• No category - 
breaker/scrap 
OwnerRetainSalvage Bool   Is the owner retaining the 
salvage? 
FinalStorage Decimal   Final storage costs 
CurrentStore Decimal   Current storage costs 
SalvageRef string 
(max 8) 
  Salvage reference 
DtSalvageMoved Datetime   Date salvage was moved 
StorageRate Decimal   Daily storage rate 
Inherited String 
(max 10) 
  Inherited charges 
DtStoreStart DateTime   Date storage of vehicle 
started 
SalvageMoved Bool   Was the salvage moved? 
VATOnSalv String   Should vat be applied to 
salvage? Possible values: 
• Yes 
• No 
VATOnOther Bool   Should vat be applied to 
other? 
VATOnPaint Bool   Should vat be applied to 
paint? 
VATOnParts Bool   Should vat be applied to 
parts? 
Rectification String   Rectification work required 
RectificationIssues String   Rectification issues 
VehImpactAsDescribed bool   Is the impact as described? 
GlassCode string 
(max 6) 
  Glass’s guide code

## Page 84

83 

83 Sentry API Documentation `0.1.0-alpha.1`.2 
GlassModelId String 
(max 9) 
  Glass’s model Id 
VehClass String 
(max 10) 
  Vehicle class. Possible 
values: 
• Agric 
• Car 
• Caravan 
• HGV 
• LCV 
• M/Cycle 
• Misc 
• Plant 
• PSV 
• Trailer 
• Tractor 
• Machinery 
VehMake String 
(max 20) 
  Vehicle Make 
VehModel String 
(max 20) 
  Vehicle Model 
VehDescription String 
(max 40) 
  Vehicle Make & Model 
VehEngineCC String 
(max 8) 
  Engine CC 
VehEngineBHP String 
(max 4) 
  Engine BHP 
VehBody String 
(max 14) 
  Vehicle body type. Possible 
values: 
• 2 Door 
• 3 Door 
• 4 Door 
• 5 Door 
• 6 Door 
• Agricultural 
• Articulated 
• Contractors 
• P .S. V . 
• Private 
• Rigid 
• Trailer 
VehBodyDescription String 
(max 30) 
  Vehicle Body Description. 
Possible values: 
• 3 Wheel Scooter 
• Access Platform 
• Ambulance

## Page 85

84 

84 Sentry API Documentation `0.1.0-alpha.1`.2 
• ATV 
• Backhoe Digger 
• Baler 
• Beavertail 
• Bicycle  
• Box Lorry 
• Box Trailer 
• Box-Van 
• Bubble Car 
• Bus 
• Caged Tipper 
• Camper Van 
• Car Derived Van 
• Caravan 
• Catering Unit 
• Cement Mixer 
• Cherry Picker 
• Coach 
• Combine 
• Compressor 
• Convertible 
• Coupe 
• Crane 
• Crew-cab 
• Crewcab Tipper 
• Crop Fertilizer 
• Crop Sprayer 
• Curtainsider 
• Digger 
• Double Decker Bus 
• Drophead 
• Dropside 
• Dumper 
• Estate 
• Excavator 
• Fire Engine 
• Flatbed 
• Food Truck 
• Forager 
• Fork-Lift 
• Front End Loader 
• Fuel Tanker 
• Grab Truck 
• Gritter 
• Hatchback

## Page 86

85 

85 Sentry API Documentation `0.1.0-alpha.1`.2 
• Hay Turner 
• Hedgetrimmer 
• Hook Loader 
• Horsebox 
• Ice Cream Van 
• Kombi 
• Light Van 
• Limousine 
• Livestock Carrier 
• Low Loader 
• Luton Van 
• M.P .V . 
• Machinery 
• Mini Excavator 
• Minibus 
• Mobile Home 
• Motor Home 
• Motorcycle 
• Mower 
• Muck Spreader 
• Omnibus 
• Other 
• P .S. V . 
• Panel Van 
• Pantechnicon 
• Parkhome 
• Pickup 
• Plough 
• Quad-Bike 
• Rake 
• Recovery Truck 
• Refrigerated Box Lorry 
• Refrigerated Box Van 
• Refrigerated Van 
• Refuse Collection 
Vehicle 
• Road Sweeper 
• Rotavator 
• Saloon 
• Scooter 
• Self Propelled Sprayer 
• Silage Trailer 
• Skelatal 
• Skid Steer 
• Skip Loader

## Page 87

86 

86 Sentry API Documentation `0.1.0-alpha.1`.2 
• Static 
• Station Wagon 
• Tandem Trailer 
• Tanker 
• Tarmac Hot Box 
• Taxi 
• Tedder 
• Telehandler 
• Tipper 
• Toilet Block 
• Touring 
• Tractor 
• Tractor Unit 
• Trailer 
• Tri Axle Trailer 
• Trike 
• Ute 
• Vacuum Tanker 
• Van 
• Vehicle Transporter 
• Wagon 
• Welfare Unit 

VehFirstRegistered String 
(max 10) 
  Month & Year vehicle was 
first registered. Format: 
MMM/yyyy 
VehOdometer String 
(max 10) 
  Odometer reading 
VehOdometerUnit string   Odometer reading units. 
Accepted values: 
• Hrs 
• Km 
• Miles 
VehColour String 
(max 40) 
  Colour of vehicle 
VehCondition String 
(max 15) 
  Condition of vehicle 
VehBrakes String 
(max 250) 
  Condition of brakes on 
vehicle 
VehSteering String 
(max 250) 
  Condition of steering on 
vehicle 
VehInCarEntertainment String 
(max 40) 
  Any in car entertainment in 
the vehicle 
VehExtras String 
(max 40) 
  Any extras on the vehicle

## Page 88

87 

87 Sentry API Documentation `0.1.0-alpha.1`.2 
VehMods String 
(max 40) 
  Any additional mods of the 
vehicle 
VehTaxExpire String 
(max 10) 
  Tax expiration date. Format: 
MMM/yyyy 
VehVin String 
(max 22) 
  Vehicle Identification 
Number (VIN) 
VehRhfTyre String 
(max 11) 
  Tyre tread depths on right-
hand front tyre 
VehLhfTyre String 
(max 11) 
  Tyre tread depths on left-
hand front tyre 
VehRhrTyre String 
(max 11) 
  Tyre tread depths on right-
hand rear tyre 
VehLhrTyre String 
(max 11) 
  Tyre tread depths on left-
hand rear tyre 
VehSpareTyre String 
(max 11) 
  Tyre tread depths on spare 
tyre 
VehRhfSBelt String 
(max 12) 
  Status of right-hand front 
seat belt. Possible values: 
• Damaged 
• Deployed 
• Fitted 
• No Access 
• None Fitted 
• Not Tested 
VehLhfSBelt String 
(max 12) 
  Status of left-hand front seat 
belt. Possible values: 
• Damaged 
• Deployed 
• Fitted 
• No Access 
• None Fitted 
• Not Tested 
VehRhrSBelt String 
(max 12) 
  Status of right-hand rear seat 
belt. Possible values: 
• Damaged 
• Deployed 
• Fitted 
• No Access 
• None Fitted 
• Not Tested 
VehLhrSBelt String 
(max 12) 
  Status of left-hand rear seat 
belt. Possible values: 
• Damaged

## Page 89

88 

88 Sentry API Documentation `0.1.0-alpha.1`.2 
• Deployed 
• Fitted 
• No Access 
• None Fitted 
• Not Tested 
VehCenSBelt String 
(max 12) 
  Status of center seat belt. 
Possible values: 
• Damaged 
• Deployed 
• Fitted 
• No Access 
• None Fitted 
• Not Tested 
VehAirBagDeployed String 
(max 25) 
  Did the airbags deploy? 
VehTransmission String 
(max 20) 
  Vehicle transmission. 
Possible values: 
• Automatic 
• CVT 
• DSG 
• EGS 
• Manual 
• N/A 
• Semi-Auto 
• Sequential 
• Sequential Automatic 
• Sequential Manual 
• Unknown 
VehFuelType String 
(max 20) 
  Vehicle fuel type.  
Possible values: 
• Biofuel 
• Compressed Natural 
Gas 
• Diesel 
• Dual 
• Electric 
• Hybrid 
• Hydrogen 
• LPG 
• Petrol 
DamageSeverity String 
(max 18) 
  First damage severity. 
Possible Values:

## Page 90

89 

89 Sentry API Documentation `0.1.0-alpha.1`.2 
• Very Heavy  
• Heavy 
• Moderate to Heavy 
• Moderate  
• Light to Moderate 
• Light 
• Very Light 
DamageType String 
(max 22) 
  First damage type. 
Possible values: 
• Accidental Damage 
• Chemical 
Contamination 
• Collision/Impact 
• Electrical Failure 
• Fire (Cause Unknown) 
• Fire (Electrical) 
• Fire (External) 
• Fire (Fuel) 
• Ground Damage 
• Mechanical Failure 
• No Damage 
• Rodent Damage 
• Storm Damage 
• Theft or Attempted 
• Unrecovered Theft 
• Vandalism 
• Water/Flood/Damp 
DamageArea String 
(max 8) 
  First damage area.  
Possible values: 
• Front 
• LH Front 
• LH Rear 
• LH Side 
• Rear 
• RH Front 
• RH Rear 
• RH Side 
DamageLocation String 
(max 20) 
  First damage location.  
Possible values: 
• Bonnet 
• Boot 
• Brake Lever 
• Bumper 
• Complete Vehicle 
• Door

## Page 91

90 

90 Sentry API Documentation `0.1.0-alpha.1`.2 
• Engine Bay 
• Fairing 
• Foot Peg 
• Frame 
• Front Fork 
• Front Panel 
• Front to Rear 
• Gear Lever 
• Handlebars 
• Interior 
• Luggage 
Compartment 
• Mirror 
• Mud Guard 
• Panniers 
• Quarter Panel 
• Rear Panel 
• Rear to Front 
• Roof 
• Swing Arm 
• Tyre 
• Underside 
• Wheel 
• Wing 
DamageSeverity2 String 
(max 18) 
  Second damage severity. 
Possible values: 
• Very Heavy  
• Heavy 
• Moderate to Heavy 
• Moderate  
• Light to Moderate 
• Light 
• Very Light 
DamageType2 String 
(max 22) 
  Second damage type. 
Possible values: 
• Accidental Damage 
• Chemical 
Contamination 
• Collision/Impact 
• Electrical Failure 
• Fire (Cause Unknown) 
• Fire (Electrical)

## Page 92

91 

91 Sentry API Documentation `0.1.0-alpha.1`.2 
• Fire (External) 
• Fire (Fuel) 
• Ground Damage 
• Mechanical Failure 
• No Damage 
• Rodent Damage 
• Storm Damage 
• Theft or Attempted 
• Unrecovered Theft 
• Vandalism 
• Water/Flood/Damp 
DamageArea2 String 
(max 8) 
  Second damage area. 
Possible values: 
• Front 
• LH Front 
• LH Rear 
• LH Side 
• Rear 
• RH Front 
• RH Rear 
• RH Side 
DamageLocation2 String 
(max 20) 
  Second damage location. 
Possible values: 
• Bonnet 
• Boot 
• Brake Lever 
• Bumper 
• Complete Vehicle 
• Door 
• Engine Bay 
• Fairing 
• Foot Peg 
• Frame 
• Front Fork 
• Front Panel 
• Front to Rear 
• Gear Lever 
• Handlebars

## Page 93

92 

92 Sentry API Documentation `0.1.0-alpha.1`.2 
• Interior 
• Luggage 
Compartment 
• Mirror 
• Mud Guard 
• Panniers 
• Quarter Panel 
• Rear Panel 
• Rear to Front 
• Roof 
• Swing Arm 
• Tyre 
• Underside 
• Wheel 
• Wing 
DamageSeverity3 String 
(max 18) 
  Third damage severity. 
Possible values: 
• Very Heavy  
• Heavy 
• Moderate to Heavy 
• Moderate  
• Light to Moderate 
• Light 
• Very Light 
DamageType3 String 
(max 22) 
  Third damage type. 
Possible values: 
• Accidental Damage 
• Chemical 
Contamination 
• Collision/Impact 
• Electrical Failure 
• Fire (Cause Unknown) 
• Fire (Electrical) 
• Fire (External) 
• Fire (Fuel) 
• Ground Damage 
• Mechanical Failure 
• No Damage 
• Rodent Damage

## Page 94

93 

93 Sentry API Documentation `0.1.0-alpha.1`.2 
• Storm Damage 
• Theft or Attempted 
• Unrecovered Theft 
• Vandalism 
• Water/Flood/Damp 
DamageArea3 String 
(max 8) 
  Third damage area.  
Possible values: 
• Front 
• LH Front 
• LH Rear 
• LH Side 
• Rear 
• RH Front 
• RH Rear 
• RH Side 
DamageLocation3 String 
(max 20) 
  Third damage location. 
Possible values: 
• Bonnet 
• Boot 
• Brake Lever 
• Bumper 
• Complete Vehicle 
• Door 
• Engine Bay 
• Fairing 
• Foot Peg 
• Frame 
• Front Fork 
• Front Panel 
• Front to Rear 
• Gear Lever 
• Handlebars 
• Interior 
• Luggage 
Compartment 
• Mirror 
• Mud Guard 
• Panniers 
• Quarter Panel

## Page 95

94 

94 Sentry API Documentation `0.1.0-alpha.1`.2 
• Rear Panel 
• Rear to Front 
• Roof 
• Swing Arm 
• Tyre 
• Underside 
• Wheel 
• Wing 
RepairName String 
(max 40) 
  Repairer name 
RepairAdd String 
(max 40) 
  Repairer address line 1 
RepairTown String 
(max 250) 
  Repairer town 
RepairCity String 
(max 250) 
  Repairer city 
RepairCounty String 
(max 250) 
  Repairer county 
RepairPCode String 
(max 10) 
  Repairer postcode 
RepairTel String 
(max 18) 
  Repairer contact number 
RepairEmail String 
(max 250) 
  Repairer email 
RepairCont String 
(max 20) 
  Repairer contact name 
RepairerVatStatus String 
(max 3) 
  Repairer Vat Status.  
Possible values: 
• Yes 
• No 
• N% 
EstimatedRecovery Decimal   Estimated recovery cost 
EstimatedLabour Decimal   Estimated labour cost 
EstimatedMaterials Decimal   Estimated materials cost 
EstimatedSundries Decimal   Estimated sundries cost 
EstimatedEpa Decimal   Estimated epa cost 
EstimatedParts Decimal   Estimated parts cost 
EstimatedOtherTotal Decimal   Estimated other cost 
EstimatedNet Decimal   Estimated net cost 
EstimatedVat Decimal   Estimated vat cost 
EstimatedGross Decimal   Estimated gross cost 
EstimatedPartsDiscount Decimal   Estimated parts discount 
EstimatedLabourRate Decimal   Estimated labour rate

## Page 96

95 

95 Sentry API Documentation `0.1.0-alpha.1`.2 
LabourRate Decimal   Assessed labour rate 
RecoveryDiscountPercent Decimal   Assessed recovery discount 
percentage 
LabourDiscountPercent Decimal   Assessed labour discount 
percentage 
MaterialDiscountPercent Decimal   Assessed material discount 
percentage 
SundryDiscountPercent Decimal   Assessed sundry discount 
percentage 
EPADiscountPercent Decimal   Assessed epa discount 
percentage 
PartsDiscountPercent Decimal   Assessed parts discount 
percentage 
RecoverySavingsNet Decimal   Recovery savings net 
between estimated and 
assessed recovery charge 
RecoverySavingsGross Decimal   Recovery savings gross 
between estimated and 
assessed recovery charge 
LabourSavingsNet Decimal   Labour savings net between 
estimated and assessed 
labour charge 
LabourSavingsGross Decimal   Labour savings gross 
between estimated and 
assessed labour charge 
MaterialSavingsNet Decimal   Materials savings net 
between estimated and 
assessed materials charge 
MaterialSavingsGross Decimal   Materials savings gross 
between estimated and 
assessed materials charge 
PartsSavingsNet Decimal   Parts savings net between 
estimated and assessed 
parts charge 
PartsSavingsGross Decimal   Parts savings gross between 
estimated and assessed 
parts charge 
OtherSavingsNet Decimal   Other savings net between 
estimated and assessed 
other charge 
OtherSavingsGross Decimal   Other savings gross between 
estimated and assessed 
other charge 
DeleteSavingsNet Decimal   Deleted estimate items 
savings net between 
estimated and assessed

## Page 97

96 

96 Sentry API Documentation `0.1.0-alpha.1`.2 
DeleteSavingsGross Decimal   Deleted estimate items 
savings gross between 
estimated and assessed 
TotalSavingsNet Decimal   Total savings net between 
estimated and assessed 
TotalSavingsGross Decimal   Total savings gross between 
estimated and assessed 
VatSavings Decimal   Vat savings between 
estimated and assessed 
PrincipalSavings Decimal   Savings for principal between 
estimated assessed 
BettermentRetail Decimal   Betterment retail figure 
BettermentDiscount Decimal   Betterment discount figure 
BettermentNet Decimal   Betterment net figure 
BettermentVat Decimal   Betterment vat figure 
BettermentGross Decimal   Betterment gross figure 
TotalExcess Decimal   Total excess 
ContractRep Decimal   Contract Repair charge 
Reserve Decimal   Reserve figure 
Balance Decimal   Balance 
RepairSettlement Decimal   Repair settlement figure 
OriginalMaterialNet Decimal   Original material net figure 
RecoveryRetail String   Retail recovery costs. 
Accepts the cost or the value 
‘TBA’ 
RecoveryNet Decimal   Assessed recovery net figure 
RecoveryVat Decimal   Assessed recovery vat figure 
RecoveryGross Decimal   Assessed recovery gross 
figure 
LabourRetail Decimal   Assessed recovery net figure 
LabourDiscountAmount Decimal   Assessed labour discount 
figure 
LabourNet Decimal   Assessed labour net figure 
LabourVat Decimal   Assessed labour vat figure 
LabourGross Decimal   Assessed labour gross figure 
MaterialRetail Decimal   Assessed material retail 
figure 
MaterialDiscount Decimal   Assessed material discount 
figure 
MaterialNet Decimal   Assessed material net figure 
MaterialVat Decimal   Assessed material vat figure 
MaterialGross Decimal   Assessed material gross 
figure 
SundryNet Decimal   Assessed sundry net figure

## Page 98

97 

97 Sentry API Documentation `0.1.0-alpha.1`.2 
SundryVat Decimal   Assessed sundry vat figure 
SundryGross Decimal   Assessed sundry gross figure 
EpaNet Decimal   Assessed epa net figure 
EpaVat Decimal   Assessed epa vat figure 
EpaGross Decimal   Assessed epa gross figure 
PartsRetail Decimal   Assessed parts retail figure 
PartsDiscountAmount Decimal   Assessed parts discount 
figure 
PartsNet Decimal   Assessed parts net figure 
PartsVat Decimal   Assessed parts vat figure 
PartsGross Decimal   Assessed parts gross figure 
OtherRetail Decimal   Assessed other retail figure 
OtherDiscountAmount Decimal   Assessed other discount 
figure 
OtherNet Decimal   Assessed other net figure 
OtherVat Decimal   Assessed other vat figure 
OtherGross Decimal   Assessed other gross figure 
TotalRetail Decimal   Assessed total retail figure 
TotalDiscountAmount Decimal   Assessed total discount 
figure 
TotalNet Decimal   Assessed total net figure 
TotalVat Decimal   Assessed total vat figure 
TotalGross Decimal   Assessed total gross figure 
ReportText String   Narrative content of the 
engineer’s report. 
IsSupplementary Bool   Indicates if the report is a 
supplementary submission. 
GlassMonth String   This is the date the valuation 
of the vehicle 
SupplementaryCount Integer   This is the count of 
supplementary completed 
FeeNet Decimal   This is the last live invoice 
nett value 
FeeVat Decimal   This is the last live invoice vat 
value 
FeeGross Decimal   This is the last live invoice 
gross value 
ImpactImage List   Directional impact details 
(see below). 
Parts List   List of parts involved (see 
table below).

## Page 99

98 

98 Sentry API Documentation `0.1.0-alpha.1`.2 
Files List   Associated files (photos, 
documents, etc.). 


    Parts Model 
Field Type Required Description 
Description string    Part name or description. 
Quantity int    Quantity of the part. 
PartType string    Type of part. Possible values: 
• Blend 
• New 
• Paint 
• R & R 
• Repair 
• Specialist 
• Unknown 
• Check 
LabourTime float   Labour time in hours. 
PaintTime float   Paint time in hours. 
MaterialCost decimal   Cost of materials for the part. 
Price decimal   Price of the part. 


File Model 
Field Type Description 
Name string File name. 
Extension string File extension (e.g., .jpg, .pdf). 
Data byte[] Base64-encoded file content.
