using System.Text.Json;
using System.Xml.Linq;
using CollisionDocNet.Model;

namespace CollisionDocNet.Packaging.Tests;

[TestClass]
public sealed class PackagingContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string[] RequiredAssetProperties = ["stableId", "path", "sha256", "length"];

    [TestMethod]
    [TestCategory("packaging")]
    public void ResultSchemaPinsRuntimeSchemaAndEveryOutcome()
    {
        using JsonDocument schema = ReadSchema("extraction-result.v1.schema.json");
        JsonElement properties = schema.RootElement.GetProperty("properties");

        Assert.AreEqual(
            ExtractionResult.CurrentSchemaVersion,
            properties.GetProperty("schemaVersion").GetProperty("const").GetString());
        string[] schemaOutcomes = schema.RootElement.GetProperty("$defs").GetProperty("outcome")
            .GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToArray();
        CollectionAssert.AreEquivalent(Enum.GetNames<ExtractionOutcome>(), schemaOutcomes);
        StringAssert.Contains(
            schema.RootElement.GetProperty("$defs").GetProperty("sha256")
                .GetProperty("properties").GetProperty("hex").GetProperty("pattern").GetString()!,
            "{64}");
    }

    [TestMethod]
    [TestCategory("packaging")]
    public void ResultSchemaPinsEveryDetectedContainerAndFormat()
    {
        using JsonDocument schema = ReadSchema("extraction-result.v1.schema.json");
        JsonElement properties = schema.RootElement.GetProperty("properties");
        string[] containers = properties.GetProperty("detectedContainer").GetProperty("enum")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();
        string[] formats = properties.GetProperty("detectedFormat").GetProperty("enum")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();

        CollectionAssert.AreEquivalent(Enum.GetNames<DetectedContainer>(), containers);
        CollectionAssert.AreEquivalent(Enum.GetNames<DetectedFormat>(), formats);
        CollectionAssert.DoesNotContain(formats, "LegacyWord");
    }

    [TestMethod]
    [TestCategory("packaging")]
    public void ResultSchemaRestrictsPublicAssetsToValidatedImageEncodings()
    {
        using JsonDocument schema = ReadSchema("extraction-result.v1.schema.json");
        JsonElement image = schema.RootElement.GetProperty("$defs").GetProperty("imageAsset");

        Assert.AreEqual("image", image.GetProperty("properties").GetProperty("kind").GetProperty("const").GetString());
        string[] mediaTypes = image.GetProperty("properties").GetProperty("mediaType").GetProperty("enum")
            .EnumerateArray().Select(static value => value.GetString()!).ToArray();
        Assert.IsTrue(mediaTypes.All(static value => value.StartsWith("image/", StringComparison.Ordinal)));
        CollectionAssert.DoesNotContain(mediaTypes, "image/svg+xml");
    }

    [TestMethod]
    [TestCategory("packaging")]
    public void BundleSchemaPinsSafeRelativeAssetContract()
    {
        using JsonDocument schema = ReadSchema("evidence-bundle-manifest.v1.schema.json");
        JsonElement properties = schema.RootElement.GetProperty("properties");

        Assert.AreEqual(
            "collisiondocnet-bundle/1",
            properties.GetProperty("schemaVersion").GetProperty("const").GetString());
        Assert.AreEqual(
            "^assets/[A-Za-z0-9._-]+$",
            properties.GetProperty("assetFiles").GetProperty("items").GetProperty("properties")
                .GetProperty("path").GetProperty("pattern").GetString());
        CollectionAssert.AreEquivalent(
            RequiredAssetProperties,
            properties.GetProperty("assetFiles").GetProperty("items").GetProperty("required")
                .EnumerateArray().Select(value => value.GetString()!).ToArray());
    }

    [TestMethod]
    [TestCategory("packaging")]
    public void CentralPackageMetadataIsVersionedAndOverridable()
    {
        XDocument props = XDocument.Load(Path.Combine(RepositoryRoot, "Directory.Build.props"));
        XElement root = props.Root ?? throw new AssertFailedException("Directory.Build.props has no root element.");

        Assert.AreEqual("0.1.0", root.Descendants("VersionPrefix").Single().Value);
        Assert.AreEqual("'$(VersionPrefix)' == ''", root.Descendants("VersionPrefix").Single().Attribute("Condition")?.Value);
        Assert.AreEqual("alpha.1", root.Descendants("VersionSuffix").Single().Value);
        Assert.AreEqual("PACKAGE.md", root.Descendants("PackageReadmeFile").Single().Value);
        Assert.AreEqual("false", root.Descendants("PackageRequireLicenseAcceptance").Single().Value);
        Assert.IsFalse(root.Descendants("PackageLicenseExpression").Any(), "No product licence has been authorised.");
    }

    [TestMethod]
    [TestCategory("packaging")]
    public void DependencyManifestTraversalIsBoundedToProjectRoots()
    {
        string script = File.ReadAllText(Path.Combine(RepositoryRoot, "scripts", "Build-ReleaseCandidate.ps1"));

        Assert.Contains("Join-Path $repositoryRoot 'src'", script);
        Assert.Contains("Join-Path $repositoryRoot 'tests'", script);
        Assert.Contains("[System.IO.FileAttributes]::ReparsePoint", script);
        Assert.DoesNotContain("Get-ChildItem -LiteralPath $repositoryRoot -Recurse", script);
        Assert.DoesNotContain("$repositoryRoot\\core\\*", script);
        Assert.DoesNotContain("$repositoryRoot\\sample-doc-files\\*", script);
        Assert.DoesNotContain("$repositoryRoot\\artifacts\\*", script);
    }

    private static JsonDocument ReadSchema(string name) => JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(RepositoryRoot, "docs", "schemas", name)));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
                File.Exists(Path.Combine(current.FullName, "PACKAGE.md")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Repository root was not found from the test output directory.");
    }
}
