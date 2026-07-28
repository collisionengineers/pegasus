# Support policy

The supported product surfaces are the managed extraction library and the one-shot headless CLI. Support is feature-row based: only inputs and behaviours with recorded evidence in the compatibility matrix are supported, and `Complete` applies only to the declared subset observed in that extraction.

The first packaging baseline is framework-dependent .NET 10 on Windows. A compatible Microsoft .NET 10 runtime is caller-owned. Linux, self-contained, single-file and Native AOT outputs are unsupported until their own evidence records exist. Desktop UI, Office/Outlook automation, external office-suite runtime use, web hosting, mailbox access and format conversion are outside support.

Security reports should include package-manifest hash, package version, extractor/schema/configuration identities, detected format/outcome, bounded resource measures and a non-sensitive correlation identifier. Reports and logs must not contain extracted content or sensitive filenames. Retain the original input privately; do not attach it to an issue or upload it without explicit data-handling authorisation.

There is no release acceptance or compatibility-service-level commitment yet. Unsupported or partially implemented features must remain visible as issues/non-complete outcomes rather than triggering another engine.
