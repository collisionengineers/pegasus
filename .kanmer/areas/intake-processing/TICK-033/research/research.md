# TICK-033 — Request-Scoped External Upload Link

## Research and implementation recommendation

**Research date:** 2026-08-18

## 1. Executive conclusion

TICK-033 should be implemented as an **application-owned upload capability**, not as another Box File Request and not as a self-contained signed URL.

The recommended first implementation is:

> A database-backed, opaque, revocable upload grant; a minimal anonymous Pegasus upload page; one-file-at-a-time streaming through the Web host into private Azure quarantine storage; and asynchronous validation, custody and intake processing through the existing Worker/Core path.

The design should hide the physical upload mechanism behind an application interface so that a future version can issue short-lived, single-file Azure Blob SAS credentials for large or resumable uploads without changing the Core contract.

This gives Pegasus direct enforcement of:

- Immediate application-level revocation.
- Expiration.
- Exact request binding.
- Per-file and aggregate limits.
- Cross-request isolation.
- Malware quarantine.
- Idempotent retries.
- Audit history.
- The rule that the anonymous uploader sees only the upload form and the result of the current upload.

ASP.NET Core supports unbuffered streaming, avoiding loading an entire upload into application memory or temporary disk. Microsoft and OWASP both recommend application-generated storage names, explicit type and size controls, storage outside the application tree, and malware validation before uploaded content is used.

### Recommendation by option

| Method | Recommendation |
|---|---|
| Pegasus-proxied streaming to private quarantine | **Implement first** |
| Direct browser-to-Azure Blob upload using a narrowly scoped SAS | Retain as a scaling option |
| Box File Request | Do not use as the target architecture |
| Stateless JWT or Data Protection token without persisted grant state | Do not use as the authorization model |
| Direct anonymous upload into the final Box case folder | Reject |

The principal reason to prefer proxy streaming initially is revocation. Once a storage SAS has been issued, Pegasus is no longer the gatekeeper for the remaining lifetime of that credential. Azure supports revoking a user delegation key, but that invalidates every SAS derived from the key and can be subject to propagation delay; it is not precise per-link revocation.

## 2. Research scope and source-access limitation

The supplied GitHub repository was not readable through the unauthenticated GitHub/web facilities available during the research session, and no GitHub repository connector was installed in that session. Exact class names, source paths, namespaces, migrations and current `dev` implementation details were therefore not asserted.

The Pegasus-specific findings in this research derive from the TICK-033 contract and previously retrieved Pegasus architectural context. Proposed file and component names are conceptual until mapped against `dev`.

External implementation guidance was checked against primary documentation from:

- Microsoft ASP.NET Core documentation.
- Microsoft Azure Storage documentation.
- Microsoft Azure architecture guidance.
- OWASP file-upload and token-security guidance.
- Box developer documentation.
- Microsoft EF Core documentation.

## 3. Ticket contract

TICK-033 requires authenticated staff to generate a temporary, revocable, expiring, request-scoped link for isolated unauthenticated image/document upload. The public surface exposes only the upload form and immediate result, never case, reference, request or another document's state.

The ticket identifies these as acceptance gates:

- Token handling.
- Upload limits.
- Custody.
- Retry behaviour.
- Revocation.
- Abuse resistance.
- Cross-request isolation.

It states that this capability supersedes Box File Request, while remaining non-blocking for the current alpha acceptance boundary.

### Derived invariants

| Requirement | Required enforcement |
|---|---|
| Only authenticated staff may create a link | Core authorization on link creation |
| The link is temporary | Server-side `ExpiresAt`, enforced on every meaningful transition |
| The link is revocable | Persisted grant state, not expiry alone |
| The link is request-scoped | Internal foreign key to the exact intake/document request |
| The uploader is anonymous | Possession of the secret grants only narrow upload authority |
| No case or request details are exposed | Separate public DTO and page model containing no domain data |
| No other documents are exposed | No public list, read, download, replace or delete operation |
| Limits are enforceable | Atomic reservations and server-side byte counting |
| Custody is durable | Quarantine record followed by the existing Worker/Core custody path |
| Retries are safe | Idempotency keys, durable state and unique constraints |
| Revocation is auditable | Staff actor, timestamp, reason and operation history |
| Abuse is bounded | Rate, concurrency, file count, byte and content controls |
| Immediate result is shown | Result means current bytes were durably received or rejected, not that downstream processing finished |

The ticket should not be interpreted as a lightweight "unguessable URL to an upload controller". It is a small capability-security system with its own state machine.

A **capability URL** means that whoever possesses the secret link is allowed to perform one narrowly defined action. It does not make them a Pegasus user and must not let them navigate around Pegasus.

## 4. Relevant Pegasus architecture

Based on retrieved project context, Pegasus separates:

- Core-owned use cases and policy.
- The authenticated Web application.
- A Worker responsible for queued intake processing.
- Durable SQL state.
- Box custody.
- Actor and operation-history records.
- Azure deployment and infrastructure.

TICK-033 should fit those boundaries rather than introduce a second intake system.

### Boundary recommendation

The anonymous Web endpoint should be an adapter around Core use cases. It should not:

- Create a case directly.
- Write directly to arbitrary Box folders.
- Construct its own receipt/case association.
- Duplicate the existing intake state machine.
- Perform long-running extraction or custody work in the HTTP request.
- Attribute the upload to the staff member who generated the link.

The upload actor should be represented as an **external upload grant**, while the staff actor remains the creator or revoker of that grant.

## 5. Implementation methods considered

### 5.1 Method A — Stream through Pegasus into private quarantine

#### Flow

```text
External browser
    |
    | HTTPS upload
    v
Pegasus Web
    |  validate grant, reserve quota, stream bytes
    v
Private quarantine storage
    |
    | durable event/outbox
    v
Pegasus Worker
    |  scan, validate, classify and invoke existing intake/custody use case
    v
Box custody / normal intake workflow
```

#### Advantages

Pegasus retains control throughout the HTTP operation. It can:

- Refuse a revoked or expired grant before upload.
- Stop reading after the permitted byte count.
- Recheck revocation before committing the file.
- Enforce count and total-byte quotas.
- Generate the storage name.
- Prevent a user from choosing another request or object.
- Produce one authoritative audit record.
- Avoid issuing storage credentials to an anonymous browser.
- Avoid browser-to-storage CORS configuration.

ASP.NET Core's buffered `IFormFile` model can use substantial memory or temporary disk under concurrent uploads. Its streaming path uses `MultipartReader` or direct request-body processing and is the appropriate basis when file sizes or concurrency are material.

#### Disadvantages

- The Web host carries the bandwidth and connection duration.
- Large or interrupted mobile uploads start again unless resumability is added.
- Web replicas need appropriate connection, request-body and rate limits.
- Slow clients can occupy server connections.

Kestrel exposes request-size, concurrent-connection and minimum body-rate controls, although the body-rate threshold must remain low enough not to reject legitimate mobile users.

#### Assessment

**Best initial fit.**

The complexity is lower than direct storage upload and its revocation semantics fit the ticket more closely.

### 5.2 Method B — Direct browser upload to Azure Blob Storage

This implements the Azure **Valet Key** pattern:

1. Pegasus validates the grant.
2. Pegasus reserves one logical upload.
3. Pegasus issues a short-lived credential for one exact blob name.
4. The browser uploads directly to Blob Storage.
5. The browser calls a Pegasus completion endpoint.
6. Pegasus verifies the resulting blob before accepting it into the workflow.

The pattern reduces Web-host bandwidth and improves scalability. Azure recommends limiting the storage location, permissions and validity period as tightly as possible.

#### Required restrictions

A direct-upload credential should grant:

- One exact blob.
- Create only where the selected storage API version/client supports it.
- No read.
- No list.
- No update of an existing blob.
- No delete.
- A lifetime measured in minutes.
- HTTPS only.

Current Azure REST documentation permits `Create (c)` for creating new block blobs through `Put Block` and `Put Block List` with sufficiently recent service versions. The actual Azure SDK and service-version behaviour must be verified before relying on create-only block uploads.

Use a **user delegation SAS** generated through managed identity rather than an account-key SAS, avoiding storage account keys in application configuration.

#### Important limitations

A storage credential generally cannot enforce the full Pegasus policy:

- It cannot reliably limit total request bytes across several files.
- It may not limit the number of operations.
- Pegasus cannot stop an already-issued credential on a per-link basis.
- The browser may still upload after the Pegasus grant is revoked, until the SAS expires.
- A global delegation-key revocation affects unrelated SAS credentials.
- CORS must be configured.
- The quarantine storage endpoint must be publicly reachable by the browser.
- A separate finalisation protocol is mandatory.

#### Safe revocation interpretation

With direct Blob upload, "revoked" can safely mean:

- No new SAS credentials are issued.
- No new reservation is created.
- Any finalisation attempt is rejected.
- Uploaded but unfinalised data is quarantined and later deleted.
- Nothing received after revocation can enter the normal intake or custody workflow.

It cannot mean that every previously issued storage credential instantly stops transmitting bytes.

#### Assessment

**Good later optimisation, but not the preferred first implementation.**

Adopt it immediately only if source inspection shows that expected upload size, mobile reliability or concurrency makes Web proxying unsuitable.

### 5.3 Method C — Box File Request

Box File Request offers an unauthenticated upload page, generated URL, expiry and active/inactive status. Deactivating a request stops new submissions, and expiry automatically makes it inactive.

However, the API creates File Requests by copying an existing template File Request associated with another folder.

#### Advantages

- Low initial UI effort.
- Box handles the public upload form.
- Expiration and deactivation exist.
- Files arrive in Box.

#### Problems for TICK-033

- It keeps Box, not Pegasus, as the public capability authority.
- Application-owned count and aggregate-byte limits are difficult.
- Box folder/template management becomes part of request creation.
- Uploads can enter Box before Pegasus validates or quarantines them.
- Cross-request isolation depends partly on externally managed folder state.
- Retry and idempotency must be reconstructed from Box events.
- Immediate upload state and subsequent Pegasus processing state can diverge.
- It does not naturally reuse the existing Core intake command.
- The ticket explicitly says INT-31 supersedes Box File Request.

A design could create a temporary Box staging folder, copy a template File Request into it, subscribe to an event and then import the uploaded file. That would add external lifecycle, webhook and cleanup complexity while still providing weaker application-level control.

#### Assessment

**Do not use as the target implementation.**

It remains a possible temporary fallback only when management explicitly accepts the reduced policy control.

### 5.4 Method D — Stateless signed token

ASP.NET Core Data Protection can create cryptographically protected, time-limited payloads and is suitable for short-lived application tokens when the shared key ring is correctly persisted across containers or replicas.

A payload could contain:

```text
request-id | expiry | maximum-files | nonce
```

and be signed or encrypted without storing a token record.

#### Problem

Expiry is easy, but immediate per-link revocation, consumed quota, usage history, reissue, concurrent reservations and abuse state all require persisted data anyway.

Adding a revocation table to a "stateless" token removes most of its purported simplicity.

#### Assessment

**Do not use Data Protection or JWT as the sole grant authority.**

Data Protection remains appropriate for:

- A short-lived internal upload-session cookie after link exchange.
- Protecting non-authoritative browser session data.
- Encrypting temporary server-generated state.

The authoritative grant should remain a database record.

## 6. Recommended architecture

### 6.1 High-level design

```text
Authenticated staff
    |
    v
Create/Revoke request-scoped upload grant
    |
    v
Core policy ------------------> SQL grant + ActionHistory
    |
    | secret link
    v
Minimal anonymous page
    |
    v
Reserve one upload atomically
    |
    v
Stream one file to private Azure quarantine
    |
    v
Return only "received" or current-file rejection
    |
    v
Durable outbox / queue
    |
    v
Worker validation and scan
    |
    v
Existing Core intake/custody use case
    |
    v
Box custody
```

### Architectural rule

The public upload mechanism should end at a durable, quarantined upload record. The HTTP request should not wait for:

- Malware scanning.
- Document extraction.
- Box transfer.
- Case creation.
- Matching.
- Classification.
- Other downstream intake processing.

### 6.2 Separate policy from transport

Introduce an abstraction conceptually equivalent to:

```text
IExternalUploadTransport
```

Possible implementations:

```text
ProxiedQuarantineUploadTransport
AzureBlobValetUploadTransport
```

Core should decide whether a grant may reserve or accept a file. The transport should only move bytes to the designated quarantine object.

This allows direct Blob upload to be introduced later without replacing grant records, quotas, public-session rules, audit events, Worker processing, staff management UI or acceptance tests.

## 7. Token and session design

### 7.1 Use an opaque random secret

Generate at least 32 cryptographically random bytes and encode them with base64url.

Persist:

- The grant identifier.
- A hash or keyed HMAC of the secret.
- Expiry and lifecycle state.

Do not persist the raw URL secret unless there is a separately accepted requirement to redisplay old links.

Recommended behaviour:

- Display the full link once when it is created.
- Later UI offers **Replace link**, not **Reveal secret**.
- Replacing creates a new grant or secret version and revokes the previous one.
- Never write the secret to ActionHistory, application logs or telemetry.

Because this is a high-entropy machine-generated secret, SHA-256 is already resistant to offline guessing. An HMAC using a Key Vault-backed application secret provides additional separation if the SQL database is disclosed.

### 7.2 Harden the URL exchange

The ordinary implementation is:

```text
/upload?t=<secret>
```

It works, but the secret can appear in reverse-proxy logs, Web-server logs, Application Insights request URLs, browser history, screenshots and referrer headers if the page loads external resources.

A hardened alternative uses two parts:

```text
/upload/<public-selector>#<secret>
```

The URL fragment is not sent as part of the initial HTTP request. Page JavaScript exchanges it in a POST body for a short-lived, restricted session cookie and immediately clears the fragment from browser history.

Recommended exchange:

1. GET the landing page using only the non-sensitive selector.
2. JavaScript POSTs selector and secret to a session endpoint.
3. Server validates the database grant.
4. Server creates a short-lived `Secure`, `HttpOnly` capability-session cookie.
5. Browser removes the secret from the visible URL.
6. All later upload operations use the restricted session.

This method requires JavaScript. If a no-JavaScript public form is mandatory, use the query/path token design with explicit proxy and telemetry redaction.

#### Email scanner rule

A GET must never consume, close or count the link. Email security products commonly fetch links before the recipient opens them. Consumption must occur only when a valid upload is committed.

### 7.3 Restricted session

The resulting session may only:

- Render the upload page.
- Reserve a file.
- Submit content for that reservation.
- Return the result of that current operation.

It must not authorize ordinary Pegasus routes.

Use:

- A distinct cookie name and authentication scheme.
- Short session lifetime bounded by the grant expiry.
- `Secure`.
- `HttpOnly`.
- Appropriate `SameSite` settings.
- Antiforgery protection on state-changing Web requests.
- A grant/version check for every reservation and final commit.

## 8. Proposed data model

Names are conceptual until mapped against `dev` conventions.

### 8.1 `ExternalUploadGrant`

| Field | Purpose |
|---|---|
| `Id` | Internal identifier |
| `PublicSelector` | Opaque non-secret value used to locate the grant |
| `SecretHash` | Hash/HMAC of the secret |
| `SecretVersion` | Invalidates existing sessions after replacement |
| `RequestId` | Exact internal intake/document request binding |
| `OrganisationId` / principal boundary | Enforces tenant ownership internally |
| `CreatedByActorId` | Authenticated staff creator |
| `CreatedOperationKey` | Idempotent creation/audit correlation |
| `CreatedAt` | Audit |
| `ExpiresAt` | Hard expiry |
| `RevokedAt` | Revocation state |
| `RevokedByActorId` | Staff revoker |
| `RevocationReason` | Controlled reason |
| `ClosedAt` | Optional intentional completion |
| `Status` | Active, Revoked, Expired, Exhausted, Closed |
| `UploadPolicyVersion` | Versioned file/limit policy |
| `MaxFiles` | Hard file-count limit |
| `MaxBytesPerFile` | Per-file limit |
| `MaxTotalBytes` | Aggregate limit |
| `ReservedFiles` / `ReservedBytes` | Concurrent quota reservations |
| `AcceptedFiles` / `AcceptedBytes` | Committed usage |
| `RowVersion` | Concurrency protection |

Multiple grants may bind to one request. This permits safe replacement or separate recipients without weakening request scope.

### 8.2 `ExternalUploadFile`

| Field | Purpose |
|---|---|
| `Id` | Internal upload identifier |
| `GrantId` | Parent grant |
| `ClientUploadKey` | Browser-generated idempotency key |
| `OriginalFileName` | Sanitised display-only name |
| `StorageObjectName` | Server-generated quarantine name |
| `DeclaredContentType` | Untrusted client claim |
| `DetectedContentType` | Server-detected type |
| `DeclaredLength` | Used for quota reservation |
| `ActualLength` | Authoritative streamed byte count |
| `Sha256` | Custody and duplicate evidence |
| `State` | Reservation and processing state |
| `FailureCode` | Controlled failure classification |
| `QuarantineLocation` | Private storage locator |
| `ReservedAt` / `ReceivedAt` | Lifecycle times |
| `ScanStatus` / `ScannedAt` | Malware gate |
| `CustodiedAt` | Final custody proof |
| `ReceiptId` | Created only after accepted intake handoff |
| `OperationKey` | Idempotent Worker/Core call |
| `RowVersion` | Concurrency protection |

Do not put a Case/PO number, registration, person name or original filename into the Azure blob path.

Suggested path:

```text
external-quarantine/<grant-id>/<upload-id>
```

Both identifiers should be opaque internal values.

## 9. Upload protocol

### 9.1 Prefer one file per operation

A browser may allow multi-select, but it should submit each selected file independently.

This improves:

- Quota accounting.
- Partial failure handling.
- Retry behaviour.
- Immediate result clarity.
- Malware state.
- Idempotency.
- Cancellation.
- Cleanup.

The client may use low parallelism, such as one or two concurrent uploads, but the server remains authoritative.

### 9.2 Reserve before receiving bytes

Recommended public protocol:

```text
POST /external-upload/files
PUT  /external-upload/files/{opaque-upload-id}/content
```

The first request supplies metadata such as:

```json
{
  "clientUploadKey": "browser-generated-random-id",
  "fileName": "vehicle-front.jpg",
  "declaredSize": 5242880,
  "declaredContentType": "image/jpeg"
}
```

The server:

1. Resolves the capability session.
2. Checks grant status and expiry.
3. Validates the declared size and candidate extension.
4. Atomically reserves one file and the declared bytes.
5. Creates the upload row and server-generated storage name.
6. Returns an opaque upload identifier.

The `PUT` streams the raw file body. This is simpler to control than a large multipart form because metadata and quota have already been resolved.

A multipart fallback can be provided if no-JavaScript upload is required. In that case, disable normal file model binding and stream with `MultipartReader`; otherwise ASP.NET Core may buffer multipart files.

### 9.3 Enforce quotas atomically

Two tabs or devices may use the same link concurrently. A read-then-write check is unsafe because both requests may observe the same remaining quota.

Use a conditional update or concurrency token, conceptually:

```sql
UPDATE ExternalUploadGrant
SET
    ReservedFiles = ReservedFiles + 1,
    ReservedBytes = ReservedBytes + @DeclaredBytes
WHERE
    Id = @GrantId
    AND Status = 'Active'
    AND ExpiresAt > @Now
    AND ReservedFiles + AcceptedFiles + 1 <= MaxFiles
    AND ReservedBytes + AcceptedBytes + @DeclaredBytes <= MaxTotalBytes;
```

Proceed only when one row was updated.

SQL Server `rowversion` and EF Core optimistic concurrency can detect conflicting changes, but conflicts must be handled and retried deliberately.

When the client does not provide a reliable length:

- Reserve the maximum permitted size for that file.
- Enforce an actual stream byte counter.
- Release unused reservation after successful commit.
- Abort as soon as the hard limit is exceeded.

Never rely only on `Content-Length`; chunked requests and incorrect values must still be bounded while reading.

### 9.4 Commit semantics

The immediate successful response should mean:

> The bytes have been durably written into private quarantine storage, their size and hash are recorded, and downstream processing has been durably scheduled.

It must not mean:

- The file is malware-free.
- The file has reached Box.
- A case was created.
- The file was matched.
- The document was extracted successfully.
- The job is ready.

Return a minimal result such as:

```json
{
  "result": "received",
  "fileName": "vehicle-front.jpg"
}
```

The public result must not include case ID, request ID, Case/PO reference, vehicle registration, Box folder, receipt ID, other uploaded filenames, queue/custody state or a file-retrieval URL.

## 10. Revocation semantics

### 10.1 Recommended rules

When a staff member revokes a grant:

1. New session exchanges fail.
2. Existing capability sessions fail on their next server operation.
3. New reservations fail.
4. Replacement or renewal fails.
5. An upload still transmitting may finish writing only to quarantine.
6. Before committing that upload as accepted, Pegasus rechecks the grant.
7. If the grant is now revoked or expired, the upload is rejected and quarantined content is deleted according to cleanup policy.
8. Files already accepted and durably queued before revocation continue processing.
9. Revocation does not erase already accepted evidence.
10. The action is written to permanent history without the link secret.

This creates a clear boundary: revocation stops future business acceptance while preserving evidence already validly received.

### 10.2 Why two checks are required

Check the grant:

- Before reservation.
- Again when the file is committed.

Without the second check, revocation during a long upload has no effect.

### 10.3 Direct Blob caveat

When using a SAS, a revoked Pegasus grant may not stop an already-issued SAS from writing bytes until its short expiry. Pegasus must reject finalisation and prevent those bytes from entering custody.

## 11. File security and custody

OWASP recommends defence in depth using extension allowlisting, MIME validation, file-signature validation, application-generated names, size limits, isolated storage and malware or sandbox inspection. No single file-type signal is sufficient.

### 11.1 Validation stages

#### Before reservation

- Candidate extension belongs to the configured upload policy.
- Declared file size is within policy.
- Filename length is within policy.
- Request still has quota.

#### While streaming

- Hard byte limit.
- Request cancellation.
- Cryptographic hash computation.
- Storage write to a server-generated object.
- No parsing of complex document contents in the Web process.

#### After durable receipt

- Extension versus detected type.
- Signature or magic-byte validation.
- Safe parser/decode check where appropriate.
- Archive and decompression-bomb controls if packaged formats are allowed.
- Malware scanning.
- Optional content disarm and reconstruction for accepted document types.
- Domain-specific intake validation.

The original filename is untrusted. Strip paths, encode it for display and generate a different storage name.

### 11.2 Allowed types

The final allowlist must be derived from:

- FRD-02.
- Existing intake adapters.
- Existing Box and extraction support.
- Actual QDOS operating evidence.

Do not add a broad "documents" list merely because the browser supplies a familiar MIME type.

Likely candidates may include common vehicle-image formats and PDF, but `.doc`, `.docx`, `.msg`, archives and other packaged formats should not be enabled until their existing parser and security boundaries are confirmed.

### 11.3 Quarantine storage

Use a private Azure Blob container or equivalent private object store:

- Anonymous storage access disabled.
- No public listing.
- No execute capability.
- Managed identity for Pegasus services.
- Random object names.
- No sensitive metadata in paths or tags.
- Short lifecycle for abandoned and rejected objects.
- Explicit retention policy for malware samples.
- Storage separate from the Web application filesystem.

Azure lifecycle rules can provide secondary cleanup for expired temporary blobs; the application should still delete known aborted uploads promptly.

### 11.4 Malware scanning

Microsoft Defender for Storage can scan uploaded blobs after Blob events and publish a scan result.

When used:

- A blob remains quarantined until an explicit clean result is recorded.
- Missing, delayed or failed scan results fail closed.
- The Worker treats duplicate scan events idempotently.
- Scan configuration and cost controls belong in Bicep and operational documentation.
- Production enablement requires the ticket's live-approval boundary.

If Defender is unavailable, use a separately accepted scanning service. Do not silently equate "scan unavailable" with "clean".

### 11.5 Evidential integrity

If Pegasus applies content disarm, re-encoding or transformation:

- Do not silently replace the received evidence.
- Preserve the original hash.
- Record the transformation and resulting hash.
- Keep original and derived artifacts distinguishable.
- Follow the FRD's custody and retention policy.

For images, decoding and re-encoding can be useful for a safe derivative, but it changes the original bytes. The original should remain identifiable as the received artifact.

## 12. Worker, queue and retry design

### 12.1 Durable dispatch

The SQL transaction that marks an upload as received should also create a durable outbox or equivalent dispatch record.

This prevents a successful storage write and database commit followed by failed queue publication from leaving an upload permanently stranded.

The Worker then processes the outbox idempotently.

### 12.2 Worker stages

```text
Received
  -> ScanPending
  -> Clean / RejectedMalicious / ScanFailed
  -> Validated
  -> IntakeQueued
  -> CustodyPending
  -> Custodied
  -> Completed
```

Retryable states should be separate from permanent rejection.

Examples:

- `ScanUnavailable` — retryable.
- `BoxTransientFailure` — retryable.
- `UnsupportedFileType` — permanent.
- `MalwareDetected` — permanent quarantine/rejection.
- `RequestRevokedBeforeCommit` — permanent rejection.
- `StorageObjectMissing` — investigate and classify.

### 12.3 Idempotency

Use:

- A browser-generated `ClientUploadKey`.
- A unique constraint on `(GrantId, ClientUploadKey)`.
- A stable Core operation key.
- A unique logical custody association.
- State-transition guards.
- Hash and storage identity evidence.

A browser retry with the same key should return the existing result rather than create a second document.

A Worker retry must not create a second intake receipt, second Box object, second request association or duplicate permanent-history entries for the same transition.

## 13. Public UI

### 13.1 Page content

The public page should show only:

- Pegasus/Collision Engineers identity.
- A concise request to select or capture files.
- Permitted file types.
- Per-file and total limits.
- Expiry information at an appropriate level.
- Upload progress for files selected in the current browser session.
- The immediate outcome of each current upload.
- A generic unavailable-link page.

It should not show:

- Case or request reference.
- Customer, provider or principal.
- Vehicle registration.
- Previously uploaded files.
- Count or state of files uploaded on another device.
- Case progress.
- Box location.
- Ordinary Pegasus navigation.
- Search, download or delete controls.

A client-side list of files selected during the current browser session is acceptable. It should not be reconstructed from a public server-side listing after reload.

### 13.2 Mobile behaviour

The page should support:

- Mobile file selection.
- Camera capture through the platform file picker.
- Interrupted connections.
- Per-file retry.
- Clear progress.
- Avoidance of one giant multipart submission.
- Accessible keyboard and screen-reader operation.

Resumable block upload should be considered only when evidence shows that real uploads are large enough or network conditions unreliable enough to justify the direct Blob option.

## 14. HTTP and browser protections

Recommended anonymous-page headers:

```text
Cache-Control: no-store, private
Pragma: no-cache
Referrer-Policy: no-referrer
X-Content-Type-Options: nosniff
X-Robots-Tag: noindex, nofollow, noarchive
Content-Security-Policy:
    default-src 'self';
    base-uri 'none';
    object-src 'none';
    frame-ancestors 'none';
    form-action 'self'
```

Also enforce:

- HTTPS and HSTS.
- No third-party analytics on the public upload page.
- No externally hosted fonts, scripts or images.
- Antiforgery protection.
- No secret in response HTML.
- No secret in error messages.
- No request URL containing a secret in telemetry.
- Generic invalid, expired and revoked responses.

## 15. Abuse controls

Use several independent controls.

### Authoritative controls

- Cryptographically strong capability secret.
- Grant expiry.
- Grant revocation.
- Per-file bytes.
- Maximum files.
- Maximum aggregate bytes.
- Atomic reservation.
- One request binding.
- Generated object identity.
- Scan-before-use.
- No retrieval permission.

### Traffic controls

- Per-grant request limiter.
- Per-IP limiter.
- Global anonymous-upload limiter.
- Concurrent-upload limiter.
- Maximum active reservations per grant.
- Reservation timeout.
- Request-body timeout and cancellation.
- Edge or WAF rules where already available.

ASP.NET Core provides fixed-window, sliding-window, token-bucket and concurrency rate-limiting mechanisms.

An in-process limiter is not the authoritative quota when multiple Web replicas are running. SQL-backed count and byte reservations remain authoritative; traffic middleware limits resource consumption on each host.

### Adaptive CAPTCHA

Do not make CAPTCHA the primary security boundary. It may be added after repeated suspicious failures, but it does not replace token, quota, rate or scanning controls and introduces accessibility/privacy considerations.

## 16. Threat-to-control mapping

| Threat | Primary controls |
|---|---|
| Secret guessed | 256-bit random secret, rate limiting |
| Secret forwarded or stolen | Expiry, staff revocation, narrow authority |
| Secret appears in logs | Fragment/session exchange or explicit URL redaction |
| Host-header link poisoning | Configured canonical public origin |
| Link scanner consumes grant | GET is read-only and non-consuming |
| CSRF | Restricted session plus antiforgery |
| Path traversal | App-generated object name; original filename display-only |
| MIME spoofing | Extension, detected type, signature and parser validation |
| Malware | Private quarantine and scan-before-use |
| Archive/decompression bomb | Deny archives or enforce expansion limits |
| Oversized body | Reservation, request limit and streaming byte counter |
| Slow upload resource exhaustion | Concurrency and body-rate/time controls |
| Two clients consume last quota | Conditional SQL update/rowversion |
| Token A targets request B | Server derives target exclusively from validated grant |
| Existing object overwritten | Random object identity and create-only/conditional write |
| Public discovery of previous files | No list/read endpoint or permission |
| Queue publication lost | Transactional outbox or equivalent recovery |
| Duplicate Worker delivery | Stable operation key and unique constraints |
| Revocation during upload | Recheck immediately before acceptance |
| Direct SAS used after revocation | Very short SAS; reject finalisation; delete orphan |
| Malicious file reaches Box | Scan result required before custody |
| Failed downstream processing exposed publicly | Public response ends at durable quarantine receipt |

## 17. Staff UI and authorization

The authenticated side should provide:

- Create upload link.
- Select an expiry within the permitted policy.
- Apply a versioned upload profile.
- Copy the link once.
- View whether the grant is active, expired, revoked or exhausted.
- Revoke with a reason.
- Replace a link.
- See internal receipt/custody status where the user already has rights.

Use a narrowly defined staff right rather than a broad Administrator requirement unless the governing FRD explicitly requires Administrator-only operation.

Creation must verify that the staff actor is authorized for the bound organisation, principal and request. A user must not create a link for a request merely by supplying another internal identifier.

The upload itself is attributed to the external grant, not to the staff member who created it.

## 18. Conceptual Core use cases

Suggested application actions:

```text
CreateRequestScopedUploadGrant
ReplaceRequestScopedUploadGrant
RevokeRequestScopedUploadGrant
ResolveExternalUploadGrant
CreateExternalUploadSession
ReserveExternalUploadFile
CommitExternalUploadFile
RejectExternalUploadFile
ExpireExternalUploadReservations
AcceptScannedExternalUpload
DispatchExternalUploadToIntake
```

Suggested ports:

```text
IExternalUploadSecretGenerator
IExternalUploadQuarantineStore
IUploadContentInspector
IMalwareScanGateway
IExternalUploadOutbox
IClock
```

Exact names should follow conventions found on `dev`.

## 19. Testing and acceptance evidence

### 19.1 Core tests

- Only an authorized staff actor can create a grant.
- Staff cannot create a grant for another organisation's request.
- Expiry must be in the permitted range.
- Replacement invalidates the old secret/version.
- Revocation is idempotent.
- Expired and revoked grants cannot reserve.
- File and byte quotas are enforced.
- Simultaneous final-slot reservations permit only one winner.
- A grant cannot bind an upload to another request.
- Revocation before commit rejects the file.
- Already accepted evidence is not deleted by later revocation.

### 19.2 Web integration tests

- The public page contains no case/reference/request details.
- It has no ordinary application navigation.
- Invalid, expired and revoked secrets produce the same public response.
- GET does not consume the grant.
- Antiforgery is required.
- A grant session cannot call authenticated or unrelated public endpoints.
- Token A cannot submit to an upload reservation created by Token B.
- Missing or incorrect content length does not bypass the byte limit.
- Oversized streaming is terminated.
- Path traversal filenames are harmless.
- Duplicate client keys return the existing result.
- Rate limits produce controlled `429` responses.
- Responses contain no cacheable secret data.
- Required security headers are present.

### 19.3 Content-security tests

- Extension versus MIME mismatch.
- Signature mismatch.
- Truncated image or document.
- Corrupt PDF.
- Executable renamed as an image.
- Double extension.
- Extremely long filename.
- Unicode and control characters.
- Archive bomb where archive formats are accepted.
- Approved malware test fixture in an isolated environment.
- Scan unavailable or timed out.
- Malicious scan result cannot advance to custody.

### 19.4 Worker tests

- Duplicate outbox deliveries do not duplicate intake.
- Worker restart resumes pending processing.
- Box transient failure retries.
- Permanent Box rejection is visible internally.
- The recorded SHA-256 matches quarantined bytes.
- A clean file reaches the existing custody path exactly once.
- A rejected file never reaches Box.
- Cleanup does not delete a valid pending upload.
- Orphaned storage objects are detected.

### 19.5 Direct Blob tests, when enabled

- SAS targets one exact blob.
- No list, read or delete permission.
- Existing blobs cannot be overwritten.
- Expired SAS fails.
- Revoked Pegasus grant cannot finalise.
- Orphaned post-revocation blob is deleted.
- Blob properties are verified server-side.
- CORS permits only the intended Pegasus origin.
- SAS lifetime is bounded independently from the longer human-facing grant.

### 19.6 Logging tests

Evidence should prove that the raw secret does not appear in:

- ASP.NET request logs.
- Reverse-proxy logs.
- Application Insights request URLs.
- Exception telemetry.
- ActionHistory.
- Audit exports.
- Worker messages.
- Browser-visible errors.

### 19.7 Production proof

Subject to exact-target approval:

1. Create a grant for a designated test request.
2. Open it in a signed-out/private browser.
3. Confirm that no internal Pegasus state is visible.
4. Upload representative supported image and document files.
5. Confirm durable quarantine, scan and existing custody processing.
6. Confirm action history attributes creation and upload correctly.
7. Revoke the grant.
8. Confirm both new and existing public sessions fail.
9. Confirm a post-revocation upload cannot enter custody.
10. Inspect telemetry for secret leakage.
11. Confirm abandoned quarantine cleanup.
12. Record operator acceptance separately from deployment evidence.

## 20. Implementation sequence

### Stage 1 — Source mapping on `dev`

Before changing code, identify:

- FRD-02 request-scoped upload section.
- Exact aggregate representing the request.
- Existing intake receipt commands.
- Existing Worker queue and recovery model.
- Existing outbox or dispatched-work recovery.
- Current Box custody adapter.
- Actor and operation-key factories.
- Permanent ActionHistory conventions.
- EF Core context and migration conventions.
- Web authorization and public-page conventions.
- Current Azure storage, networking and managed identities.
- Existing file-type and upload-size configuration.
- Existing telemetry URL sanitisation.
- Relevant unit, integration and deployment tests.

This pass determines whether private temporary Blob storage already exists and whether direct browser access is compatible with current networking.

### Stage 2 — Governing decisions

Record:

- Request aggregate.
- Upload type allowlist.
- Per-file, file-count and total-byte limits.
- Default and maximum grant lifetime.
- In-flight revocation semantics.
- Multiple-file completion rule.
- Malware engine.
- Quarantine retention.
- Public-link URL model.
- Whether JavaScript is mandatory.
- Whether original link secrets can ever be redisplayed.
- Whether direct Blob upload is required initially.

### Stage 3 — Domain and persistence

Add:

- Grant entity.
- Upload entity.
- Lifecycle states.
- Concurrency token.
- Unique idempotency constraints.
- Migration.
- Core creation, reservation, commit and revocation actions.
- Audit events.

### Stage 4 — Infrastructure

Add or configure:

- Private quarantine container.
- Managed-identity access.
- Storage lifecycle cleanup.
- Malware integration.
- Durable outbox/dispatch.
- Secret redaction.
- Metrics and alerts.

### Stage 5 — Authenticated UI

Implement:

- Create.
- Copy once.
- Revoke.
- Replace.
- Internal status and audit.

### Stage 6 — Anonymous UI and transport

Implement:

- Token/session exchange.
- Minimal public page.
- Atomic reservation.
- One-file streaming endpoint.
- Immediate result.
- Rate and concurrency policies.
- Security headers.

### Stage 7 — Worker integration

Implement:

- Scan gating.
- File inspection.
- Idempotent intake handoff.
- Existing Box custody reuse.
- Retries.
- Failure classification.
- Cleanup.

### Stage 8 — Verification

Run the complete test matrix and approved live proof before claiming the capability is complete.

## 21. Direct-upload decision gate

Use direct Azure Blob upload in the first release only when all of the following are true:

- Real files are too large or concurrent for efficient Web streaming.
- Mobile interruption/resume is a demonstrated requirement.
- The quarantine storage endpoint may be browser-accessible.
- Storage CORS and public-network exposure are accepted.
- Management accepts that an already-issued SAS cannot be revoked per grant immediately.
- The application finalisation gate is sufficient for the business meaning of revocation.
- The selected Azure SDK and service version have been tested with the intended create-only permissions.

Otherwise, implement proxied streaming and defer SAS issuance.

## 22. Product and engineering decisions still required

| Decision | Recommendation |
|---|---|
| What is the scope object? | Bind to the exact intake/document request, not the overall case |
| Can several links exist? | Yes; separate grants may bind to one request |
| Can one link accept several files? | Yes, within an explicit policy |
| How are files submitted? | One logical file per reservation and HTTP operation |
| Default link lifetime | Product-configured; keep a server-enforced hard maximum |
| Can a secret be viewed later? | No; replace rather than reveal |
| What does successful upload mean? | Durably quarantined and scheduled |
| Does revocation erase accepted files? | No |
| What happens to an in-flight revoked upload? | Finish only to quarantine, then reject at commit |
| Storage transport | Proxy stream first |
| Final storage | Existing Box custody path |
| Temporary storage | Private Azure quarantine |
| Scan failure | Fail closed and retry/alert |
| Public history | None |
| Public receipt identifier | Avoid unless a support requirement is established |
| Original filename | Display metadata only |
| IP/user-agent retention | Security telemetry only, with explicit retention policy |
| Box File Request | Retire after INT-31 acceptance |
| Ticket workflow stage | Research and planning should precede implementation |

## 23. Final recommendation

Implement TICK-033 as the following secure minimum slice:

1. **Database-backed opaque grant**
   - Cryptographically random secret.
   - Secret stored only as a hash/HMAC.
   - Exact request binding.
   - Expiry, revocation, version, count and byte limits.
   - Concurrency token.

2. **Authenticated staff controls**
   - Create, copy once, replace and revoke.
   - Narrow authorization.
   - Permanent history without the secret.

3. **Restricted anonymous session**
   - Secret exchanged for an upload-only session.
   - No ordinary Pegasus authority.
   - No case or request data.
   - No public listing or retrieval.

4. **Two-step file protocol**
   - Atomically reserve.
   - Stream one file.
   - Recheck revocation before commit.
   - Return only the current file's immediate result.

5. **Private Azure quarantine**
   - Random object identity.
   - Server-side byte count and SHA-256.
   - No public access.
   - Cleanup policy.

6. **Asynchronous trusted processing**
   - Malware and type validation.
   - Durable outbox.
   - Existing Worker/Core intake path.
   - Existing Box custody adapter.
   - Idempotent retry and recovery.

7. **Operational hardening**
   - Rate and concurrency limits.
   - Token and URL redaction.
   - No-store/no-referrer/CSP headers.
   - Alerts for stuck scans, custody failures and orphaned objects.

8. **Transport abstraction**
   - Keep direct Azure Blob SAS upload as an optional future transport.
   - Do not make its weaker per-link revocation characteristics part of the initial security contract.

This design meets the ticket's central intent: an external recipient can contribute evidence through Pegasus without becoming a Pegasus user and without gaining any visibility into Pegasus itself.

## 24. Primary external references

- Microsoft — ASP.NET Core file uploads: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads
- Microsoft Azure Architecture Center — Valet Key pattern: https://learn.microsoft.com/en-us/azure/architecture/patterns/valet-key
- Microsoft — Create a user delegation SAS: https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blob-user-delegation-sas-create-cli
- Microsoft — Azure Blob browser upload pattern: https://learn.microsoft.com/en-us/azure/developer/javascript/tutorial/browser-file-upload-azure-storage-blob
- Microsoft — Azure Blob lifecycle management: https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-policy-delete
- Microsoft — Defender for Storage on-upload malware scanning: https://learn.microsoft.com/en-us/azure/defender-for-cloud/on-upload-malware-scanning
- Microsoft — EF Core optimistic concurrency: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- Microsoft — ASP.NET Core rate limiting: https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit
- OWASP — File Upload Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html
- OWASP — Forgot Password Cheat Sheet (token properties applicable to capability links): https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html
- Box — File Requests API guide: https://developer.box.com/guides/file-requests/

## 25. Source mapping update — 2026-08-18

The earlier architecture recommendation is now superseded by a read-only source inspection of the current `dev` branch:

- The capability is already implemented at the existing Core/Web/Infrastructure boundary: `RequestUploadPolicy` owns opaque token validation, expiry, revocation, request limits and replay decisions; `EfDocumentRequestStore` persists the link/receipt and custody data; `/Uploads/{token}` is the only anonymous caller.
- Staff creation and revocation are already thin authenticated handlers on the case custody page; the public page has antiforgery, no-store caching and a per-token attempt limiter.
- Durable integration tests cover retention rollback/retry and Web tests cover the staff controls. `docs/current-architecture.md` records the source-state caller and explicitly distinguishes it from deployment and acceptance evidence.
- Commit `f43e3a2b` removes the obsolete Box File Request persistence/caller. The capability inventory still says “UI removal pending”, which is stale against that source history.

**Implication:** Do not create a second upload implementation. TICK-033 is a reconciliation/evidence task: correct the one stale capability-inventory boundary statement, preserve the existing implementation, and verify the targeted test suite. Live activation and browser acceptance remain outside this ticket because they require exact-target approval.
