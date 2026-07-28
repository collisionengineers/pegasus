# EV-2026-07-23 — Wave 6 EML extraction subset

Scope: BCL-only bounded EML/RFC 5322 and MIME extraction subset. This record supports the row-specific `locally-verified`, `partial`, and `implemented` statuses in the compatibility matrix. It does not claim complete RFC conformance or Wave 6 acceptance.

## Correctness boundary

The tested subset includes ordered bounded headers, folding, selected encoded-word/RFC2231/address forms, multipart and nested-message traversal, incremental Base64/quoted-printable decoding, selected charsets, plain/inert HTML, stable passive assets/CID evidence, cumulative budgets, raw source spans, sticky terminal outcomes, periodic cancellation/deadlines and explicit non-complete handling for unsupported/signed/encrypted/flowed/delivery-report/TNEF structures.

The implementation does not execute HTML/script, retrieve external content, verify signatures, decrypt protected content or semantically decode TNEF.

## Commands and results

```powershell
dotnet test --project tests\unit\CollisionDocNet.Email.Tests\CollisionDocNet.Email.Tests.csproj --configuration Release --no-restore --filter "TestCategory!=LocalCohort"
dotnet test --project tests\unit\CollisionDocNet.Email.Tests\CollisionDocNet.Email.Tests.csproj --configuration Release --no-restore --filter "TestCategory=LocalCohort"
```

Primary-agent rerun: both commands exit `0`; 28/28 focused tests and one opt-in cohort test passed. The cohort test processed four opaque local EML samples; all returned `Complete`, with deterministic canonical retry JSON and non-empty evidence asserted. No source content, filename, path, hash or identifier was printed.

The requested static performance scan found no critical API-pattern defect. It identified one bounded HTML chunk substring and bounded parser collection allocations for later measurement; this is not benchmark evidence.

## Remaining gates

- complete modern and obsolete RFC 5322 grammar;
- every MIME subtype, extension transfer encoding and charset;
- complete alternative/related/flowed policy;
- DSN, MDN, feedback and TNEF semantic extraction;
- exact signed-octet projection and cryptographic verification/decryption policy;
- specification-derived conformance, independent semantic differential, fuzz/property, parser-smuggling, benchmarks, hidden holdout and independent acceptance.
