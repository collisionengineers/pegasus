using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pegasus.Core.ReferenceData;

namespace Pegasus.Core.Tests.ReferenceData;

public sealed class PrincipalDomainPolicyTests
{
    [Fact]
    public void PrincipalDomainValidationAcceptsExactBoundPackageBytes()
    {
        var package = ValidPackage();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(package);

        var result = ReferenceDataPolicy.Validate(Requested(package, bytes), bytes);

        Assert.True(result.IsValid, string.Join(",", result.Issues.Select(issue => issue.Subject)));
    }

    [Fact]
    public void PrincipalDomainValidationRejectsChangedBytesAndVersion()
    {
        var package = ValidPackage();
        var originalBytes = JsonSerializer.SerializeToUtf8Bytes(package);
        var changedBytes = JsonSerializer.SerializeToUtf8Bytes(package with { Version = "provider-domains-v2" });

        var result = ReferenceDataPolicy.Validate(Requested(package, originalBytes), changedBytes);

        Assert.Contains(result.Issues, issue => issue.Code == PrincipalDomainValidationIssueCode.PackageHashMismatch);
        Assert.Contains(result.Issues, issue => issue.Code == PrincipalDomainValidationIssueCode.VersionMismatch);
    }

    [Fact]
    public void PrincipalDomainValidationRejectsUnknownAndDuplicateJsonMembers()
    {
        var package = ValidPackage();
        var node = JsonSerializer.SerializeToNode(package)!.AsObject();
        node["unexpected"] = true;
        var unknownBytes = JsonSerializer.SerializeToUtf8Bytes(node);
        var duplicateBytes = """
            {"schemaVersion":1,"schemaVersion":1,"version":"provider-domains-v1","source":{"path":"reference/source.xlsx","contentSha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","sheet":"Sheet1","rowCount":1},"providers":[{"code":"QDOS","sourceRow":1,"domainSuffixes":["@qdosassist.co.uk"]}]}
            """u8.ToArray();

        var unknown = ReferenceDataPolicy.Validate(Requested(package, unknownBytes), unknownBytes);
        var duplicate = ReferenceDataPolicy.Validate(Requested(package, duplicateBytes), duplicateBytes);

        Assert.Contains(unknown.Issues, issue => issue.Code == PrincipalDomainValidationIssueCode.InvalidJson);
        Assert.Contains(duplicate.Issues, issue => issue.Code == PrincipalDomainValidationIssueCode.InvalidJson);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"version\":\"provider-domains-v1\",\"source\":null,\"providers\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"version\":\"provider-domains-v1\",\"source\":{\"path\":\"reference/source.xlsx\",\"contentSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"sheet\":\"Sheet1\",\"rowCount\":1},\"providers\":null}")]
    [InlineData("{\"schemaVersion\":1,\"version\":\"provider-domains-v1\",\"source\":{\"path\":\"reference/source.xlsx\",\"contentSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"sheet\":\"Sheet1\",\"rowCount\":1}}")]
    public void PrincipalDomainValidationRejectsNullOrMissingRequiredValues(string json)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(JsonNode.Parse(json));
        var requested = new PrincipalDomainPackageVersion(1, "provider-domains-v1", Hash(bytes));

        var result = ReferenceDataPolicy.Validate(requested, bytes);

        Assert.False(result.IsValid);
        Assert.All(result.Issues, issue => Assert.StartsWith("$", issue.Subject, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("qdos")]
    [InlineData("QDOS_")]
    [InlineData("-QDOS")]
    [InlineData("QDOS-")]
    [InlineData("QDOS--LAW")]
    [InlineData("QDOS-LONGER-THAN-TWENTY")]
    public void PrincipalDomainValidationRejectsInvalidPrincipalCodes(string code)
    {
        var package = ValidPackage() with
        {
            Principals = [ValidPackage().Principals[0] with { Code = code }]
        };

        var result = Validate(package);

        Assert.Contains(result.Issues, issue => issue.Code == PrincipalDomainValidationIssueCode.InvalidPrincipalCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("qdosassist.co.uk")]
    [InlineData("@@qdosassist.co.uk")]
    [InlineData("@QDOSASSIST.CO.UK")]
    [InlineData("@localhost")]
    [InlineData("@-invalid.example")]
    [InlineData("@invalid-.example")]
    [InlineData("@invalid..example")]
    [InlineData("@invalid_example.test")]
    [InlineData("@invalid.example ")]
    public void PrincipalDomainSuffixGrammarRejectsNoncanonicalValues(string? suffix)
    {
        Assert.False(ReferenceDataPolicy.IsCanonicalDomainSuffix(suffix));
    }

    [Fact]
    public void PrincipalDomainValidationRejectsDuplicateCodesRowsAndPerPrincipalSuffixes()
    {
        var principal = ValidPackage().Principals[0];
        var package = ValidPackage() with
        {
            Principals =
            [
                principal with { DomainSuffixes = ["@qdosassist.co.uk", "@qdosassist.co.uk"] },
                principal
            ]
        };

        var result = Validate(package);

        Assert.Contains(result.Issues, issue => issue.Code == PrincipalDomainValidationIssueCode.DuplicateDomainSuffix);
        Assert.Contains(result.Issues, issue => issue.Code == PrincipalDomainValidationIssueCode.DuplicatePrincipalCode);
        Assert.Contains(result.Issues, issue => issue.Code == PrincipalDomainValidationIssueCode.DuplicateSourceRow);
    }

    [Fact]
    public void PrincipalDomainCandidateCreationReturnsSortedSharedEvidenceWithoutChoosingWinner()
    {
        var result = ReferenceDataPolicy.CreateCandidates(
            "@shared.example",
            ["ZETA", "ALPHA", "ZETA"]);

        Assert.Equal(PrincipalDomainCandidateStatus.Ambiguous, result.Status);
        Assert.Collection(
            result.PrincipalCodes,
            code => Assert.Equal("ALPHA", code),
            code => Assert.Equal("ZETA", code));
    }

    [Fact]
    public void PrincipalDomainCandidateCreationIsExactAndCaseSensitive()
    {
        var invalid = ReferenceDataPolicy.CreateCandidates("@QDOSASSIST.CO.UK", ["QDOS"]);
        var unknown = ReferenceDataPolicy.CreateCandidates("@unknown.invalid", []);
        var found = ReferenceDataPolicy.CreateCandidates("@qdosassist.co.uk", ["QDOS"]);

        Assert.Equal(PrincipalDomainCandidateStatus.InvalidSuffix, invalid.Status);
        Assert.Empty(invalid.PrincipalCodes);
        Assert.Equal(PrincipalDomainCandidateStatus.Unknown, unknown.Status);
        Assert.Equal(PrincipalDomainCandidateStatus.Found, found.Status);
        Assert.Collection(found.PrincipalCodes, code => Assert.Equal("QDOS", code));
    }

    [Fact]
    public void PrincipalDomainSuffixExtractionReturnsOnlyCanonicalLowercaseSuffix()
    {
        var extracted = ReferenceDataPolicy.TryExtractDomainSuffix(
            "  synthetic-local-001@EXAMPLE.INVALID  ",
            out var suffix);

        Assert.True(extracted);
        Assert.Equal("@example.invalid", suffix);
        Assert.DoesNotContain("synthetic-local-001", suffix, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("missing-separator")]
    [InlineData("@example.invalid")]
    [InlineData("synthetic-local-001@localhost")]
    public void PrincipalDomainSuffixExtractionRejectsInvalidInput(string? input)
    {
        Assert.False(ReferenceDataPolicy.TryExtractDomainSuffix(input, out var suffix));
        Assert.Null(suffix);
    }

    [Fact]
    public void PrincipalDomainPackageVersionRequiresSupportedCanonicalTuple()
    {
        Assert.True(ReferenceDataPolicy.IsValidPackageVersion(
            new PrincipalDomainPackageVersion(1, "provider-domains-v1", new string('a', 64))));
        Assert.False(ReferenceDataPolicy.IsValidPackageVersion(
            new PrincipalDomainPackageVersion(2, "provider-domains-v1", new string('a', 64))));
        Assert.False(ReferenceDataPolicy.IsValidPackageVersion(
            new PrincipalDomainPackageVersion(1, "Provider-Domains-V1", new string('a', 64))));
        Assert.False(ReferenceDataPolicy.IsValidPackageVersion(
            new PrincipalDomainPackageVersion(1, "provider-domains-v1", new string('A', 64))));
    }

    [Fact]
    public void PrincipalDomainValidationRejectsInvalidSourceIdentityAndRows()
    {
        var package = ValidPackage() with
        {
            Source = ValidPackage().Source with
            {
                Path = "../source.xlsx",
                ContentSha256 = new string('A', 64),
                Sheet = "",
                RowCount = 0
            }
        };

        var result = Validate(package);

        Assert.Equal(4, result.Issues.Count(issue => issue.Code == PrincipalDomainValidationIssueCode.InvalidSource));
        Assert.Contains(result.Issues, issue => issue.Code == PrincipalDomainValidationIssueCode.InvalidSourceRow);
    }

    private static PrincipalDomainValidationResult Validate(PrincipalDomainPackage package)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(package);
        return ReferenceDataPolicy.Validate(Requested(package, bytes), bytes);
    }

    private static PrincipalDomainPackageVersion Requested(PrincipalDomainPackage package, byte[] bytes) =>
        new(package.SchemaVersion, package.Version, Hash(bytes));

    private static PrincipalDomainPackage ValidPackage() =>
        new(
            ReferenceDataPolicy.SupportedSchemaVersion,
            "provider-domains-v1",
            new PrincipalDomainSource(
                "reference/workproviders-and-repairers/initial.xlsx",
                new string('a', 64),
                "Sheet1",
                1),
            [new PrincipalDomainReference("QDOS", 1, ["@qdosassist.co.uk"])]);

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
