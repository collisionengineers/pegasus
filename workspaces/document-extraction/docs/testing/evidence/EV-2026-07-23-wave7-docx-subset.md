# EV-2026-07-23 — Wave 7 DOCX subset

Scope: custom BCL-only managed DOCX/WordprocessingML extraction subset using the shared ZIP/OPC/XML foundations. Independent review rejected the initial 11-test implementation; the correction added exact allowlists/reachability, source ordering, cumulative budgets/deadlines, orphan evidence and honest XML provenance. This remains a partial row-specific subset, not full ECMA-376 parity or Wave 7 acceptance.

The subset recognises Strict/Transitional packages and encrypted CFB wrappers; discovers main, header, footer, footnote, endnote and comment stories; projects core paragraph/text/table/section/field/bookmark/hyperlink/deleted-revision evidence; inventories properties/styles/numbering/settings/fonts/themes; and retains media, embeddings, custom XML, VBA, ActiveX, signatures, charts and diagrams as deterministic passive assets. External relationships are not retrieved. DTD/entities are denied. Unsupported MCE, control binding, altChunk, drawing and dependency semantics force visible `Partial` issues.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Writer.OpenXml.Tests\CollisionDocNet.Writer.OpenXml.Tests.csproj --configuration Release --no-restore
```

Corrected focused result: exit `0`; 31 succeeded, 0 failed, 0 skipped. An earlier full-solution run after correction passed 407/407; a later rerun was blocked by concurrent Wave 5 public-project renaming rather than a DOCX failure. Production/test formatting passed and the requested static performance scan found no critical pattern. Inputs were owned synthetic packages only.

Remaining gates include full MCE processing, style/numbering resolution, content-control/forms/mail-merge semantics, full fields/comments/revisions, graphical/OMML semantics, signature assurance, nested embedded extraction, shared OPC corrections, conformance, differential, fuzz, corpus and performance acceptance.
