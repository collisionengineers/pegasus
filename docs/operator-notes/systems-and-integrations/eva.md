# EVA

EVA is the current case-management system used by Collision Engineers. It also
contains their estimating systems; it does not provide them directly, but wraps
systems such as Audatex or Glass's.

When a case is ready to be passed to an Engineer, it is input into EVA. It is then assigned to an Engineer.

Currently, Collision Engineers use cedocumentmapper to extract details from a PDF into a JSON file. Then, this JSON is dragged and dropped into EVA, which fills in most of the key details.

EVA also contains integrations with valuation services, and stores vehicle valuations for cases.

It is also responsible for generating the final report sent back to a provider.

The intention with this project is to eventually replace EVA in all of its functions and integrations, whilst also providing far greater automation for the business.

EVA also offers an API. Supplied details are routed from the
[EVA API schema](../../reference/EVA/EVA_API_SCHEMA.md).
