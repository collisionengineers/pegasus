# CollisionDocNetExtractor local release candidate

This package is a framework-dependent `.NET 10` development candidate for the custom managed CollisionDocNetExtractor library. The public entry point is `CollisionDocNet.Extraction.DocumentExtractor`; format-specific packages are implementation dependencies rather than separate extraction engines.

This candidate is not an accepted production release. Format coverage remains row-specific in the repository compatibility matrix. Distribution is not authorised until product ownership and licence terms are approved. `PackageRequireLicenseAcceptance` is false because no licence text has yet been authorised for a consumer to accept; that metadata must not be read as a grant of rights.

The product never requires Microsoft Office, Outlook, an external office suite, a desktop session, a hosted conversion service or a third-party format-extraction engine.

The supported extracted payload is text plus image files only. JSON also carries the minimum provenance, issue, outcome and resource-control evidence required to explain those payloads. The package does not emit arbitrary non-image attachments or embedded-object bytes.
