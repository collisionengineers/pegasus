using CollisionDocNet.Storage.Opc;
using CollisionDocNet.Storage.Tests.Zip;

namespace CollisionDocNet.Storage.Tests.Opc;

[TestClass]
public sealed class OpcPackageReaderTests
{
    [TestMethod]
    public void Read_MinimalWordPackage_MapsContentType()
    {
        OpcReadResult result = OpcPackageReader.Read(ZipFixture.CreateMinimalDocx());

        Assert.IsTrue(result.IsSuccess);
        OpcPart part = Assert.ContainsSingle(result.Package!.Parts);
        Assert.AreEqual("/word/document.xml", part.Name);
        Assert.Contains("wordprocessingml", part.ContentType);
    }

    [TestMethod]
    public void Read_RelationshipResolvesWithinPackage_ReturnsGraphEdge()
    {
        OpcReadResult result = OpcPackageReader.Read(ZipFixture.CreateMinimalDocx("document.xml"));

        Assert.IsTrue(result.IsSuccess);
        OpcRelationship relationship = Assert.ContainsSingle(result.Package!.Relationships);
        Assert.AreEqual("/word/document.xml", relationship.SourcePart);
        Assert.AreEqual("/word/document.xml", relationship.ResolvedPart);
        Assert.IsFalse(relationship.IsExternal);
    }

    [TestMethod]
    public void Read_RelationshipEscapesPackage_ReturnsInvalidRelationshipTarget()
    {
        OpcReadResult result = OpcPackageReader.Read(ZipFixture.CreateMinimalDocx("../../outside.xml"));

        Assert.AreEqual(OpcReadError.InvalidRelationshipTarget, result.Error);
    }

    [TestMethod]
    public void Read_ExternalRelationship_RecordsWithoutResolving()
    {
        byte[] bytes = CreateExternalRelationshipPackage();

        OpcReadResult result = OpcPackageReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        OpcRelationship relationship = Assert.ContainsSingle(result.Package!.Relationships);
        Assert.IsTrue(relationship.IsExternal);
        Assert.IsNull(relationship.ResolvedPart);
    }

    [TestMethod]
    public void Read_MissingInternalRelationshipTarget_ReturnsMissingTarget()
    {
        OpcReadResult result = OpcPackageReader.Read(ZipFixture.CreateMinimalDocx("missing.xml"));

        Assert.AreEqual(OpcReadError.MissingRelationshipTarget, result.Error);
    }

    [TestMethod]
    public void Read_RelationshipsWithWrongRoot_ReturnsRelationshipPartInvalid()
    {
        byte[] bytes = CreatePackageWithRelationshipXml(
            "<NotRelationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'/>");

        OpcReadResult result = OpcPackageReader.Read(bytes);

        Assert.AreEqual(OpcReadError.RelationshipPartInvalid, result.Error);
    }

    [TestMethod]
    public void Read_NestedRelationshipElement_ReturnsRelationshipPartInvalid()
    {
        byte[] bytes = CreatePackageWithRelationshipXml("""
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Container><Relationship Id="rId1" Type="urn:test" Target="document.xml"/></Container>
            </Relationships>
            """);

        OpcReadResult result = OpcPackageReader.Read(bytes);

        Assert.AreEqual(OpcReadError.RelationshipPartInvalid, result.Error);
    }

    [TestMethod]
    public void Read_UnknownTargetMode_ReturnsRelationshipPartInvalid()
    {
        byte[] bytes = CreatePackageWithRelationshipXml("""
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="urn:test" Target="document.xml" TargetMode="Remote"/>
            </Relationships>
            """);

        OpcReadResult result = OpcPackageReader.Read(bytes);

        Assert.AreEqual(OpcReadError.RelationshipPartInvalid, result.Error);
    }

    [TestMethod]
    public void Read_RelationshipRequiredAttributeIsNamespaceQualified_ReturnsRelationshipPartInvalid()
    {
        byte[] bytes = CreatePackageWithRelationshipXml("""
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships" xmlns:f="urn:foreign">
              <Relationship f:Id="rId1" Type="urn:test" Target="document.xml"/>
            </Relationships>
            """);

        OpcReadResult result = OpcPackageReader.Read(bytes);

        Assert.AreEqual(OpcReadError.RelationshipPartInvalid, result.Error);
    }

    [TestMethod]
    public void Read_RelationshipPartSourceDoesNotExist_ReturnsRelationshipPartInvalid()
    {
        byte[] bytes = ZipFixture.Create(
            ("[Content_Types].xml", System.Text.Encoding.UTF8.GetBytes("""
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                </Types>
                """)),
            ("word/document.xml", "<x/>"u8.ToArray()),
            ("word/_rels/missing.xml.rels", System.Text.Encoding.UTF8.GetBytes("""
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"/>
                """)));

        OpcReadResult result = OpcPackageReader.Read(bytes);

        Assert.AreEqual(OpcReadError.RelationshipPartInvalid, result.Error);
    }

    [TestMethod]
    public void Read_ContentTypeRequiredAttributeIsNamespaceQualified_ReturnsContentTypesInvalid()
    {
        byte[] bytes = ZipFixture.Create(
            ("[Content_Types].xml", System.Text.Encoding.UTF8.GetBytes("""
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types" xmlns:f="urn:foreign">
                  <Default Extension="xml" f:ContentType="application/xml"/>
                </Types>
                """)),
            ("word/document.xml", "<x/>"u8.ToArray()));

        OpcReadResult result = OpcPackageReader.Read(bytes);

        Assert.AreEqual(OpcReadError.ContentTypesInvalid, result.Error);
    }

    [TestMethod]
    public void Read_UnknownContentTypesChild_ReturnsContentTypesInvalid()
    {
        byte[] bytes = ZipFixture.Create(
            ("[Content_Types].xml", System.Text.Encoding.UTF8.GetBytes("""
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Other ContentType="application/xml"/>
                </Types>
                """)));

        OpcReadResult result = OpcPackageReader.Read(bytes);

        Assert.AreEqual(OpcReadError.ContentTypesInvalid, result.Error);
    }

    [TestMethod]
    public void Read_EncodedPathSeparatorInActualPartName_ReturnsInvalidPartName()
    {
        byte[] bytes = ZipFixture.Create(
            ("[Content_Types].xml", System.Text.Encoding.UTF8.GetBytes("""
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="xml" ContentType="application/xml"/>
                </Types>
                """)),
            ("word%2Fdocument.xml", "<x/>"u8.ToArray()));

        OpcReadResult result = OpcPackageReader.Read(bytes);

        Assert.AreEqual(OpcReadError.InvalidPartName, result.Error);
    }

    [TestMethod]
    [DataRow("word/document?.xml")]
    [DataRow("word/document#fragment.xml")]
    [DataRow("word./document.xml")]
    [DataRow("word/%41.xml")]
    [DataRow("word/%2e/document.xml")]
    public void Read_NonCanonicalPartName_ReturnsInvalidPartName(string name)
    {
        byte[] bytes = ZipFixture.Create(
            ("[Content_Types].xml", System.Text.Encoding.UTF8.GetBytes("""
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="xml" ContentType="application/xml"/>
                </Types>
                """)),
            (name, "<x/>"u8.ToArray()));

        OpcReadResult result = OpcPackageReader.Read(bytes);

        Assert.AreEqual(OpcReadError.InvalidPartName, result.Error);
    }

    [TestMethod]
    public void Read_PartWithoutContentType_ReturnsPartContentTypeMissing()
    {
        byte[] bytes = ZipFixture.Create(
            ("[Content_Types].xml", System.Text.Encoding.UTF8.GetBytes("""
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"/>
                """)),
            ("data.bin", [1]));

        OpcReadResult result = OpcPackageReader.Read(bytes);

        Assert.AreEqual(OpcReadError.PartContentTypeMissing, result.Error);
    }

    private static byte[] CreateExternalRelationshipPackage()
    {
        string external = """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="urn:test" Target="https://example.invalid/a" TargetMode="External"/>
            </Relationships>
            """;
        byte[] baseline = ZipFixture.CreateMinimalDocx();
        using var input = new MemoryStream(baseline);
        using var output = new MemoryStream();
        using (var source = new System.IO.Compression.ZipArchive(input, System.IO.Compression.ZipArchiveMode.Read))
        using (var target = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            foreach (System.IO.Compression.ZipArchiveEntry item in source.Entries)
            {
                System.IO.Compression.ZipArchiveEntry copy = target.CreateEntry(item.FullName);
                using Stream sourceStream = item.Open();
                using Stream targetStream = copy.Open();
                sourceStream.CopyTo(targetStream);
            }

            System.IO.Compression.ZipArchiveEntry rel = target.CreateEntry("word/_rels/document.xml.rels");
            using Stream relStream = rel.Open();
            relStream.Write(System.Text.Encoding.UTF8.GetBytes(external));
        }

        return output.ToArray();
    }

    private static byte[] CreatePackageWithRelationshipXml(string relationships) =>
        ZipFixture.Create(
            ("[Content_Types].xml", System.Text.Encoding.UTF8.GetBytes("""
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                </Types>
                """)),
            ("word/document.xml", "<x/>"u8.ToArray()),
            ("word/_rels/document.xml.rels", System.Text.Encoding.UTF8.GetBytes(relationships)));
}
