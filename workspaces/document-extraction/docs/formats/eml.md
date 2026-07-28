# RFC 5322/MIME `.eml` extraction plan

## Boundary

EML has no reliable magic number. The detector requires bounded Internet Message Format header evidence and records extension/media-type hints only as untrusted context.

The parser preserves source positions, field/part order and semantic values. Every leaf is classified as text, image, nested supported content, protected content or non-payload content.

HTML rendering, script execution, remote retrieval, automatic decryption, DNS/key lookup and online signature/authentication verification are excluded.

The `.eml` payload is decoded textual headers, body and report content plus MIME image parts. Non-image attachments, signatures, certificates, ciphertext, TNEF and opaque MIME leaf bytes are inventory/control evidence and are not emitted. Supported nested messages/documents may contribute only text and images.

## Complete feature surface

### Octet, line and format detection

- Exact raw offsets, canonical CRLF and explicit compatibility handling for LF-only/CR-only input.
- Optional BOM, missing terminal newline and one leading mbox `From_` separator as labelled compatibility cases.
- Header/body separator, line-length limits, controls, overlong/truncated lines and cancellation.
- Detection requires plausible field-name/colon syntax and bounded header structure rather than extension alone.

### RFC 5322 syntax and fields

- Folding/unfolding, comments/folding whitespace, quoted strings, atoms/dot-atoms, domain literals, groups and mailbox lists.
- Modern and required obsolete receiving syntax with raw-preserving issues.
- Original field order, duplicates, casing, raw spans and unknown/X-fields.
- Date, From, Sender, Reply-To, To, Cc, Bcc.
- Message-ID, In-Reply-To, References, Subject, Comments and Keywords.
- Resent blocks, Return-Path and ordered Received trace fields.
- No silent winner when singleton fields are duplicated or conflict.

### Internationalisation and encoded values

- RFC 6532 UTF-8 header values/internationalised addresses while field names remain ASCII.
- RFC 2047 B/Q encoded words with context, adjacency and whitespace rules.
- RFC 2231 charset/language/percent-encoded parameters and continuations.
- Deterministic address/date/message-ID parsing with raw value retained on ambiguity.
- No machine-default charset.

### MIME entity tree

- MIME-Version; Content-Type, Transfer-Encoding, Disposition, ID, Location, Language and Description.
- Media-type/parameter defaults and duplicate/conflict policy.
- Recursive entities with stable source-order part paths, preamble and epilogue.
- Multipart `mixed`, `alternative`, `digest`, `parallel`, `related`, `report`, `signed` and `encrypted`.
- Unknown multipart subtype handled conservatively as mixed with an issue.
- Correct outer-boundary recognition when inner parts are truncated.
- Boundary collision, missing close and parser-differential cases.

### Transfer and charset decoding

- `7bit`, `8bit`, `binary`, quoted-printable and Base64 with strict/compatibility states.
- Bounded decoded output and cumulative expansion accounting.
- Unknown transfer encodings retain bounded source evidence with `UnsupportedFeature`/partial treatment; undecoded bytes are not emitted.
- Explicit compatibility units for uuencode, BinHex and AppleDouble if later required.
- Declared charset mapping, BOM conflicts and invalid-sequence handling.
- Strict US-ASCII default for `text/plain` without charset; any UTF-8/Windows-1252 recovery is configuration-labelled and issue-producing.

### Body representations

- Retain every `multipart/alternative` candidate; select a canonical representation using a versioned policy without discarding others.
- `text/plain`, including `format=flowed` and `delsp`.
- Inert HTML-to-text: character references, meaningful alt/title text and passive links.
- Exclude script/style text from canonical content; never render, execute, refresh, redirect or load remote resources.
- Preserve representation divergence and source part paths.

### Images, inline relationships and attachment inventory

- Content-Disposition and media-type semantics without trusting attachment names.
- RFC 2231/encoded-word filename variants, conflicts and raw values.
- Image identity uses source part path plus decoded-content hash; attachment filenames never become output paths.
- `multipart/related`, Content-ID and `cid:` resolution only within the parsed message.
- Content-Location, remote images and URLs retained as passive relationships, never fetched.
- Inline/attachment ambiguity and duplicate Content-ID reporting.

### Nested and special message bodies

- Recursive `message/rfc822` and `message/global` under cumulative budgets.
- `message/partial` retained as a fragment with `Partial`; never search for sibling fragments.
- `message/external-body` metadata only; never retrieve.
- Delivery-status and disposition-notification bodies, internationalised DSN/MDN and abuse-feedback reports.
- TNEF/`winmail.dat` is classified and reported; future shared primitives may extract contained text/images without emitting the TNEF bytes.

### Operational/reporting headers

- Mailing-list and List-ID/one-click unsubscribe metadata.
- Trace/resent fields and delivery/report relationships.
- Authentication-Results, DKIM, SPF, ARC and DMARC fields as reported assertions only.
- No verification claim without a separately supplied trust boundary and DNS/key evidence.

### Signed and encrypted content

- Clear-signed MIME with exact canonical signed octets preserved.
- CMS/S/MIME and PGP/MIME structural recognition.
- Clear text/images extracted when present; signatures, certificates and ciphertext are inventory-only and their bytes are not emitted.
- Signed versus encrypted/protected state distinguished.
- `Encrypted` for inaccessible payloads; no automatic decryption, key lookup, online revocation or trust-chain claim.

### Malformed and hostile input

- Extreme comment/group/address nesting, overlong fields and control characters.
- Duplicate/conflicting Content-Type, transfer-encoding, disposition and boundary fields.
- Invalid encoded-word placement and RFC 2231 continuations.
- Missing/colliding boundaries and outer-boundary recovery.
- Malformed Base64/quoted-printable and decoded-output bombs.
- Unknown/conflicting charsets with no platform-dependent fallback.
- Recursive messages, partial/external bodies and TNEF property bombs.
- HTML scripts, meta refresh, remote resources and hostile URLs.
- Parser-smuggling/differential fixtures and exact strict-versus-compatibility status.

## Deterministic projection

- Stable field occurrence and MIME part paths preserve original source order.
- Raw octets/spans and decoded values coexist.
- Every text/image occurrence, relationship descriptor, nested result and protected/non-payload part remains addressable without emitting non-image bytes.
- Canonical-body selection records policy/version and never changes completeness by hiding alternatives.
- Unknown fields/media types are retained; unknown decoders never silently discard bytes.

## Port units

| ID | Responsibility |
|---|---|
| `EXT-EML-001` | Detection, line scanner, raw spans and syntax limits |
| `EXT-EML-002` | RFC 5322 modern/obsolete/trace/resent/unknown headers |
| `EXT-EML-003` | UTF-8, encoded words, parameters, addresses, dates and identifiers |
| `EXT-EML-004` | MIME entity tree, defaults, boundaries and multipart semantics |
| `EXT-EML-005` | Transfer and charset decoding with explicit compatibility profiles |
| `EXT-EML-006` | Disposition, images, CID/related graph and stable identities |
| `EXT-EML-007` | Alternative-body policy, flowed text and inert HTML extraction |
| `EXT-EML-008` | Nested/global/partial/external-body handling and recursion |
| `EXT-EML-009` | DSN, MDN, feedback, list, trace and reported-authentication semantics |
| `EXT-EML-010` | TNEF and explicitly selected legacy transport encodings |
| `EXT-EML-011` | Multipart signatures, S/MIME and PGP/MIME protected content |
| `EXT-EML-012` | Projection, conformance/recovery/parser-smuggling/fuzz/differential/performance/corpus acceptance |

## Evidence matrix

- CRLF/LF/CR, BOM/mbox, missing separators/newlines, overlong/control/nesting cases.
- Duplicate/conflicting core/MIME fields and every RFC 5322 address/date/ID/obsolete form.
- Encoded words and RFC 2231 segments including malformed placement/continuation.
- Every multipart subtype, boundary collision/truncation and outer recovery.
- Every transfer encoding and charset state with expansion/invalid-sequence limits.
- Divergent alternatives, flowed text, HTML active/remote content and CID graphs.
- Recursive/global/partial/external messages, DSN/MDN/feedback and TNEF.
- Exact signed-octet preservation, encrypted outcomes and spoofed authentication assertions.
- Cancellation, concurrency, repeat determinism and independent parser comparison.

## Primary sources

- [RFC 5322 Internet Message Format](https://www.rfc-editor.org/rfc/rfc5322.html)
- [RFC 2045 MIME body format](https://www.rfc-editor.org/rfc/rfc2045.html)
- [RFC 2046 MIME media types](https://www.rfc-editor.org/rfc/rfc2046.html)
- [RFC 2047 encoded words](https://www.rfc-editor.org/rfc/rfc2047.html)
- [RFC 2183 Content-Disposition](https://www.rfc-editor.org/rfc/rfc2183.html)
- [RFC 2231 MIME parameters](https://www.rfc-editor.org/rfc/rfc2231.html)
- [RFC 2387 multipart/related](https://www.rfc-editor.org/rfc/rfc2387.html)
- [RFC 2392 Content-ID and Message-ID URLs](https://www.rfc-editor.org/rfc/rfc2392.html)
- [RFC 3676 format=flowed](https://www.rfc-editor.org/rfc/rfc3676.html)
- [RFC 6532 internationalised headers](https://www.rfc-editor.org/rfc/rfc6532.html)
- [RFC 8551 S/MIME 4.0](https://www.rfc-editor.org/rfc/rfc8551.html)
- [[MS-OXTNEF]](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxtnef/1f0544d7-30b7-4194-b58f-adc82f3763bb)
