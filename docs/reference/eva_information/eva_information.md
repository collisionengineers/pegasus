**EVA Information**



EVA is a case management system used by Collision Engineers, developed by Minotaur Software Ltd.



It is quite an old, esoteric system.

The long term aim would be full replacement of this system. In the interim, integration and working with this system will be required.

EVA does provide an API, however this is not currently being utilized as far as I am able to determine.

The full Sentry API capability set documented in `docs/reference_information/imported_originals/eva/sentry_api_complete_guide.md` is now available for Collision Command Centre planning and implementation. This includes the documented GET report endpoints as well as the documented POST token, instruction, claim update, note, and report submission endpoints.

Earlier planning treated the GET/report retrieval documentation as unavailable for the Collision Engineers tenant. That assumption is now superseded: GET endpoints will be available, and the complete imported Sentry API guide should be treated as the current EVA API specification unless later first-class source material narrows or changes it.

The guide does not document a general claim search endpoint or native batch endpoint. Specific cases and reports should therefore still follow the identifier and report-list patterns documented in the guide.



Additionally, for small / helpful changes, they do seem to be open to assisting and working with us on this.



**Fields in EVA:**



Case/Po - Our reference. This is manually created by an admin worker during addition to EVA. 



Claim no - "Their" ref - ie the work providers reference



Principal - An internal code that Collision Engineers Use for work providers



Type - Outcome of the inspection. If this is blank, the Engineer has not completed this.



Released - This is a date field. This contains a date that the report is sent to the client, if the Engineer has completed the assessment.




