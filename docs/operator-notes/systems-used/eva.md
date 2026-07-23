EVA is the current case management system used by Collision Engineers. It also contains their estimating systems (it does not provide these directly - it is a wrapper around other systems such as Audatex or Glass's).

When a case is ready to be passed to an Engineer, it is input into EVA. It is then assigned to an Engineer.

Currently, Collision Engineers use cedocumentmapper to extract details from a PDF into a JSON file. Then, this JSON is dragged and dropped into EVA, which fills in most of the key details.

EVA also contains integrations with valuation services, and stores vehicle valuations for cases.

It also is responsible for generating their final report that is sent back to a provider.

The intention with this project is to eventually replace EVA in all of its functions and integrations, whilst also providing far greater automation for the business.

EVA also offers an API which details can be found on here: docs/reference/EVA/EVA_API_SCHEMA
