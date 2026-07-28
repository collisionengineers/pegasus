# EV-2026-07-23 — Wave 10 passive PDF evidence surface

Scope: passive, non-executing PDF evidence layered on the Wave 9 core. Independent review rejected the initial surface; correction tied evidence to authoritative object state, marked encoded fallbacks, tightened signature coverage and added revision/object/occurrence asset identity. This supports partial implementation only, not complete ISO 32000 clause-family extraction, profile validation or Wave 10 acceptance.

The subset projects bounded Info/XMP claims with validation explicitly not performed; tagged/marked content including MCID/ActualText; optional-content inventory; outlines/page labels/name trees; annotations and AcroForm/passive XFA; stable SHA-256 bounded image/mask/embedded/associated/portfolio/media assets; passive actions/JavaScript/URI/launch/media/3D with execution and retrieval disabled; structural signature ByteRanges with trust=false; and Standard/Adobe.PubSec classification without interpreting encrypted content. It also adds inherited resources, bounded Form XObject text recursion and cancellation inside decoding/content loops.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Pdf.Tests\CollisionDocNet.Pdf.Tests.csproj --configuration Release --no-restore
```

Corrected focused result: exit `0`; 46 succeeded, 0 failed, 0 skipped. Production/test formatting passed and the requested performance scan found no critical pattern after pooled-buffer/token hot-path corrections. Inputs were owned synthetic PDFs only.

Remaining gates include PDF profile validation, signature digest/trust/revocation, decryption, inline-image payload parsing, DCT/JPX/JBIG2/media semantics, XFA semantics, Form matrix coordinate transforms, deep navigation-tree semantics, referenced marked-content property lists, complete security clause breadth, conformance, fuzz, differential, corpus and performance acceptance.
