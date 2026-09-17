# Operations — how it works (external work only so far)

Read from the live source on 13 September 2026. Only the external-work part is written up; the rest of the page is still to come.

External work is a job the Worker runs against a service outside Pegasus, queued durably with an attempt count (`src/Pegasus.Core/Custody/ExternalWorkProcessing.cs`):

| Kind | What it does | Outside service |
| --- | --- | --- |
| Create case custody | creates the Case's file storage when a Case is created | Box |
| Create audit reference custody | creates storage for an audit reference | Box |
| Create image case custody | creates storage for an image-initiated Case | Box |
| Merge image case custody | moves an image case's files into the Case it was merged with | Box |
| Vehicle lookup | fetches vehicle data for a registration | vehicle data provider |
| Intake OCR | reads text from a scanned received file | OCR provider |

A dependency-shaped failure retries itself at 1, 5 and 15 minutes, then 1 and 6 hours, six attempts in all (`ImageCustodyRetryPolicy`; vehicle lookup follows the same shape). When the failure is terminal or the attempts run out it becomes a request operation in state Failed with `CanRetry` (`src/Pegasus.Core/Operations/RequestOperations.cs`). Operations lists it with Retry; the Work Centre today also lists it as a High row pointing at Operations. All three staff roles can open Operations.

Governing documentation: [FRD-12 § Operations](../../../../../docs/frd/frd-12-operator-experience.md#operations), [FRD-05](../../../../../docs/frd/frd-05-documents-extraction-and-custody.md) (custody), [FRD-06](../../../../../docs/frd/frd-06-vehicle-and-engineering-evidence.md) (vehicle lookup).
