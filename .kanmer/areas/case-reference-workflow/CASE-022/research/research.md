# Research — CASE-022: production public-upload failure

## Question

Why does every attempted upload through a generated INT-31 link fail, and what
does the implementation and live production estate show about the failure?

## Findings

- The production upload path is wired and reachable, but its content-store call
  cannot succeed. `EfDocumentRequestStore.IUploadToRequest.ExecuteAsync` creates
  document/version/occurrence entities and then calls
  `IDocumentContentStore.StoreAsync`
  (`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:174-303`).
  Production registers `BoxDocumentContentStore`, whose `StoreAsync`
  deliberately always throws `InvalidOperationException`: managed Box writes
  require a persisted `ManagedDocumentContentAddress`
  (`src/Pegasus.Infrastructure/Custody/BoxDocumentContentStore.cs:175-183`).
  This is a deterministic production-only incompatibility, not an intermittent
  Box or Azure outage.
- The working managed-document path already exists in
  `EfDocumentCustodyStore`: it allocates the case document ordinal, carries
  `CustodyRootRemoteId`, builds a `ManagedDocumentContentAddress`, and calls
  `StoreVersionAsync`
  (`src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs:47-124`).
  The request-upload path does none of those things. Its new
  `CaseDocumentEntity` and `DocumentOccurrenceEntity` also leave `Ordinal`
  at the default value, so merely switching the method name would still not
  produce the required Box address.
- Live Application Insights corroborates the exact code path. At
  2026-09-03 13:30:06Z production recorded one
  `POST /Uploads/Request`; its correlated exception at 13:30:07Z is
  `System.InvalidOperationException: Managed Box writes require the persisted
  business occurrence and revision address.` All correlated SQL dependencies
  succeeded and no Box dependency was attempted. The handler catches that
  exception and returns the upload form with an error, so request telemetry
  records HTTP 200/success even though the upload failed
  (`src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs:79-147`).
- The live database contains one request link, now `Revoked`, created
  2026-09-03 13:29:32Z. It has zero accepted files and bytes.
  `RequestUploadReceipts` contains zero rows and there are zero
  `DocumentOccurrences` with source `RequestUpload`. No uploaded content or
  successful receipt needs migration or cleanup.
- Azure control-plane evidence shows the current Web revision
  `pegasus-prod-web-252ow37gij--0f0e90ae44ff` is Healthy, Provisioned, receives
  100% traffic, and has one replica. Activity-log writes on 2026-08-30 and
  2026-09-02 succeeded. AppLens and Azure Resource Health do not support
  `Microsoft.App/containerApps`; the available revision, activity, SQL, and
  Application Insights evidence rules out general resource unavailability.
- The existing tests miss the production boundary. The web test substitutes a
  recording `IUploadToRequest`
  (`tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs:82-109`), while the
  durability test constructs `EfDocumentRequestStore` with
  `LocalDocumentContentStore`
  (`tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs:284-380`).
  No test composes the real request store with the managed Box content-store
  contract.
- Application Insights request telemetry retains the full upload URL, including
  the bearer token in the route path. The live query exposed that credential;
  it is intentionally not copied into this ticket. Because possession of the
  URL authorizes upload, telemetry must redact or suppress that path value.
  The observed link has already been revoked, limiting the immediate exposure.
- The route currently materialises each file multiple times: ASP.NET buffers the
  form file, the page copies it to `MemoryStream`, `ToArray()` creates another
  byte array, and the Box client accepts in-memory content. This does not cause
  the observed 10 MiB failure, but it is a binding constraint before raising
  limits toward the ticket's earlier 250 MiB/1 GiB proposal.
- The ticket body predates later delivery work. INTK-051 activated the existing
  route under interim limits in release 37; activation did not repair the
  storage-contract mismatch. Current governing text records a fixed 10 MiB
  interim limit and further limit/session work under INTK-052/INTK-055, so the
  failure repair must not silently broaden limits or duplicate those tickets.
- No project research sources are declared for this area/label set. Repository
  sources and read-only live Azure/SQL observations were used.

## Implications

CASE-022 must repair the request upload as a managed Box write: allocate the
same persisted business address fields as the existing custody writer, call the
managed `StoreVersionAsync` contract, and preserve the current atomic
SQL/content rollback and replay semantics. It also needs a production-shaped
integration test that would fail against the current `BoxDocumentContentStore`
contract, and upload-token redaction in telemetry. No compatibility path is
needed: the live estate has no successful request-upload records.

The immediate correctness repair is separate from changing interim upload
limits or the later 15-minute add/replace session. Those remain with the
tickets and governing decisions that already own them.

## Open questions

None. The failure, required storage contract, absence of successful live data,
and telemetry-token exposure are established by read-only evidence.
